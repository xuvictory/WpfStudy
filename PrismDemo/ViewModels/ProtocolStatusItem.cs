using Prism.Mvvm;
using PrismDemo.Models;

namespace PrismDemo.ViewModels;

/// <summary>标题栏上的协议状态指示项。</summary>
public class ProtocolStatusItem : BindableBase
{
    private ConnectionState _state = ConnectionState.Disconnected;

    public ProtocolStatusItem(ProtocolType type, string shortName, string name)
    {
        Type = type;
        ShortName = shortName;
        Name = name;
    }

    public ProtocolType Type { get; }

    /// <summary>短标签，如 MB / OPC / COM</summary>
    public string ShortName { get; }

    /// <summary>完整名称</summary>
    public string Name { get; }

    /// <summary>连接状态（驱动状态变化时由主窗口刷新）</summary>
    public ConnectionState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }
}
