using System.Windows;
using System.Windows.Media;

namespace CommunityToolkitDemo.Views;

/// <summary>
/// 通用对话框窗口：替代系统 MessageBox，风格与主页面保持一致。
/// 支持提示 / 警告 / 错误 / 确认 四种场景。
/// </summary>
public partial class DialogWindow : Window
{
    public static readonly DependencyProperty DialogTitleProperty = DependencyProperty.Register(
        nameof(DialogTitle), typeof(string), typeof(DialogWindow), new PropertyMetadata("提示"));

    public static readonly DependencyProperty DialogMessageProperty = DependencyProperty.Register(
        nameof(DialogMessage), typeof(string), typeof(DialogWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconGlyphProperty = DependencyProperty.Register(
        nameof(IconGlyph), typeof(string), typeof(DialogWindow), new PropertyMetadata("\uE946"));

    public static readonly DependencyProperty IconForegroundProperty = DependencyProperty.Register(
        nameof(IconForeground), typeof(Brush), typeof(DialogWindow), new PropertyMetadata((Brush?)null));

    public static readonly DependencyProperty IconBackgroundProperty = DependencyProperty.Register(
        nameof(IconBackground), typeof(Brush), typeof(DialogWindow), new PropertyMetadata((Brush?)null));

    public static readonly DependencyProperty IsConfirmProperty = DependencyProperty.Register(
        nameof(IsConfirm), typeof(bool), typeof(DialogWindow), new PropertyMetadata(false));

    public DialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>对话框标题。</summary>
    public string DialogTitle
    {
        get => (string)GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    /// <summary>对话框消息正文。</summary>
    public string DialogMessage
    {
        get => (string)GetValue(DialogMessageProperty);
        set => SetValue(DialogMessageProperty, value);
    }

    /// <summary>图标 Segoe MDL2 Assets 字符。</summary>
    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    /// <summary>图标前景色。</summary>
    public Brush IconForeground
    {
        get => (Brush)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    /// <summary>图标背景色。</summary>
    public Brush IconBackground
    {
        get => (Brush)GetValue(IconBackgroundProperty);
        set => SetValue(IconBackgroundProperty, value);
    }

    /// <summary>是否为确认模式（显示取消按钮）。</summary>
    public bool IsConfirm
    {
        get => (bool)GetValue(IsConfirmProperty);
        set => SetValue(IsConfirmProperty, value);
    }

    /// <summary>用户是否点击了确定。</summary>
    public bool Result { get; private set; }

    /// <summary>
    /// 显示一个通用对话框。
    /// </summary>
    /// <param name="owner">宿主窗口；为 null 时以主窗口为宿主。</param>
    /// <param name="message">正文消息。</param>
    /// <param name="title">标题。</param>
    /// <param name="type">对话框类型。</param>
    /// <returns>确认模式下返回是否点击确定；其他模式固定返回 true。</returns>
    public static bool Show(
        Window? owner,
        string message,
        string title = "提示",
        DialogType type = DialogType.Info)
    {
        var window = new DialogWindow
        {
            DialogTitle = title,
            DialogMessage = message,
            IsConfirm = type == DialogType.Confirm,
            Owner = owner ?? Application.Current?.MainWindow
        };

        window.ApplyStyle(type);
        window.ShowDialog();
        return window.Result;
    }

    private void ApplyStyle(DialogType type)
    {
        IconGlyph = type switch
        {
            DialogType.Info => "\uE946",
            DialogType.Warning => "\uE7BA",
            DialogType.Error => "\uE783",
            DialogType.Confirm => "\uE897",
            _ => "\uE946"
        };

        (IconForeground, IconBackground) = type switch
        {
            DialogType.Info => (FindBrush("InfoBrush"), FindBrush("InfoSoftBrush")),
            DialogType.Warning => (FindBrush("WarningBrush"), FindBrush("WarningSoftBrush")),
            DialogType.Error => (FindBrush("DangerBrush"), FindBrush("DangerSoftBrush")),
            DialogType.Confirm => (FindBrush("PrimaryBrush"), FindBrush("PrimarySoftBrush")),
            _ => (FindBrush("InfoBrush"), FindBrush("InfoSoftBrush"))
        };

        // 仅提示场景隐藏取消按钮，并把确定按钮改为对应强调色
        if (!IsConfirm)
        {
            CancelButton.Visibility = Visibility.Collapsed;

            // 找不到样式时保持 PrimaryButton 不变，而不是赋 null（赋 null 会让按钮退回 WPF 默认外观）
            if (type == DialogType.Error && FindStyle("DangerButton") is { } dangerStyle)
            {
                ConfirmButton.Style = dangerStyle;
            }
            else if (type == DialogType.Warning && FindStyle("WarningButton", "PrimaryButton") is { } warningStyle)
            {
                ConfirmButton.Style = warningStyle;
            }
        }
    }

    /// <summary>按给定顺序查找第一个存在的按钮样式；全部缺失时返回 null。</summary>
    /// <remarks>
    /// 必须用 <c>TryFindResource</c>：<c>FindResource</c> 在键不存在时会直接抛
    /// <c>ResourceReferenceKeyNotFoundException</c>，而不是返回 null。
    /// 因此 <c>(Style)FindResource(a) ?? (Style)FindResource(b)</c> 这种写法里的"兜底"
    /// 永远不会生效 —— 找不到 a 时异常已经抛出，根本走不到 b。
    /// </remarks>
    private Style? FindStyle(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryFindResource(key) is Style style)
            {
                return style;
            }
        }

        return null;
    }

    private Brush FindBrush(string key)
    {
        if (TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        // 兜底：找不到资源时返回中性色
        return Brushes.Gray;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void OnMaskMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 仅点击遮罩层本身时关闭，点击内容卡片不关闭
        if (e.OriginalSource is System.Windows.Controls.Grid)
        {
            Result = false;
            Close();
        }
    }

    /// <summary>对话框类型。</summary>
    public enum DialogType
    {
        Info,
        Warning,
        Error,
        Confirm
    }
}
