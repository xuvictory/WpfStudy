using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Messages;

/// <summary>
/// 设置已保存消息（设置页 → 主窗口状态栏 / 收银台 / 购物车服务）。
/// 保存后由设置服务统一广播，订阅方各自刷新，不需要互相引用。
/// </summary>
public sealed class SettingsChangedMessage : ValueChangedMessage<PosSettings>
{
    public SettingsChangedMessage(PosSettings settings) : base(settings) { }
}
