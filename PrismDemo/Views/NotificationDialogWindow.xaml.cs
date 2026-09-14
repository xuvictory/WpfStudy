using System.Windows;
using Prism.Services.Dialogs;

namespace PrismDemo.Views;

/// <summary>
/// 对话框宿主窗口：实现 Prism 的 <see cref="IDialogWindow"/>，
/// 让 <c>IDialogService</c> 用"我们自己的外观"承载对话框，而不是 WPF 默认的系统窗口。
/// </summary>
/// <remarks>
/// 覆盖的 Prism 知识点：
/// 1. <see cref="IDialogWindow"/> —— 自定义对话框窗口只需实现该接口（本类只差一个
///    <see cref="Result"/> 属性，其余成员 <see cref="Window"/> 已经全部具备），
///    然后在 <c>RegisterTypes</c> 中注册为 <c>IDialogWindow</c> 的实现；
/// 2. <c>DialogService</c> 会把对话框视图赋给窗口的 <c>Content</c>，
///    所以窗口自身不需要也不应该在 XAML 里放内容；
/// 3. <see cref="Result"/> 是窗口关闭后回传给调用方的结果（由 Prism 写入）。
/// </remarks>
public partial class NotificationDialogWindow : Window, IDialogWindow
{
    public NotificationDialogWindow()
    {
        InitializeComponent();

        // 复刻迁移前"相对主窗口居中"的行为（Prism 也会设置 Owner，这里作兜底）。
        if (Application.Current?.MainWindow is { } owner && !ReferenceEquals(owner, this))
        {
            Owner = owner;
        }
    }

    /// <inheritdoc />
    public IDialogResult Result { get; set; } = null!;
}
