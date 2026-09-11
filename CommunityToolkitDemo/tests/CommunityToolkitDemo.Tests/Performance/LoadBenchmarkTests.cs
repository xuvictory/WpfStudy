using System.Diagnostics;
using System.IO;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;
using Xunit;
using Xunit.Abstractions;

namespace CommunityToolkitDemo.Tests.Performance;

/// <summary>
/// 加载期性能与内存基准：量化 <see cref="MarkdownTableParser"/> 在 10w 行规模下的耗时与内存，
/// 覆盖"改造前的字典中间表示路径"（基线 A/B/C）与"改造后的流式路径"（验证 A'/B'）两组对照。
/// </summary>
/// <remarks>
/// 这些用例只做"度量 + 少量健全性断言"，不做过紧的性能断言：
/// 同一台机器上不同负载会带来 2~3 倍波动，过紧的阈值会让测试变成噪音源。
/// 真正的对比结论记录在 <c>docs/性能与内存专项分析报告.md</c>。
/// </remarks>
[Collection(PerformanceTestCollection.Name)]
public sealed class LoadBenchmarkTests
{
    private const int Count = 100_000;

    private readonly ITestOutputHelper _output;

    public LoadBenchmarkTests(ITestOutputHelper output) => _output = output;

    /// <summary>基线 A：只解析为"字段名 → 文本"字典（保留全部中间表示）。</summary>
    [Fact]
    public void Parse_DictionaryIntermediate_100k()
    {
        var markdown = TestProductData.BuildMarkdown(Count);

        var (rows, elapsedMs, retainedBytes, allocatedBytes) =
            Timed(() => MarkdownTableParser.ParseFirstTable(markdown));

        _output.WriteLine(
            $"[基线 A] 仅解析为字典：{Count:N0} 行，耗时 {elapsedMs:F1} ms，" +
            $"保留内存 {ToMegabytes(retainedBytes):F1} MB，总分配 {ToMegabytes(allocatedBytes):F1} MB");

        Assert.Equal(Count, rows.Count);
        GC.KeepAlive(markdown);
    }

    /// <summary>基线 B：解析并映射为 <see cref="Models.Product"/>（生产路径的完整行为）。</summary>
    [Fact]
    public void Parse_ToProduct_100k()
    {
        var markdown = TestProductData.BuildMarkdown(Count);

        var (products, elapsedMs, retainedBytes, allocatedBytes) =
            Timed(() => MarkdownTableParser.ParseFirstTable<Product>(markdown, TestProductData.MapProduct));

        _output.WriteLine(
            $"[基线 B] 解析并映射为 Product：{Count:N0} 条，耗时 {elapsedMs:F1} ms，" +
            $"保留内存 {ToMegabytes(retainedBytes):F1} MB，总分配 {ToMegabytes(allocatedBytes):F1} MB");

        Assert.Equal(Count, products.Count);
        GC.KeepAlive(markdown);
    }

    /// <summary>基线 C：只保留 <see cref="Models.Product"/> 实体，不含任何解析中间表示。</summary>
    /// <remarks>
    /// 基线 B 与基线 C 的差值，就是"字典中间表示 + 行数组 + 单元格数组"这部分
    /// 在加载期额外占用的内存——即改造要消除的峰值来源。
    /// </remarks>
    [Fact]
    public void ProductEntitiesOnly_100k()
    {
        var (products, elapsedMs, retainedBytes, allocatedBytes) = Timed(() => TestProductData.BuildProducts(Count));

        _output.WriteLine(
            $"[基线 C] 仅构造 Product 实体：{Count:N0} 条，耗时 {elapsedMs:F1} ms，" +
            $"保留内存 {ToMegabytes(retainedBytes):F1} MB，总分配 {ToMegabytes(allocatedBytes):F1} MB");

        Assert.Equal(Count, products.Count);
    }

    /// <summary>
    /// 改造验证 D：10 万行商品文件（实测约 8.4 MB）必须能真正加载出来。
    /// </summary>
    /// <remarks>
    /// 这是对"8 MB 上限导致 10w 数据被整体跳过"这一功能阻断的回归护栏：
    /// 改造前该文件会命中 <c>MaxDataFileBytes</c> 被跳过，商品列表为空而非"加载变慢"。
    /// 用例会临时覆盖运行目录下的 Data/products.md，结束后还原，避免影响其他用例。
    /// </remarks>
    [Fact]
    public async Task LargeFile_LoadsSuccessfully_100k()
    {
        var markdown = TestProductData.BuildMarkdown(Count);

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        var productsPath = Path.Combine(dataDirectory, "products.md");
        var backup = File.Exists(productsPath) ? await File.ReadAllTextAsync(productsPath) : null;

        await File.WriteAllTextAsync(productsPath, markdown);
        var fileLength = new FileInfo(productsPath).Length;

        try
        {
            var repository = new PosRepository();
            var service = new MarkdownDataService(repository);

            var (_, elapsedMs) = await TimedAsync(() => service.LoadAsync());

            _output.WriteLine(
                $"[改造验证 D] {Count:N0} 行文件体积 {ToMegabytes(fileLength):F2} MB（兜底阈值 64 MB），" +
                $"LoadAsync 耗时 {elapsedMs:F1} ms，商品数 {repository.Products.Count:N0}，" +
                $"错误：{(service.LoadErrors.Count == 0 ? "无" : string.Join(" / ", service.LoadErrors))}");

            Assert.Equal(Count, repository.Products.Count);
            Assert.DoesNotContain(
                service.LoadErrors,
                error => error.Contains("数据文件过大", StringComparison.Ordinal));
        }
        finally
        {
            if (backup is null)
            {
                File.Delete(productsPath);
            }
            else
            {
                await File.WriteAllTextAsync(productsPath, backup);
            }
        }
    }

    /// <summary>
    /// 改造验证 A'：流式解析并映射为 <see cref="Models.Product"/>，不产生逐行字典。
    /// </summary>
    /// <remarks>
    /// 与基线 B 使用完全相同的输入文本与映射语义，差异只在解析方式：
    /// B 先构建全部字典再二次映射，A' 读一行映射一行。
    /// 两者的耗时 / 总分配差值即"消除中间表示"的收益。
    /// </remarks>
    [Fact]
    public void ParseStreaming_ToProduct_100k()
    {
        var markdown = TestProductData.BuildMarkdown(Count);

        var (products, elapsedMs, retainedBytes, allocatedBytes) = Timed(() =>
        {
            using var reader = new StringReader(markdown);
            return MarkdownTableParser.ParseFirstTable<Product>(reader, TestProductData.MapProductFromRow);
        });

        _output.WriteLine(
            $"[改造验证 A'] 流式解析并映射为 Product：{Count:N0} 条，耗时 {elapsedMs:F1} ms，" +
            $"保留内存 {ToMegabytes(retainedBytes):F1} MB，总分配 {ToMegabytes(allocatedBytes):F1} MB");

        Assert.Equal(Count, products.Count);
        GC.KeepAlive(markdown);
    }

    /// <summary>改造验证 B'：流式解析在 <c>maxRows</c> 上限下会截断并给出标记。</summary>
    [Fact]
    public void ParseStreaming_RespectsMaxRows()
    {
        var markdown = TestProductData.BuildMarkdown(1_000);

        using var reader = new StringReader(markdown);
        var products = MarkdownTableParser.ParseFirstTable<Product>(
            reader,
            TestProductData.MapProductFromRow,
            maxRows: 100,
            out var truncated);

        _output.WriteLine($"[改造验证 B'] 流式解析截断：maxRows=100，实际 {products.Count} 条，截断={truncated}");

        Assert.Equal(100, products.Count);
        Assert.True(truncated);
    }

    /// <summary>
    /// 测量"执行耗时 + GC 后保留内存增量 + 总分配字节"，返回值由调用方持有以防被提前回收。
    /// </summary>
    /// <remarks>
    /// 为什么同时采集两个内存指标？
    /// - <b>保留内存</b>（<see cref="GC.GetTotalMemory(bool)"/> 差值）：反映结果对象在 GC 回收后
    ///   仍占据的稳态空间，但会被"测量期间恰好触发的 GC"干扰 —— 若解析过程的垃圾在本次测量内
    ///   被回收，差值会偏小，甚至出现"分配更多反而数值更小"的假象。
    /// - <b>总分配字节</b>（<see cref="GC.GetTotalAllocatedBytes(bool)"/> 差值）：累计分配量，
    ///   不受 GC 时机影响，能真实反映"这个操作制造了多少托管对象"，也就是 GC 压力与内存峰值的来源。
    /// 两者的差值还能直接量化"被丢弃的中间表示"规模。
    /// </remarks>
    private static (T Result, double ElapsedMs, long RetainedBytes, long AllocatedBytes) Timed<T>(Func<T> factory)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);

        var stopwatch = Stopwatch.StartNew();
        var result = factory();
        stopwatch.Stop();

        var allocatedAfter = GC.GetTotalAllocatedBytes(precise: false);
        var retainedAfter = GC.GetTotalMemory(forceFullCollection: false);

        return (
            result,
            stopwatch.Elapsed.TotalMilliseconds,
            retainedAfter - retainedBefore,
            allocatedAfter - allocatedBefore);
    }

    /// <summary>异步版本的耗时测量（用于含真实文件 IO 的加载路径）。</summary>
    private static async Task<(bool Result, double ElapsedMs)> TimedAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return (true, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static double ToMegabytes(long bytes) => bytes / 1024.0 / 1024.0;
}
