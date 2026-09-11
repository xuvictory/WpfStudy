using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>
/// 实时数据表的一行：某个协议上报的某个指标的最新值。
///
/// 同一个实例会同时出现在"设备卡片"和"实时数据表"两个视图中，
/// 因此一次属性变更能让两处同步刷新（ObservableObject 的价值）。
/// </summary>
public partial class LiveMetricItem : ObservableObject
{
    public LiveMetricItem(ProtocolType protocol, string name)
    {
        Protocol = protocol;
        Name = name;
    }

    /// <summary>来源协议</summary>
    public ProtocolType Protocol { get; }

    /// <summary>指标名，如"重量""温度""纸量"</summary>
    public string Name { get; }

    /// <summary>最新值（带单位）</summary>
    [ObservableProperty]
    private string _value = "-";

    /// <summary>最近一次更新时间</summary>
    [ObservableProperty]
    private DateTime _updatedAt;

    /// <summary>数值刚更新时为 true，用于界面高亮闪烁</summary>
    [ObservableProperty]
    private bool _isFlashing;
}
