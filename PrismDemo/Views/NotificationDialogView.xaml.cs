using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PrismDemo.Common.Dialogs;
using PrismDemo.ViewModels;

namespace PrismDemo.Views;

/// <summary>
/// 通用对话框内容视图：承担原 <c>DialogWindow</c> 的全部外观与交互。
/// </summary>
/// <remarks>
/// 之所以把外观适配放在视图里而不是 ViewModel：图标字形、画刷、按钮样式全部来自
/// 视图资源字典，属于"视图关注点"。ViewModel 只负责语义（标题、正文、通知类型）。
/// </remarks>
public partial class NotificationDialogView : UserControl
{
    public NotificationDialogView()
    {
        InitializeComponent();

        // 外观依赖 ViewModel 的通知类型，且参数要等 OnDialogOpened 才写入，因此三重保障：
        // 1. Loaded —— 覆盖"AutoWireViewModel 在 InitializeComponent 期间就设好 DataContext，
        //    导致 DataContextChanged 早于本构造函数订阅"的时序；Loaded 必定晚于 OnDialogOpened，
        //    能拿到最终参数（否则 Info 这类"与默认值相同"的类型不会触发变更通知，取消按钮会误显示）；
        // 2. DataContextChanged —— 覆盖 DataContext 晚于 Loaded 被替换的场景；
        // 3. Kind/IsConfirm 变更 —— 覆盖运行期再次改变外观的场景。
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyStyle();

    private NotificationDialogViewModel? ViewModel => DataContext as NotificationDialogViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 先摘掉旧订阅，避免 DataContext 被替换时重复注册同一个处理器
        if (e.OldValue is INotifyPropertyChanged oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyStyle();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NotificationDialogViewModel.Kind) or nameof(NotificationDialogViewModel.IsConfirm))
        {
            ApplyStyle();
        }
    }

    /// <summary>按通知类型刷新图标与按钮外观。</summary>
    private void ApplyStyle()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var kind = viewModel.Kind;

        IconText.Text = kind switch
        {
            NotificationKind.Info => "\uE946",
            NotificationKind.Warning => "\uE7BA",
            NotificationKind.Error => "\uE783",
            NotificationKind.Confirm => "\uE897",
            _ => "\uE946"
        };

        var (foreground, background) = kind switch
        {
            NotificationKind.Info => (FindBrush("InfoBrush"), FindBrush("InfoSoftBrush")),
            NotificationKind.Warning => (FindBrush("WarningBrush"), FindBrush("WarningSoftBrush")),
            NotificationKind.Error => (FindBrush("DangerBrush"), FindBrush("DangerSoftBrush")),
            NotificationKind.Confirm => (FindBrush("PrimaryBrush"), FindBrush("PrimarySoftBrush")),
            _ => (FindBrush("InfoBrush"), FindBrush("InfoSoftBrush"))
        };

        IconText.Foreground = foreground;
        IconBorder.Background = background;

        // 仅提示场景隐藏取消按钮，并把确定按钮改为对应强调色
        CancelButton.Visibility = viewModel.IsConfirm ? Visibility.Visible : Visibility.Collapsed;

        if (viewModel.IsConfirm)
        {
            return;
        }

        // 找不到样式时保持 PrimaryButton 不变，而不是赋 null（赋 null 会让按钮退回 WPF 默认外观）
        if (kind == NotificationKind.Error && FindStyle("DangerButton") is { } dangerStyle)
        {
            ConfirmButton.Style = dangerStyle;
        }
        else if (kind == NotificationKind.Warning && FindStyle("WarningButton", "PrimaryButton") is { } warningStyle)
        {
            ConfirmButton.Style = warningStyle;
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

    /// <summary>
    /// 点击遮罩空白处等价于"取消"（仅点击遮罩层本身，点内容卡片不关闭）。
    /// </summary>
    private void OnMaskMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid)
        {
            ViewModel?.CancelCommand.Execute();
        }
    }
}
