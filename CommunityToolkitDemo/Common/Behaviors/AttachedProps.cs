using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CommunityToolkitDemo.Common.Behaviors;

/// <summary>
/// 附加属性集合：以"行为"的方式给原生控件补充能力，避免引入第三方行为库。
/// 知识点：依赖属性注册、属性变更回调、事件挂载/卸载。
/// </summary>
public static class AttachedProps
{
    #region Placeholder —— 输入框占位提示

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(AttachedProps),
            new PropertyMetadata(string.Empty));

    public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);

    #endregion

    #region AutoScrollToEnd —— 日志列表自动滚动到底部

    public static readonly DependencyProperty AutoScrollToEndProperty =
        DependencyProperty.RegisterAttached(
            "AutoScrollToEnd",
            typeof(bool),
            typeof(AttachedProps),
            new PropertyMetadata(false, OnAutoScrollToEndChanged));

    public static bool GetAutoScrollToEnd(DependencyObject obj) => (bool)obj.GetValue(AutoScrollToEndProperty);

    public static void SetAutoScrollToEnd(DependencyObject obj, bool value) => obj.SetValue(AutoScrollToEndProperty, value);

    private static void OnAutoScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnAutoScrollTargetLoaded;

        if (e.NewValue is true)
        {
            // 可直接挂在 ScrollViewer 上；也可挂在 ListBox 等宿主上，
            // 待模板应用后从可视树里找到内部的 ScrollViewer（适配虚拟化列表）。
            if (element.IsLoaded)
            {
                HookScrollViewer(element);
            }
            else
            {
                element.Loaded += OnAutoScrollTargetLoaded;
            }
        }
        else
        {
            UnhookScrollViewer(element);
        }
    }

    private static void OnAutoScrollTargetLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            HookScrollViewer(element);
        }
    }

    private static void HookScrollViewer(FrameworkElement element)
    {
        if (ResolveScrollViewer(element) is { } viewer)
        {
            viewer.ScrollChanged -= OnScrollChanged;
            viewer.ScrollChanged += OnScrollChanged;
        }
    }

    private static void UnhookScrollViewer(FrameworkElement element)
    {
        if (ResolveScrollViewer(element) is { } viewer)
        {
            viewer.ScrollChanged -= OnScrollChanged;
        }
    }

    private static ScrollViewer? ResolveScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer)
        {
            return viewer;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (ResolveScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 内容高度增长（有新日志）时，自动跟随到最新一条
        if (sender is ScrollViewer viewer && e.ExtentHeightChange > 0)
        {
            viewer.ScrollToEnd();
        }
    }

    #endregion

    #region EnterKeyCommand —— 回车触发命令

    public static readonly DependencyProperty EnterKeyCommandProperty =
        DependencyProperty.RegisterAttached(
            "EnterKeyCommand",
            typeof(ICommand),
            typeof(AttachedProps),
            new PropertyMetadata(null, OnEnterKeyCommandChanged));

    public static ICommand? GetEnterKeyCommand(DependencyObject obj) => (ICommand?)obj.GetValue(EnterKeyCommandProperty);

    public static void SetEnterKeyCommand(DependencyObject obj, ICommand? value) => obj.SetValue(EnterKeyCommandProperty, value);

    private static void OnEnterKeyCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.KeyDown -= OnTextBoxKeyDown;
        if (e.NewValue is ICommand)
        {
            textBox.KeyDown += OnTextBoxKeyDown;
        }
    }

    private static void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || sender is not TextBox textBox)
        {
            return;
        }

        var command = GetEnterKeyCommand(textBox);
        if (command?.CanExecute(textBox.Text) == true)
        {
            command.Execute(textBox.Text);
        }

        e.Handled = true;
    }

    #endregion

    #region DecimalOnly —— 只允许输入数字与小数点

    public static readonly DependencyProperty DecimalOnlyProperty =
        DependencyProperty.RegisterAttached(
            "DecimalOnly",
            typeof(bool),
            typeof(AttachedProps),
            new PropertyMetadata(false, OnDecimalOnlyChanged));

    public static bool GetDecimalOnly(DependencyObject obj) => (bool)obj.GetValue(DecimalOnlyProperty);

    public static void SetDecimalOnly(DependencyObject obj, bool value) => obj.SetValue(DecimalOnlyProperty, value);

    private static void OnDecimalOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.PreviewTextInput -= OnPreviewTextInput;
        if (e.NewValue is true)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var ch in e.Text)
        {
            if (!char.IsDigit(ch) && ch != '.')
            {
                e.Handled = true;
                return;
            }
        }

        // 只允许一个小数点
        if (e.Text.Contains('.') && sender is TextBox { Text: var text } && text.Contains('.'))
        {
            e.Handled = true;
        }
    }

    #endregion

    #region FocusOnLoaded —— 页面加载后自动聚焦

    public static readonly DependencyProperty FocusOnLoadedProperty =
        DependencyProperty.RegisterAttached(
            "FocusOnLoaded",
            typeof(bool),
            typeof(AttachedProps),
            new PropertyMetadata(false, OnFocusOnLoadedChanged));

    public static bool GetFocusOnLoaded(DependencyObject obj) => (bool)obj.GetValue(FocusOnLoadedProperty);

    public static void SetFocusOnLoaded(DependencyObject obj, bool value) => obj.SetValue(FocusOnLoadedProperty, value);

    private static void OnFocusOnLoadedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded;
        if (e.NewValue is true)
        {
            element.Loaded += OnElementLoaded;
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.Focus();
            if (element is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }
    }

    #endregion
}
