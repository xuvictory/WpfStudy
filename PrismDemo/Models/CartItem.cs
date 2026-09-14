using Prism.Mvvm;

namespace PrismDemo.Models;

/// <summary>
/// 购物车行项。
/// 知识点：Prism 的 <see cref="BindableBase"/> + <c>SetProperty</c> 手动属性，
/// 以及"派生属性随源属性一起刷新"的通知写法（数量变化时同步通知 Subtotal / IsMaxQuantity）。
/// </summary>
public class CartItem : BindableBase
{
    private int _quantity;
    private int _maxQuantity = int.MaxValue;

    public CartItem(Product product, int quantity = 1, int maxQuantity = int.MaxValue)
    {
        Product = product;
        _quantity = quantity;
        _maxQuantity = maxQuantity;
    }

    /// <summary>关联商品</summary>
    public Product Product { get; }

    /// <summary>
    /// 数量。变更时同步通知 <see cref="Subtotal"/> 与 <see cref="IsMaxQuantity"/>。
    /// </summary>
    /// <remarks>
    /// 原实现由 <c>[ObservableProperty]</c> + 两个 <c>[NotifyPropertyChangedFor]</c> 生成；
    /// Prism 的 <c>SetProperty</c> 只负责一个属性名的通知，派生属性需要显式再抛一次。
    /// </remarks>
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (!SetProperty(ref _quantity, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(Subtotal));
            RaisePropertyChanged(nameof(IsMaxQuantity));
        }
    }

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
                RaisePropertyChanged(nameof(IsMaxQuantity));
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
