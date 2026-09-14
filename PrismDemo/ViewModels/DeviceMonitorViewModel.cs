using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Events;
using PrismDemo.Common;
using PrismDemo.Common.Commands;
using PrismDemo.Common.Mvvm;
using PrismDemo.Events;
using PrismDemo.Models;
using PrismDemo.Protocols;

namespace PrismDemo.ViewModels;

/// <summary>
/// 设备监控 ViewModel：5 种协议的连接开关、实时数据表、报警与报文日志。
///
/// 覆盖的 Prism 知识点：
/// - <see cref="Prism.IActiveAware"/> + <c>OnActivated/OnDeactivated</c>：离开页面即退订驱动事件
///   与停止闪烁计时器，回来再恢复，避免无谓的 UI 刷新；
/// - <c>IEventAggregator</c> 的"激活期订阅"：报警事件只在页面可见时接收。
/// </summary>
public class DeviceMonitorViewModel : ViewModelBase
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

    private DeviceAlarm? _currentAlarm;
    private bool _isLogPaused;
    private int _frameCount;
    private int _alarmCount;
    private int _connectedCount;
    private bool _isBusy;

    private AsyncDelegateCommand? _connectAllCommand;
    private AsyncDelegateCommand? _disconnectAllCommand;
    private DelegateCommand? _clearLogCommand;
    private DelegateCommand? _acknowledgeAlarmCommand;

    public DeviceMonitorViewModel(IProtocolManager manager, IEventAggregator eventAggregator) : base(eventAggregator)
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

        // 页面导航通知：本页自己决定何时进入/离开激活状态，
        // 这样导航外壳（MainWindowViewModel）无需持有页面 ViewModel 的引用。
        Subscribe<NavigateEvent, string>(OnPageNavigated);

        // 首次进入设备监控页（本 ViewModel 随视图创建而被实例化）即进入激活状态
        IsActive = true;
    }

    #region 绑定数据

    /// <summary>协议设备卡片</summary>
    public ObservableCollection<DeviceCardViewModel> Cards { get; }

    /// <summary>实时数据表（所有协议指标聚合）</summary>
    public ObservableCollection<LiveMetricItem> Metrics { get; } = new();

    /// <summary>报文日志（新报文追加到尾部，超出容量截断头部）</summary>
    public ObservableCollection<ProtocolFrame> Logs { get; } = new();

    /// <summary>当前未确认的报警</summary>
    public DeviceAlarm? CurrentAlarm
    {
        get => _currentAlarm;
        set
        {
            if (SetProperty(ref _currentAlarm, value))
            {
                RaisePropertyChanged(nameof(HasAlarm));
            }
        }
    }

    /// <summary>暂停日志写入（便于查看报文详情）</summary>
    public bool IsLogPaused
    {
        get => _isLogPaused;
        set => SetProperty(ref _isLogPaused, value);
    }

    /// <summary>累计报文数</summary>
    public int FrameCount
    {
        get => _frameCount;
        set => SetProperty(ref _frameCount, value);
    }

    /// <summary>累计报警数</summary>
    public int AlarmCount
    {
        get => _alarmCount;
        set => SetProperty(ref _alarmCount, value);
    }

    /// <summary>已连接设备数</summary>
    public int ConnectedCount
    {
        get => _connectedCount;
        set => SetProperty(ref _connectedCount, value);
    }

    /// <summary>
    /// 连接/断开操作进行中（防止重复点击）。
    /// 变化时刷新两个批量命令的可用性（原 <c>[NotifyCanExecuteChangedFor]</c> 的效果）。
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            ConnectAllCommand.RaiseCanExecuteChanged();
            DisconnectAllCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>设备总数</summary>
    public int DeviceCount => Cards.Count;

    public bool HasAlarm => CurrentAlarm is not null;

    #endregion

    #region 命令

    /// <summary>一键连接全部设备（带忙状态与异常兜底）</summary>
    public AsyncDelegateCommand ConnectAllCommand
        => _connectAllCommand ??= new AsyncDelegateCommand(ConnectAllAsync, CanToggleAll);

    /// <summary>一键断开全部设备（带忙状态与异常兜底）</summary>
    public AsyncDelegateCommand DisconnectAllCommand
        => _disconnectAllCommand ??= new AsyncDelegateCommand(DisconnectAllAsync, CanToggleAll);

    /// <summary>清空日志与实时数据</summary>
    public DelegateCommand ClearLogCommand
        => _clearLogCommand ??= new DelegateCommand(ClearLog);

    /// <summary>确认并清除当前报警</summary>
    public DelegateCommand AcknowledgeAlarmCommand
        => _acknowledgeAlarmCommand ??= new DelegateCommand(AcknowledgeAlarm);

    #endregion

    #region 命令实现

    private async Task ConnectAllAsync()
    {
        IsBusy = true;

        try
        {
            await _manager.ConnectAllAsync();
            SyncCardStates();
            Notify("已请求连接全部模拟设备", StatusLevel.Info);
        }
        catch (Exception ex)
        {
            Notify($"连接设备失败：{ex.Message}", StatusLevel.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectAllAsync()
    {
        IsBusy = true;

        try
        {
            await _manager.DisconnectAllAsync();
            SyncCardStates();
            Notify("已断开全部模拟设备", StatusLevel.Info);
        }
        catch (Exception ex)
        {
            Notify($"断开设备失败：{ex.Message}", StatusLevel.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>批量操作可用性：非忙时可用</summary>
    private bool CanToggleAll() => !IsBusy;

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

    private void AcknowledgeAlarm() => CurrentAlarm = null;

    #endregion

    #region 生命周期（OnActivated / OnDeactivated）

    /// <summary>进入页面才订阅驱动事件并启动闪烁计时器。</summary>
    protected override void OnActivated()
    {
        base.OnActivated();

        _manager.FrameReceived += OnFrameReceived;
        _manager.DriverStateChanged += OnDriverStateChanged;

        // 报警事件同样只在页面可见时接收
        SubscribeWhileActive<DeviceAlarmEvent, DeviceAlarm>(OnDeviceAlarm);

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

        // 退订"仅激活期间有效"的报警订阅（保留 NavigateEvent 等常驻订阅）
        base.OnDeactivated();
    }

    #endregion

    #region 事件订阅

    /// <summary>页面切换：只有设备监控页可见时才激活本 ViewModel。</summary>
    private void OnPageNavigated(string pageKey)
        => IsActive = pageKey == NavigateEvent.Pages.Device;

    /// <summary>协议层广播的报警：更新警示条。</summary>
    private void OnDeviceAlarm(DeviceAlarm alarm)
    {
        CurrentAlarm = alarm;
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

    private void Notify(string text, StatusLevel level)
        => Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(text, level));

    #endregion

    #region 生命周期（释放）

    /// <summary>释放资源：停止计时器、退订驱动事件与全局事件。</summary>
    protected override void DisposeCore()
    {
        _manager.FrameReceived -= OnFrameReceived;
        _manager.DriverStateChanged -= OnDriverStateChanged;

        _flashTimer.Stop();
        _flushTimer.Stop();

        base.DisposeCore();
    }

    #endregion
}
