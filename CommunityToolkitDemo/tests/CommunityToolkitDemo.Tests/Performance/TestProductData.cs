using System.Globalization;
using System.Text;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.Tests.Performance;

/// <summary>
/// 性能基准共享数据源：按 <c>Data/products.md</c> 的字段约定生成大规模商品，
/// 并复刻 <c>MarkdownDataService.MapProduct</c> 的映射语义。
/// </summary>
/// <remarks>
/// 为什么不直接依赖生产代码里的映射方法？
/// <c>MarkdownDataService.MapProduct</c> 是 <c>private</c>，测试程序集无法访问。
/// 这里保持"字段名、默认值、空值兜底"三处语义与其完全一致，
/// 这样基准测得的耗时/内存与生产解析行为同源；解析规则的边界条件
/// 由 <c>MarkdownRowReaderTests</c> 使用真实实现覆盖。
/// </remarks>
internal static class TestProductData
{
    /// <summary>与 <c>Data/categories.md</c> 保持一致的分类集合。</summary>
    public static readonly string[] Categories = ["drink", "snack", "dairy", "fresh", "staple", "daily", "liquor"];

    private static readonly string[] Names =
    [
        "饮用天然水", "纯净水", "可乐", "冰红茶", "乌龙茶", "薯片", "夹心饼干", "每日坚果",
        "纯牛奶", "酸奶", "鲜牛奶", "全脂奶粉", "红富士苹果", "香蕉", "小青菜", "五花肉",
        "稻花香大米", "橄榄油", "红烧牛肉面", "抽纸", "洗衣液", "香皂", "牙膏", "啤酒", "干红葡萄酒"
    ];

    private static readonly string[] Units = ["瓶", "罐", "袋", "盒", "包", "斤", "提", "支"];

    private static readonly string[] Specs = ["550ml", "330ml", "1L", "散装", "500g", "250ml", "70g", "104g", "5kg", "750ml"];

    /// <summary>生成一份包含表头/分隔行的 Markdown 商品表格文本。</summary>
    public static string BuildMarkdown(int count)
    {
        var sb = new StringBuilder(count * 128);

        sb.AppendLine("# 商品主数据（性能基准数据）");
        sb.AppendLine();
        sb.AppendLine("| Code | Name | CategoryId | Price | Barcode | Stock | WarnStock | Unit | Spec |");
        sb.AppendLine("| ---- | ---- | ---------- | ----- | ------- | ----- | --------- | ---- | ---- |");

        for (var i = 1; i <= count; i++)
        {
            sb.Append("| ").Append(CodeOf(i))
              .Append(" | ").Append(NameOf(i))
              .Append(" | ").Append(CategoryOf(i))
              .Append(" | ").Append(PriceOf(i).ToString("F2", CultureInfo.InvariantCulture))
              .Append(" | ").Append(BarcodeOf(i))
              .Append(" | ").Append(StockOf(i))
              .Append(" | ").Append(WarnStockOf(i))
              .Append(" | ").Append(UnitOf(i))
              .Append(" | ").Append(SpecOf(i))
              .AppendLine(" |");
        }

        return sb.ToString();
    }

    /// <summary>直接构造 <paramref name="count"/> 个商品实体（不经过 Markdown 文本）。</summary>
    public static List<Product> BuildProducts(int count)
    {
        var list = new List<Product>(count);
        for (var i = 1; i <= count; i++)
        {
            list.Add(CreateProduct(i));
        }

        return list;
    }

    /// <summary>
    /// 与生产 <c>MapProduct</c> 语义一致的映射委托，基于流式行视图（当前生产路径）。
    /// </summary>
    public static Product? MapProductFromRow(MarkdownRow row)
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

    /// <summary>与生产 <c>MapProduct</c> 语义一致的映射委托（字典式旧路径，用于改造前基线）。</summary>
    public static Product? MapProduct(IReadOnlyDictionary<string, string> row)
    {
        var code = Get(row, "Code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return new Product
        {
            Code = code,
            Name = Get(row, "Name"),
            CategoryId = Get(row, "CategoryId"),
            Price = ParseDecimal(Get(row, "Price")),
            Barcode = Get(row, "Barcode"),
            Stock = ParseInt(Get(row, "Stock")),
            WarnStock = ParseInt(Get(row, "WarnStock"), 10),
            Unit = Get(row, "Unit") is { Length: > 0 } unit ? unit : "件",
            Spec = Get(row, "Spec")
        };
    }

    private static Product CreateProduct(int i) => new()
    {
        Code = CodeOf(i),
        Name = NameOf(i),
        CategoryId = CategoryOf(i),
        Price = PriceOf(i),
        Barcode = BarcodeOf(i),
        Stock = StockOf(i),
        WarnStock = WarnStockOf(i),
        Unit = UnitOf(i),
        Spec = SpecOf(i)
    };

    private static string CodeOf(int i) => $"P{i:D6}";

    private static string NameOf(int i) => $"{Names[(i - 1) % Names.Length]} {i}";

    private static string CategoryOf(int i) => Categories[(i - 1) % Categories.Length];

    private static decimal PriceOf(int i) => Math.Round(1m + (i * 37 % 19900) / 100m, 2);

    private static string BarcodeOf(int i) => $"69{i % 100_000_000_000:D11}";

    private static int StockOf(int i) => i * 17 % 501;

    private static int WarnStockOf(int i) => 5 + i % 26;

    private static string UnitOf(int i) => Units[(i - 1) % Units.Length];

    private static string SpecOf(int i) => Specs[(i - 1) % Specs.Length];

    private static string Get(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value : string.Empty;

    private static decimal ParseDecimal(string text)
        => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static int ParseInt(string text, int fallback = 0)
        => int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
