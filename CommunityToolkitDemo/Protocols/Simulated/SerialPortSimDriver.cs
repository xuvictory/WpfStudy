using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.Protocols.Simulated;

/// <summary>
/// 串口模拟驱动：一台上位机通过 COM 口连接扫码枪与小票打印机。
///
/// - 周期性回读打印机状态（自发数据）；
/// - 收到上位机的"触发扫码"指令时，才回传一条条码报文（请求-应答模型），
///   这样扫码是"受控"的，不会刷屏。
/// </summary>
public sealed class SerialPortSimDriver : ProtocolDriverBase
{
    /// <summary>触发扫码的指令载荷</summary>
    /// <remarks>取值统一由 <see cref="ProtocolKeys.ScanCommand"/> 定义，此处保留常量以兼容既有引用。</remarks>
    public const string ScanCommand = ProtocolKeys.ScanCommand;

    /// <summary>条码在 <see cref="ProtocolFrame.Values"/> 中的键名（供消息桥接器识别扫码报文）</summary>
    /// <remarks>取值统一由 <see cref="ProtocolKeys.Barcode"/> 定义，此处保留常量以兼容既有引用。</remarks>
    public const string BarcodeKey = ProtocolKeys.Barcode;

    /// <summary>只读商品目录：仅用于"随机挑一件商品"模拟扫码结果，不需要整个仓储。</summary>
    private readonly IProductCatalog _catalog;

    private int _tick;

    public SerialPortSimDriver(IDeviceCatalog devices, IProductCatalog catalog)
        : base(devices.GetDeviceOrDefault("scanner", ProtocolType.SerialPort, "扫码枪与小票打印机", "COM3"))
    {
        _catalog = catalog;
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
    {
        _tick++;

        // 每 4 个轮询周期回读一次打印机状态，其余周期保持静默
        if (_tick % 4 != 0)
        {
            return Task.FromResult<IReadOnlyList<ProtocolFrame>>(Array.Empty<ProtocolFrame>());
        }

        IReadOnlyList<ProtocolFrame> frames = new[]
        {
            new ProtocolFrame(
                Type,
                ProtocolDirection.Out,
                Device.Address,
                "1B 40 1B 61 01",
                "小票打印机初始化指令"),

            new ProtocolFrame(
                Type,
                ProtocolDirection.In,
                Device.Address,
                "06 ESC/POS READY",
                "小票打印机状态",
                new Dictionary<string, string>
                {
                    ["打印状态"] = "就绪",
                    ["纸量"] = $"{Random.Shared.Next(60, 100)} %"
                })
        };

        return Task.FromResult(frames);
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> OnSendAsync(ProtocolFrame frame, CancellationToken cancellationToken)
    {
        var responses = new List<ProtocolFrame>
        {
            new(Type, ProtocolDirection.Out, Device.Address, frame.Payload, frame.Description, frame.Values)
        };

        if (frame.Payload.Contains(ScanCommand, StringComparison.OrdinalIgnoreCase))
        {
            var product = PickRandomProduct();
            var barcode = product?.Barcode ?? "6921168509256";

            responses.Add(new ProtocolFrame(
                Type,
                ProtocolDirection.In,
                Device.Address,
                $"SCAN EAN13 {barcode}",
                "扫码枪条码",
                new Dictionary<string, string>
                {
                    [BarcodeKey] = barcode,
                    ["匹配商品"] = product?.Name ?? "未匹配到商品"
                }));
        }

        return Task.FromResult<IReadOnlyList<ProtocolFrame>>(responses);
    }

    private Product? PickRandomProduct()
    {
        var count = _catalog.ProductCount;
        return count == 0 ? null : _catalog.GetProductAt(Random.Shared.Next(count));
    }
}
