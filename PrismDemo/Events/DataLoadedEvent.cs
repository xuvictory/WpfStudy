using Prism.Events;

namespace PrismDemo.Events;

/// <summary>
/// 数据加载完成事件（启动 / 重新加载数据源后广播）。
/// 用于"窗口先出现 → 数据就绪后自动刷新"的异步加载模式。
/// </summary>
public sealed class DataLoadedEvent : PubSubEvent<DataLoaded>
{
}
