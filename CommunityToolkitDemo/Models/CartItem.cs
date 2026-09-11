using CommunityToolkit.Mvvm.ComponentModel;

namespace CommunityToolkitDemo.Models;

/// <summary>
/// 购物车行项。
/// 知识点：ObservableObject + [ObservableProperty] 源生成器（字段 _quantity → 属性 Quantity），
/// 以及 [NotifyPropertyChangedFor] 属性联动（数量变化时自动通知 Subtotal）。
/// </summary>
public partial class CartItem : ObservableObject
{
    public CartItem(Product product, int quantity = 1, int maxQuantity = int.MaxValue)
    {
        Product = product;
        _quantity = quantity;
        _maxQuantity = maxQuantity;
    }

    /// <summary>关联商品</summary>
    public Product Product { get; }

    /// <summary>数量：源生成器会生成 Quantity 属性与 QuantityChanged 通知</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(IsMaxQuantity))]
    private int _quantity;

    private int _maxQuantity = int.MaxValue;

    /// <summary>
    /// 本行可加入的数量上限，由购物车服务按"是否允许超卖"写入。
    /// </summary>
    /// <remarks>
    /// 把上限放在行项上而不是每次用 <c>Product.Stock</c> 现算，是为了让界面绑定
    /// （如"+"按钮的可用状态）与服务的实际约束<b>同源</b>：
    /// 若直接用 Stock 判断，在"允许超卖"时服务其实不限量、界面却会显示已达上限，
    /// 两者行为不一致。<br/>
    /// setter 为 internal：仅同程序集的购物车服务需要修改它。
    /// </remarks>
    public int MaxQuantity
    {
        get => _maxQuantity;
        internal set
        {
            if (SetProperty(ref _maxQuantity, value))
            {
                OnPropertyChanged(nameof(IsMaxQuantity));
            }
        }
    }

    /// <summary>小计（只读派生属性）</summary>
    public decimal Subtotal => Product.Price * Quantity;

    // ---- 为方便 XAML 直接绑定而暴露的透传属性 ----
    public string Name => Product.Name;
    public string Spec => Product.Spec;
    public decimal Price => Product.Price;
    public string Unit => Product.Unit;
    public string Barcode => Product.Barcode;

    /// <summary>数量已达可加购上限（与购物车服务的约束同源，供"+"按钮绑定）。</summary>
    public bool IsMaxQuantity => Quantity >= MaxQuantity;
}
