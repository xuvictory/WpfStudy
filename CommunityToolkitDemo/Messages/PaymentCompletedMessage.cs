using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Messages;

/// <summary>支付完成消息：收银台 → 主窗口状态栏 / 报表。</summary>
public sealed class PaymentCompletedMessage : ValueChangedMessage<Order>
{
    public PaymentCompletedMessage(Order order) : base(order) { }
}
