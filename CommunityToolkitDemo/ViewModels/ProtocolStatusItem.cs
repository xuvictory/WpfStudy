using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>标题栏上的协议状态指示项。</summary>
public partial class ProtocolStatusItem : ObservableObject
{
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

    /// <summary>连接状态：源生成器生成 State 属性与 StateChanged 通知</summary>
    [ObservableProperty]
    private ConnectionState _state = ConnectionState.Disconnected;
}
