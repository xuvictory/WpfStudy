using Prism.Events;

namespace PrismDemo.Events;

/// <summary>
/// 全局提示条事件（任意页面 → 主窗口底部提示条）。
/// 载荷中的 <see cref="StatusLevel"/> 决定提示条配色。
/// </summary>
public sealed class StatusNotificationEvent : PubSubEvent<StatusNotification>
{
}
