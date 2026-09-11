using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CommunityToolkitDemo.Messages;

/// <summary>
/// 扫码枪扫到条码消息（串口协议 → 收银台）。
/// 演示"设备数据反向驱动 UI"的跨模块通信。
/// </summary>
public sealed class BarcodeScannedMessage : ValueChangedMessage<string>
{
    public BarcodeScannedMessage(string barcode) : base(barcode) { }
}
