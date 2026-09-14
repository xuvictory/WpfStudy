using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>
/// 商品查询条件。
/// </summary>
/// <param name="CategoryId">分类 Id；为空或等于"全部"时表示不限定分类。</param>
/// <param name="Keyword">关键字（名称 / 编码 / 条码，忽略大小写）；为空表示不限定关键字。</param>
/// <param name="Offset">跳过前 N 条命中（分页用）。</param>
/// <param name="Limit">本次最多返回的条目数；&lt;= 0 表示使用实现默认值。</param>
public readonly record struct ProductQueryRequest(
    string? CategoryId,
    string? Keyword,
    int Offset = 0,
    int Limit = 0);

/// <summary>
/// 商品查询结果。
/// </summary>
/// <param name="Items">命中的商品（最多 <see cref="ProductQueryRequest.Limit"/> 条）。</param>
/// <param name="HasMore">是否因结果上限被截断（还有未返回的命中）。</param>
public sealed record ProductQueryResult(IReadOnlyList<Product> Items, bool HasMore);

/// <summary>
/// 商品查询契约：表现层"看商品"的唯一入口。
///
/// 设计说明：批次 3 之前，收银台直接把仓储里的全量 <c>List&lt;Product&gt;</c> 交给
/// <c>ListCollectionView.Filter</c>，每次搜索/切分类都对 10w 项逐个回调谓词，
/// 并触发一次 Reset（可视容器整体重建）。本契约把"过滤 + 切片"从表现层下沉到服务层：
/// <list type="bullet">
/// <item>分类切片直接命中仓储的分类索引，复杂度 O(结果数) 而非 O(全量)；</item>
/// <item>关键字过滤（CPU 密集）在大数据量时移到后台线程执行，不阻塞 UI；</item>
/// <item>结果始终受上限约束，避免把 10w 项塞进可观察集合。</item>
/// </list>
/// </summary>
public interface IProductQuery
{
    /// <summary>商品总数（不区分分类）。</summary>
    int TotalCount { get; }

    /// <summary>
    /// 按条件查询商品。
    /// </summary>
    /// <remarks>
    /// 实现约定：不抛 <see cref="OperationCanceledException"/> 之外的异常；
    /// 调用方以 <paramref name="cancellationToken"/> 取消上一次尚未完成的查询（用户连续输入场景）。
    /// </remarks>
    Task<ProductQueryResult> QueryAsync(
        ProductQueryRequest request,
        CancellationToken cancellationToken = default);
}
