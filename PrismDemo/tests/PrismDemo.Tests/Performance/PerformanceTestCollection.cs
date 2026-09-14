using Xunit;

namespace PrismDemo.Tests.Performance;

/// <summary>
/// 性能基准测试集合定义。
/// </summary>
/// <remarks>
/// 内存测量依赖进程级 GC 状态（<see cref="GC.GetTotalMemory(bool)"/>），
/// 若与其它测试并行运行，其它测试的分配会被计入差值，导致数字不可复现。
/// 因此这里禁用并行：基准用例串行执行，且不与其它集合交叉。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceTestCollection
{
    /// <summary>集合名称，供 <see cref="CollectionAttribute"/> 引用。</summary>
    public const string Name = "Performance";
}
