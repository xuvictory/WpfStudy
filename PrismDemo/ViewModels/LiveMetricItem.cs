using Prism.Mvvm;
using PrismDemo.Models;

namespace PrismDemo.ViewModels;

/// <summary>
/// 实时数据表的一行：某个协议上报的某个指标的最新值。
///
/// 同一个实例会同时出现在"设备卡片"和"实时数据表"两个视图中，
/// 因此一次属性变更能让两处同步刷新（<see cref="BindableBase"/> 变更通知的价值）。
/// </summary>
public class LiveMetricItem : BindableBase
{
    private string _value = "-";
    private DateTime _updatedAt;
    private bool _isFlashing;

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
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>最近一次更新时间</summary>
    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    /// <summary>数值刚更新时为 true，用于界面高亮闪烁</summary>
    public bool IsFlashing
    {
        get => _isFlashing;
        set => SetProperty(ref _isFlashing, value);
    }
}
