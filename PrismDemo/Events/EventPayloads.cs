using PrismDemo.Models;

namespace PrismDemo.Events;

/// <summary>购物车汇总信息（事件载荷，不可变）。</summary>
/// <param name="ItemCount">商品总件数</param>
/// <param name="KindCount">商品种类数</param>
/// <param name="Total">合计金额</param>
public sealed record CartSummary(int ItemCount, int KindCount, decimal Total)
{
    public static readonly CartSummary Empty = new(0, 0, 0m);
}

/// <summary>设备报警载荷。</summary>
/// <param name="Protocol">来源协议</param>
/// <param name="DeviceName">设备名称</param>
/// <param name="Message">报警描述</param>
/// <param name="At">发生时间</param>
public sealed record DeviceAlarm(ProtocolType Protocol, string DeviceName, string Message, DateTime At);

/// <summary>数据加载完成载荷。</summary>
/// <param name="Succeeded">是否全部加载成功（无错误）</param>
/// <param name="Errors">加载过程中的错误信息</param>
/// <param name="LoadedAt">完成时间</param>
public sealed record DataLoaded(bool Succeeded, IReadOnlyList<string> Errors, DateTime LoadedAt);

/// <summary>
/// 全局提示条载荷（任意页面 → 主窗口底部提示条）。
/// Level 决定提示条配色。
/// </summary>
/// <param name="Text">提示文本</param>
/// <param name="Level">提示级别</param>
/// <param name="At">产生时间</param>
public sealed record StatusNotification(string Text, StatusLevel Level, DateTime At)
{
    /// <summary>
    /// 便捷构造：时间取"当前时刻"。
    /// 对应迁移前 <c>StatusNotificationMessage(text, level)</c> 的写法（内部自动填充 <c>DateTime.Now</c>）。
    /// </summary>
    public StatusNotification(string text, StatusLevel level)
        : this(text, level, DateTime.Now)
    {
    }
}

/// <summary>提示条级别：决定底部提示条的指示灯与文字配色。</summary>
public enum StatusLevel
{
    Info,
    Success,
    Warning,
    Error
}
