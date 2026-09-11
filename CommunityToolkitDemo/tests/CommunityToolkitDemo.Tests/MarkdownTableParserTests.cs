using CommunityToolkitDemo.Services;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="MarkdownTableParser"/> 的行为回归。
/// 解析器是全部 Markdown 数据源的入口，一旦解析规则被误改，
/// 表现层会表现为"商品/设备列表为空"，因此这里把关键规则固化成用例。
/// </summary>
public sealed class MarkdownTableParserTests
{
    private const string Sample =
        "# 商品清单\r\n" +
        "\r\n" +
        "一些说明文字，不应被当作表格。\r\n" +
        "\r\n" +
        "| Code | Name | Price |\r\n" +
        "| :--- | :--: | ----: |\r\n" +
        "| P001 | 可乐 | 3.50 |\r\n" +
        "| P002 | 薯片 | 6.00 |\r\n";

    [Fact]
    public void ParseFirstTable_ReturnsAllDataRows()
    {
        var rows = MarkdownTableParser.ParseFirstTable(Sample);

        Assert.Equal(2, rows.Count);
        Assert.Equal("P001", rows[0]["Code"]);
        Assert.Equal("可乐", rows[0]["Name"]);
        Assert.Equal("3.50", rows[0]["Price"]);
        Assert.Equal("P002", rows[1]["Code"]);
    }

    [Fact]
    public void ParseFirstTable_HeaderLookupIsCaseInsensitive()
    {
        var rows = MarkdownTableParser.ParseFirstTable(Sample);

        // 表头大小写不应影响取值（数据文件由人手维护，大小写难免不一致）
        Assert.Equal("薯片", rows[1]["name"]);
        Assert.Equal("6.00", rows[1]["PRICE"]);
    }

    [Fact]
    public void ParseFirstTable_MissingCells_BecomeEmptyStrings()
    {
        const string markdown =
            "| A | B | C |\n" +
            "| - | - | - |\n" +
            "| 1 | 2 |\n";

        var rows = MarkdownTableParser.ParseFirstTable(markdown);

        var row = Assert.Single(rows);
        Assert.Equal("1", row["A"]);
        Assert.Equal("2", row["B"]);
        Assert.Equal(string.Empty, row["C"]);
    }

    [Fact]
    public void ParseFirstTable_StopsAtFirstNonTableLine()
    {
        const string markdown =
            "| A |\n" +
            "| - |\n" +
            "| 1 |\n" +
            "\n" +
            "| 2 |\n";

        var rows = MarkdownTableParser.ParseFirstTable(markdown);

        // 空行视为表格结束，第二张表不应被并入
        var row = Assert.Single(rows);
        Assert.Equal("1", row["A"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("没有任何表格的正文")]
    public void ParseFirstTable_NoTable_ReturnsEmpty(string markdown)
    {
        Assert.Empty(MarkdownTableParser.ParseFirstTable(markdown));
    }

    [Fact]
    public void ParseFirstTableGeneric_SkipsRowsMappedToNull()
    {
        const string markdown =
            "| Value |\n" +
            "| ----- |\n" +
            "| 1 |\n" +
            "| bad |\n" +
            "| 2 |\n";

        // 泛型重载约束 T : class，因此用 string 承载：无法解析的行返回 null 被自动跳过
        var values = MarkdownTableParser.ParseFirstTable<string>(markdown, row =>
            int.TryParse(row["Value"], out _) ? row["Value"] : null);

        Assert.Equal(new[] { "1", "2" }, values);
    }
}
