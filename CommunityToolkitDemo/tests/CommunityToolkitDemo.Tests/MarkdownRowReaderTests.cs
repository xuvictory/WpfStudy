using System.IO;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="MarkdownRowReader"/> 的行为回归。
/// </summary>
/// <remarks>
/// 流式解析器与 <see cref="MarkdownTableParser"/> 共用同一套解析规则（由前者实现、后者包装），
/// 但"一次性返回列表"变成"逐行产出"后，边界行为（表头识别、分隔行、缺少列、截断）
/// 必须逐一锁定，否则表现层会表现为"商品/设备列表为空"这类难以定位的问题。
/// </remarks>
public sealed class MarkdownRowReaderTests
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
    public void ReadRows_SkipsPreambleAndSeparator()
    {
        var rows = Read(Sample, out var truncated);

        Assert.False(truncated);
        Assert.Equal(2, rows.Count);
        Assert.Equal("P001", rows[0].GetString("Code"));
        Assert.Equal("可乐", rows[0].GetString("Name"));
        Assert.Equal("3.50", rows[0].GetString("Price"));
        Assert.Equal("P002", rows[1].GetString("Code"));
    }

    [Fact]
    public void ReadRows_HeaderLookupIsCaseInsensitive()
    {
        var rows = Read(Sample, out _);

        // 数据文件由人手维护，表头大小写难免不一致
        Assert.Equal("薯片", rows[1].GetString("name"));
        Assert.Equal("6.00", rows[1].GetString("PRICE"));
    }

    [Fact]
    public void ReadRows_UnknownColumn_ReturnsFallback()
    {
        var rows = Read(Sample, out _);

        Assert.Equal(string.Empty, rows[0].GetString("NotExists"));
        Assert.Equal("默认", rows[0].GetString("NotExists", "默认"));
        Assert.Equal(7, rows[0].GetInt("NotExists", 7));
    }

    [Fact]
    public void ReadRows_MissingCells_FallBackToDefaults()
    {
        const string markdown =
            "| A | B | C |\n" +
            "| - | - | - |\n" +
            "| 1 | 2 |\n";

        var rows = Read(markdown, out _);

        var row = Assert.Single(rows);
        Assert.Equal("1", row.GetString("A"));
        Assert.Equal("2", row.GetString("B"));
        Assert.Equal(string.Empty, row.GetString("C"));
        Assert.Equal(9, row.GetInt("C", 9));
    }

    [Fact]
    public void ReadRows_StopsAtFirstNonTableLine()
    {
        const string markdown =
            "| A |\n" +
            "| - |\n" +
            "| 1 |\n" +
            "\n" +
            "| 2 |\n";

        var rows = Read(markdown, out _);

        // 空行视为表格结束，第二张表不应被并入
        var row = Assert.Single(rows);
        Assert.Equal("1", row.GetString("A"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("没有任何表格的正文")]
    public void ReadRows_NoTable_YieldsNothing(string markdown)
    {
        Assert.Empty(Read(markdown, out _));
    }

    [Fact]
    public void ReadRows_StopsAtMaxRows_AndReportsTruncation()
    {
        var rows = Read(BuildTable(5), maxRows: 3, out var truncated);

        Assert.Equal(3, rows.Count);
        Assert.True(truncated);
    }

    /// <summary>
    /// 边界：数据行数恰好等于上限时不应报告截断（否则会误报"数据被截断"）。
    /// </summary>
    [Fact]
    public void ReadRows_ExactlyAtMaxRows_IsNotTruncated()
    {
        var rows = Read(BuildTable(3), maxRows: 3, out var truncated);

        Assert.Equal(3, rows.Count);
        Assert.False(truncated);
    }

    /// <summary>
    /// 流式路径与字典路径的取值必须完全一致。
    /// </summary>
    /// <remarks>
    /// 两条路径共用同一套解析规则，此用例把它们锁在一起：
    /// 任何一侧规则被改动而另一侧未同步，都会在这里暴露。
    /// </remarks>
    [Fact]
    public void StreamingPath_MatchesDictionaryPath()
    {
        var expected = MarkdownTableParser.ParseFirstTable(Sample);
        var actual = Read(Sample, out _);

        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            foreach (var (key, value) in expected[i])
            {
                if (key.Length == 0)
                {
                    continue;
                }

                Assert.Equal(value, actual[i].GetString(key));
            }
        }
    }

    [Fact]
    public void ReadRows_StreamingParseToProduct_MapsAllFields()
    {
        using var reader = new StringReader(
            "| Code | Name | CategoryId | Price | Barcode | Stock | WarnStock | Unit | Spec |\n" +
            "| ---- | ---- | ---------- | ----- | ------- | ----- | --------- | ---- | ---- |\n" +
            "| P001 | 可乐 | drink | 3.50 | 6900000000001 | 12 | 3 | 瓶 | 330ml |\n" +
            "|  | 缺编码应被跳过 | drink | 1.00 | 6900000000002 | 1 | 1 | 瓶 | 250ml |\n");

        var products = MarkdownTableParser.ParseFirstTable<Product>(reader, MapProduct);

        var product = Assert.Single(products);
        Assert.Equal("P001", product.Code);
        Assert.Equal("可乐", product.Name);
        Assert.Equal("drink", product.CategoryId);
        Assert.Equal(3.50m, product.Price);
        Assert.Equal("6900000000001", product.Barcode);
        Assert.Equal(12, product.Stock);
        Assert.Equal(3, product.WarnStock);
        Assert.Equal("瓶", product.Unit);
        Assert.Equal("330ml", product.Spec);
    }

    [Fact]
    public void ReadRows_MissingUnit_UsesDefaultChineseUnit()
    {
        using var reader = new StringReader(
            "| Code | Unit |\n" +
            "| ---- | ---- |\n" +
            "| P001 |  |\n");

        // 与生产 MapProduct 的空值兜底保持一致
        var products = MarkdownTableParser.ParseFirstTable<Product>(reader, MapProduct);

        Assert.Equal("件", Assert.Single(products).Unit);
    }

    private static Product? MapProduct(MarkdownRow row)
    {
        var code = row.GetString("Code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return new Product
        {
            Code = code,
            Name = row.GetString("Name"),
            CategoryId = row.GetString("CategoryId"),
            Price = row.GetDecimal("Price"),
            Barcode = row.GetString("Barcode"),
            Stock = row.GetInt("Stock"),
            WarnStock = row.GetInt("WarnStock", 10),
            Unit = row.GetString("Unit") is { Length: > 0 } unit ? unit : "件",
            Spec = row.GetString("Spec")
        };
    }

    private static List<MarkdownRow> Read(string markdown, out bool truncated)
        => Read(markdown, int.MaxValue, out truncated);

    private static List<MarkdownRow> Read(string markdown, int maxRows, out bool truncated)
    {
        using var reader = new StringReader(markdown);
        var rowReader = new MarkdownRowReader(reader, maxRows);
        var rows = rowReader.ReadRows().ToList();
        truncated = rowReader.Truncated;
        return rows;
    }

    private static string BuildTable(int dataRows)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("| A |\n| - |\n");

        for (var i = 1; i <= dataRows; i++)
        {
            builder.Append("| ").Append(i).Append(" |\n");
        }

        return builder.ToString();
    }
}
