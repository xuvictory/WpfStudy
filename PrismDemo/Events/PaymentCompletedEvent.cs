using Prism.Events;
using PrismDemo.Models;

namespace PrismDemo.Events;

/// <summary>支付完成事件：收银台 → 主窗口状态栏 / 报表。</summary>
public sealed class PaymentCompletedEvent : PubSubEvent<Order>
{
}
