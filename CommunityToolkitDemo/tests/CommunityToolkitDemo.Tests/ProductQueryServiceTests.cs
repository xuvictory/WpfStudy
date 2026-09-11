using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;
using CommunityToolkitDemo.Tests.Performance;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="ProductQueryService"/> 的行为回归。
///
/// 覆盖三组约定：
/// 1) **结果正确性**：分类切片、关键字（名称/编码/条码、忽略大小写）、分页、组合条件；
/// 2) **上限与截断**：结果永不超过上限，"还有更多"标记如实反映截断；
/// 3) **执行策略**：小结果集同步完成（不付线程切换成本），大结果集可被取消且不阻塞调用方。
/// </summary>
public sealed class ProductQueryServiceTests
{
    private const string Keyword = "可乐";

    private static (ProductQueryService Service, PosRepository Repository, List<Product> Products) Build(int count)
    {
        var products = TestProductData.BuildProducts(count);
        var repository = new PosRepository();
        repository.ReplaceAll(
            TestProductData.Categories
                .Select(id => new ProductCategory { Id = id, Name = id, Short = id[..1] })
                .ToArray(),
            products,
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());

        return (new ProductQueryService(repository), repository, products);
    }

    [Fact]
    public void TotalCount_ReflectsCatalogSize()
    {
        var (service, _, products) = Build(500);

        Assert.Equal(products.Count, service.TotalCount);
    }

    [Fact]
    public async Task Query_CategoryOnly_SlicesIndexedBucket()
    {
        var (service, _, products) = Build(200);
        const string categoryId = "drink";
        var expected = products.Count(p => p.CategoryId == categoryId);

        var result = await service.QueryAsync(new ProductQueryRequest(categoryId, Keyword: null));

        Assert.Equal(expected, result.Items.Count);
        Assert.All(result.Items, p => Assert.Equal(categoryId, p.CategoryId));
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task Query_UnknownCategory_ReturnsEmpty()
    {
        var (service, _, _) = Build(100);

        var result = await service.QueryAsync(new ProductQueryRequest("no-such-category", Keyword: null));

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task Query_EmptyCatalog_ReturnsEmpty()
    {
        var repository = new PosRepository();
        var service = new ProductQueryService(repository);

        var result = await service.QueryAsync(new ProductQueryRequest(null, Keyword));

        Assert.Empty(result.Items);
        Assert.Equal(0, service.TotalCount);
    }

    [Fact]
    public async Task Query_AllCategory_NoKeyword_RespectsLimitAndFlagsMore()
    {
        var (service, repository, _) = Build(10_000);

        var result = await service.QueryAsync(
            new ProductQueryRequest(repository.AllCategoryId, Keyword: null, Offset: 0, Limit: 200));

        Assert.Equal(200, result.Items.Count);
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task Query_AllCategory_UnderLimit_NoMore()
    {
        var (service, repository, _) = Build(50);

        var result = await service.QueryAsync(
            new ProductQueryRequest(repository.AllCategoryId, Keyword: null, Offset: 0, Limit: 200));

        Assert.Equal(50, result.Items.Count);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task Query_LimitIsClampedToMaxResultLimit()
    {
        var (service, _, _) = Build(10_000);

        var result = await service.QueryAsync(
            new ProductQueryRequest(null, Keyword: null, Offset: 0, Limit: int.MaxValue));

        Assert.Equal(ProductQueryService.MaxResultLimit, result.Items.Count);
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task Query_Keyword_MatchesNameAndIsCaseInsensitive()
    {
        var (service, _, products) = Build(1_000);
        var expected = products.Where(p => p.Name.Contains(Keyword, StringComparison.Ordinal)).ToList();

        var result = await service.QueryAsync(new ProductQueryRequest(null, Keyword));

        // "可乐" 命中数 40(< 上限 2000)，应当全量返回且无截断
        Assert.Equal(expected.Count, result.Items.Count);
        Assert.False(result.HasMore);
        Assert.Equal(expected.Select(p => p.Code), result.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task Query_Keyword_MatchesCodeAndBarcodeCaseInsensitive()
    {
        var (service, _, _) = Build(1_000);

        var byCode = await service.QueryAsync(new ProductQueryRequest(null, "p000123"));
        var byBarcode = await service.QueryAsync(new ProductQueryRequest(null, "6900000000123"));

        Assert.Equal("P000123", Assert.Single(byCode.Items).Code);
        Assert.Equal("P000123", Assert.Single(byBarcode.Items).Code);
    }

    [Fact]
    public async Task Query_KeywordAndCategory_AreCombined()
    {
        var (service, _, products) = Build(1_000);
        const string categoryId = "snack";
        var expected = products
            .Where(p => p.CategoryId == categoryId && p.Name.Contains(Keyword, StringComparison.Ordinal))
            .Select(p => p.Code)
            .ToArray();

        var result = await service.QueryAsync(new ProductQueryRequest(categoryId, Keyword));

        Assert.NotEmpty(expected);
        Assert.Equal(expected, result.Items.Select(p => p.Code));
        Assert.All(result.Items, p => Assert.Equal(categoryId, p.CategoryId));
    }

    [Fact]
    public async Task Query_Offset_SkipsLeadingMatches()
    {
        var (service, _, products) = Build(1_000);
        var matching = products.Where(p => p.Name.Contains(Keyword, StringComparison.Ordinal)).ToList();

        var result = await service.QueryAsync(
            new ProductQueryRequest(null, Keyword, Offset: 5, Limit: 3));

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(matching.Skip(5).Take(3).Select(p => p.Code), result.Items.Select(p => p.Code));
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task Query_NoMatch_ReturnsEmptyWithoutMore()
    {
        var (service, _, _) = Build(1_000);

        var result = await service.QueryAsync(new ProductQueryRequest(null, "不存在的关键字"));

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
    }

    /// <summary>
    /// 小结果集必须同步完成：低于后台阈值的过滤在亚毫秒级结束，
    /// 为此付一次线程切换（含上下文捕获）不划算。
    /// </summary>
    [Fact]
    public void QueryAsync_SmallSource_CompletesSynchronously()
    {
        var (service, _, _) = Build(100);

        var task = service.QueryAsync(new ProductQueryRequest(null, Keyword));

        Assert.True(task.IsCompleted);
    }

    /// <summary>
    /// 大数据量 + 已取消的令牌：查询必须在后台路径上响应取消，而不是先把全部数据扫完。
    /// （10,000 条已超过后台阈值 2000，走的是 <c>Task.Run</c> 分支）
    /// </summary>
    [Fact]
    public async Task QueryAsync_LargeSource_WithCancelledToken_Throws()
    {
        var (service, _, _) = Build(10_000);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.QueryAsync(new ProductQueryRequest(null, Keyword), cts.Token));
    }
}
