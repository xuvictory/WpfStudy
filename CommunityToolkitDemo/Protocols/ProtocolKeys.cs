namespace CommunityToolkitDemo.Protocols;

/// <summary>
/// 协议中立常量：把"跨层引用的报文键名与指令载荷"从具体驱动里抽出来。
///
/// 为什么需要它？
/// 报文键名（如条码）与指令载荷（如触发扫码）是<b>驱动与上层之间的隐式契约</b>：
/// 驱动负责"生产"，协议管理器与报文桥接器负责"消费"。
/// 如果这些字面量定义在某个具体驱动类里，上层就会被迫依赖该驱动类型（分层倒置），
/// 而字符串常量本身也没有编译期保护 —— 一旦两处不一致，故障只在运行时才暴露。
/// 收敛到本类后，生产方与消费方引用同一常量，改动只需改一处。
/// </summary>
public static class ProtocolKeys
{
    /// <summary>条码在 <see cref="Models.ProtocolFrame.Values"/> 中的键名。</summary>
    /// <remarks>生产方：串口驱动；消费方：<c>ProtocolMessageBridge</c>（识别扫码报文）。</remarks>
    public const string Barcode = "条码";

    /// <summary>触发扫码的指令载荷。</summary>
    /// <remarks>生产方：<c>ProtocolManager.RequestBarcodeScanAsync</c>；消费方：串口驱动。</remarks>
    public const string ScanCommand = "TRIGGER_SCAN";

    /// <summary>客显屏金额指令前缀（完整指令形如 <c>AMT=12.50</c>）。</summary>
    /// <remarks>生产方：<c>ProtocolManager.PushDisplayAmountAsync</c>；消费方：Socket 驱动。</remarks>
    public const string DisplayAmountPrefix = "AMT=";

    /// <summary>打开钱箱的指令载荷。</summary>
    /// <remarks>生产方：<c>ProtocolManager.OpenCashBoxAsync</c>；消费方：Socket 驱动。</remarks>
    public const string OpenCashBox = "CASHBOX=OPEN";
}
