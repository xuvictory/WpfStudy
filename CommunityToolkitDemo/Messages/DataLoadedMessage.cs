using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CommunityToolkitDemo.Messages;

/// <summary>
/// 数据加载完成消息（启动 / 重新加载数据源后广播）。
/// 知识点：值消息 ValueChangedMessage&lt;T&gt;，用于"窗口先出现 → 数据就绪后自动刷新"的异步加载模式。
/// </summary>
public sealed class DataLoadedMessage : ValueChangedMessage<DataLoaded>
{
    public DataLoadedMessage(DataLoaded payload) : base(payload) { }
}
