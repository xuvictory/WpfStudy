using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CommunityToolkitDemo.Messages;

/// <summary>
/// 购物车变更消息（收银台 → 主窗口状态栏 / 其他页面）。
/// 知识点：值消息 ValueChangedMessage&lt;T&gt; —— 最常用的强类型消息基类。
/// </summary>
public sealed class CartChangedMessage : ValueChangedMessage<CartSummary>
{
    public CartChangedMessage(CartSummary summary) : base(summary) { }
}
