using PrismDemo.Models;
using PrismDemo.Protocols;

namespace PrismDemo.Services;

/// <summary>结算后外设联动结果（打印小票 / 开钱箱 / 云端上报）。</summary>
/// <param name="Printed">小票是否打印成功</param>
/// <param name="CashBoxOpened">钱箱是否弹开（非现金支付恒为 false）</param>
/// <param name="Uploaded">订单是否成功上报云端</param>
public readonly record struct OrderFollowUpResult(bool Printed, bool CashBoxOpened, bool Uploaded);

/// <summary>
/// 结算外设编排：把"打印小票 / 开钱箱 / 云端上报"这段设备协调逻辑
/// 从收银台 ViewModel 中独立出来。
///
/// 为什么单独抽出来？
/// 这段逻辑只关心"设备之间如何配合"，不关心"界面如何显示"。留在 ViewModel 里会让
/// 收银台类同时承担界面状态与设备编排两种职责，也无法脱离 UI 单独验证。
/// 抽出后 ViewModel 只负责发起调用与展示结果，设备失败语义集中在
/// <see cref="BuildDeviceNote"/> 一处描述。
/// </summary>
public sealed class CheckoutCoordinator
{
    private readonly IProtocolManager _protocols;

    public CheckoutCoordinator(IProtocolManager protocols)
    {
        _protocols = protocols;
    }

    /// <summary>
    /// 结算后并行执行三个互不依赖的设备动作。
    /// </summary>
    /// <param name="order">已生成的订单</param>
    /// <param name="openCashBox">是否开钱箱（仅现金支付需要）</param>
    /// <returns>各设备的执行结果</returns>
    /// <remarks>
    /// 三件事分属不同设备、彼此无依赖，因此并行下发以缩短结算等待时间；
    /// 单个设备未连接时其对应方法返回 false（由协议管理器保证不抛异常），
    /// 因此这里不需要额外的 try/catch，失败会体现为结果里的 false。
    /// </remarks>
    public async Task<OrderFollowUpResult> CompleteAsync(Order order, bool openCashBox)
    {
        var printTask = _protocols.PrintReceiptAsync(order);
        var cashBoxTask = openCashBox ? _protocols.OpenCashBoxAsync() : Task.FromResult(false);
        var uploadTask = _protocols.PublishOrderAsync(order);

        // 用 WhenAll 的返回值取值，而不是各任务上再读 .Result：
        // 后者容易被误读为"同步阻塞"（sync-over-async）而引发无谓的改造。
        var results = await Task.WhenAll(printTask, cashBoxTask, uploadTask);

        return new OrderFollowUpResult(results[0], results[1], results[2]);
    }

    /// <summary>把外设联动结果整理成结算对话框中的设备说明文字。</summary>
    public static string BuildDeviceNote(bool displayPushed, OrderFollowUpResult result)
    {
        var lines = new List<string>
        {
            displayPushed ? "客显屏 ✓ 已显示应收金额" : "客显屏 ✗ 未连接，已跳过",
            result.Printed ? "小票打印机 ✓ 已打印" : "小票打印机 ✗ 未连接，已跳过",
            result.CashBoxOpened ? "钱箱 ✓ 已弹开" : "钱箱 — 未触发",
            result.Uploaded ? "云端 MQTT ✓ 订单已上报" : "云端 MQTT ✗ 未连接，已跳过"
        };

        return string.Join("\n", lines);
    }
}
