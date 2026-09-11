using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Common;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Protocols;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// 协议层 → 消息总线的桥接器。
///
/// 为什么需要它？
/// 协议驱动只认识"报文"，业务页面只认识"消息"，两者不该互相依赖；
/// 由这个单例在中间做一次翻译，就把通信层与表现层彻底解耦：
/// - 串口扫到条码  → <see cref="BarcodeScannedMessage"/>（收银台自动加购）
/// - 驱动上报报警  → <see cref="DeviceAlarmMessage"/>（提示条 + 监控页）
///
/// 线程说明：驱动事件来自后台线程，这里统一经 <see cref="UiDispatcher"/> 切回 UI 线程再发送，
/// 保证接收方可以安全地更新 WPF 可观察集合。
/// </summary>
public sealed class ProtocolMessageBridge : IDisposable
{
    private readonly IProtocolManager _manager;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public ProtocolMessageBridge(IProtocolManager manager, IMessenger messenger)
    {
        _manager = manager;
        _messenger = messenger;

        _manager.FrameReceived += OnFrameReceived;
        _manager.AlarmRaised += OnAlarmRaised;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _manager.FrameReceived -= OnFrameReceived;
        _manager.AlarmRaised -= OnAlarmRaised;
    }

    /// <summary>识别串口扫码枪回传的条码报文并广播。</summary>
    private void OnFrameReceived(object? sender, ProtocolFrame frame)
    {
        if (frame.Type is not ProtocolType.SerialPort ||
            frame.Direction is not ProtocolDirection.In ||
            frame.Values is null ||
            !frame.Values.TryGetValue(ProtocolKeys.Barcode, out var barcode) ||
            string.IsNullOrWhiteSpace(barcode))
        {
            return;
        }

        UiDispatcher.Post(() => _messenger.Send(new BarcodeScannedMessage(barcode)));
    }

    private void OnAlarmRaised(object? sender, DeviceAlarm alarm)
        => UiDispatcher.Post(() => _messenger.Send(new DeviceAlarmMessage(alarm)));
}
