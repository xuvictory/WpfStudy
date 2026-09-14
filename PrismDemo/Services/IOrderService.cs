using System.Collections.ObjectModel;
using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>
/// 订单服务：负责下单、生成订单号、写入仓储，并向订阅方暴露订单集合。
/// </summary>
public interface IOrderService
{
    /// <summary>订单集合（历史订单 + 本次运行新增），供订单列表绑定。</summary>
    ObservableCollection<Order> Orders { get; }

    /// <summary>结算：把购物车转成订单并落库；购物车为空时返回 null。</summary>
    Order? Checkout(PaymentMethod payment, decimal paid);
}
