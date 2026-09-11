using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Common;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Protocols;
using CommunityToolkitDemo.Protocols.Simulated;
using CommunityToolkitDemo.Services;
using CommunityToolkitDemo.ViewModels;
using CommunityToolkitDemo.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkitDemo;

/// <summary>
/// 应用入口：负责搭建依赖注入容器并初始化 CommunityToolkit 的 Ioc。
///
/// 知识点：CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.ConfigureServices
/// 让整个应用可以直接通过 Ioc.Default.GetService&lt;T&gt;() 拿到服务；
/// 同时我们仍然以"构造函数注入"为主，Ioc 只是兜底通道。
/// </summary>
public partial class App : Application
{
    /// <summary>全局服务提供器（窗口关闭时用于释放资源）</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// 协议消息桥接器（协议事件 → 全局消息）。
    /// </summary>
    /// <remarks>
    /// 必须在启动时创建并保持存活：它只有在构造函数中订阅了协议事件之后才能工作。
    /// 之前这里用"解析后丢弃返回值"的方式触发副作用，读代码的人很容易误以为是无用代码而删掉；
    /// 改为字段持有后，生命周期一目了然。
    /// </remarks>
    private ProtocolMessageBridge? _bridge;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);

        Services = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(Services);

        // 启动即建立桥接：构造时订阅协议事件，此后条码扫描/设备报警才能转成全局消息。
        _bridge = Services.GetRequiredService<ProtocolMessageBridge>();

        // 先显示窗口，再做数据加载与设备连接：
        // 替换旧的 sync-over-async（GetAwaiter().GetResult()）写法，避免阻塞 UI 线程导致启动白屏。
        var window = Services.GetRequiredService<MainWindow>();
        window.DataContext = Services.GetRequiredService<MainWindowViewModel>();
        window.Show();

        _ = InitializeAsync();
    }

    /// <summary>
    /// 后台初始化：加载 Markdown 数据源 → 广播 DataLoadedMessage → 连接全部模拟设备。
    /// 全程异步、不阻塞 UI；异常统一记录，避免 fire-and-forget 静默失败。
    /// </summary>
    private static async Task InitializeAsync()
    {
        try
        {
            var dataService = Services.GetRequiredService<IMarkdownDataService>();
            await dataService.LoadAsync();

            // 数据就绪广播：页面据此刷新（如收银台重建分类 / 商品视图）
            Services.GetRequiredService<IMessenger>().Send(new DataLoadedMessage(
                new DataLoaded(dataService.LoadErrors.Count == 0, dataService.LoadErrors.ToArray(), DateTime.Now)));

            // 数据就绪后再连接设备（驱动会按仓储中的设备配置建立会话）
            await Services.GetRequiredService<IProtocolManager>().ConnectAllAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[启动] 初始化失败：{ex}");

            // 启动阶段的失败如果只写 Debug，用户看到的是"界面空白且毫无提示"，无从排查；
            // 因此同时经全局提示条告知（主窗口此时已显示，提示条可直接呈现）。
            NotifyStatus($"应用初始化失败，部分数据可能不可用：{ex.Message}", StatusNotificationMessage.StatusLevel.Error);
        }
    }

    /// <summary>向全局提示条推送一条状态（自动切回 UI 线程）。</summary>
    private static void NotifyStatus(string text, StatusNotificationMessage.StatusLevel level)
    {
        if (Services.GetService<IMessenger>() is not { } messenger)
        {
            return;
        }

        UiDispatcher.Post(() => messenger.Send(new StatusNotificationMessage(text, level)));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 先退订协议事件（Dispose 幂等，容器随后还会再释放一次，重复调用安全）
        _bridge?.Dispose();
        _bridge = null;

        // 再断开全部协议驱动（优雅停止后台仿真循环），最后释放容器内所有单例
        if (Services.GetService<IProtocolManager>() is { } manager)
        {
            try
            {
                manager.DisconnectAllAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[退出] 断开协议失败：{ex.Message}");
            }
        }

        // 释放主窗口/页面 ViewModel：退订事件、停止计时器、注销消息订阅
        (Services.GetService<MainWindowViewModel>() as IDisposable)?.Dispose();

        // 释放实现了 IDisposable 的服务（协议管理器 / 各驱动等）
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    /// <summary>集中注册所有服务，注册顺序即依赖顺序。</summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ---------- 基础设施 ----------
        // 全局消息总线：整个应用共用同一个 WeakReferenceMessenger 实例
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);

        // ---------- 数据访问层 ----------
        services.AddSingleton<PosRepository>();

        // 仓储按"最小契约"对外暴露：调用方只依赖自己真正需要的能力（商品目录 / 设备配置），
        // 而不是拿到包含订单在内的整个仓储。两个接口都解析到同一个 PosRepository 单例，
        // 因此"全应用唯一数据落点"的既有语义完全不变。
        services.AddSingleton<IProductCatalog>(sp => sp.GetRequiredService<PosRepository>());
        services.AddSingleton<IDeviceCatalog>(sp => sp.GetRequiredService<PosRepository>());

        services.AddSingleton<IMarkdownDataService, MarkdownDataService>();

        // ---------- 通信层（模拟协议驱动） ----------
        // 每种协议注册为 IProtocolDriver：新增协议只需在此追加一行，ProtocolManager 无需改动。
        services.AddSingleton<IProtocolDriver, ModbusTcpSimDriver>();
        services.AddSingleton<IProtocolDriver, OpcUaSimDriver>();
        services.AddSingleton<IProtocolDriver, SerialPortSimDriver>();
        services.AddSingleton<IProtocolDriver, SocketSimDriver>();
        services.AddSingleton<IProtocolDriver, MqttSimDriver>();

        // 协议管理器：构造时注入 IEnumerable<IProtocolDriver> 聚合全部驱动
        services.AddSingleton<IProtocolManager, ProtocolManager>();

        // ---------- 业务服务层 ----------
        // 商品查询：表现层"看商品"的唯一入口（分类索引切片 + 关键字后台过滤 + 结果上限），
        // 使收银台不再直接持有全量商品列表做全量过滤。
        services.AddSingleton<IProductQuery, ProductQueryService>();

        // 均为单例：购物车、订单、门店设置需要在页面切换间保持状态
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICartService, CartService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IDialogService, DialogService>();

        // 结算外设编排（打印 / 开钱箱 / 云端上报）：与界面解耦，可单独验证
        services.AddSingleton<CheckoutCoordinator>();

        // 协议报文 → 全局消息的桥接器（透明翻译，页面无需感知协议细节）
        services.AddSingleton<ProtocolMessageBridge>();

        // ---------- 窗口 / 页面 ViewModel ----------
        services.AddSingleton<MainWindow>();

        // 页面解析器：把"按类型取页面 ViewModel"收窄为一个委托，
        // 使 MainWindowViewModel 不必依赖整个 IServiceProvider（服务定位器反模式）。
        // 仍由容器解析，因此页面单例语义与惰性创建行为保持不变。
        services.AddSingleton<Func<Type, ObservableObject?>>(
            sp => type => sp.GetService(type) as ObservableObject);

        // 页面 ViewModel 使用单例，切换页面时保留状态（购物车、日志等）
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<CashierViewModel>();
        services.AddSingleton<DeviceMonitorViewModel>();
        services.AddSingleton<SettingsViewModel>();
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
                "CommunityToolkitDemo");

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
