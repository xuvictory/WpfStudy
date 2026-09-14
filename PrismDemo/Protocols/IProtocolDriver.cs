using PrismDemo.Models;

namespace PrismDemo.Protocols;

/// <summary>
/// 统一协议驱动抽象。
///
/// 设计要点：把 Modbus / OPC UA / 串口 / Socket / MQTT 的差异全部收敛到实现内部，
/// 上层（ProtocolManager / ViewModel）只面对"状态 + 收发报文 + 事件"这三个概念，
/// 因此新增一种协议时上层代码完全不用改（开闭原则）。
/// </summary>
public interface IProtocolDriver : IDisposable
{
    /// <summary>设备配置（来自 Data/devices.md）</summary>
    DeviceInfo Device { get; }

    /// <summary>设备名称</summary>
    string Name { get; }

    /// <summary>协议类型</summary>
    ProtocolType Type { get; }

    /// <summary>当前连接状态</summary>
    ConnectionState State { get; }

    /// <summary>收到/发出报文时触发（可能在后台线程触发，订阅方需自行切回 UI 线程）</summary>
    event EventHandler<ProtocolFrame>? FrameReceived;

    /// <summary>连接状态变化时触发</summary>
    event EventHandler<ConnectionState>? StateChanged;

    /// <summary>设备报警（通信异常、环境超限等）</summary>
    event EventHandler<string>? AlarmRaised;

    /// <summary>建立连接：完成握手并启动仿真轮询循环。</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>断开连接：取消轮询循环并释放资源。</summary>
    Task DisconnectAsync();

    /// <summary>向上位机 → 设备方向发送一条报文。</summary>
    Task SendAsync(ProtocolFrame frame, CancellationToken cancellationToken = default);
}
