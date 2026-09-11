using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using CommunityToolkitDemo.Common.Collection;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="CommunityToolkitDemo.Common.Controls.VirtualizingWrapPanel"/> 的容器生命周期回归。
///
/// 关注点（对应性能报告 P0-4）：
/// 1. <c>CollectionChanged(Reset)</c>（整体替换 <c>ItemsSource</c>）时，容器复用由 WPF 生成器决定：
///    框架会在面板收到通知之前执行 <c>generator.RemoveAll()</c> 并清空子元素，位置映射随之失效，
///    因此任何面板（含内置 <c>VirtualizingStackPanel</c>）都无法在此路径上复用容器；
///    面板能保证的是"容器数量有界、不随反复过滤持续增长"。
/// 2. 滚动/增量变更路径上，<c>Recycling</c> 必须真正生效，滚出可视区的容器应进入回收池被复用。
/// </summary>
public sealed class VirtualizingWrapPanelTests
{
    /// <summary>与 CashierView.xaml 同构的宿主：ListBox + 虚拟化换行面板 + Recycling。</summary>
    private const string PanelXaml = """
        <ListBox xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:controls="clr-namespace:CommunityToolkitDemo.Common.Controls;assembly=CommunityToolkitDemo"
                 Width="800" Height="400"
                 ScrollViewer.CanContentScroll="True"
                 VirtualizingPanel.IsVirtualizing="True"
                 VirtualizingPanel.VirtualizationMode="Recycling">
            <ListBox.ItemsPanel>
                <ItemsPanelTemplate>
                    <controls:VirtualizingWrapPanel ItemWidth="100" ItemHeight="50" />
                </ItemsPanelTemplate>
            </ListBox.ItemsPanel>
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding}" />
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
        """;

    private static readonly Size HostSize = new(800, 400);

    /// <summary>与 <see cref="PanelXaml"/> 等价的宿主，但使用内置 <c>VirtualizingStackPanel</c> 作为对照组。</summary>
    private const string BuiltInPanelXaml = """
        <ListBox xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 Width="800" Height="400"
                 ScrollViewer.CanContentScroll="True"
                 VirtualizingPanel.IsVirtualizing="True"
                 VirtualizingPanel.VirtualizationMode="Recycling" />
        """;

    /// <summary>
    /// 特征化测试：Reset 后容器复用数必须与内置 <c>VirtualizingStackPanel</c> 一致。
    ///
    /// WPF 生成器在 Reset 时会先执行 <c>RemoveAll()</c>（早于面板收到 <c>ItemsChanged</c>），
    /// 面板此时已拿不到有效的生成器坐标，无法把容器放回回收池。
    /// 该测试锁定"自定义面板不比框架更差"这一事实，避免日后误以为此处还能继续优化。
    /// </summary>
    [Fact]
    public void Reset_DropsContainersLikeBuiltIn()
    {
        StaTestRunner.Run(() =>
        {
            var customReuse = MeasureResetReuse(PanelXaml);
            var builtInReuse = MeasureResetReuse(BuiltInPanelXaml);

            Assert.Equal(builtInReuse, customReuse);
        });
    }

    /// <summary>执行"实现 → Reset → 再 Reset"，返回第二次 Reset 后仍被复用的容器数量。</summary>
    private static int MeasureResetReuse(string xaml)
    {
        var items = new RangeObservableCollection<int>();
        items.AddRange(Enumerable.Range(0, 5000));

        var host = (ListBox)XamlReader.Parse(xaml);
        var window = ShowInWindow(host);
        using var _ = window;

        host.ItemsSource = items;
        window.UpdateLayout();

        // 先做一次 Reset 让容器进入稳定状态，与运行时"加载完成后整体替换"一致。
        window.UpdateLayout();
        FindPanel(host)?.InvalidateMeasure();
        window.UpdateLayout();
        items.ReplaceAll(items.ToList());
        window.UpdateLayout();

        var before = RealizedContainers(host).ToHashSet(ReferenceEqualityComparer.Instance);
        Assert.NotEmpty(before);

        // 模拟"搜索/切分类"：整体替换为另一批等量数据 → 一次 Reset 通知
        items.ReplaceAll(Enumerable.Range(1000, 5000));
        window.UpdateLayout();

        var after = RealizedContainers(host).ToList();
        Assert.NotEmpty(after);

        return after.Count(container => before.Contains(container));
    }

    [Fact]
    public void Reset_KeepsRealizedContainerCountBounded()
    {
        StaTestRunner.Run(() =>
        {
            var items = new RangeObservableCollection<int>();
            items.AddRange(Enumerable.Range(0, 5000));

            var (host, window) = CreateHost(items);
            using var _ = window;

            var first = RealizedContainers(host).Count();
            Assert.True(first > 0, "可视区应至少实现一个容器");
            Assert.True(first < 5000, "虚拟化应只实现可视区内的容器");

            // 反复过滤（Reset）不应让"当前存活容器数"持续增长
            for (var round = 0; round < 10; round++)
            {
                items.ReplaceAll(Enumerable.Range(round * 1000, 5000));
                window.UpdateLayout();
            }

            Assert.Equal(first, RealizedContainers(host).Count());
        });
    }

    /// <summary>
    /// 回归：宿主上声明的 <c>VirtualizingPanel.VirtualizationMode="Recycling"</c> 必须真正生效，
    /// 否则 <c>CleanUpItems</c> 会退化成 <c>Remove</c>，滚出可视区的容器不会进入回收池，
    /// 滚回来只能重新实例化。该附加属性不会自动传到自定义面板，需要面板主动向宿主解析。
    /// </summary>
    [Fact]
    public void Scroll_ReusesContainersFromRecyclePool()
    {
        StaTestRunner.Run(() =>
        {
            var items = new RangeObservableCollection<int>();
            items.AddRange(Enumerable.Range(0, 5000));

            var (host, window) = CreateHost(items);
            using var _ = window;

            var panel = (Common.Controls.VirtualizingWrapPanel)FindPanel(host)!;

            var first = RealizedContainers(host).ToHashSet(ReferenceEqualityComparer.Instance);
            Assert.NotEmpty(first);

            panel.SetVerticalOffset(panel.ExtentHeight);
            window.UpdateLayout();
            Assert.NotEmpty(RealizedContainers(host));

            panel.SetVerticalOffset(0);
            window.UpdateLayout();

            var back = RealizedContainers(host).ToList();
            Assert.Contains(back, container => first.Contains(container));
        });
    }

    private static (ListBox Host, HostWindow Window) CreateHost(RangeObservableCollection<int> items)
    {
        var host = (ListBox)XamlReader.Parse(PanelXaml);
        var window = ShowInWindow(host);

        host.ItemsSource = items;
        window.UpdateLayout();

        // 应用的首次实现由"加载完成后整体替换集合（Reset）"驱动；本合成宿主下需要先
        // 强制一次测量，再用一次 Reset 触发实现，才能得到与运行时一致的容器生命周期。
        FindPanel(host)?.InvalidateMeasure();
        window.UpdateLayout();

        if (items.Count > 0)
        {
            items.ReplaceAll(items.ToList());
            window.UpdateLayout();
        }

        return (host, window);
    }

    private static HostWindow ShowInWindow(FrameworkElement content)
    {
        var window = new Window
        {
            Width = HostSize.Width,
            Height = HostSize.Height,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Left = -32000,
            Top = -32000,
            Content = content,
        };

        window.Show();
        window.UpdateLayout();
        return new HostWindow(window);
    }

    private static VirtualizingPanel? FindPanel(DependencyObject node)
    {
        if (node is VirtualizingPanel panel)
        {
            return panel;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            if (FindPanel(VisualTreeHelper.GetChild(node, i)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<object> RealizedContainers(ItemsControl host)
    {
        var generator = host.ItemContainerGenerator;

        for (var i = 0; i < host.Items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is { } container)
            {
                yield return container;
            }
        }
    }

    /// <summary>把离屏窗口包成可释放对象，保证测试结束一定关闭窗口、释放 HwndSource。</summary>
    private sealed class HostWindow(Window window) : IDisposable
    {
        public void UpdateLayout() => window.UpdateLayout();

        public void Dispose() => window.Close();
    }
}
