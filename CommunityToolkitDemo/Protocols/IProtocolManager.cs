using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Protocols;

/// <summary>
/// 协议管理器：统一持有并调度所有协议驱动，向 UI 暴露聚合后的事件与指令入口。
/// </summary>
public interface IProtocolManager : IDisposable
{
    /// <summary>全部已注册的驱动（顺序即 Data/devices.md 中的注册顺序）</summary>
    IReadOnlyList<IProtocolDriver> Drivers { get; }

    /// <summary>任一驱动产生报文时触发（可能来自后台线程）</summary>
    event EventHandler<ProtocolFrame>? FrameReceived;

    /// <summary>任一驱动状态变化时触发</summary>
    event EventHandler<IProtocolDriver>? DriverStateChanged;

    /// <summary>任一驱动报警时触发</summary>
    event EventHandler<DeviceAlarm>? AlarmRaised;

    /// <summary>连接全部驱动（应用启动时调用）</summary>
    Task ConnectAllAsync();

    /// <summary>断开全部驱动（应用退出时调用）</summary>
    Task DisconnectAllAsync();

    /// <summary>切换单个驱动的连接状态</summary>
    Task ToggleAsync(IProtocolDriver driver);

    /// <summary>请求串口扫码枪扫一次条码；返回是否成功下发指令。</summary>
    Task<bool> RequestBarcodeScanAsync(CancellationToken cancellationToken = default);

    /// <summary>把应收金额推送到客显屏（Socket）；返回是否成功下发。</summary>
    Task<bool> PushDisplayAmountAsync(decimal amount, CancellationToken cancellationToken = default);

    /// <summary>打开钱箱（Socket）；返回是否成功下发。</summary>
    Task<bool> OpenCashBoxAsync(CancellationToken cancellationToken = default);

    /// <summary>向小票打印机（串口）下发打印指令；返回是否成功下发。</summary>
    Task<bool> PrintReceiptAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>向云端（MQTT）上报订单；返回是否成功下发。</summary>
    Task<bool> PublishOrderAsync(Order order, CancellationToken cancellationToken = default);
}
