using CommunityToolkitDemo.Models;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="ProtocolDisplay"/> 是协议 / 状态 / 支付方式显示文案的单一事实来源，
/// ViewModel 与 XAML 转换器共用同一份映射，这里锁定其输出。
/// </summary>
public sealed class ProtocolDisplayTests
{
    [Theory]
    [InlineData(ProtocolType.ModbusTcp, "Modbus TCP")]
    [InlineData(ProtocolType.OpcUa, "OPC UA")]
    [InlineData(ProtocolType.SerialPort, "串口")]
    [InlineData(ProtocolType.Socket, "Socket")]
    [InlineData(ProtocolType.Mqtt, "MQTT")]
    [InlineData((ProtocolType)999, "未知")]
    public void ProtocolName_MapsAllValues(ProtocolType type, string expected)
        => Assert.Equal(expected, ProtocolDisplay.ProtocolName(type));

    [Theory]
    [InlineData(ProtocolType.ModbusTcp, "MB")]
    [InlineData(ProtocolType.OpcUa, "OPC")]
    [InlineData(ProtocolType.SerialPort, "COM")]
    [InlineData(ProtocolType.Socket, "TCP")]
    [InlineData(ProtocolType.Mqtt, "MQTT")]
    [InlineData((ProtocolType)999, "?")]
    public void ProtocolShortName_MapsAllValues(ProtocolType type, string expected)
        => Assert.Equal(expected, ProtocolDisplay.ProtocolShortName(type));

    [Theory]
    [InlineData(ConnectionState.Connected, "已连接")]
    [InlineData(ConnectionState.Connecting, "连接中")]
    [InlineData(ConnectionState.Faulted, "故障")]
    [InlineData(ConnectionState.Disconnected, "未连接")]
    public void ConnectionStateText_MapsAllValues(ConnectionState state, string expected)
        => Assert.Equal(expected, ProtocolDisplay.ConnectionStateText(state));

    [Theory]
    [InlineData(PaymentMethod.Cash, "现金")]
    [InlineData(PaymentMethod.QrCode, "扫码支付")]
    [InlineData(PaymentMethod.BankCard, "银行卡")]
    [InlineData((PaymentMethod)999, "-")]
    public void PaymentMethodText_MapsAllValues(PaymentMethod method, string expected)
        => Assert.Equal(expected, ProtocolDisplay.PaymentMethodText(method));
}
