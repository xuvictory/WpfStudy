using PrismDemo.Models;
using PrismDemo.Services;

namespace PrismDemo.Protocols.Simulated;

/// <summary>
/// MQTT 模拟驱动：以"主题 + 发布/应答"的方式模拟门店与云端的心跳和订单上报。
/// </summary>
public sealed class MqttSimDriver : ProtocolDriverBase
{
    private int _sequence;

    public MqttSimDriver(IDeviceCatalog devices)
        : base(devices.GetDeviceOrDefault("cloud", ProtocolType.Mqtt, "云端订单通道", "pos/store001/order"))
    {
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
    {
        _sequence++;
        var online = Random.Shared.Next(3, 7);

        IReadOnlyList<ProtocolFrame> frames = new[]
        {
            new ProtocolFrame(
                Type,
                ProtocolDirection.Out,
                Device.Address,
                $"PUBLISH seq={_sequence} payload={{online:{online}}}",
                "上报门店心跳"),

            new ProtocolFrame(
                Type,
                ProtocolDirection.In,
                Device.Address,
                $"PUBACK seq={_sequence}",
                "云端心跳应答",
                new Dictionary<string, string>
                {
                    ["上报序号"] = _sequence.ToString(),
                    ["在线设备数"] = online.ToString()
                })
        };

        return Task.FromResult(frames);
    }

    protected override async Task<IReadOnlyList<ProtocolFrame>> OnSendAsync(ProtocolFrame frame, CancellationToken cancellationToken)
    {
        var echoed = await base.OnSendAsync(frame, cancellationToken).ConfigureAwait(false);
        _sequence++;

        var responses = new List<ProtocolFrame>(echoed)
        {
            new(
                Type,
                ProtocolDirection.In,
                Device.Address,
                $"PUBACK seq={_sequence}",
                "订单上报应答",
                new Dictionary<string, string> { ["上报序号"] = _sequence.ToString() })
        };

        return responses;
    }
}
