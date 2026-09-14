using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PrismDemo.Common.Controls;

/// <summary>
/// 虚拟化换行面板：保持 WrapPanel 的逐行换行布局，同时只实例化可视区内的子项。
///
/// 知识点（WPF 自定义布局与滚动）：
/// - 继承 <see cref="VirtualizingPanel"/> 并实现 <see cref="IScrollInfo"/>：
///   面板自行接管滚动偏移与视口，ScrollViewer 只负责显示滚动条；
/// - 通过 <see cref="IItemContainerGenerator"/> 按索引增量生成容器，
///   离开可视区的容器由 <see cref="CleanUpItems"/> 回收，
///   可视元素数量由 O(全部项) 降到 O(可视区)，从根本上避免大数据量下的测量/排列卡顿；
/// - 固定项尺寸（<see cref="ItemWidth"/>/<see cref="ItemHeight"/>）是虚拟化能精确定位的前提。
/// </summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    #region 依赖属性

    /// <summary>每个子项占用的宽度（含外边距）。未设置时回退为实测首个容器尺寸。</summary>
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>每个子项占用的高度（含外边距）。未设置时回退为实测首个容器尺寸。</summary>
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    #endregion

    #region 字段

    private const double LineDelta = 16d;
    private const double WheelDelta = 48d;

    private Size _extent = new(0, 0);
    private Size _viewport = new(0, 0);
    private Point _offset;

    #endregion

    #region 测量 / 排布

    protected override Size MeasureOverride(Size availableSize)
    {
        // 必须在触碰生成器（StartAt / GenerateNext / Remove）之前先访问 InternalChildren：
        // 首次访问会触发 WPF 内部的 ConnectToGenerator()，其中包含一次 RemoveAll() 重置生成器映射。
        // 若在 StartAt 生成过程中才首次访问，映射会在生成中途被清空，
        // 表现为“容器无法生成/无法复用”，甚至 Remove 时抛出 NullReferenceException。
        _ = InternalChildren.Count;

        var itemsControl = ItemsControl.GetItemsOwner(this);
        int itemCount = itemsControl?.Items.Count ?? 0;

        var (itemWidth, itemHeight) = ResolveItemSize();

        double availableWidth = double.IsInfinity(availableSize.Width) ? itemWidth : availableSize.Width;
        double availableHeight = double.IsInfinity(availableSize.Height) ? 0d : availableSize.Height;

        int itemsPerRow = Math.Max(1, (int)Math.Floor(availableWidth / itemWidth));
        int rowCount = itemCount == 0 ? 0 : (int)Math.Ceiling(itemCount / (double)itemsPerRow);

        _extent = new Size(availableWidth, rowCount * itemHeight);
        _viewport = new Size(availableWidth, availableHeight);
        CoerceOffsets();

        if (itemCount == 0)
        {
            return new Size(availableWidth, availableHeight);
        }

        // 计算当前视口覆盖的行区间（多测量一行，滚动时不至于立刻出现空白）
        int firstRow = Math.Max(0, (int)Math.Floor(_offset.Y / itemHeight));
        int lastRow = Math.Min(rowCount - 1, (int)Math.Ceiling((_offset.Y + _viewport.Height) / itemHeight) - 1);
        if (lastRow < firstRow)
        {
            lastRow = firstRow;
        }

        int firstIndex = firstRow * itemsPerRow;
        int lastIndex = Math.Min(itemCount - 1, ((lastRow + 1) * itemsPerRow) - 1);

        RealizeItems(firstIndex, lastIndex, itemWidth, itemHeight);
        CleanUpItems(firstIndex, lastIndex);

        return new Size(availableWidth, availableHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        if (itemsControl is null || itemsControl.Items.Count == 0 || InternalChildren.Count == 0)
        {
            return finalSize;
        }

        var (itemWidth, itemHeight) = ResolveItemSize();
        int itemsPerRow = Math.Max(1, (int)Math.Floor(finalSize.Width / itemWidth));
        double leftPadding = Math.Max(0, (finalSize.Width - (itemsPerRow * itemWidth)) / 2);

        var generator = ItemContainerGenerator;
        if (generator is null)
        {
            return finalSize;
        }

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index < 0)
            {
                continue;
            }

            int row = index / itemsPerRow;
            int column = index % itemsPerRow;

            // 直接减去滚动偏移，避免额外的渲染变换层
            child.Arrange(new Rect(
                leftPadding + (column * itemWidth),
                (row * itemHeight) - _offset.Y,
                itemWidth,
                itemHeight));
        }

        return finalSize;
    }

    #endregion

    #region 容器生成 / 回收

    private void RealizeItems(int firstIndex, int lastIndex, double itemWidth, double itemHeight)
    {
        // 面板刚由模板实例化、尚未挂接为 ItemsControl 的 items host 时，
        // ItemContainerGenerator 会返回 null（此时不可生成容器），直接跳过，
        // 待挂接完成后的下一次布局再接续生成，避免 NullReferenceException。
        var generator = ItemContainerGenerator;
        if (generator is null)
        {
            return;
        }

        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        int childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (int itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                // 生成器可能因数据源变化提前耗尽，返回非 UIElement 时终止本次生成。
                if (generator.GenerateNext(out bool isNewlyRealized) is not UIElement child)
                {
                    break;
                }

                if (isNewlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }

                child.Measure(new Size(itemWidth, itemHeight));
            }
        }
    }

    private void CleanUpItems(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        if (generator is null)
        {
            return;
        }

        bool recycling = generator is IRecyclingItemContainerGenerator
                         && ResolveVirtualizationMode() == VirtualizationMode.Recycling;

        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index >= firstIndex && index <= lastIndex)
            {
                continue;
            }

            var position = new GeneratorPosition(i, 0);
            if (recycling)
            {
                ((IRecyclingItemContainerGenerator)generator).Recycle(position, 1);
            }
            else
            {
                generator.Remove(position, 1);
            }

            RemoveInternalChildRange(i, 1);
        }
    }

    /// <summary>集合变化时同步内部子元素，避免索引错位。</summary>
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Reset:
                // Reset（整体替换）时框架的 OnItemsChangedInternal → ResetChildren → ClearChildren
                // 已经调用过 generator.RemoveAll() 并清空了 InternalChildren，生成器的位置映射随之失效。
                // 此刻再把容器交回回收池已不可能（Remove/Recycle 的坐标不再有效），
                // 内置的 VirtualizingStackPanel 行为完全一致（见 Reset_DropsContainersLikeBuiltIn 对照测试）。
                // 因此这里只做兜底清理，保证不残留内部子元素；回收复用只在明确受支持的滚动/增量变更路径生效。
                if (InternalChildren.Count > 0)
                {
                    RemoveInternalChildRange(0, InternalChildren.Count);
                }
                break;
        }

        InvalidateMeasure();
    }

    /// <summary>
    /// 解析生效的虚拟化模式。
    ///
    /// <c>VirtualizingPanel.VirtualizationMode</c> 是附加属性且不参与属性值继承，
    /// XAML 中写在 <see cref="ItemsControl"/>（如 ListBox）上的值不会自动传到 ItemsPanel 实例上。
    /// 因此这里以面板自身显式设置为优先，未设置时回退读取 items host 的宿主控件，
    /// 否则 <c>Recycling</c> 声明会被静默忽略、回收池永远为空。
    /// </summary>
    private VirtualizationMode ResolveVirtualizationMode()
    {
        var mode = GetVirtualizationMode(this);
        if (mode != VirtualizationMode.Standard)
        {
            return mode;
        }

        var owner = ItemsControl.GetItemsOwner(this);
        return owner is null ? mode : GetVirtualizationMode(owner);
    }

    #endregion

    #region 尺寸换算

    private (double Width, double Height) ResolveItemSize()
    {
        double width = ItemWidth;
        double height = ItemHeight;

        bool needWidth = double.IsNaN(width) || width <= 0;
        bool needHeight = double.IsNaN(height) || height <= 0;

        if (needWidth || needHeight)
        {
            Size measured = InternalChildren.Count > 0
                ? MeasureFirstChild()
                : new Size(100, 100);

            if (needWidth)
            {
                width = measured.Width;
            }

            if (needHeight)
            {
                height = measured.Height;
            }
        }

        return (Math.Max(width, 1d), Math.Max(height, 1d));
    }

    private Size MeasureFirstChild()
    {
        var first = InternalChildren[0];
        first.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var desired = first.DesiredSize;
        return new Size(desired.Width > 0 ? desired.Width : 1d, desired.Height > 0 ? desired.Height : 1d);
    }

    private void CoerceOffsets()
    {
        double maxX = Math.Max(0, _extent.Width - _viewport.Width);
        double maxY = Math.Max(0, _extent.Height - _viewport.Height);

        _offset = new Point(
            Math.Max(0, Math.Min(_offset.X, maxX)),
            Math.Max(0, Math.Min(_offset.Y, maxY)));
    }

    #endregion

    #region IScrollInfo

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; }

    public double ExtentWidth => _extent.Width;

    public double ExtentHeight => _extent.Height;

    public double ViewportWidth => _viewport.Width;

    public double ViewportHeight => _viewport.Height;

    public double HorizontalOffset => _offset.X;

    public double VerticalOffset => _offset.Y;

    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(VerticalOffset - LineDelta);

    public void LineDown() => SetVerticalOffset(VerticalOffset + LineDelta);

    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - LineDelta);

    public void LineRight() => SetHorizontalOffset(HorizontalOffset + LineDelta);

    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - WheelDelta);

    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + WheelDelta);

    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - WheelDelta);

    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + WheelDelta);

    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);

    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);

    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);

    public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

    public void SetHorizontalOffset(double offset)
    {
        double maxX = Math.Max(0, _extent.Width - _viewport.Width);
        offset = Math.Max(0, Math.Min(offset, maxX));

        if (Math.Abs(offset - _offset.X) < 0.01)
        {
            return;
        }

        _offset.X = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public void SetVerticalOffset(double offset)
    {
        double maxY = Math.Max(0, _extent.Height - _viewport.Height);
        offset = Math.Max(0, Math.Min(offset, maxY));

        if (Math.Abs(offset - _offset.Y) < 0.01)
        {
            return;
        }

        _offset.Y = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is not UIElement element)
        {
            return rectangle;
        }

        // MakeVisible 常由 BringIntoView 在布局过程中被同步调用（例如点击卡片后 Button 获得焦点）。
        // 此时若视口尚未测量（高度为 0）或生成器未就绪，任何滚动计算都会退化成：
        // SetVerticalOffset → InvalidateMeasure → 再次布局 → 再次 MakeVisible 的重入链，
        // 极端情况下会反复重入导致栈溢出。这里直接跳过，等视口有效后再处理。
        var generator = ItemContainerGenerator;
        if (generator is null || _viewport.Height <= 0)
        {
            return rectangle;
        }

        int index = -1;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            if (ReferenceEquals(InternalChildren[i], element))
            {
                index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
                break;
            }
        }

        if (index < 0)
        {
            return rectangle;
        }

        var (itemWidth, itemHeight) = ResolveItemSize();
        int itemsPerRow = Math.Max(1, (int)Math.Floor(_viewport.Width / itemWidth));
        int row = index / itemsPerRow;

        double top = row * itemHeight;
        double bottom = top + itemHeight;

        if (top < _offset.Y)
        {
            SetVerticalOffset(top);
        }
        else if (bottom > _offset.Y + _viewport.Height)
        {
            SetVerticalOffset(bottom - _viewport.Height);
        }

        return new Rect(0, top, itemWidth, itemHeight);
    }

    #endregion
}
