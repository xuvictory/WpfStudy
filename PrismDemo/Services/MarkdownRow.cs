namespace PrismDemo.Services;

/// <summary>
/// Markdown 表格的一行数据，按列名取值。
/// </summary>
/// <remarks>
/// 相比「每行一个 <c>Dictionary&lt;string,string&gt;</c>」的旧表示，这里只持有一份
/// 共享的表头与当前行的单元格数组：
/// <list type="bullet">
/// <item>不再为每一行分配字典对象（10 万行即 10 万个字典，实测保留内存约 110 MB）；</item>
/// <item>取值时只做一次「列名下标的字典查找 + 数组下标访问」，无需遍历。</item>
/// </list>
/// 由于单元格数组是逐行新建的，该结构只在「当前行」有效，不能跨行缓存 ——
/// 这也是它适合「读一行、映射一行、立刻丢弃」的流式场景的原因。
/// </remarks>
public readonly struct MarkdownRow
{
    private readonly string[] _cells;
    private readonly MarkdownHeader? _header;

    internal MarkdownRow(string[] cells, MarkdownHeader header)
    {
        _cells = cells;
        _header = header;
    }

    /// <summary>按列名取单元格文本；列不存在或该行缺少该列时返回 <paramref name="fallback"/>。</summary>
    public string GetString(string column, string fallback = "")
    {
        if (_header is not null && _header.TryGetIndex(column, out var columnIndex))
        {
            var cellIndex = columnIndex + Offset;
            if ((uint)cellIndex < (uint)_cells.Length)
            {
                return _cells[cellIndex];
            }
        }

        return fallback;
    }

    public decimal GetDecimal(string column, decimal fallback = 0m)
        => MarkdownValue.ToDecimal(GetString(column), fallback);

    public int GetInt(string column, int fallback = 0)
        => MarkdownValue.ToInt32(GetString(column), fallback);

    public double GetDouble(string column, double fallback = 0d)
        => MarkdownValue.ToDouble(GetString(column), fallback);

    public bool GetBool(string column, bool fallback = false)
        => MarkdownValue.ToBoolean(GetString(column), fallback);

    public DateTime GetDateTime(string column, DateTime fallback)
        => MarkdownValue.ToDateTime(GetString(column), fallback);

    public TEnum? GetEnum<TEnum>(string column)
        where TEnum : struct, Enum
        => MarkdownValue.ToEnum<TEnum>(GetString(column));

    /// <summary>
    /// 转换为「字段名 → 单元格文本」的字典，供依赖旧表示的调用方（兼容路径、单测）使用。
    /// </summary>
    /// <remarks>此方法会重新引入每行一个字典的分配，生产加载路径不应调用它。</remarks>
    internal IReadOnlyDictionary<string, string> ToDictionary()
    {
        var names = _header!.Names;
        var result = new Dictionary<string, string>(names.Count, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < names.Count; i++)
        {
            result[names[i]] = GetStringAt(i);
        }

        return result;
    }

    private string GetStringAt(int columnIndex)
    {
        var cellIndex = columnIndex + Offset;
        return (uint)cellIndex < (uint)_cells.Length ? _cells[cellIndex] : string.Empty;
    }

    /// <summary>
    /// 行首必然以 <c>|</c> 开头，因此按 <c>|</c> 拆分后首个元素是空串，有效数据从下标 1 开始。
    /// </summary>
    private const int Offset = 1;
}
