using Prism.Events;

namespace PrismDemo.Events;

/// <summary>设备报警事件：由协议层广播，主窗口提示条与设备监控页共同消费。</summary>
public sealed class DeviceAlarmEvent : PubSubEvent<DeviceAlarm>
{
}
