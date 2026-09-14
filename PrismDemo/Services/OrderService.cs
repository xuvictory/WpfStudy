using System.Collections.ObjectModel;
using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>订单服务实现。</summary>
public sealed class OrderService : IOrderService
{
    private readonly PosRepository _repository;
    private readonly ICartService _cart;

    public OrderService(PosRepository repository, ICartService cart)
    {
        _repository = repository;
        _cart = cart;
    }

    public ObservableCollection<Order> Orders => _repository.Orders;

    public Order? Checkout(PaymentMethod payment, decimal paid)
    {
        if (_cart.Items.Count == 0)
        {
            return null;
        }

        var order = new Order
        {
            OrderNo = BuildOrderNo(),
            CreatedAt = DateTime.Now,
            Payment = payment,
            Total = _cart.Total,
            Paid = paid,
            ItemCount = _cart.ItemCount,
            Items = _cart.CreateOrderItems().ToList()
        };

        // 新订单插到最前面，界面按时间倒序展示（仓储内部会裁剪超出上限的历史订单）
        _repository.AddOrder(order);
        _cart.Clear();

        return order;
    }

    /// <summary>订单号：POS + yyyyMMdd + HHmmss + 3 位随机，保证演示场景唯一。</summary>
    private static string BuildOrderNo()
        => $"POS{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(100, 1000)}";
}
