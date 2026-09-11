using System.Collections.ObjectModel;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// 购物车服务：收银台的核心业务状态。
///
/// 为什么放在 Service 而不是 ViewModel？
/// 购物车需要在页面切换（收银台 ↔ 设备监控）之间保持，并且要驱动主窗口状态栏，
/// 因此把它做成单例服务，页面 ViewModel 只负责"调用 + 展示"。
/// 每次变更后由本层统一广播 <see cref="CartChangedMessage"/>，
/// 主窗口与其他页面无需知道购物车在哪里。
/// </summary>
public interface ICartService
{
    /// <summary>购物车行项集合（UI 直接绑定）</summary>
    ObservableCollection<CartItem> Items { get; }

    /// <summary>商品总件数</summary>
    int ItemCount { get; }

    /// <summary>商品种类数</summary>
    int KindCount { get; }

    /// <summary>合计金额</summary>
    decimal Total { get; }

    /// <summary>当前汇总快照</summary>
    CartSummary Summary { get; }

    /// <summary>加入商品：已存在则累加数量。</summary>
    CartItem Add(Product product, int quantity = 1);

    /// <summary>按条码加入（扫码枪场景）。</summary>
    bool TryAddByBarcode(string barcode, out Product? product, out string message);

    /// <summary>增加数量；返回是否成功（受库存上限约束）。</summary>
    bool Increase(CartItem item);

    /// <summary>减少数量；数量归零时自动移除。</summary>
    void Decrease(CartItem item);

    /// <summary>移除指定行项。</summary>
    void Remove(CartItem item);

    /// <summary>清空购物车。</summary>
    void Clear();

    /// <summary>生成订单行项快照（用于下单）。</summary>
    IReadOnlyList<OrderItem> CreateOrderItems();
}
