using System.IO;

namespace PrismDemo.Services;

/// <summary>
/// 通用 Markdown 表格解析器。
///
/// 解析规则：
/// 1. 跳过表格之前的所有正文（如标题、说明）；
/// 2. 第一个以 <c>|</c> 开头的行视为表头；
/// 3. 紧随其后由 <c>-</c> / <c>:</c> 组成的行视为分隔行，跳过；
/// 4. 之后每行作为一条记录，直到遇到不以 <c>|</c> 开头的行（含空行）或文件结束。
///
/// 返回值是"字段名 → 单元格文本"的字典列表，与具体业务类型解耦。
/// </summary>
/// <remarks>
/// 解析规则只由 <see cref="MarkdownRowReader"/> 一处实现，本类负责把它包装成两种使用方式：
/// <list type="bullet">
/// <item><b>流式</b>（推荐，生产加载路径）：<c>TextReader</c> + <see cref="MarkdownRow"/> 委托，
/// 读一行映射一行，不产生逐行字典，内存占用约为 1 倍结果集；</item>
/// <item><b>字典式</b>（兼容）：接收字符串、返回字典列表，供既有调用方与单测使用。</item>
/// </list>
/// 之所以统一到一处实现，是因为此前「解析」在数据源与性能基准中各存在一份拷贝，
/// 任何规则调整都必须同步两处，极易漂移。
/// </remarks>
public static class MarkdownTableParser
{
    /// <summary>解析文本中的第一张 Markdown 表格，返回"字段名 → 单元格文本"列表。</summary>
    /// <remarks>该重载会为每行分配一个字典；大批量场景请改用流式重载。</remarks>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseFirstTable(string markdown)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return rows;
        }

        using var reader = new StringReader(markdown);
        foreach (var row in new MarkdownRowReader(reader).ReadRows())
        {
            rows.Add(row.ToDictionary());
        }

        return rows;
    }

    /// <summary>
    /// 流式解析并映射为强类型实体，自动跳过映射返回 null 的行。
    /// </summary>
    /// <param name="reader">表格文本来源。</param>
    /// <param name="map">行 → 实体映射；返回 null 表示该行应被丢弃。</param>
    /// <param name="maxRows">数据行数上限，超出后停止读取并视为截断。</param>
    public static IReadOnlyList<T> ParseFirstTable<T>(
        TextReader reader,
        Func<MarkdownRow, T?> map,
        int maxRows = int.MaxValue)
        where T : class
        => ParseFirstTable(reader, map, maxRows, out _);

    /// <summary>
    /// 流式解析并映射为强类型实体，同时报告是否因超过 <paramref name="maxRows"/> 被截断。
    /// </summary>
    public static IReadOnlyList<T> ParseFirstTable<T>(
        TextReader reader,
        Func<MarkdownRow, T?> map,
        int maxRows,
        out bool truncated)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(map);

        var rowReader = new MarkdownRowReader(reader, maxRows);
        var result = new List<T>();

        foreach (var row in rowReader.ReadRows())
        {
            var entity = map(row);
            if (entity is not null)
            {
                result.Add(entity);
            }
        }

        truncated = rowReader.Truncated;
        return result;
    }

    /// <summary>解析并映射为强类型实体，自动跳过映射返回 null 的行（字典式兼容重载）。</summary>
    public static IReadOnlyList<T> ParseFirstTable<T>(
        string markdown,
        Func<IReadOnlyDictionary<string, string>, T?> map)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(map);

        var result = new List<T>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return result;
        }

        using var reader = new StringReader(markdown);
        foreach (var row in new MarkdownRowReader(reader).ReadRows())
        {
            var entity = map(row.ToDictionary());
            if (entity is not null)
            {
                result.Add(entity);
            }
        }

        return result;
    }
}
