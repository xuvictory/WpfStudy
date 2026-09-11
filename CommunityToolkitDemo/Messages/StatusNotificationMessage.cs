using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CommunityToolkitDemo.Messages;

/// <summary>
/// 全局提示条消息（任意页面 → 主窗口底部提示条）。
/// 使用带附加信息的消息：Level 决定提示条配色。
/// </summary>
public sealed class StatusNotificationMessage : ValueChangedMessage<StatusNotificationMessage.Payload>
{
    public StatusNotificationMessage(string text, StatusLevel level = StatusLevel.Info)
        : base(new Payload(text, level, DateTime.Now))
    {
    }

    /// <summary>提示内容与级别</summary>
    public sealed record Payload(string Text, StatusLevel Level, DateTime At);

    public enum StatusLevel
    {
        Info,
        Success,
        Warning,
        Error
    }
}
