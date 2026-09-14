using System.Collections.ObjectModel;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Events;
using Prism.Regions;
using PrismDemo.Common;
using PrismDemo.Common.Mvvm;
using PrismDemo.Events;
using PrismDemo.Models;
using PrismDemo.Protocols;
using PrismDemo.Services;

namespace PrismDemo.ViewModels;

/// <summary>
/// 主窗口（导航外壳）ViewModel。
///
/// 覆盖的 Prism 知识点：
/// 1. <see cref="IRegionManager"/> —— 用"区域 + 导航请求"替代手写的 CurrentViewModel 切换；
/// 2. <c>IEventAggregator</c> —— 订阅 6 类全局事件（购物车/提示/报警/支付/设置/导航）；
/// 3. <c>BindableBase.SetProperty</c> —— 手写属性通知，并用 <c>RaisePropertyChanged</c> 联动派生属性；
/// 4. <c>DelegateCommand</c> —— 方法 → ICommand；
/// 5. 属性 setter 内做"变化后处理" —— 取代原来的 OnXxxChanged 分部钩子。
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// 内容区域名。三个页面视图在此区域内切换。
    /// </summary>
    /// <remarks>
    /// 与 <c>MainWindow.xaml</c> 中的
    /// <c>prism:RegionManager.RegionName="ContentRegion"</c> 必须保持一致。
    /// </remarks>
    public const string ContentRegionName = "ContentRegion";

    private const string IdleMessage = "就绪 · 请选择商品开始收银";

    private readonly IRegionManager _regionManager;
    private readonly IProtocolManager _protocolManager;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _statusTimer;

    /// <summary>协议类型 → 标题栏状态灯项：状态变化时按类型直达，避免线性查找</summary>
    private readonly Dictionary<ProtocolType, ProtocolStatusItem> _statusIndex = new();

    /// <summary>上次刷新日期的"天"，用于日期文本按天更新，避免每秒重复格式化</summary>
    private DateTime _lastClockDate = DateTime.MinValue;

    private NavItem? _selectedNavItem;
    private string _storeName = "智汇便利 · 001 门店";
    private string _cashierName = "工号 001 · 前台收银";
    private string _currentTime = "--:--:--";
    private string _currentDate = string.Empty;
    private string _statusMessage = IdleMessage;
    private StatusLevel _statusLevel = StatusLevel.Info;
    private int _cartItemCount;
    private decimal _cartTotal;

    public MainWindowViewModel(
        IRegionManager regionManager,
        IProtocolManager protocolManager,
        ISettingsService settings,
        IEventAggregator eventAggregator) : base(eventAggregator)
    {
        _regionManager = regionManager;
        _protocolManager = protocolManager;

        // 顶部门店名称来自设置（可被设置页修改）
        _storeName = settings.Current.StoreName;

        NavItems = new ObservableCollection<NavItem>
        {
            new(NavigateEvent.Pages.Cashier, "收银台", "\uE7BF"),
            new(NavigateEvent.Pages.Device, "设备监控", "\uE9D9"),
            new(NavigateEvent.Pages.Settings, "设置", "\uE713")
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
        // （设备报警由 ProtocolMessageBridge 负责翻译成 DeviceAlarmEvent，本类只需订阅事件）
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
            StatusLevel = StatusLevel.Info;
        };

        NavigateCommand = new DelegateCommand<string>(Navigate);

        SubscribeEvents();

        // 默认选中收银台。
        // 注意这里直接给字段赋值：外壳此时刚被容器创建，ContentRegion 还没生成，
        // 真正的首次导航由 App 在 OnInitialized 里调用 NavigateToInitialPage() 触发。
        _selectedNavItem = NavItems[0];

        // 外壳始终处于激活状态（区域导航所需的激活语义由各页面自行负责）
        IsActive = true;
    }

    #region 绑定数据

    public ObservableCollection<NavItem> NavItems { get; }

    public ObservableCollection<ProtocolStatusItem> ProtocolStatuses { get; }

    /// <summary>当前选中的导航项。变化即请求区域导航。</summary>
    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            if (SetProperty(ref _selectedNavItem, value))
            {
                OnSelectedNavItemChanged(value);
            }
        }
    }

    public string StoreName
    {
        get => _storeName;
        set => SetProperty(ref _storeName, value);
    }

    public string CashierName
    {
        get => _cashierName;
        set => SetProperty(ref _cashierName, value);
    }

    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    public string CurrentDate
    {
        get => _currentDate;
        set => SetProperty(ref _currentDate, value);
    }

    /// <summary>底部提示条文本（同时决定提示条是否显示）</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                RaisePropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    /// <summary>提示级别（决定提示点颜色）</summary>
    public StatusLevel StatusLevel
    {
        get => _statusLevel;
        set => SetProperty(ref _statusLevel, value);
    }

    /// <summary>购物车件数（由购物车事件驱动）</summary>
    public int CartItemCount
    {
        get => _cartItemCount;
        set => SetProperty(ref _cartItemCount, value);
    }

    /// <summary>购物车合计（由购物车事件驱动）</summary>
    public decimal CartTotal
    {
        get => _cartTotal;
        set => SetProperty(ref _cartTotal, value);
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    #endregion

    #region 命令

    /// <summary>导航命令：任意控件（含标题栏协议灯）都可调用</summary>
    public DelegateCommand<string> NavigateCommand { get; }

    #endregion

    #region 导航

    /// <summary>
    /// 执行首次导航。必须在 <c>ContentRegion</c> 已注册之后调用
    /// （由 <c>App.OnInitialized</c> 在外壳显示、区域就绪后触发）。
    /// </summary>
    public void NavigateToInitialPage()
    {
        if (_selectedNavItem is { } item)
        {
            NavigateToPage(item.Key);
        }
    }

    /// <summary>导航项变化 → 通知订阅方并请求区域导航。</summary>
    private void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null)
        {
            return;
        }

        NavigateToPage(value.Key);
    }

    /// <summary>导航命令的实际动作：把选中项切到目标页面（已在目标页则不做任何事）。</summary>
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

    /// <summary>
    /// 切换到指定页面。
    /// </summary>
    /// <remarks>
    /// 顺序很重要：先广播"当前页面已切换"，再请求区域导航。
    /// 设备监控页首次进入时会随视图创建而被实例化，它在构造时就把自己置为激活；
    /// 之后每次切换都由它自己根据本事件订阅/退订高频报文，因此这里不需要（也不应该）
    /// 反过来持有页面 ViewModel 的引用。
    /// </remarks>
    private void NavigateToPage(string pageKey)
    {
        Publish<NavigateEvent, string>(pageKey);

        // 外壳构造阶段区域尚未注册，此时静默跳过；App.OnInitialized 会补齐首次导航
        if (!_regionManager.Regions.ContainsRegionWithName(ContentRegionName))
        {
            return;
        }

        _regionManager.RequestNavigate(ContentRegionName, pageKey);
    }

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        Subscribe<CartChangedEvent, CartSummary>(OnCartChanged);
        Subscribe<StatusNotificationEvent, StatusNotification>(OnStatusNotification);
        Subscribe<DeviceAlarmEvent, DeviceAlarm>(OnDeviceAlarm);
        Subscribe<PaymentCompletedEvent, Order>(OnPaymentCompleted);
        Subscribe<SettingsChangedEvent, PosSettings>(OnSettingsChanged);
        Subscribe<NavigateEvent, string>(OnNavigateRequested);
    }

    private void OnCartChanged(CartSummary summary)
    {
        CartItemCount = summary.ItemCount;
        CartTotal = summary.Total;
    }

    private void OnStatusNotification(StatusNotification notification)
    {
        StatusMessage = notification.Text;
        StatusLevel = notification.Level;
        RestartStatusTimer();
    }

    private void OnDeviceAlarm(DeviceAlarm alarm)
    {
        StatusMessage = $"[{alarm.DeviceName}] {alarm.Message}";
        StatusLevel = StatusLevel.Warning;
        RestartStatusTimer();
    }

    private void OnPaymentCompleted(Order order)
    {
        StatusMessage = $"订单 {order.OrderNo} 支付成功，收款 ¥{order.Total:N2}（{order.PaymentText}）";
        StatusLevel = StatusLevel.Success;
        RestartStatusTimer();
    }

    /// <summary>设置保存后同步门店名称。</summary>
    private void OnSettingsChanged(PosSettings settings) => StoreName = settings.StoreName;

    /// <summary>响应来自其它模块的导航请求（如收银台结账后跳转）。</summary>
    private void OnNavigateRequested(string pageKey)
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
    /// 释放资源：退订协议事件与全局事件、停止内部计时器。
    /// 单例 ViewModel 若持有事件订阅而不释放，会导致应用运行期间持续回调与内存驻留。
    /// </summary>
    protected override void DisposeCore()
    {
        _protocolManager.DriverStateChanged -= OnDriverStateChanged;

        _clockTimer.Stop();
        _statusTimer.Stop();

        base.DisposeCore();
    }

    #endregion
}
