using System.Globalization;

namespace PrismDemo.Services;

/// <summary>
/// Markdown 单元格文本 → 业务类型的容错转换。
/// </summary>
/// <remarks>
/// 抽取出来的目的：让「行视图取值」（<see cref="MarkdownRow"/>）与「字典视图取值」
/// 两条路径共用同一套解析规则。若各写一份，后续改动很容易只改一处，导致两者语义漂移。
///
/// 所有方法都不抛异常：格式非法时回退到调用方给定的默认值，
/// 这样人手维护的模拟数据里出现空值/错字不会让整个文件加载失败。
/// </remarks>
internal static class MarkdownValue
{
    public static decimal ToDecimal(string text, decimal fallback = 0m)
        => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    public static int ToInt32(string text, int fallback = 0)
        => int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    public static double ToDouble(string text, double fallback = 0d)
        => double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    public static DateTime ToDateTime(string text, DateTime fallback)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : fallback;

    public static TEnum? ToEnum<TEnum>(string text)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) ? value : null;

    public static bool ToBoolean(string text, bool fallback = false)
        => text switch
        {
            "是" or "1" or "true" or "True" or "TRUE" or "Y" or "y" => true,
            "否" or "0" or "false" or "False" or "FALSE" or "N" or "n" => false,
            _ => fallback
        };
}
