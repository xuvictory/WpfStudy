using PrismDemo.Models;
using PrismDemo.Services;
using Xunit;

namespace PrismDemo.Tests;

/// <summary>
/// <see cref="CartLimits"/> 是"能否加购 / 最多加多少"规则的单一事实来源，
/// 购物车服务、收银台 ViewModel 与 "+" 按钮绑定都从这里取数，必须被重点锁死。
/// </summary>
public sealed class CartLimitsTests
{
    private static Product ProductWithStock(int stock) => new()
    {
        Code = "P",
        Name = "测试商品",
        Price = 1m,
        Stock = stock
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void MaxQuantityFor_ZeroOrNegativeStock_WithoutOversell_IsZero(int stock)
    {
        Assert.Equal(0, CartLimits.MaxQuantityFor(ProductWithStock(stock), allowOversell: false));
    }

    [Fact]
    public void MaxQuantityFor_WithoutOversell_EqualsStock()
    {
        Assert.Equal(7, CartLimits.MaxQuantityFor(ProductWithStock(7), allowOversell: false));
    }

    [Fact]
    public void MaxQuantityFor_WithOversell_IsUnlimited()
    {
        Assert.Equal(CartLimits.Unlimited, CartLimits.MaxQuantityFor(ProductWithStock(0), allowOversell: true));
    }

    [Fact]
    public void CanAdd_WithoutOversell_RejectsZeroStock()
    {
        Assert.False(CartLimits.CanAdd(ProductWithStock(0), allowOversell: false));
    }

    [Fact]
    public void CanAdd_WithOversell_AllowsZeroStock()
    {
        Assert.True(CartLimits.CanAdd(ProductWithStock(0), allowOversell: true));
    }

    [Fact]
    public void CanAdd_WithoutOversell_AllowsPositiveStock()
    {
        Assert.True(CartLimits.CanAdd(ProductWithStock(1), allowOversell: false));
    }
}
