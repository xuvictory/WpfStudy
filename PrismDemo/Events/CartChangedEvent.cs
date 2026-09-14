using Prism.Events;

namespace PrismDemo.Events;

/// <summary>
/// 购物车变更事件（收银台 → 主窗口状态栏 / 其他页面）。
/// </summary>
/// <remarks>
/// 知识点：<see cref="PubSubEvent{TPayload}"/> 的类型参数就是载荷类型，
/// 接收方无需再像消息类那样 <c>message.Value</c> 取值，回调参数即汇总快照。
/// </remarks>
public sealed class CartChangedEvent : PubSubEvent<CartSummary>
{
}
