using Prism.Events;

namespace PrismDemo.Events;

/// <summary>
/// 扫码枪扫到条码事件（串口协议 → 收银台）。
/// 演示"设备数据反向驱动 UI"的跨模块通信。
/// </summary>
/// <remarks>
/// 知识点：<see cref="PubSubEvent{TPayload}"/> —— Prism 事件聚合器最常用的强类型事件基类，
/// 发布方 <c>Publish(payload)</c>，订阅方 <c>Subscribe(handler)</c> 即收到载荷本身。
/// </remarks>
public sealed class BarcodeScannedEvent : PubSubEvent<string>
{
}
