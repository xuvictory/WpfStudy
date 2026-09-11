using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Protocols;

/// <summary>
/// 协议管理器实现。
///
/// 设计说明：构造函数注入 <c>IEnumerable&lt;IProtocolDriver&gt;</c>，
/// 由依赖注入容器把注册过的所有驱动一次性交给管理器，
/// 之后新增协议只需要在 App.xaml.cs 里多注册一行，本类无需改动。
/// </summary>
public sealed class ProtocolManager : IProtocolManager
{
    private readonly List<IProtocolDriver> _drivers;

    /// <summary>对外暴露的只读视图：构造时创建一次，避免每次访问都分配包装对象。</summary>
    private readonly ReadOnlyCollection<IProtocolDriver> _driversView;

    private bool _disposed;

    public ProtocolManager(IEnumerable<IProtocolDriver> drivers)
    {
        _drivers = drivers.ToList();
        _driversView = _drivers.AsReadOnly();

        foreach (var driver in _drivers)
        {
            driver.FrameReceived += OnDriverFrameReceived;
            driver.StateChanged += OnDriverStateChanged;
            driver.AlarmRaised += OnDriverAlarmRaised;
        }
    }

    /// <summary>
    /// 已注册的驱动列表。
    /// </summary>
    /// <remarks>
    /// 返回只读包装而非内部 List：若直接暴露 List，调用方可以向下转型后增删元素，
    /// 从而绕过构造函数的"事件订阅"步骤 —— 新加的驱动不会参与事件转发，
    /// 表现为该设备的状态与报文静默丢失，非常难排查。
    /// </remarks>
    public IReadOnlyList<IProtocolDriver> Drivers => _driversView;

    public event EventHandler<ProtocolFrame>? FrameReceived;

    public event EventHandler<IProtocolDriver>? DriverStateChanged;

    public event EventHandler<DeviceAlarm>? AlarmRaised;

    public async Task ConnectAllAsync()
    {
        // 各驱动相互独立，可并行连接；单个失败不影响其它驱动（逐个隔离异常）。
        await Task.WhenAll(_drivers.Select(ConnectSafelyAsync)).ConfigureAwait(false);
    }

    public async Task DisconnectAllAsync()
    {
        // 并行断开，缩短应用退出时的等待时间。
        await Task.WhenAll(_drivers.Select(DisconnectSafelyAsync)).ConfigureAwait(false);
    }

    /// <summary>连接单个驱动并隔离异常，避免一个设备失败拖垮整体连接。</summary>
    private static async Task ConnectSafelyAsync(IProtocolDriver driver)
    {
        try
        {
            await driver.ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[协议] {driver.Name} 连接失败：{ex.Message}");
        }
    }

    /// <summary>断开单个驱动并隔离异常。</summary>
    private static async Task DisconnectSafelyAsync(IProtocolDriver driver)
    {
        try
        {
            await driver.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[协议] {driver.Name} 断开失败：{ex.Message}");
        }
    }

    public async Task ToggleAsync(IProtocolDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        if (driver.State is ConnectionState.Connected or ConnectionState.Connecting)
        {
            await driver.DisconnectAsync().ConfigureAwait(false);
        }
        else
        {
            await driver.ConnectAsync().ConfigureAwait(false);
        }
    }

    public Task<bool> RequestBarcodeScanAsync(CancellationToken cancellationToken = default)
        => SendToAsync(ProtocolType.SerialPort, ProtocolKeys.ScanCommand, "上位机触发扫码", cancellationToken);

    public Task<bool> PushDisplayAmountAsync(decimal amount, CancellationToken cancellationToken = default)
        => SendToAsync(
            ProtocolType.Socket,
            $"{ProtocolKeys.DisplayAmountPrefix}{amount.ToString("F2", CultureInfo.InvariantCulture)}",
            "推送客显屏应收金额",
            cancellationToken);

    public Task<bool> OpenCashBoxAsync(CancellationToken cancellationToken = default)
        => SendToAsync(ProtocolType.Socket, ProtocolKeys.OpenCashBox, "打开钱箱", cancellationToken);

    public Task<bool> PrintReceiptAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // 用 ESC/POS 风格的文本模拟小票内容，便于在报文日志中阅读
        var lines = new List<string>
        {
            "1B 40 1B 61 01",                       // 初始化 + 居中
            $"RECEIPT {order.OrderNo}",
            $"TOTAL {order.Total:F2}",
            $"PAY {order.PaymentText} PAID {order.Paid:F2}",
            $"ITEMS {order.ItemCount}",
            "1D 56 42 00"                            // 切纸
        };

        return SendToAsync(ProtocolType.SerialPort, string.Join(" | ", lines), "打印小票", cancellationToken);
    }

    public Task<bool> PublishOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var payload = $"order={order.OrderNo} total={order.Total:F2} pay={order.PaymentText} items={order.ItemCount}";
        return SendToAsync(ProtocolType.Mqtt, payload, "上报订单到云端", cancellationToken);
    }

    /// <summary>
    /// 向指定协议的驱动下发一条报文。
    /// 未连接时安全返回 false（收银台允许"离线收银"，不因设备未接而报错）。
    /// </summary>
    private async Task<bool> SendToAsync(
        ProtocolType type,
        string payload,
        string description,
        CancellationToken cancellationToken)
    {
        var driver = Find(type);
        if (driver is null || driver.State is not ConnectionState.Connected)
        {
            return false;
        }

        await driver.SendAsync(
            new ProtocolFrame(type, ProtocolDirection.Out, driver.Device.Address, payload, description),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var driver in _drivers)
        {
            driver.FrameReceived -= OnDriverFrameReceived;
            driver.StateChanged -= OnDriverStateChanged;
            driver.AlarmRaised -= OnDriverAlarmRaised;
            driver.Dispose();
        }

        _drivers.Clear();
    }

    /// <summary>按协议类型定位驱动。</summary>
    /// <remarks>
    /// 已知限制：当同一协议注册了多个设备时，这里只会返回<b>最先注册</b>的那一个，
    /// 因此"同类型第二台设备"目前无法被寻址（例如第二块客显屏、第二台小票机）。
    /// 若要支持多设备，需要把 <see cref="IProtocolManager"/> 的语义方法扩展为
    /// "按设备 Id + 指令"寻址（属破坏性接口变更，未在本次向后兼容优化范围内实施），
    /// 或改为按设备 Id 注册独立的管理器实例。
    /// </remarks>
    private IProtocolDriver? Find(ProtocolType type)
        => _drivers.FirstOrDefault(d => d.Type == type);

    private void OnDriverFrameReceived(object? sender, ProtocolFrame frame)
        => FrameReceived?.Invoke(sender, frame);

    private void OnDriverStateChanged(object? sender, ConnectionState state)
    {
        if (sender is IProtocolDriver driver)
        {
            DriverStateChanged?.Invoke(this, driver);
        }
    }

    private void OnDriverAlarmRaised(object? sender, string message)
    {
        if (sender is IProtocolDriver driver)
        {
            AlarmRaised?.Invoke(this, new DeviceAlarm(driver.Type, driver.Name, message, DateTime.Now));
        }
    }
}
