namespace PrismDemo.Models;

/// <summary>支付方式。</summary>
public enum PaymentMethod
{
    /// <summary>现金</summary>
    Cash,

    /// <summary>扫码支付</summary>
    QrCode,

    /// <summary>银行卡</summary>
    BankCard
}

/// <summary>订单行项。</summary>
public sealed class OrderItem
{
    public string Name { get; set; } = string.Empty;
    public string Spec { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }

    public decimal Subtotal => Price * Quantity;
}

/// <summary>订单。</summary>
public sealed class Order
{
    public string OrderNo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public PaymentMethod Payment { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public int ItemCount { get; set; }
    public string Cashier { get; set; } = "001 · 前台";
    public List<OrderItem> Items { get; set; } = new();

    /// <summary>找零</summary>
    public decimal Change => Paid - Total;

    /// <summary>支付方式中文名（与 XAML 转换器共用同一份映射）</summary>
    public string PaymentText => ProtocolDisplay.PaymentMethodText(Payment);

    /// <summary>下单时间（HH:mm:ss）</summary>
    public string TimeText => CreatedAt.ToString("HH:mm:ss");
}
