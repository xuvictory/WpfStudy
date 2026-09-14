using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PrismDemo.Events;
using PrismDemo.Models;

namespace PrismDemo.Common.Converters;

/// <summary>
/// 画刷缓存：转换器里千万不要每次 Convert 都 new 一个画刷。
/// 未冻结的 Freezable 会被 WPF 属性系统登记变更通知，既增加 GC 压力又带来额外订阅开销；
/// 报文日志这类"每来一帧就新建一行"的场景会持续放大这个问题。
/// 统一使用静态只读 + Freeze 的画刷。
/// </summary>
internal static class BrushCache
{
    public static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// 值为 null / 空字符串时可见（用于 TextBox 占位提示）。
/// ConverterParameter = "Inverse" 时逻辑反转。
/// </summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value is null || (value is string s && s.Length == 0);
        var inverse = string.Equals(parameter as string, "Inverse", StringComparison.OrdinalIgnoreCase);
        return isEmpty ^ inverse ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool → Visibility</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>bool → 反转的 Visibility</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

/// <summary>bool 取反，常用于 IsEnabled</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>金额格式化：12.5 → ¥12.50；ConverterParameter = "Plain" 时不带货币符号</summary>
public sealed class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double db => (decimal)db,
            int i => i,
            float f => (decimal)f,
            _ => 0m
        };

        var plain = string.Equals(parameter as string, "Plain", StringComparison.OrdinalIgnoreCase);
        return plain ? amount.ToString("N2", culture) : "¥" + amount.ToString("N2", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>连接状态 → 状态色画刷</summary>
/// <remarks>
/// 转换器无法使用 <c>StaticResource</c>，因此这里只能写 RGB 字面量。
/// 取值与 <c>Common/Styles/Colors.xaml</c> 的设计令牌一一对应，便于统一改色时比对：
/// Disconnected = TextMutedBrush、Connecting = AmberBrush、Connected = SuccessBrush、Faulted = DangerBrush。
/// </remarks>
public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Disconnected = BrushCache.Frozen(0x94, 0xA3, 0xB8);
    private static readonly SolidColorBrush Connecting = BrushCache.Frozen(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush Connected = BrushCache.Frozen(0x16, 0xA3, 0x4A);
    private static readonly SolidColorBrush Faulted = BrushCache.Frozen(0xDC, 0x26, 0x26);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ConnectionState.Connected => Connected,
            ConnectionState.Connecting => Connecting,
            ConnectionState.Faulted => Faulted,
            _ => Disconnected
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>连接状态 → 中文描述（映射与 <c>DeviceCardViewModel.StateText</c> 同源）</summary>
public sealed class ConnectionStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ProtocolDisplay.ConnectionStateText(
            value is ConnectionState state ? state : ConnectionState.Disconnected);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>协议类型 → 中文名称</summary>
public sealed class ProtocolTypeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProtocolType type ? ProtocolDisplay.ProtocolName(type) : "未知";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>协议类型 → 短标签，用于卡片角标（无字体图标依赖）</summary>
public sealed class ProtocolTypeToShortTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProtocolType type ? ProtocolDisplay.ProtocolShortName(type) : "?";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>枚举 ↔ 分段选择：值等于 ConverterParameter 时返回 true</summary>
/// <remarks>
/// 当前未被任何 XAML 引用（分段选择统一使用 <see cref="PaymentMethodToBooleanConverter"/>）。
/// 它是通用实现，保留以备其他枚举的分段控件直接复用；删除它属于移除公开类型，
/// 因此按"保留 + 说明"处理。
/// </remarks>
public sealed class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null &&
           string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
        {
            var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (enumType.IsEnum)
            {
                return Enum.Parse(enumType, parameter.ToString()!);
            }
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}

/// <summary>
/// 支付方式 ↔ 分段选择（RadioButton）。
/// 正向：当前支付方式等于参数时返回 true（选中态）；
/// 反向：仅当被选中时写回枚举值，未选中的按钮返回 DoNothing 避免互相覆盖。
/// </summary>
public sealed class PaymentMethodToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PaymentMethod method && parameter is not null &&
           string.Equals(method.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null &&
           Enum.TryParse<PaymentMethod>(parameter.ToString(), ignoreCase: true, out var method)
            ? method
            : Binding.DoNothing;
}

/// <summary>集合数量 → Visibility（0 时折叠；ConverterParameter = "Inverse" 反转）</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var inverse = string.Equals(parameter as string, "Inverse", StringComparison.OrdinalIgnoreCase);
        return (count > 0) ^ inverse ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>时间 → HH:mm:ss</summary>
/// <remarks>
/// 当前未被任何 XAML 引用：报文日志改为在 <c>ProtocolFrame</c> 上直接暴露格式化后的文本，
/// 避免每条日志都经过一次转换器。保留该公开类型以备复用。
/// </remarks>
public sealed class TimeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? dt.ToString("HH:mm:ss") : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>支付方式 → 中文（映射与 <c>Order.PaymentText</c> 同源）</summary>
/// <remarks>
/// 当前未被 XAML 引用：订单列表直接绑定 <c>Order.PaymentText</c>。
/// 保留以兼容既有引用方。
/// </remarks>
public sealed class PaymentMethodToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PaymentMethod method ? ProtocolDisplay.PaymentMethodText(method) : "-";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>协议方向 → 箭头</summary>
public sealed class DirectionToArrowConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ProtocolDirection.Out ? "↑ 发送" : "↓ 接收";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>提示级别 → 状态色画刷</summary>
/// <remarks>
/// 这些颜色是"深色提示条上的前景色"，比 <c>Colors.xaml</c> 的功能色更亮，
/// 因此不能直接复用令牌色值。对应关系：
/// Warning = AmberBrush、Info = PrimaryLightBrush；
/// Success / Error 为深色背景专用亮色，暂无令牌（若将来提示条改版需同步调整）。
/// </remarks>
public sealed class StatusLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Success = BrushCache.Frozen(0x4A, 0xDE, 0x80);
    private static readonly SolidColorBrush Warning = BrushCache.Frozen(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush Error = BrushCache.Frozen(0xF8, 0x71, 0x71);
    private static readonly SolidColorBrush Info = BrushCache.Frozen(0x93, 0xC5, 0xFD);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            StatusLevel.Success => Success,
            StatusLevel.Warning => Warning,
            StatusLevel.Error => Error,
            _ => Info
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>协议类型 → 用于设备卡片的强调色画刷</summary>
/// <remarks>
/// 转换器无法使用 <c>StaticResource</c>，故写 RGB 字面量。与 <c>Colors.xaml</c> 令牌的对应关系：
/// Modbus = PrimaryBrush、SerialPort = InfoBrush、Socket = SuccessBrush、Mqtt = WarningBrush、
/// Unknown = TextMutedBrush；OpcUa 取品牌渐变终点色（无独立令牌）。
/// </remarks>
public sealed class ProtocolTypeToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Modbus = BrushCache.Frozen(0x25, 0x63, 0xEB);
    private static readonly SolidColorBrush OpcUa = BrushCache.Frozen(0x7C, 0x3A, 0xED);
    private static readonly SolidColorBrush SerialPort = BrushCache.Frozen(0x0E, 0xA5, 0xE9);
    private static readonly SolidColorBrush Socket = BrushCache.Frozen(0x16, 0xA3, 0x4A);
    private static readonly SolidColorBrush Mqtt = BrushCache.Frozen(0xF9, 0x73, 0x16);
    private static readonly SolidColorBrush Unknown = BrushCache.Frozen(0x94, 0xA3, 0xB8);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ProtocolType.ModbusTcp => Modbus,
            ProtocolType.OpcUa => OpcUa,
            ProtocolType.SerialPort => SerialPort,
            ProtocolType.Socket => Socket,
            ProtocolType.Mqtt => Mqtt,
            _ => Unknown
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
