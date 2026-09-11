using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Messages;

/// <summary>购物车汇总信息（消息载荷，不可变）。</summary>
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
