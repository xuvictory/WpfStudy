using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.Protocols.Simulated;

/// <summary>
/// OPC UA 模拟驱动：订阅冷藏柜温湿度节点，温度超过上限时主动上报报警，
/// 用于演示"设备侧异常 → 上位机报警"的推送链路。
/// </summary>
public sealed class OpcUaSimDriver : ProtocolDriverBase
{
    /// <summary>冷藏柜温度上限（℃）</summary>
    private const decimal TemperatureLimit = 7.0m;

    public OpcUaSimDriver(IDeviceCatalog devices)
        : base(devices.GetDeviceOrDefault("env", ProtocolType.OpcUa, "冷藏柜环境传感器", "ns=2;s=Store.ColdRoom.Temp"))
    {
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
    {
        // 模拟 1.5 ~ 7.5 ℃、45% ~ 85% 的冷藏柜环境
        var temperature = Math.Round(1.5m + (decimal)Random.Shared.NextDouble() * 6.0m, 1);
        var humidity = Random.Shared.Next(45, 86);

        if (temperature > TemperatureLimit)
        {
            RaiseAlarm($"冷藏柜温度 {temperature:F1}℃ 超过上限 {TemperatureLimit:F1}℃");
        }

        IReadOnlyList<ProtocolFrame> frames = new[]
        {
            new ProtocolFrame(
                Type,
                ProtocolDirection.In,
                Device.Address,
                $"DataValue Temp={temperature:F1} Hum={humidity}",
                "环境温湿度",
                new Dictionary<string, string>
                {
                    ["温度"] = $"{temperature:F1} ℃",
                    ["湿度"] = $"{humidity} %"
                })
        };

        return Task.FromResult(frames);
    }
}
