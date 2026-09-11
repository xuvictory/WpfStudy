using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// 基于商品目录索引的查询实现（<see cref="IProductQuery"/>）。
///
/// 关键点：
/// - **分类切片**直接命中 <see cref="IProductCatalog.GetProductsInCategory"/> 的索引，不再全量扫描；
/// - **关键字过滤**在数据量大时移到线程池，避免在 UI 线程同步吃掉多帧预算；
/// - 结果始终受上限约束（<see cref="MaxResultLimit"/>），避免 10w 项涌入可观察集合。
/// </summary>
public sealed class ProductQueryService : IProductQuery
{
    /// <summary>"全部"分类且无关键字时的默认结果上限。</summary>
    /// <remarks>
    /// POS 场景以扫码 + 搜索为主，把 10w 条全部塞进可视列表滚动意义有限。
    /// 超出上限时界面会提示"请输入关键字缩小范围"（产品取舍见分析报告第 7 章）。
    /// </remarks>
    public const int AllCategoryDefaultLimit = 200;

    /// <summary>任何查询的单次结果硬上限，防止超大分类整体进入可观察集合。</summary>
    public const int MaxResultLimit = 2000;

    /// <summary>源规模超过该值时，关键字过滤移到线程池执行。</summary>
    /// <remarks>
    /// 阈值取 2000：低于它时单次过滤在亚毫秒级完成，线程切换（含上下文捕获）反而不划算。
    /// </remarks>
    private const int BackgroundThreshold = 2000;

    private readonly IProductCatalog _catalog;

    public ProductQueryService(IProductCatalog catalog) => _catalog = catalog;

    /// <inheritdoc />
    public int TotalCount => _catalog.ProductCount;

    /// <inheritdoc />
    public Task<ProductQueryResult> QueryAsync(
        ProductQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var categoryId = string.IsNullOrWhiteSpace(request.CategoryId)
            ? _catalog.AllCategoryId
            : request.CategoryId;

        var source = _catalog.GetProductsInCategory(categoryId);

        var limit = request.Limit > 0 ? Math.Min(request.Limit, MaxResultLimit) : MaxResultLimit;
        var offset = Math.Max(request.Offset, 0);
        var keyword = request.Keyword?.Trim();

        if (string.IsNullOrEmpty(keyword))
        {
            // 无关键字：索引桶已是最终结果，只需按页切片
            return Task.FromResult(Slice(source, offset, limit));
        }

        if (source.Count <= BackgroundThreshold)
        {
            return Task.FromResult(Filter(source, keyword, offset, limit, cancellationToken));
        }

        // 关键字过滤是"3 字段 × 忽略大小写"的 CPU 密集比较，10w 规模下远超单帧预算。
        // 放到线程池：调用方 await 返回的 Task，续体在其原始上下文（UI 线程）中执行。
        return Task.Run(
            () => Filter(source, keyword, offset, limit, cancellationToken),
            cancellationToken);
    }

    /// <summary>无关键字时的分页切片。</summary>
    private static ProductQueryResult Slice(IReadOnlyList<Product> source, int offset, int limit)
    {
        if (offset >= source.Count)
        {
            return new ProductQueryResult(Array.Empty<Product>(), HasMore: false);
        }

        var take = Math.Min(limit, source.Count - offset);

        // 全量无需分页：直接复用索引桶（避免一次多余复制）；调用方只读
        if (offset == 0 && take == source.Count)
        {
            return new ProductQueryResult(source, HasMore: false);
        }

        var items = new List<Product>(take);
        for (var i = 0; i < take; i++)
        {
            items.Add(source[offset + i]);
        }

        return new ProductQueryResult(items, HasMore: offset + take < source.Count);
    }

    /// <summary>关键字过滤：收集到本页上限即提前退出，不再扫描剩余数据。</summary>
    private static ProductQueryResult Filter(
        IReadOnlyList<Product> source,
        string keyword,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = new List<Product>(Math.Min(limit, 256));
        var skipped = 0;

        for (var i = 0; i < source.Count; i++)
        {
            // 每 1024 项检查一次取消：对 10w 规模足够灵敏，检查本身也不构成开销
            if ((i & 0x3FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var product = source[i];
            if (!Matches(product, keyword))
            {
                continue;
            }

            if (skipped < offset)
            {
                skipped++;
                continue;
            }

            if (items.Count >= limit)
            {
                // 本页已收满：存在未返回的命中，直接退出
                return new ProductQueryResult(items, HasMore: true);
            }

            items.Add(product);
        }

        return new ProductQueryResult(items, HasMore: false);
    }

    /// <summary>关键字匹配规则：名称 / 编码 / 条码任一命中（忽略大小写），与改造前谓词一致。</summary>
    private static bool Matches(Product product, string keyword)
        => product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
           || product.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
           || product.Barcode.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
