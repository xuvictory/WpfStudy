using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>
/// 购物车数量上限规则（单一事实来源）。
///
/// 为什么单独抽出来？
/// 同一条"能否加购/最多加多少"的规则同时被三处需要：
/// 购物车服务（约束实际数量）、收银台 ViewModel（拦截零库存并给出提示）、
/// 以及购物车行项（驱动"+"按钮的可用状态）。
/// 规则分散在多处时，任何一处漏改都会造成"按钮可点但加不进去"之类的不一致，
/// 因此集中到这里，全应用只保留一份判断。
/// </summary>
public static class CartLimits
{
    /// <summary>允许超库存销售时不再限制数量。</summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>
    /// 在给定"是否允许超卖"策略下，该商品可加入购物车的数量上限。
    /// 不允许超卖时上限即当前库存（库存为负视为 0）。
    /// </summary>
    public static int MaxQuantityFor(Product product, bool allowOversell)
        => allowOversell ? Unlimited : Math.Max(product.Stock, 0);

    /// <summary>
    /// 该商品当前是否允许加入购物车。
    /// 允许超卖时始终可加；不允许超卖时零库存商品不可加购。
    /// </summary>
    public static bool CanAdd(Product product, bool allowOversell)
        => MaxQuantityFor(product, allowOversell) > 0;
}
