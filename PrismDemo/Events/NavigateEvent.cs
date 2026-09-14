using Prism.Events;

namespace PrismDemo.Events;

/// <summary>页面导航事件：允许任意 ViewModel 请求主窗口切换页面。</summary>
public sealed class NavigateEvent : PubSubEvent<string>
{
    /// <summary>页面标识常量（即 Region 导航目标名），避免魔法字符串</summary>
    public static class Pages
    {
        public const string Cashier = "cashier";
        public const string Device = "device";
        public const string Settings = "settings";
    }
}
