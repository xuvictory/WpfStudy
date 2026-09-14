namespace PrismDemo.Services;

/// <summary>
/// Markdown 表格表头：维护「列名 → 列下标」映射，以及按原始顺序排列的列名。
/// </summary>
/// <remarks>
/// 表头每张表只解析一次，随后所有数据行共享同一个实例（这是流式解析消除
/// 「每行一个字典」那一层中间表示的关键：列名映射从"每行重建"变成"每表一份"）。
/// 列名比较使用 <see cref="StringComparer.OrdinalIgnoreCase"/>，与既有行为一致 ——
/// 数据文件由人手维护，大小写难免不一致。
/// </remarks>
internal sealed class MarkdownHeader
{
    private readonly string[] _names;
    private readonly Dictionary<string, int> _index;

    private MarkdownHeader(string[] names, Dictionary<string, int> index)
    {
        _names = names;
        _index = index;
    }

    /// <summary>列数（含列名为空的列，与既有行为一致）。</summary>
    public int Count => _names.Length;

    /// <summary>按表头原始顺序返回列名（转换为字典时用于保持键顺序）。</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>
    /// 从已拆分（且已 Trim）的单元格数组解析表头。
    /// </summary>
    /// <param name="cells">整行按 <c>|</c> 拆分的结果，首元素为行首竖线前的空串。</param>
    /// <param name="startIndex">有效列在 <paramref name="cells"/> 中的起始下标。</param>
    public static MarkdownHeader Parse(string[] cells, int startIndex)
    {
        var count = Math.Max(0, cells.Length - startIndex);
        var names = new string[count];
        var index = new Dictionary<string, int>(count, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < count; i++)
        {
            var name = cells[startIndex + i];
            names[i] = name;

            // 空列名仍保留在 Names 中（转换字典时会出现 "" 键），但不参与查找
            if (name.Length > 0)
            {
                index[name] = i;
            }
        }

        return new MarkdownHeader(names, index);
    }

    public bool TryGetIndex(string column, out int index) => _index.TryGetValue(column, out index);

    /// <summary>
    /// 判断自 <paramref name="startIndex"/> 起的单元格是否为 <c>| --- | :--: |</c> 形式的分隔行。
    /// </summary>
    /// <remarks>判定规则：至少出现一个 <c>-</c>，且不存在 <c>-</c> / <c>:</c> / 空白以外的字符。</remarks>
    public static bool IsSeparatorCells(string[] cells, int startIndex)
    {
        var hasDash = false;

        for (var i = startIndex; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell.Length == 0)
            {
                continue;
            }

            foreach (var ch in cell)
            {
                if (ch == '-')
                {
                    hasDash = true;
                    continue;
                }

                if (ch is not (':' or ' ' or '\t'))
                {
                    return false;
                }
            }
        }

        return hasDash;
    }
}
