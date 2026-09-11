using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Common;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Protocols;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>
/// 主窗口（导航外壳）ViewModel。
///
/// 覆盖的 CommunityToolkit 知识点：
/// 1. <see cref="ObservableRecipient"/> —— 既具备 ObservableObject 能力，又内置消息接收；
/// 2. <c>IRecipient&lt;T&gt;</c> —— 声明式订阅，配合 IsActive = true 自动注册/注销；
/// 3. <c>[ObservableProperty]</c> —— 字段 → 属性源生成；
/// 4. <c>[RelayCommand]</c> —— 方法 → ICommand 源生成；
/// 5. <c>OnXxxChanged</c> 分部钩子 —— 属性变化时执行自定义逻辑。
/// </summary>
public partial class MainWindowViewModel
    : ObservableRecipient,
      IRecipient<CartChangedMessage>,
      IRecipient<StatusNotificationMessage>,
      IRecipient<DeviceAlarmMessage>,
      IRecipient<PaymentCompletedMessage>,
      IRecipient<SettingsChangedMessage>,
      IRecipient<NavigateMessage>,
      IDisposable
{
    /// <summary>页面标识 → ViewModel 类型（配合 Ioc 容器解析）</summary>
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        [NavigateMessage.Pages.Cashier] = typeof(CashierViewModel),
        [NavigateMessage.Pages.Device] = typeof(DeviceMonitorViewModel),
        [NavigateMessage.Pages.Settings] = typeof(SettingsViewModel)
    };

    private const string IdleMessage = "就绪 · 请选择商品开始收银";

    /// <summary>
    /// 页面 ViewModel 工厂：按类型解析页面实例。
    /// </summary>
    /// <remarks>
    /// 这里注入的是"解析一个类型"这一件事，而不是整个 <see cref="IServiceProvider"/>：
    /// 后者等于把服务容器交给 ViewModel，任何依赖都能被随手取出，
    /// 既看不出真实依赖，也无法在测试中替换。收窄成委托后，本类的依赖面一目了然，
    /// 同时保留"导航时才创建页面"的惰性行为与单例语义。
    /// </remarks>
    private readonly Func<Type, ObservableObject?> _pageFactory;

    private readonly IProtocolManager _protocolManager;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _statusTimer;

    /// <summary>协议类型 → 标题栏状态灯项：状态变化时按类型直达，避免线性查找</summary>
    private readonly Dictionary<ProtocolType, ProtocolStatusItem> _statusIndex = new();

    /// <summary>上次刷新日期的"天"，用于日期文本按天更新，避免每秒重复格式化</summary>
    private DateTime _lastClockDate = DateTime.MinValue;

    private bool _disposed;

    public MainWindowViewModel(
        Func<Type, ObservableObject?> pageFactory,
        IProtocolManager protocolManager,
        ISettingsService settings,
        IMessenger messenger) : base(messenger)
    {
        _pageFactory = pageFactory;
        _protocolManager = protocolManager;

        // 顶部门店名称来自设置（可被设置页修改）
        StoreName = settings.Current.StoreName;

        NavItems = new ObservableCollection<NavItem>
        {
            new(NavigateMessage.Pages.Cashier, "收银台", "\uE7BF"),
            new(NavigateMessage.Pages.Device, "设备监控", "\uE9D9"),
            new(NavigateMessage.Pages.Settings, "设置", "\uE713")
        };

        // 标题栏 5 个协议状态灯：直接由协议管理器中的驱动实例构建，并用当前状态初始化
        ProtocolStatuses = new ObservableCollection<ProtocolStatusItem>(
            protocolManager.Drivers.Select(d => new ProtocolStatusItem(d.Type, ProtocolDisplay.ProtocolShortName(d.Type), d.Name)
            {
                State = d.State
            }));

        foreach (var status in ProtocolStatuses)
        {
            _statusIndex[status.Type] = status;
        }

        // 订阅协议状态：事件来自后台线程，统一经 UiDispatcher 切回 UI 线程
        // （设备报警由 ProtocolMessageBridge 负责翻译成 DeviceAlarmMessage，本类只需接收消息）
        _protocolManager.DriverStateChanged += OnDriverStateChanged;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusMessage = IdleMessage;
            StatusLevel = StatusNotificationMessage.StatusLevel.Info;
        };

        // 默认展示收银台，OnSelectedNavItemChanged 会完成页面解析
        SelectedNavItem = NavItems[0];

        // 激活消息接收：ObservableRecipient 内部会调用 Messenger.RegisterAll(this)
        IsActive = true;
    }

    #region 绑定数据

    public ObservableCollection<NavItem> NavItems { get; }

    public ObservableCollection<ProtocolStatusItem> ProtocolStatuses { get; }

    /// <summary>当前选中的导航项</summary>
    [ObservableProperty]
    private NavItem? _selectedNavItem;

    /// <summary>内容区承载的当前页面 ViewModel（配合隐式 DataTemplate 自动渲染 View）</summary>
    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private string _storeName = "智汇便利 · 001 门店";

    [ObservableProperty]
    private string _cashierName = "工号 001 · 前台收银";

    [ObservableProperty]
    private string _currentTime = "--:--:--";

    [ObservableProperty]
    private string _currentDate = string.Empty;

    /// <summary>底部提示条文本</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = IdleMessage;

    /// <summary>提示级别（决定提示点颜色）</summary>
    [ObservableProperty]
    private StatusNotificationMessage.StatusLevel _statusLevel = StatusNotificationMessage.StatusLevel.Info;

    /// <summary>购物车件数（由购物车消息驱动）</summary>
    [ObservableProperty]
    private int _cartItemCount;

    /// <summary>购物车合计（由购物车消息驱动）</summary>
    [ObservableProperty]
    private decimal _cartTotal;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    #endregion

    #region 命令

    /// <summary>导航命令：任意控件（含标题栏协议灯）都可调用</summary>
    [RelayCommand]
    private void Navigate(string? pageKey)
    {
        if (string.IsNullOrWhiteSpace(pageKey))
        {
            return;
        }

        var target = NavItems.FirstOrDefault(i => i.Key == pageKey);
        if (target is not null && !ReferenceEquals(target, SelectedNavItem))
        {
            SelectedNavItem = target;
        }
    }

    #endregion

    #region 属性变化钩子

    /// <summary>源生成器产生的分部钩子：导航项变化即切换页面。</summary>
    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null || !PageMap.TryGetValue(value.Key, out var viewModelType))
        {
            return;
        }

        // 页面 ViewModel 均为单例，切换时保留状态（如购物车）
        if (_pageFactory(viewModelType) is not { } viewModel)
        {
            return;
        }

        // ObservableRecipient 知识点：IsActive 控制消息订阅的注册/注销。
        // 设备监控页会产生大量报文，离开时置为非激活以停止订阅，回到页面再恢复。
        // 注意：仅按"当前/目标实例类型"判断，避免为了激活而解析 DeviceMonitorViewModel 导致提前实例化。
        if (CurrentViewModel is DeviceMonitorViewModel leaving && !ReferenceEquals(leaving, viewModel))
        {
            leaving.IsActive = false;
        }

        if (viewModel is DeviceMonitorViewModel entering)
        {
            entering.IsActive = true;
        }

        CurrentViewModel = viewModel;
    }

    #endregion

    #region 消息接收

    public void Receive(CartChangedMessage message)
    {
        CartItemCount = message.Value.ItemCount;
        CartTotal = message.Value.Total;
    }

    public void Receive(StatusNotificationMessage message)
    {
        StatusMessage = message.Value.Text;
        StatusLevel = message.Value.Level;
        RestartStatusTimer();
    }

    public void Receive(DeviceAlarmMessage message)
    {
        StatusMessage = $"[{message.Value.DeviceName}] {message.Value.Message}";
        StatusLevel = StatusNotificationMessage.StatusLevel.Warning;
        RestartStatusTimer();
    }

    public void Receive(PaymentCompletedMessage message)
    {
        StatusMessage = $"订单 {message.Value.OrderNo} 支付成功，收款 ¥{message.Value.Total:N2}（{message.Value.PaymentText}）";
        StatusLevel = StatusNotificationMessage.StatusLevel.Success;
        RestartStatusTimer();
    }

    public void Receive(NavigateMessage message) => Navigate(message.Value);

    /// <summary>设置保存后同步门店名称。</summary>
    public void Receive(SettingsChangedMessage message) => StoreName = message.Value.StoreName;

    #endregion

    #region 私有方法

    /// <summary>协议状态变化（后台线程）→ 刷新标题栏状态灯。</summary>
    private void OnDriverStateChanged(object? sender, IProtocolDriver driver)
        => UiDispatcher.Post(() =>
        {
            if (_statusIndex.TryGetValue(driver.Type, out var item))
            {
                item.State = driver.State;
            }
        });

    private void UpdateClock()
    {
        var now = DateTime.Now;
        CurrentTime = now.ToString("HH:mm:ss");

        // 日期（含星期）只在跨天时重新格式化，避免每秒触发一次字符串分配与文本布局
        if (now.Date != _lastClockDate)
        {
            _lastClockDate = now.Date;
            CurrentDate = now.ToString("yyyy年MM月dd日 dddd");
        }
    }

    private void RestartStatusTimer()
    {
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    #endregion

    #region 生命周期

    /// <summary>
    /// 释放资源：退订协议事件、停止内部计时器、停用当前页面并注销消息接收。
    /// 单例 ViewModel 若持有事件订阅而不释放，会导致应用运行期间持续回调与内存驻留。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _protocolManager.DriverStateChanged -= OnDriverStateChanged;

        _clockTimer.Stop();
        _statusTimer.Stop();

        // 当前若停留在设备监控页，停用它会顺带退订高频报文事件
        if (CurrentViewModel is DeviceMonitorViewModel monitor)
        {
            monitor.IsActive = false;
        }

        // ObservableRecipient：IsActive = false 会注销所有 IRecipient 订阅
        IsActive = false;

        GC.SuppressFinalize(this);
    }

    #endregion
}
