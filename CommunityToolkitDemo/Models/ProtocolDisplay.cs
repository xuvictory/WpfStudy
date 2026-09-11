namespace CommunityToolkitDemo.Models;

/// <summary>
/// 枚举 → 显示文本的统一映射（单一事实来源）。
///
/// 为什么需要它？
/// 同一份文案此前在 ViewModel 与 XAML 转换器里各写了一遍：协议短名同时存在于
/// <c>MainWindowViewModel</c> 与 <c>ProtocolTypeToShortTextConverter</c>；
/// 连接状态文案同时存在于 <c>DeviceCardViewModel</c> 与 <c>ConnectionStateToTextConverter</c>。
/// 两处并存时，任何一处漏改都会造成"同一个枚举在不同界面显示不同文字"的低级不一致，
/// 因此收敛到这里，ViewModel 与 Converter 都调用同一方法。
/// </summary>
public static class ProtocolDisplay
{
    /// <summary>协议类型 → 全称（用于设备列表、详情等需要完整名称的位置）。</summary>
    public static string ProtocolName(ProtocolType type) => type switch
    {
        ProtocolType.ModbusTcp => "Modbus TCP",
        ProtocolType.OpcUa => "OPC UA",
        ProtocolType.SerialPort => "串口",
        ProtocolType.Socket => "Socket",
        ProtocolType.Mqtt => "MQTT",
        _ => "未知"
    };

    /// <summary>协议类型 → 短标签（用于卡片角标，不依赖字体图标）。</summary>
    public static string ProtocolShortName(ProtocolType type) => type switch
    {
        ProtocolType.ModbusTcp => "MB",
        ProtocolType.OpcUa => "OPC",
        ProtocolType.SerialPort => "COM",
        ProtocolType.Socket => "TCP",
        ProtocolType.Mqtt => "MQTT",
        _ => "?"
    };

    /// <summary>连接状态 → 中文描述。</summary>
    public static string ConnectionStateText(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "已连接",
        ConnectionState.Connecting => "连接中",
        ConnectionState.Faulted => "故障",
        _ => "未连接"
    };

    /// <summary>支付方式 → 中文描述（未知值显示 <c>-</c> 占位）。</summary>
    public static string PaymentMethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "现金",
        PaymentMethod.QrCode => "扫码支付",
        PaymentMethod.BankCard => "银行卡",
        _ => "-"
    };
}
