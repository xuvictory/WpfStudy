using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Prism.DryIoc;
using Prism.Events;
using Prism.Ioc;
using PrismDemo.Common;
using PrismDemo.Common.Dialogs;
using PrismDemo.Events;
using PrismDemo.Protocols;
using PrismDemo.Protocols.Simulated;
using PrismDemo.Services;
using PrismDemo.ViewModels;
using PrismDemo.Views;
// 项目内的对话框抽象与 Prism 的 IDialogService/DialogService 同名，
// 这里用别名区分，避免注册时产生歧义。
using AppDialogService = PrismDemo.Services.IDialogService;
using AppDialogServiceImpl = PrismDemo.Services.DialogService;

namespace PrismDemo;

/// <summary>
/// 应用入口。
///
/// 覆盖的 Prism 知识点：
/// <list type="number">
/// <item><b>PrismApplication</b> —— 取代"手写 ServiceCollection + Ioc.Default"，
/// 由框架接管容器创建与生命周期；</item>
/// <item><b>RegisterTypes(IContainerRegistry)</b> —— 集中注册类型（本项目使用 DryIoc）；</item>
/// <item><b>CreateShell()</b> —— 由容器解析外壳窗口，窗口自身也能享受依赖注入；</item>
/// <item><b>OnInitialized()</b> —— 外壳已创建/显示后的初始化时机（首次区域导航在此发起）；</item>
/// <item><b>IsRegistered&lt;T&gt; / Resolve&lt;T&gt;</b> —— 容器查询与解析。</item>
/// </list>
///
/// 迁移前的 Ioc.Default 只用于"兜底取服务"，项目本身就以构造函数注入为主，
/// 因此 Prism 化之后绝大多数类不需要改动注入方式，只是注册地从 ServiceCollection 换成 IContainerRegistry。
/// </summary>
public partial class App : PrismApplication
{
    /// <summary>
    /// 协议消息桥接器（协议事件 → 全局事件）。
    /// </summary>
    /// <remarks>
    /// 必须在启动时创建并保持存活：它只有在构造函数中订阅了协议事件之后才能工作。
    /// 用字段持有（而不是"解析后丢弃返回值"）可以让生命周期一目了然，避免被误当作无用代码删除。
    /// </remarks>
    private ProtocolMessageBridge? _bridge;

    public App()
    {
        // 越早订阅越好：即使 Prism 尚未完成初始化，UI 线程上的异常也能落盘并提示。
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    /// <summary>
    /// 创建外壳窗口。交给容器解析后，MainWindow 也可以直接注入服务，
    /// 且 <c>prism:ViewModelLocator.AutoWireViewModel</c> 能按约定装配 MainWindowViewModel。
    /// </summary>
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    /// <summary>集中注册所有类型，注册顺序即依赖顺序（与迁移前的 ConfigureServices 一一对应）。</summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // ---------- 基础设施 ----------
        // IEventAggregator 由 Prism 默认注册为全局单例（充当迁移前 WeakReferenceMessenger.Default 的角色），
        // 这里刻意不重复注册，避免出现两个事件总线实例。

        // ---------- 数据访问层 ----------
        containerRegistry.RegisterSingleton<PosRepository>();

        // 仓储按"最小契约"对外暴露：调用方只依赖自己真正需要的能力（商品目录 / 设备配置）。
        // 两个接口都用工厂解析到同一个 PosRepository 单例，因此"全应用唯一数据落点"的语义不变；
        // 注意不要用 RegisterSingleton<IProductCatalog, PosRepository>()，那会各自建立实例，
        // 破坏"同一份数据"的前提。
        containerRegistry.RegisterSingleton<IProductCatalog>(() => Container.Resolve<PosRepository>());
        containerRegistry.RegisterSingleton<IDeviceCatalog>(() => Container.Resolve<PosRepository>());

        containerRegistry.RegisterSingleton<IMarkdownDataService, MarkdownDataService>();

        // ---------- 通信层（模拟协议驱动） ----------
        // 每种协议注册为 IProtocolDriver：新增协议只需在此追加一行，ProtocolManager 无需改动。
        // 重复注册同一契约不会互相覆盖，容器会把它们聚合成 IEnumerable<IProtocolDriver> 注入 ProtocolManager。
        containerRegistry.RegisterSingleton<IProtocolDriver, ModbusTcpSimDriver>();
        containerRegistry.RegisterSingleton<IProtocolDriver, OpcUaSimDriver>();
        containerRegistry.RegisterSingleton<IProtocolDriver, SerialPortSimDriver>();
        containerRegistry.RegisterSingleton<IProtocolDriver, SocketSimDriver>();
        containerRegistry.RegisterSingleton<IProtocolDriver, MqttSimDriver>();

        containerRegistry.RegisterSingleton<IProtocolManager, ProtocolManager>();

        // ---------- 业务服务层 ----------
        containerRegistry.RegisterSingleton<IProductQuery, ProductQueryService>();

        // 均为单例：购物车、订单、门店设置需要在页面切换间保持状态
        containerRegistry.RegisterSingleton<ISettingsService, SettingsService>();
        containerRegistry.RegisterSingleton<ICartService, CartService>();
        containerRegistry.RegisterSingleton<IOrderService, OrderService>();

        // 对话框：业务层依赖项目内的 IDialogService 抽象，由它转调 Prism 的 IDialogService
        containerRegistry.RegisterSingleton<AppDialogService, AppDialogServiceImpl>();

        // 结算外设编排（打印 / 开钱箱 / 云端上报）：与界面解耦，可单独验证
        containerRegistry.RegisterSingleton<CheckoutCoordinator>();

        // 协议报文 → 全局事件的桥接器（透明翻译，页面无需感知协议细节）
        containerRegistry.RegisterSingleton<ProtocolMessageBridge>();

        // ---------- 对话框宿主与内容 ----------
        // 自定义宿主窗口：外观（无边框 + 透明 + 相对主窗口居中）与原 DialogWindow 一致
        containerRegistry.RegisterDialogWindow<NotificationDialogWindow>();

        // 对话框内容视图 + 其 ViewModel（实现 Prism 的 IDialogAware）
        containerRegistry.Register<NotificationDialogViewModel>();
        containerRegistry.RegisterDialog<NotificationDialogView, NotificationDialogViewModel>(
            DialogServiceExtensions.NotificationDialogName);

        // ---------- 窗口 / 页面 ViewModel ----------
        containerRegistry.RegisterSingleton<MainWindow>();

        // 页面 ViewModel 使用单例：切换页面时保留状态（购物车、日志等）。
        // 视图在每次导航时新建，但状态载体是这些单例，因此"切页不丢数据"的语义不变。
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.RegisterSingleton<CashierViewModel>();
        containerRegistry.RegisterSingleton<DeviceMonitorViewModel>();
        containerRegistry.RegisterSingleton<SettingsViewModel>();

        // ---------- 区域导航 ----------
        // 把"页面 key → 视图类型"登记到容器，导航时由容器创建视图实例。
        containerRegistry.RegisterForNavigation<CashierView>(NavigateEvent.Pages.Cashier);
        containerRegistry.RegisterForNavigation<DeviceMonitorView>(NavigateEvent.Pages.Device);
        containerRegistry.RegisterForNavigation<SettingsView>(NavigateEvent.Pages.Settings);
    }

    /// <summary>
    /// 外壳已就绪后的初始化。
    /// </summary>
    /// <remarks>
    /// 顺序经过刻意安排：
    /// <list type="number">
    /// <item>先建立协议桥接（订阅协议事件），否则后续连接设备产生的报文无人转发；</item>
    /// <item>再导航到首屏 —— 页面单例 ViewModel 会在此时被创建并完成事件订阅，
    /// 因此后面的"数据就绪"广播一定不会早于订阅发生；</item>
    /// <item>最后才在后台加载数据、连接设备，保证"窗口先显示、数据后到"，
    /// 不会出现启动白屏（替代迁移前的 sync-over-async 写法）。</item>
    /// </list>
    /// </remarks>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        _bridge = Container.Resolve<ProtocolMessageBridge>();

        // ContentRegion 要等外壳的视觉树加载后才存在，因此首次导航挂在 Loaded 上（已加载则立即执行）。
        if (MainWindow is { } shell)
        {
            if (shell.IsLoaded)
            {
                NavigateToInitialPage();
            }
            else
            {
                shell.Loaded += (_, _) => NavigateToInitialPage();
            }
        }

        _ = InitializeAsync();
    }

    /// <summary>后台初始化：加载 Markdown 数据源 → 广播数据就绪 → 连接全部模拟设备。</summary>
    private async Task InitializeAsync()
    {
        try
        {
            var dataService = Container.Resolve<IMarkdownDataService>();
            await dataService.LoadAsync();

            // 数据就绪广播：页面据此刷新（如收银台重建分类 / 商品视图）
            var eventAggregator = Container.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<DataLoadedEvent>().Publish(new DataLoaded(
                dataService.LoadErrors.Count == 0,
                dataService.LoadErrors.ToArray(),
                DateTime.Now));

            // 数据就绪后再连接设备（驱动会按仓储中的设备配置建立会话）
            await Container.Resolve<IProtocolManager>().ConnectAllAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[启动] 初始化失败：{ex}");

            // 启动阶段的失败如果只写 Debug，用户看到的是"界面空白且毫无提示"，无从排查；
            // 因此同时经全局提示条告知（主窗口此时已显示，提示条可直接呈现）。
            NotifyStatus($"应用初始化失败，部分数据可能不可用：{ex.Message}", StatusLevel.Error);
        }
    }

    /// <summary>执行首次导航（目标页由 MainWindowViewModel 记录的默认导航项决定）。</summary>
    private void NavigateToInitialPage()
        => (MainWindow?.DataContext as MainWindowViewModel)?.NavigateToInitialPage();

    /// <summary>向全局提示条推送一条状态（自动切回 UI 线程）。</summary>
    private void NotifyStatus(string text, StatusLevel level)
    {
        if (!Container.IsRegistered<IEventAggregator>())
        {
            return;
        }

        var eventAggregator = Container.Resolve<IEventAggregator>();
        UiDispatcher.Post(() => eventAggregator.GetEvent<StatusNotificationEvent>()
            .Publish(new StatusNotification(text, level)));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 先退订协议事件（Dispose 幂等）
        _bridge?.Dispose();
        _bridge = null;

        // 再断开全部协议驱动（优雅停止后台仿真循环），并释放驱动持有的资源
        if (Container.IsRegistered<IProtocolManager>())
        {
            try
            {
                var manager = Container.Resolve<IProtocolManager>();
                manager.DisconnectAllAsync().GetAwaiter().GetResult();
                manager.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[退出] 断开协议失败：{ex.Message}");
            }
        }

        // 释放主窗口 ViewModel：退订协议事件与全局事件、停止内部计时器。
        // 页面 ViewModel 与其它单例不再逐一强制创建后释放：它们要么已被上面的 manager 释放连带清理，
        // 要么其资源（计时器 / 订阅）随进程退出自然回收，此时强行实例化反而可能触发无谓的收尾逻辑。
        if (Container.IsRegistered<MainWindowViewModel>()
            && Container.Resolve<MainWindowViewModel>() is IDisposable mainViewModel)
        {
            mainViewModel.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[UI 未处理异常] {e.Exception}");

        // 详情落盘、界面只给可读文案：
        // 直接把 Exception.Message 弹给用户既可能暴露内部实现细节，也无法留存现场供后续排查。
        var logPath = AppendErrorLog(e.Exception);

        MessageBox.Show(
            logPath is null
                ? "操作未能完成，请重试。"
                : $"操作未能完成，请重试。\n\n错误详情已记录到：\n{logPath}",
            "系统提示",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    /// <summary>
    /// 将异常详情追加写入本地日志文件，返回日志路径；写入失败时返回 null。
    /// </summary>
    /// <remarks>
    /// 这里绝不能向外抛异常：否则会在未处理异常处理器内部再产生一个未处理异常，
    /// 造成递归崩溃（表现为进程直接退出、看不到任何提示）。
    /// </remarks>
    private static string? AppendErrorLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrismDemo");

            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "error.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");

            return path;
        }
        catch (Exception logError)
        {
            Debug.WriteLine($"[日志] 写入错误日志失败：{logError.Message}");
            return null;
        }
    }
}
