using Prism.Events;
using PrismDemo.Common;
using PrismDemo.Events;
using PrismDemo.Models;
using PrismDemo.Protocols;

namespace PrismDemo.Services;

/// <summary>
/// 协议层 → 事件总线的桥接器。
///
/// 为什么需要它？
/// 协议驱动只认识"报文"，业务页面只认识"事件"，两者不该互相依赖；
/// 由这个单例在中间做一次翻译，就把通信层与表现层彻底解耦：
/// - 串口扫到条码  → <see cref="BarcodeScannedEvent"/>（收银台自动加购）
/// - 驱动上报报警  → <see cref="DeviceAlarmEvent"/>（提示条 + 监控页）
///
/// 线程说明：驱动事件来自后台线程，这里统一经 <see cref="UiDispatcher"/> 切回 UI 线程再发布，
/// 保证订阅方可以安全地更新 WPF 可观察集合。
/// </summary>
public sealed class ProtocolMessageBridge : IDisposable
{
    private readonly IProtocolManager _manager;
    private readonly IEventAggregator _eventAggregator;
    private bool _disposed;

    public ProtocolMessageBridge(IProtocolManager manager, IEventAggregator eventAggregator)
    {
        _manager = manager;
        _eventAggregator = eventAggregator;

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

        UiDispatcher.Post(() => _eventAggregator.GetEvent<BarcodeScannedEvent>().Publish(barcode));
    }

    private void OnAlarmRaised(object? sender, DeviceAlarm alarm)
        => UiDispatcher.Post(() => _eventAggregator.GetEvent<DeviceAlarmEvent>().Publish(alarm));
}
