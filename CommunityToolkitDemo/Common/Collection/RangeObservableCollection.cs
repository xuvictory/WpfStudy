using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CommunityToolkitDemo.Common.Collection;

/// <summary>
/// 支持批量替换的 <see cref="ObservableCollection{T}"/>。
/// </summary>
/// <remarks>
/// <see cref="ObservableCollection{T}"/> 本身没有 AddRange：逐条 Add 会为每个元素各发一次
/// <c>CollectionChanged</c>。当集合已绑定到列表控件时，这会表现为"逐条插入 + 逐条重排"，
/// 元素越多累计代价越高；本类型把"清空 + 批量填充"合并成一次
/// <see cref="NotifyCollectionChangedAction.Reset"/>，绑定方只重建一次视图。
/// </remarks>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>用给定元素整体替换当前内容，只发出一次集合变更通知。</summary>
    /// <remarks>
    /// 若给定元素与当前内容逐一相等（按 <see cref="EqualityComparer{T}.Default"/>），则直接返回、
    /// 不发出任何通知。搜索与切换分类可能在结果未变时重复触发（重复按键、点选同一分类），
    /// 无谓的 Reset 会让虚拟化面板把可视区内的容器全部重建一遍。
    /// </remarks>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        // 先物化再改动集合：避免清空之后才去枚举惰性序列，出现"边清边取"的意外
        var incoming = items as IList<T> ?? items.ToList();

        if (HasSameContent(incoming))
        {
            return;
        }

        CheckReentrancy();

        Items.Clear();
        foreach (var item in incoming)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    /// <summary>判断给定序列与当前内容是否逐项相等（长度与顺序都一致）。</summary>
    private bool HasSameContent(IList<T> incoming)
    {
        if (Items.Count != incoming.Count)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < Items.Count; i++)
        {
            if (!comparer.Equals(Items[i], incoming[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>追加一批元素，只发出一次集合变更通知。</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var incoming = items as IList<T> ?? items.ToList();
        if (incoming.Count == 0)
        {
            return;
        }

        CheckReentrancy();

        foreach (var item in incoming)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    /// <summary>发出 Reset 通知，并补齐绑定方依赖的 Count / Item[] 属性变更。</summary>
    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
