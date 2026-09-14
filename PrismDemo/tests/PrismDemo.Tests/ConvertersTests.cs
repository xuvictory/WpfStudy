using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PrismDemo.Common.Converters;
using PrismDemo.Events;
using PrismDemo.Models;
using Xunit;

namespace PrismDemo.Tests;

/// <summary>
/// XAML 转换器的纯逻辑回归。
/// 转换器出错在界面上往往只表现为"某处空白/颜色不对"，不易被构建发现，
/// 因此对取值与回写语义单独固化用例。
/// </summary>
public sealed class ConvertersTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void CurrencyConverter_FormatsWithSymbol()
    {
        var converter = new CurrencyConverter();

        Assert.Equal("¥12.50", converter.Convert(12.5m, typeof(string), null, Invariant));
    }

    [Fact]
    public void CurrencyConverter_PlainParameter_OmitsSymbol()
    {
        var converter = new CurrencyConverter();

        Assert.Equal("12.50", converter.Convert(12.5m, typeof(string), "Plain", Invariant));
    }

    [Fact]
    public void CurrencyConverter_NonNumericValue_BecomesZero()
    {
        var converter = new CurrencyConverter();

        Assert.Equal("¥0.00", converter.Convert("abc", typeof(string), null, Invariant));
    }

    [Fact]
    public void CountToVisibility_ZeroIsCollapsed_PositiveIsVisible()
    {
        var converter = new CountToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, converter.Convert(0, typeof(Visibility), null, Invariant));
        Assert.Equal(Visibility.Visible, converter.Convert(3, typeof(Visibility), null, Invariant));
    }

    [Fact]
    public void CountToVisibility_InverseParameter_FlipsResult()
    {
        var converter = new CountToVisibilityConverter();

        Assert.Equal(Visibility.Visible, converter.Convert(0, typeof(Visibility), "Inverse", Invariant));
        Assert.Equal(Visibility.Collapsed, converter.Convert(3, typeof(Visibility), "Inverse", Invariant));
    }

    [Fact]
    public void NullOrEmptyToVisibility_NullOrEmptyIsVisible()
    {
        var converter = new NullOrEmptyToVisibilityConverter();

        Assert.Equal(Visibility.Visible, converter.Convert(null, typeof(Visibility), null, Invariant));
        Assert.Equal(Visibility.Visible, converter.Convert(string.Empty, typeof(Visibility), null, Invariant));
        Assert.Equal(Visibility.Collapsed, converter.Convert("abc", typeof(Visibility), null, Invariant));
    }

    [Fact]
    public void InverseBooleanToVisibility_FlipsBool()
    {
        var converter = new InverseBooleanToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, converter.Convert(true, typeof(Visibility), null, Invariant));
        Assert.Equal(Visibility.Visible, converter.Convert(false, typeof(Visibility), null, Invariant));
    }

    [Fact]
    public void PaymentMethodToBoolean_RoundTripsSelectionState()
    {
        var converter = new PaymentMethodToBooleanConverter();

        Assert.Equal(true, converter.Convert(PaymentMethod.Cash, typeof(bool), "Cash", Invariant));
        Assert.Equal(false, converter.Convert(PaymentMethod.Cash, typeof(bool), "QrCode", Invariant));

        // 仅被选中的按钮写回枚举值，未选中的返回 DoNothing 以免互相覆盖
        Assert.Equal(PaymentMethod.QrCode, converter.ConvertBack(true, typeof(PaymentMethod), "QrCode", Invariant));
        Assert.Same(System.Windows.Data.Binding.DoNothing,
            converter.ConvertBack(false, typeof(PaymentMethod), "QrCode", Invariant));
    }

    [Fact]
    public void EqualityToBoolean_ComparesStringRepresentation()
    {
        var converter = new EqualityToBooleanConverter();

        Assert.Equal(true, converter.Convert(PaymentMethod.BankCard, typeof(bool), "bankcard", Invariant));
        Assert.Equal(false, converter.Convert(PaymentMethod.BankCard, typeof(bool), "Cash", Invariant));
        Assert.Equal(PaymentMethod.BankCard,
            converter.ConvertBack(true, typeof(PaymentMethod), nameof(PaymentMethod.BankCard), Invariant));
    }

    [Fact]
    public void DirectionToArrow_LabelsBothDirections()
    {
        var converter = new DirectionToArrowConverter();

        Assert.Equal("↑ 发送", converter.Convert(ProtocolDirection.Out, typeof(string), null, Invariant));
        Assert.Equal("↓ 接收", converter.Convert(ProtocolDirection.In, typeof(string), null, Invariant));
    }

    [Fact]
    public void TextConverters_DelegateToSharedMapping()
    {
        Assert.Equal("已连接",
            new ConnectionStateToTextConverter().Convert(ConnectionState.Connected, typeof(string), null, Invariant));
        Assert.Equal("MQTT",
            new ProtocolTypeToTextConverter().Convert(ProtocolType.Mqtt, typeof(string), null, Invariant));
        Assert.Equal("COM",
            new ProtocolTypeToShortTextConverter().Convert(ProtocolType.SerialPort, typeof(string), null, Invariant));
        Assert.Equal("扫码支付",
            new PaymentMethodToTextConverter().Convert(PaymentMethod.QrCode, typeof(string), null, Invariant));
    }

    [Theory]
    [InlineData(ConnectionState.Connected, 0x16, 0xA3, 0x4A)]
    [InlineData(ConnectionState.Faulted, 0xDC, 0x26, 0x26)]
    [InlineData(ConnectionState.Disconnected, 0x94, 0xA3, 0xB8)]
    public void ConnectionStateToBrush_ReturnsExpectedColor(ConnectionState state, byte r, byte g, byte b)
    {
        var brush = Assert.IsType<SolidColorBrush>(
            new ConnectionStateToBrushConverter().Convert(state, typeof(Brush), null, Invariant));

        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void StatusLevelToBrush_MapsAllLevels()
    {
        var converter = new StatusLevelToBrushConverter();

        Assert.IsType<SolidColorBrush>(converter.Convert(
            StatusLevel.Success, typeof(Brush), null, Invariant));
        Assert.IsType<SolidColorBrush>(converter.Convert(
            StatusLevel.Error, typeof(Brush), null, Invariant));
    }
}
