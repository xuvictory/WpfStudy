using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CommunityToolkitDemo.Messages;

/// <summary>页面导航消息：允许任意 ViewModel 请求主窗口切换页面。</summary>
public sealed class NavigateMessage : ValueChangedMessage<string>
{
    public NavigateMessage(string pageKey) : base(pageKey) { }

    /// <summary>页面标识常量，避免魔法字符串</summary>
    public static class Pages
    {
        public const string Cashier = "cashier";
        public const string Device = "device";
        public const string Settings = "settings";
    }
}
