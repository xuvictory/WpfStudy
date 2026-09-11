namespace CommunityToolkitDemo.Models;

/// <summary>
/// 外设/协议设备定义（来自 Data/devices.md）。
/// 模拟驱动的连接地址、轮询周期、故障率等参数都由这里驱动。
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>驱动标识</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>设备名称，如"电子秤"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型</summary>
    public ProtocolType Protocol { get; set; }

    /// <summary>连接地址（IP:Port / COMx / 节点 ID / MQTT 主题）</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>轮询周期（毫秒）</summary>
    public int IntervalMs { get; set; } = 1000;

    /// <summary>模拟故障概率（0~1），用于演示报警</summary>
    public double FaultRate { get; set; } = 0.03;

    /// <summary>设备说明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>推送数据的键名</summary>
    public string Metrics { get; set; } = string.Empty;
}
