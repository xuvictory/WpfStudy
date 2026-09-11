using System.IO;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// Markdown 表格的流式读取器：逐行读取文本流并产出 <see cref="MarkdownRow"/>。
/// </summary>
/// <remarks>
/// 与旧的「整份文本读入 → 逐行拆分成字典数组 → 再二次遍历映射」相比：
/// <list type="bullet">
/// <item>不需要把整份文件读成字符串，也不需要 <c>Replace</c> 复制两份文本、<c>Split('\n')</c> 生成全部行数组；</item>
/// <item>不产生「每行一个字典」的中间表示；</item>
/// <item>调用方读一行即可映射一行，中间表示随时可被回收，峰值内存降到约 1 倍结果集规模。</item>
/// </list>
/// 解析规则与 <see cref="MarkdownTableParser"/> 完全一致（见该类文档）；
/// 差异仅在于「一次性返回列表」变成「逐行产出」，因此边界行为由单测逐一锁定。
/// </remarks>
public sealed class MarkdownRowReader
{
    private readonly TextReader _reader;
    private readonly int _maxRows;

    /// <param name="reader">表格文本来源（文件流 / <see cref="StringReader"/> 均可）。</param>
    /// <param name="maxRows">
    /// 最多产出的数据行数，用于防止异常数据把结果集撑爆。
    /// 达到上限后停止读取，并把 <see cref="Truncated"/> 置为 <c>true</c>。
    /// </param>
    public MarkdownRowReader(TextReader reader, int maxRows = int.MaxValue)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _maxRows = maxRows;
    }

    /// <summary>
    /// 是否因为超过 <c>maxRows</c> 而提前结束。
    /// </summary>
    /// <remarks>必须在 <see cref="ReadRows"/> 完整枚举结束后读取才有效。</remarks>
    public bool Truncated { get; private set; }

    /// <summary>逐行产出表格数据（不含表头行与分隔行）。</summary>
    public IEnumerable<MarkdownRow> ReadRows()
    {
        MarkdownHeader? header = null;
        var emitted = 0;

        string? rawLine;
        while ((rawLine = _reader.ReadLine()) is not null)
        {
            var line = rawLine.Trim();

            if (!line.StartsWith('|'))
            {
                // 表格已开始后再遇到非表格行（含空行）即视为表格结束
                if (header is not null)
                {
                    break;
                }

                // 表格之前的标题/说明文字，跳过
                continue;
            }

            // 行以 '|' 开头 → 拆分后首个元素必为空串，有效列从下标 1 开始；
            // 行尾竖线会额外产生一个空串元素，但它落在列数之外，取值时不会被访问。
            var cells = line.Split('|', StringSplitOptions.TrimEntries);

            if (header is null)
            {
                header = MarkdownHeader.Parse(cells, startIndex: 1);
                continue;
            }

            if (MarkdownHeader.IsSeparatorCells(cells, startIndex: 1))
            {
                continue;
            }

            if (emitted >= _maxRows)
            {
                Truncated = true;
                break;
            }

            emitted++;
            yield return new MarkdownRow(cells, header);
        }
    }
}
