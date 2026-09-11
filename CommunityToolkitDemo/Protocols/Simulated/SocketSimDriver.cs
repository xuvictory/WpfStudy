using System.Globalization;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.Protocols.Simulated;

/// <summary>
/// Socket 模拟驱动：与客显屏保持 TCP 长连接。
///
/// - 收银台金额变化时通过 <c>AMT=xx.xx</c> 指令推送应收金额；
/// - 支付完成时通过 <c>CASHBOX=OPEN</c> 指令开钱箱；
/// - 周期性回传当前显示金额与钱箱状态。
/// </summary>
public sealed class SocketSimDriver : ProtocolDriverBase
{
    private decimal _displayAmount;
    private bool _cashBoxOpen;

    public SocketSimDriver(IDeviceCatalog devices)
        : base(devices.GetDeviceOrDefault("customer", ProtocolType.Socket, "客显屏", "127.0.0.1:8899"))
    {
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
    {
        var state = _cashBoxOpen ? "OPEN" : "CLOSED";

        IReadOnlyList<ProtocolFrame> frames = new[]
        {
            new ProtocolFrame(
                Type,
                ProtocolDirection.In,
                Device.Address,
                $"STATUS AMT={_displayAmount.ToString("F2", CultureInfo.InvariantCulture)} CASHBOX={state}",
                "客显屏回传",
                new Dictionary<string, string>
                {
                    ["显示金额"] = $"¥{_displayAmount:F2}",
                    ["钱箱"] = _cashBoxOpen ? "打开" : "关闭"
                })
        };

        // 钱箱打开状态只回传一次，随后自动回到关闭
        _cashBoxOpen = false;

        return Task.FromResult(frames);
    }

    protected override Task<IReadOnlyList<ProtocolFrame>> OnSendAsync(ProtocolFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Payload.StartsWith(ProtocolKeys.DisplayAmountPrefix, StringComparison.OrdinalIgnoreCase) &&
            decimal.TryParse(
                frame.Payload[ProtocolKeys.DisplayAmountPrefix.Length..],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            _displayAmount = amount;
        }
        else if (frame.Payload.StartsWith(ProtocolKeys.OpenCashBox, StringComparison.OrdinalIgnoreCase))
        {
            _cashBoxOpen = true;
        }

        return base.OnSendAsync(frame, cancellationToken);
    }
}
