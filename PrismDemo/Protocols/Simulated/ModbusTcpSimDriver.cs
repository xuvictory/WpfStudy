using PrismDemo.Models;
using PrismDemo.Services;

namespace PrismDemo.Protocols.Simulated;

/// <summary>
/// Modbus TCP 模拟驱动：周期性读取电子秤的保持寄存器 40001（数值 = 重量 kg × 100）。
/// 每次轮询产生"请求 + 响应"两条报文，完整还原主从问答模型。
/// </summary>
public sealed class ModbusTcpSimDriver : ProtocolDriverBase
{
    /// <summary>从站地址（模拟）</summary>
    private const byte SlaveId = 0x01;

    /// <summary>保持寄存器地址（40001 → 偏移 0）</summary>
    private const string RegisterAddress = "40001";

    public ModbusTcpSimDriver(IDeviceCatalog devices)
        : base(devices.GetDeviceOrDefault("scale", ProtocolType.ModbusTcp, "电子秤", "192.168.1.20:502"))
    {
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
    {
        // 模拟 0.20 ~ 5.00 kg 的称重值
        var weight = Math.Round(Random.Shared.Next(20, 501) / 100m, 2);
        var raw = (int)(weight * 100);
        var high = (byte)(raw >> 8);
        var low = (byte)(raw & 0xFF);
        var address = $"从站 {SlaveId:X2} · 寄存器 {RegisterAddress}";

        IReadOnlyList<ProtocolFrame> frames = new[]
        {
            new ProtocolFrame(
                Type,
                ProtocolDirection.Out,
                address,
                $"{SlaveId:X2} 03 00 00 00 01 84 0A",
                "读保持寄存器请求"),

            new ProtocolFrame(
                Type,
                ProtocolDirection.In,
                address,
                $"{SlaveId:X2} 03 02 {high:X2} {low:X2}",
                "电子秤重量",
                new Dictionary<string, string>
                {
                    ["重量"] = $"{weight:F2} kg",
                    ["寄存器 40001"] = raw.ToString()
                })
        };

        return Task.FromResult(frames);
    }
}
