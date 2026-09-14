namespace PrismDemo.Models;

/// <summary>
/// 收银机参数配置（设置页表单）。使用不可变 record，保存时整体替换，
/// 避免多处引用到同一个可变对象而产生"改了这里、那里也变"的隐患。
/// </summary>
public sealed record PosSettings(
    string StoreName,
    string StoreAddress,
    string Phone,
    string ReceiptHeader,
    string ReceiptFooter,
    int ReceiptCopies,
    bool PrintQrCode,
    PaymentMethod DefaultPayment,
    bool ShowChangeHint,
    bool AllowOversell)
{
    /// <summary>出厂默认值</summary>
    public static PosSettings Default { get; } = new(
        StoreName: "智汇便利 · 001 门店",
        StoreAddress: "深圳市南山区科技园科苑路 15 号",
        Phone: "0755-88886666",
        ReceiptHeader: "智汇便利 欢迎光临",
        ReceiptFooter: "谢谢惠顾，欢迎再次光临！",
        ReceiptCopies: 1,
        PrintQrCode: true,
        DefaultPayment: PaymentMethod.Cash,
        ShowChangeHint: true,
        AllowOversell: false);
}
