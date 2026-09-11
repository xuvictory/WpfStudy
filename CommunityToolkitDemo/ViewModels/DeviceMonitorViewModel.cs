using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Common;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Protocols;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>
/// 设备监控 ViewModel：5 种协议的连接开关、实时数据表、报警与报文日志。
///
/// 覆盖的 CommunityToolkit 知识点：
/// - <see cref="ObservableRecipient.OnActivated"/>/<see cref="ObservableRecipient.OnDeactivated"/>：
///   离开页面即退订驱动事件与停止闪烁计时器，回来再恢复，避免无谓的 UI 刷新；
/// - <c>IRecipient&lt;DeviceAlarmMessage&gt;</c>：接收协议层广播的报警。
/// </summary>
public partial class DeviceMonitorViewModel : ObservableRecipient, IRecipient<DeviceAlarmMessage>
{
    /// <summary>报文日志容量上限（环形缓冲，防止长时间运行内存膨胀）</summary>
    private const int MaxLogItems = 200;

    private readonly IProtocolManager _manager;
    private readonly DispatcherTimer _flashTimer;
    private readonly DispatcherTimer _flushTimer;
    private readonly Dictionary<string, LiveMetricItem> _metricIndex = new();

    /// <summary>协议类型 → 设备卡片，避免每帧线性查找</summary>
    private readonly Dictionary<ProtocolType, DeviceCardViewModel> _cardIndex = new();

    /// <summary>后台驱动线程写入、UI 计时器批量读取的待处理报文队列</summary>
    private readonly ConcurrentQueue<ProtocolFrame> _pendingFrames = new();

    public DeviceMonitorViewModel(IProtocolManager manager, IMessenger messenger) : base(messenger)
    {
        _manager = manager;

        Cards = new ObservableCollection<DeviceCardViewModel>(
            manager.Drivers.Select(driver => new DeviceCardViewModel(driver, manager)));

        // 协议类型 → 卡片：状态变化与报文刷新都按类型直达，避免每帧线性查找。
        // 注意：同一协议当前只注册一个驱动，因此该索引是一对一的
        //（多设备寻址受 IProtocolManager 按类型查找的语义限制，见 ProtocolManager.Find 的说明）。
        foreach (var card in Cards)
        {
            _cardIndex[card.Type] = card;
        }

        // 单一计时器统一清除闪烁标记，避免每个数据项各自开计时器
        _flashTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _flashTimer.Tick += (_, _) => ClearFlashing();

        // 高频报文先入无锁队列，再由该计时器按固定节奏批量刷新界面：
        // 把"每帧 N 次 UI 操作"降为"每 150ms 一次批量操作"，显著减少布局抖动。
        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _flushTimer.Tick += (_, _) => FlushPendingFrames();
    }

    #region 绑定数据

    /// <summary>协议设备卡片</summary>
    public ObservableCollection<DeviceCardViewModel> Cards { get; }

    /// <summary>实时数据表（所有协议指标聚合）</summary>
    public ObservableCollection<LiveMetricItem> Metrics { get; } = new();

    /// <summary>报文日志（新报文追加到尾部，超出容量截断头部）</summary>
    public ObservableCollection<ProtocolFrame> Logs { get; } = new();

    /// <summary>当前未确认的报警</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlarm))]
    private DeviceAlarm? _currentAlarm;

    /// <summary>暂停日志写入（便于查看报文详情）</summary>
    [ObservableProperty]
    private bool _isLogPaused;

    /// <summary>累计报文数</summary>
    [ObservableProperty]
    private int _frameCount;

    /// <summary>累计报警数</summary>
    [ObservableProperty]
    private int _alarmCount;

    /// <summary>已连接设备数</summary>
    [ObservableProperty]
    private int _connectedCount;

    /// <summary>连接/断开操作进行中（防止重复点击）</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectAllCommand))]
    private bool _isBusy;

    /// <summary>设备总数</summary>
    public int DeviceCount => Cards.Count;

    public bool HasAlarm => CurrentAlarm is not null;

    #endregion

    #region 命令

    /// <summary>一键连接全部设备（带忙状态与异常兜底）</summary>
    [RelayCommand(CanExecute = nameof(CanToggleAll))]
    private async Task ConnectAllAsync()
    {
        IsBusy = true;

        try
        {
            await _manager.ConnectAllAsync();
            SyncCardStates();
            Notify("已请求连接全部模拟设备", StatusNotificationMessage.StatusLevel.Info);
        }
        catch (Exception ex)
        {
            Notify($"连接设备失败：{ex.Message}", StatusNotificationMessage.StatusLevel.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>一键断开全部设备（带忙状态与异常兜底）</summary>
    [RelayCommand(CanExecute = nameof(CanToggleAll))]
    private async Task DisconnectAllAsync()
    {
        IsBusy = true;

        try
        {
            await _manager.DisconnectAllAsync();
            SyncCardStates();
            Notify("已断开全部模拟设备", StatusNotificationMessage.StatusLevel.Info);
        }
        catch (Exception ex)
        {
            Notify($"断开设备失败：{ex.Message}", StatusNotificationMessage.StatusLevel.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>批量操作可用性：非忙时可用</summary>
    private bool CanToggleAll() => !IsBusy;

    /// <summary>清空日志与实时数据</summary>
    [RelayCommand]
    private void ClearLog()
    {
        Logs.Clear();
        Metrics.Clear();
        _metricIndex.Clear();
        _pendingFrames.Clear();
        FrameCount = 0;

        foreach (var card in Cards)
        {
            card.Metrics.Clear();
        }
    }

    /// <summary>确认并清除当前报警</summary>
    [RelayCommand]
    private void AcknowledgeAlarm() => CurrentAlarm = null;

    #endregion

    #region 生命周期（OnActivated / OnDeactivated）

    /// <summary>进入页面才订阅驱动事件并启动闪烁计时器。</summary>
    protected override void OnActivated()
    {
        base.OnActivated();

        _manager.FrameReceived += OnFrameReceived;
        _manager.DriverStateChanged += OnDriverStateChanged;

        SyncCardStates();
        _flashTimer.Start();
        _flushTimer.Start();
    }

    /// <summary>离开页面立即退订，避免后台报文继续驱动界面刷新。</summary>
    protected override void OnDeactivated()
    {
        _manager.FrameReceived -= OnFrameReceived;
        _manager.DriverStateChanged -= OnDriverStateChanged;

        _flashTimer.Stop();
        _flushTimer.Stop();
        ClearFlashing();

        base.OnDeactivated();
    }

    #endregion

    #region 消息接收

    /// <summary>协议层广播的报警：更新警示条。</summary>
    public void Receive(DeviceAlarmMessage message)
    {
        CurrentAlarm = message.Value;
        AlarmCount++;
    }

    #endregion

    #region 私有方法

    /// <summary>驱动报文回调（后台线程）→ 仅入队，由 UI 计时器批量刷新，避免逐帧切换线程。</summary>
    private void OnFrameReceived(object? sender, ProtocolFrame frame) => _pendingFrames.Enqueue(frame);

    private void OnDriverStateChanged(object? sender, IProtocolDriver driver)
        => UiDispatcher.Post(() => ApplyDriverState(driver));

    /// <summary>批量刷新待处理报文（UI 线程，由 _flushTimer 按固定节奏驱动）。</summary>
    private void FlushPendingFrames()
    {
        if (_pendingFrames.IsEmpty)
        {
            return;
        }

        int processed = 0;

        while (_pendingFrames.TryDequeue(out var frame))
        {
            processed++;
            UpdateMetrics(frame);

            if (IsLogPaused)
            {
                continue;
            }

            Logs.Add(frame);
        }

        // 计数一次性累加，避免逐帧触发属性通知
        FrameCount += processed;

        // 环形缓冲：一次性裁剪超出部分，避免逐条 RemoveAt(0) 造成反复位移与重排
        int overflow = Logs.Count - MaxLogItems;
        for (int i = 0; i < overflow; i++)
        {
            Logs.RemoveAt(0);
        }
    }

    /// <summary>把报文中的键值对写入实时数据表（新指标自动建行）。</summary>
    private void UpdateMetrics(ProtocolFrame frame)
    {
        if (frame.Values is null || frame.Values.Count == 0)
        {
            return;
        }

        _cardIndex.TryGetValue(frame.Type, out var card);

        foreach (var (name, value) in frame.Values)
        {
            var key = $"{frame.Type}.{name}";

            if (!_metricIndex.TryGetValue(key, out var item))
            {
                item = new LiveMetricItem(frame.Type, name);
                _metricIndex[key] = item;

                // 同一实例同时挂在卡片与总表上，一次赋值两处同步刷新
                Metrics.Add(item);
                card?.Metrics.Add(item);
            }

            // 值未变化就不触发属性通知与高亮，避免无意义的 UI 刷新
            if (string.Equals(item.Value, value, StringComparison.Ordinal))
            {
                continue;
            }

            item.Value = value;
            item.UpdatedAt = frame.Timestamp;
            item.IsFlashing = true;
        }
    }

    private void ClearFlashing()
    {
        foreach (var item in Metrics)
        {
            if (item.IsFlashing)
            {
                item.IsFlashing = false;
            }
        }
    }

    private void ApplyDriverState(IProtocolDriver driver)
    {
        // 走 _cardIndex 而不是 Cards.FirstOrDefault：本类已维护该索引，
        // 用线性查找既与索引的存在自相矛盾，也会在状态频繁变化时产生无谓扫描。
        if (_cardIndex.TryGetValue(driver.Type, out var card))
        {
            card.State = driver.State;
        }

        ConnectedCount = Cards.Count(c => c.IsConnected);
    }

    private void SyncCardStates()
    {
        foreach (var card in Cards)
        {
            card.State = card.Driver.State;
        }

        ConnectedCount = Cards.Count(c => c.IsConnected);
    }

    private void Notify(string text, StatusNotificationMessage.StatusLevel level)
        => Messenger.Send(new StatusNotificationMessage(text, level));

    #endregion
}
