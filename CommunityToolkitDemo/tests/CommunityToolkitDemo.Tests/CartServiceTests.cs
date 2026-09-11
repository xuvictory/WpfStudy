using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="CartService"/> 的上限与零库存行为回归。
///
/// 这是本次改造的核心业务红线：
/// 1) 不允许超卖时，零库存商品不得进入购物车；
/// 2) 允许超卖时不受库存限制；
/// 3) 行项上的 <see cref="CartItem.MaxQuantity"/> 必须与服务约束同源，
///    使界面 "+" 按钮的可用状态不会与服务判断产生分歧。
/// </summary>
public sealed class CartServiceTests
{
    private readonly PosRepository _repository = new();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly SettingsService _settings;
    private readonly CartService _cart;

    public CartServiceTests()
    {
        _settings = new SettingsService(_messenger);
        _cart = new CartService(_repository, _settings, _messenger);
    }

    private void SetAllowOversell(bool value)
        => _settings.Save(PosSettings.Default with { AllowOversell = value });

    private static Product Product(string code, int stock, decimal price = 1m, string barcode = "") => new()
    {
        Code = code,
        Name = $"商品{code}",
        Price = price,
        Stock = stock,
        Barcode = barcode
    };

    [Fact]
    public void Add_ZeroStock_WithoutOversell_DoesNotEnterCart()
    {
        var product = Product("ZERO", 0);

        var item = _cart.Add(product);

        Assert.Empty(_cart.Items);
        Assert.Equal(0, item.Quantity);
        Assert.Equal(0, item.MaxQuantity);
    }

    [Fact]
    public void Add_ZeroStock_WithOversell_IsAccepted()
    {
        SetAllowOversell(true);

        var item = _cart.Add(Product("ZERO", 0));

        Assert.Single(_cart.Items);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(CartLimits.Unlimited, item.MaxQuantity);
    }

    [Fact]
    public void Add_ClampsQuantityToStock()
    {
        var item = _cart.Add(Product("LIMITED", 3), quantity: 10);

        Assert.Equal(3, item.Quantity);
        Assert.Equal(3, item.MaxQuantity);
        Assert.True(item.IsMaxQuantity);
    }

    [Fact]
    public void Add_SetsMaxQuantityToStock_SoPlusButtonStopsAtLimit()
    {
        var item = _cart.Add(Product("S5", 5));

        Assert.Equal(5, item.MaxQuantity);
        Assert.False(item.IsMaxQuantity);

        // 反复自增直到上限：IsMaxQuantity 必须与服务约束同时到达
        while (_cart.Increase(item))
        {
        }

        Assert.Equal(5, item.Quantity);
        Assert.True(item.IsMaxQuantity);
    }

    [Fact]
    public void Increase_ReturnsFalse_WhenAtLimit()
    {
        var item = _cart.Add(Product("S1", 1));

        Assert.False(_cart.Increase(item));
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public void Increase_AfterEnablingOversell_ReopensTheLimit()
    {
        var item = _cart.Add(Product("S1", 1));

        Assert.False(_cart.Increase(item));

        SetAllowOversell(true);

        // 设置变更后服务应放开限制，并同步行项上限（界面 "+" 随之可用）
        Assert.True(_cart.Increase(item));
        Assert.Equal(2, item.Quantity);
        Assert.Equal(CartLimits.Unlimited, item.MaxQuantity);
        Assert.False(item.IsMaxQuantity);
    }

    [Fact]
    public void Add_ExistingItem_KeepsSingleRowAndRespectsLimit()
    {
        var product = Product("S3", 3);

        _cart.Add(product, quantity: 2);
        var item = _cart.Add(product, quantity: 5);

        Assert.Single(_cart.Items);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void TryAddByBarcode_UnknownBarcode_ReturnsFalse()
    {
        var ok = _cart.TryAddByBarcode("NOT-EXIST", out var product, out var message);

        Assert.False(ok);
        Assert.Null(product);
        Assert.Contains("未找到", message);
    }

    [Fact]
    public void TryAddByBarcode_ZeroStock_WithoutOversell_ReturnsFalseWithReason()
    {
        var product = Product("ZERO", 0, barcode: "6900000000001");
        _repository.Products.Add(product);

        var ok = _cart.TryAddByBarcode(product.Barcode, out var found, out var message);

        Assert.False(ok);
        Assert.Same(product, found);
        Assert.Contains("无库存", message);
        Assert.Empty(_cart.Items);
    }

    [Fact]
    public void TryAddByBarcode_ZeroStock_WithOversell_Succeeds()
    {
        SetAllowOversell(true);
        var product = Product("ZERO", 0, barcode: "6900000000002");
        _repository.Products.Add(product);

        var ok = _cart.TryAddByBarcode(product.Barcode, out _, out var message);

        Assert.True(ok);
        Assert.Single(_cart.Items);
        Assert.Contains("已加入", message);
    }

    [Fact]
    public void SummaryAndPublish_TrackCurrentState()
    {
        CartSummary? captured = null;
        _messenger.Register<CartChangedMessage>(this, (_, m) => captured = m.Value);

        var item = _cart.Add(Product("S9", 9, price: 2m), quantity: 3);

        Assert.Equal(3, _cart.ItemCount);
        Assert.Equal(1, _cart.KindCount);
        Assert.Equal(6m, _cart.Total);
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.ItemCount);

        _cart.Remove(item);

        Assert.Empty(_cart.Items);
        Assert.Equal(CartSummary.Empty.Total, _cart.Total);
    }

    [Fact]
    public void Decrease_LastUnit_RemovesRow()
    {
        var item = _cart.Add(Product("S9", 9));

        _cart.Decrease(item);

        Assert.Empty(_cart.Items);
    }
}
