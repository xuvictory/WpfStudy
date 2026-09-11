using System.Windows;
using System.Windows.Controls;

namespace CommunityToolkitDemo.Common.Controls;

/// <summary>
/// 缓存式内容宿主：按内容类型缓存隐式 DataTemplate 生成的 View 实例，
/// 切换页面时复用旧视觉树，避免每次导航都重建整棵 UI（商品卡片 / 日志行等）。
///
/// 知识点（WPF 内容模型与资源查找）：
/// - ContentPresenter 遇到 UIElement 内容时直接渲染，不会再套一次模板；
/// - 通过 <see cref="DataTemplateKey"/> 在资源链中查找按类型匹配的隐式 DataTemplate，
///   自行 LoadContent 并缓存，从而绕开"每次换内容就重建视图"的默认行为；
/// - 使用 <see cref="DependencyObject.SetCurrentValue"/> 回填视图，
///   不会破坏外部对 Content 的绑定。
/// </summary>
public class CachedContentControl : ContentControl
{
    /// <summary>视图模型类型 → 已实例化的视图，导航切换时复用</summary>
    private readonly Dictionary<Type, FrameworkElement> _views = new();

    protected override void OnContentChanged(object? oldContent, object? newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        // 只处理"视图模型"内容；换成缓存视图后（FrameworkElement）不再二次处理，避免递归
        if (newContent is null || newContent is FrameworkElement)
        {
            return;
        }

        var view = GetOrCreateView(newContent);
        if (view is null)
        {
            return;
        }

        // 视图是复用实例，每次换页都要把 DataContext 指回当前视图模型
        view.DataContext = newContent;

        if (!ReferenceEquals(Content, view))
        {
            SetCurrentValue(ContentProperty, view);
        }
    }

    private FrameworkElement? GetOrCreateView(object viewModel)
    {
        var type = viewModel.GetType();

        if (_views.TryGetValue(type, out var cached))
        {
            return cached;
        }

        if (FindTemplate(type)?.LoadContent() is not FrameworkElement view)
        {
            return null;
        }

        _views[type] = view;
        return view;
    }

    /// <summary>沿资源链查找按类型匹配的隐式 DataTemplate。</summary>
    private DataTemplate? FindTemplate(Type type)
    {
        var key = new DataTemplateKey(type);

        return TryFindResource(key) as DataTemplate
               ?? Application.Current?.TryFindResource(key) as DataTemplate;
    }
}
