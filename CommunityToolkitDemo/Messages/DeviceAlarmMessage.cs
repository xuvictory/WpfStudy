using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CommunityToolkitDemo.Messages;

/// <summary>设备报警消息：由协议层广播，主窗口提示条与设备监控页共同消费。</summary>
public sealed class DeviceAlarmMessage : ValueChangedMessage<DeviceAlarm>
{
    public DeviceAlarmMessage(DeviceAlarm alarm) : base(alarm) { }
}
