using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Services;

/// <summary>购物车服务实现：维护行项集合并广播变更消息。</summary>
public sealed class CartService : ICartService
{
    private readonly PosRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IMessenger _messenger;

    public CartService(PosRepository repository, ISettingsService settings, IMessenger messenger)
    {
        _repository = repository;
        _settings = settings;
        _messenger = messenger;
    }

    public ObservableCollection<CartItem> Items { get; } = new();

    public int ItemCount => Items.Sum(i => i.Quantity);

    public int KindCount => Items.Count;

    public decimal Total => Items.Sum(i => i.Subtotal);

    public CartSummary Summary => new(ItemCount, KindCount, Total);

    public CartItem Add(Product product, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(product);

        var maxQuantity = CartLimits.MaxQuantityFor(product, _settings.Current.AllowOversell);

        if (maxQuantity <= 0)
        {
            // 零库存且不允许超卖：拒绝加购。
            // 这里既不加入集合、也不改动可能已存在的行项（避免把数量清零成"空行"），
            // 直接返回一个未挂载的展示用行项。
            // 调用方应先用 CartLimits.CanAdd 判断并给出用户提示（UI 路径已在 ViewModel 中拦截）。
            return new CartItem(product, quantity: 0, maxQuantity: 0);
        }

        var item = Items.FirstOrDefault(i =>
            string.Equals(i.Product.Code, product.Code, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            item = new CartItem(product, Math.Clamp(quantity, 1, maxQuantity), maxQuantity);
            Items.Add(item);
        }
        else
        {
            item.MaxQuantity = maxQuantity;
            item.Quantity = Math.Min(item.Quantity + quantity, maxQuantity);
        }

        Publish();
        return item;
    }

    public bool TryAddByBarcode(string barcode, out Product? product, out string message)
    {
        product = _repository.FindByBarcode(barcode);

        if (product is null)
        {
            message = $"未找到条码 {barcode} 对应的商品";
            return false;
        }

        if (!CartLimits.CanAdd(product, _settings.Current.AllowOversell))
        {
            message = $"{product.Name} 当前无库存，无法加入购物车";
            return false;
        }

        Add(product);
        message = $"已加入：{product.Name}";
        return true;
    }

    public bool Increase(CartItem item)
    {
        if (item is null)
        {
            return false;
        }

        var maxQuantity = CartLimits.MaxQuantityFor(item.Product, _settings.Current.AllowOversell);

        // 顺手同步行项上限：库存或"允许超卖"设置变化后，
        // 界面绑定的"+"按钮可用状态能跟着回到正确值。
        item.MaxQuantity = maxQuantity;

        if (item.Quantity >= maxQuantity)
        {
            return false;
        }

        item.Quantity++;
        Publish();
        return true;
    }

    public void Decrease(CartItem item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Quantity <= 1)
        {
            Items.Remove(item);
        }
        else
        {
            item.Quantity--;
        }

        Publish();
    }

    public void Remove(CartItem item)
    {
        if (item is null)
        {
            return;
        }

        if (Items.Remove(item))
        {
            Publish();
        }
    }

    public void Clear()
    {
        if (Items.Count == 0)
        {
            return;
        }

        Items.Clear();
        Publish();
    }

    public IReadOnlyList<OrderItem> CreateOrderItems()
        => Items.Select(i => new OrderItem
        {
            Name = i.Name,
            Spec = i.Spec,
            Price = i.Price,
            Quantity = i.Quantity
        }).ToList();

    /// <summary>广播购物车汇总：主窗口状态栏等订阅方据此刷新。</summary>
    private void Publish() => _messenger.Send(new CartChangedMessage(Summary));
}
