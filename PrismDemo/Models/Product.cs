namespace PrismDemo.Models;

/// <summary>商品分类（来自 Data/categories.md）。</summary>
public sealed class ProductCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>分类角标短文本，如"饮"。用文字替代字体图标，避免图标字体缺失。</summary>
    public string Short { get; set; } = string.Empty;
}

/// <summary>商品（来自 Data/products.md）。</summary>
public sealed class Product
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public int Stock { get; set; }
    public string Unit { get; set; } = "件";
    public string Spec { get; set; } = string.Empty;

    /// <summary>库存预警阈值</summary>
    public int WarnStock { get; set; } = 10;

    /// <summary>库存是否偏低</summary>
    public bool IsLowStock => Stock <= WarnStock;
}
