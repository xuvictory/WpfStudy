using System.Diagnostics;
using System.Windows.Data;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;
using Xunit;
using Xunit.Abstractions;

namespace CommunityToolkitDemo.Tests.Performance;

/// <summary>
/// 查询/过滤期性能基线：量化 10w 商品下"扫码查找""分类过滤""关键字搜索"
/// 三条高频路径的耗时，作为索引化与服务层查询改造的对比基准。
/// </summary>
/// <remarks>
/// 耗时测量策略：
/// - 单次查找类（<see cref="PosRepository.FindByBarcode"/> 等）取 <see cref="Iterations"/> 次平均值，
///   因为它们在扫码场景下会被高频重复调用；
/// - 全量扫描 / 视图刷新类取多轮中的<b>最优值</b>：这类操作受当前机器负载影响可达 3 倍波动，
///   取最优值更接近"该算法本身需要多少时间"，也使前后对比更具可复现性。
/// </remarks>
[Collection(PerformanceTestCollection.Name)]
public sealed class QueryBenchmarkTests
{
    private const int Count = 100_000;
    private const int Iterations = 100;
    private const int Rounds = 5;

    private readonly ITestOutputHelper _output;

    public QueryBenchmarkTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 指标 E：索引化后按条码查找为 O(1)。
    /// 同轮内同时测"复刻旧实现的线性扫描"与"索引查找"，避免跨轮次比较受机器负载干扰。
    /// </summary>
    [Fact]
    public void FindByBarcode_Indexed_100k()
    {
        var repository = BuildRepository(out var products);
        var worstCaseBarcode = products[^1].Barcode;

        // 同轮对照 A：复刻改造前的线性扫描（命中末位 = 最坏情况）
        var linearMs = TimePerCall(Iterations, () => products.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.Barcode) &&
            string.Equals(p.Barcode, worstCaseBarcode.Trim(), StringComparison.Ordinal)));

        // 同轮对照 B：索引查找
        var indexedMs = TimePerCall(Iterations, () => repository.FindByBarcode(worstCaseBarcode));

        _output.WriteLine(
            $"[指标 E] FindByBarcode（命中末位，最坏情况）：{Count:N0} 商品，" +
            $"线性扫描 {linearMs:F4} ms → 索引查找 {indexedMs:F4} ms，" +
            $"加速 {Speedup(linearMs, indexedMs):N0} 倍");

        Assert.Equal(products[^1].Code, repository.FindByBarcode(worstCaseBarcode)!.Code);
        Assert.Null(repository.FindByBarcode("0000000000000"));
    }

    /// <summary>指标 F：索引化后按编码查找同为 O(1)，同样使用同轮线性对照。</summary>
    [Fact]
    public void FindByCode_Indexed_100k()
    {
        var repository = BuildRepository(out var products);
        var worstCaseCode = products[^1].Code;

        var linearMs = TimePerCall(Iterations, () => products.FirstOrDefault(p =>
            string.Equals(p.Code, worstCaseCode, StringComparison.OrdinalIgnoreCase)));

        var indexedMs = TimePerCall(Iterations, () => repository.FindByCode(worstCaseCode));

        _output.WriteLine(
            $"[指标 F] FindByCode（命中末位，最坏情况）：{Count:N0} 商品，" +
            $"线性扫描 {linearMs:F4} ms → 索引查找 {indexedMs:F4} ms，" +
            $"加速 {Speedup(linearMs, indexedMs):N0} 倍");

        Assert.Equal(products[^1].Code, repository.FindByCode(worstCaseCode)!.Code);
    }

    /// <summary>
    /// 指标 E'：索引构建的一次性成本。
    /// 索引是惰性构建的，首个查询会承担全部构建耗时；这里把它单独量出来，
    /// 避免"首调用被预热吞掉后看起来 O(1) 无成本"的误读。
    /// </summary>
    [Fact]
    public void BuildIndex_FirstLookup_100k()
    {
        var products = TestProductData.BuildProducts(Count);
        var repository = new PosRepository();
        repository.ReplaceAll(
            Array.Empty<ProductCategory>(),
            products,
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());

        var stopwatch = Stopwatch.StartNew();
        var hit = repository.FindByBarcode(products[^1].Barcode);
        stopwatch.Stop();

        _output.WriteLine(
            $"[指标 E'] 条码+编码+分类索引首次构建（含一次查找）：{Count:N0} 商品，" +
            $"耗时 {stopwatch.Elapsed.TotalMilliseconds:F1} ms");

        Assert.NotNull(hit);
    }

    /// <summary>基线 G：分类过滤需要遍历全部商品（当前无分类索引）。</summary>
    [Fact]
    public void CategoryFilter_FullScan_100k()
    {
        var products = TestProductData.BuildProducts(Count);
        const string categoryId = "drink";

        var (firstMs, bestMs, matched) = MeasureRounds(Rounds, () =>
        {
            var hits = 0;
            foreach (var product in products)
            {
                // 复刻 CashierViewModel.FilterProduct 的分类分支
                if (string.Equals(product.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    hits++;
                }
            }

            return hits;
        });

        _output.WriteLine(
            $"[基线 G] 分类过滤全量扫描：{Count:N0} 商品命中 {matched:N0} 条，" +
            $"首次 {firstMs:F1} ms / 最优 {bestMs:F1} ms");

        Assert.True(matched > 0);
    }

    /// <summary>
    /// 指标 E''：索引的稳态内存成本 —— 100k 商品下条码/编码/分类索引额外占用的保留内存。
    /// </summary>
    /// <remarks>
    /// 索引是"用空间换时间"：这份数据用于核对它没有把批次 1 省下的内存又吃回去。
    /// 度量前后各强制一次完整 GC，只统计 GC 后仍存活的增量。
    /// </remarks>
    [Fact]
    public void BuildIndex_RetainedMemory_100k()
    {
        var repository = BuildRepository(out _);

        var before = MeasureRetainedBytes();
        GC.KeepAlive(repository.FindByBarcode("0000000000000")); // 触发条码/编码/分类索引构建
        GC.KeepAlive(repository.GetProductsInCategory("drink"));
        var after = MeasureRetainedBytes();

        var deltaMb = (after - before) / 1024.0 / 1024.0;

        _output.WriteLine(
            $"[指标 E''] 索引稳态内存增量（{Count:N0} 商品，条码+编码+分类）：{deltaMb:F1} MB");

        Assert.True(deltaMb > 0, "索引应当占用可测量的内存");
        Assert.True(deltaMb < 20, $"索引内存增量异常偏高（{deltaMb:F1} MB），可能存在重复存储");
    }

    private static long MeasureRetainedBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>指标 G'：分类索引切片 —— 直接取索引桶，不再遍历全部商品。</summary>
    [Fact]
    public void CategoryFilter_Indexed_100k()
    {
        var repository = BuildRepository(out var products);
        const string categoryId = "drink";

        // 预热：分类索引为惰性构建，首次调用成本已在 BuildIndex_FirstLookup_100k 单独量化，
        // 这里只测稳态切片成本。
        _ = repository.GetProductsInCategory(categoryId);

        // 同轮对照：复刻基线 G 的全量扫描
        var (_, linearBestMs, _) = MeasureRounds(Rounds, () =>
        {
            var hits = 0;
            foreach (var product in products)
            {
                if (string.Equals(product.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    hits++;
                }
            }

            return hits;
        });

        var (_, bestMs, matched) = MeasureRounds(
            Rounds,
            () => repository.GetProductsInCategory(categoryId).Count);

        _output.WriteLine(
            $"[指标 G'] 分类切片：{Count:N0} 商品命中 {matched:N0} 条，" +
            $"线性扫描最优 {linearBestMs:F4} ms → 索引切片最优 {bestMs:F4} ms，" +
            $"加速 {Speedup(linearBestMs, bestMs):N0} 倍");

        Assert.True(matched > 0);
    }

    /// <summary>基线 H：关键字搜索对 名称/编码/条码 三个字段做非向量化忽略大小写比较。</summary>
    [Fact]
    public void KeywordFilter_FullScan_100k()
    {
        var products = TestProductData.BuildProducts(Count);
        const string keyword = "可乐";

        var (firstMs, bestMs, matched) = MeasureRounds(Rounds, () =>
        {
            var hits = 0;
            foreach (var product in products)
            {
                // 复刻 CashierViewModel.FilterProduct 的关键字分支
                if (product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || product.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || product.Barcode.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    hits++;
                }
            }

            return hits;
        });

        _output.WriteLine(
            $"[基线 H] 关键字过滤全量扫描：{Count:N0} 商品命中 {matched:N0} 条，" +
            $"首次 {firstMs:F1} ms / 最优 {bestMs:F1} ms");

        Assert.True(matched > 0);
    }

    /// <summary>
    /// 基线 I：真实的 <see cref="ListCollectionView.Refresh"/>（**改造前**收银台采用的方案，保留为对照），
    /// 场景为"分类=全部 + 关键字搜索" —— 最坏情况，<c>FilterProduct</c> 的分类短路失效，
    /// 需对全部 10w 商品做 3 个字段的忽略大小写匹配。
    /// </summary>
    [Fact]
    public void ListCollectionView_Refresh_KeywordOnAllCategories_100k()
    {
        var products = TestProductData.BuildProducts(Count);
        const string keyword = "可乐";

        // 复刻 CashierViewModel.FilterProduct：CurrentCategoryId 为"全部"时不比较分类，直接进入关键字分支
        var view = new ListCollectionView(products)
        {
            Filter = obj => obj is Product product
                            && (product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                || product.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                || product.Barcode.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        };

        var (firstMs, bestMs, matched) = MeasureRefresh(Rounds, view);

        _output.WriteLine(
            $"[基线 I] ListCollectionView.Refresh（全部 + 关键字，最坏情况）：{Count:N0} 商品命中 {matched:N0} 条，" +
            $"首次 {firstMs:F1} ms / 最优 {bestMs:F1} ms");

        Assert.Equal(matched, view.Count);
    }

    /// <summary>基线 J：切分类场景 —— 只按 CategoryId 过滤，无关键字。</summary>
    [Fact]
    public void ListCollectionView_Refresh_CategorySwitchOnly_100k()
    {
        var products = TestProductData.BuildProducts(Count);

        var view = new ListCollectionView(products)
        {
            Filter = obj => obj is Product product
                            && string.Equals(product.CategoryId, "drink", StringComparison.OrdinalIgnoreCase)
        };

        var (firstMs, bestMs, matched) = MeasureRefresh(Rounds, view);

        _output.WriteLine(
            $"[基线 J] ListCollectionView.Refresh（切到单一分类）：{Count:N0} 商品命中 {matched:N0} 条，" +
            $"首次 {firstMs:F1} ms / 最优 {bestMs:F1} ms");

        Assert.True(matched > 0);
    }

    /// <summary>
    /// 指标 K：批次 3 的服务层查询 vs 改造前的 <see cref="ListCollectionView.Refresh"/>（同轮对照）。
    /// 场景同为"全部 + 关键字"，即最坏情况。
    /// </summary>
    /// <remarks>
    /// 两者并非完全等价：服务层查询带结果上限（<see cref="ProductQueryService.MaxResultLimit"/>），
    /// 收满即提前退出，因此它做的工作量本身就少于无上限的全量刷新。
    /// 这正是批次 3 的设计意图（限制进入可观察集合的条目数），此处如实同时报告"命中数"与"返回数"。
    /// </remarks>
    [Fact]
    public void QueryService_KeywordVsListView_100k()
    {
        var products = TestProductData.BuildProducts(Count);
        const string keyword = "可乐";

        var repository = new PosRepository();
        repository.ReplaceAll(
            Array.Empty<ProductCategory>(),
            products,
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());
        var query = new ProductQueryService(repository);

        // 旧路径：无上限的全量视图刷新
        var view = new ListCollectionView(products)
        {
            Filter = obj => obj is Product product
                            && (product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                || product.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                || product.Barcode.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        };
        var (_, listViewBestMs, matched) = MeasureRefresh(Rounds, view);

        // 新路径：服务层查询（大数据量走后台线程 + 结果上限）
        var request = new ProductQueryRequest(null, keyword, Offset: 0, Limit: ProductQueryService.MaxResultLimit);
        var (_, queryBestMs, returned) = MeasureQuery(Rounds, query, request);

        _output.WriteLine(
            $"[指标 K] 全部+关键字查询：{Count:N0} 商品，命中 {matched:N0} 条；" +
            $"ListCollectionView.Refresh 最优 {listViewBestMs:F1} ms → QueryAsync 最优 {queryBestMs:F1} ms" +
            $"（受上限 {ProductQueryService.MaxResultLimit:N0} 约束，返回 {returned:N0} 条）");

        Assert.Equal(matched, view.Count);
        Assert.Equal(Math.Min(matched, ProductQueryService.MaxResultLimit), returned);
    }

    private static PosRepository BuildRepository(out List<Product> products)
    {
        products = TestProductData.BuildProducts(Count);
        var repository = new PosRepository();
        repository.ReplaceAll(
            Array.Empty<ProductCategory>(),
            products,
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());
        return repository;
    }

    /// <summary>计算加速比；对"亚微秒级"的耗时做下限保护，避免除零。</summary>
    private static double Speedup(double beforeMs, double afterMs)
        => beforeMs / Math.Max(afterMs, 1e-6);

    private static double TimePerCall(int iterations, Func<Product?> action)
    {
        // 预热，排除首次 JIT / 缓存冷启动的影响
        GC.KeepAlive(action());

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            GC.KeepAlive(action());
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / iterations;
    }

    private static (double FirstMs, double BestMs, int Result) MeasureRounds(int rounds, Func<int> scan)
    {
        var first = Stopwatch.StartNew();
        var result = scan();
        first.Stop();

        var best = double.MaxValue;
        for (var i = 0; i < rounds; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            scan();
            stopwatch.Stop();
            best = Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
        }

        return (first.Elapsed.TotalMilliseconds, best, result);
    }

    /// <summary>测量服务层查询的最优耗时与返回条数（同步等待，模拟"结果就绪"的时刻）。</summary>
    private static (double FirstMs, double BestMs, int Count) MeasureQuery(
        int rounds,
        IProductQuery query,
        ProductQueryRequest request)
    {
        var first = Stopwatch.StartNew();
        var count = query.QueryAsync(request).GetAwaiter().GetResult().Items.Count;
        first.Stop();

        var best = first.Elapsed.TotalMilliseconds;
        for (var i = 1; i < rounds; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            count = query.QueryAsync(request).GetAwaiter().GetResult().Items.Count;
            stopwatch.Stop();
            best = Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
        }

        return (first.Elapsed.TotalMilliseconds, best, count);
    }

    private static (double FirstMs, double BestMs, int Count) MeasureRefresh(int rounds, ListCollectionView view)
    {
        var first = Stopwatch.StartNew();
        view.Refresh();
        var count = view.Count;
        first.Stop();

        var best = double.MaxValue;
        for (var i = 0; i < rounds; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            view.Refresh();
            _ = view.Count;
            stopwatch.Stop();
            best = Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
        }

        return (first.Elapsed.TotalMilliseconds, best, count);
    }
}
