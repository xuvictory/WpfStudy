using Prism.Events;
using PrismDemo.Models;

namespace PrismDemo.Events;

/// <summary>
/// 设置已保存事件（设置页 → 主窗口状态栏 / 收银台 / 购物车服务）。
/// 保存后由设置服务统一发布，订阅方各自刷新，不需要互相引用。
/// </summary>
public sealed class SettingsChangedEvent : PubSubEvent<PosSettings>
{
}
