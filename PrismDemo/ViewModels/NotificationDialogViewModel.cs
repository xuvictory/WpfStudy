using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using PrismDemo.Common.Dialogs;

namespace PrismDemo.ViewModels;

/// <summary>
/// 通用对话框 ViewModel。
///
/// 覆盖的 Prism 知识点：
/// 1. <see cref="IDialogAware"/> —— 对话框 ViewModel 的约定接口：
///    <see cref="OnDialogOpened"/> 读取调用方传入的参数，<see cref="RequestClose"/> 请求关闭
///    并把 <see cref="IDialogResult"/> 回传给 <c>IDialogService.ShowDialog</c> 的回调；
/// 2. <c>IDialogParameters</c> —— 强类型参数袋，替代自定义窗口上的依赖属性；
/// 3. <c>ButtonResult</c> / <c>DialogResult</c> —— 标准化的"确定 / 取消"返回值。
/// </summary>
/// <remarks>
/// 迁移前这些信息（标题、正文、类型、是否确认）是挂在 <c>DialogWindow</c> 上的依赖属性；
/// 现在它们属于 ViewModel，窗口只负责承载外观。
/// </remarks>
public class NotificationDialogViewModel : BindableBase, IDialogAware
{
    private string _title = "提示";
    private string _message = string.Empty;
    private NotificationKind _kind = NotificationKind.Info;

    private DelegateCommand? _confirmCommand;
    private DelegateCommand? _cancelCommand;

    /// <summary>对话框标题（<see cref="IDialogAware"/> 要求只读暴露）。</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>正文消息。</summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>通知类型：决定图标字形、配色与"确定"按钮风格。</summary>
    public NotificationKind Kind
    {
        get => _kind;
        private set
        {
            if (SetProperty(ref _kind, value))
            {
                RaisePropertyChanged(nameof(IsConfirm));
            }
        }
    }

    /// <summary>是否为确认模式（需要显示"取消"按钮）。</summary>
    public bool IsConfirm => _kind == NotificationKind.Confirm;

    /// <summary>确定：返回 <see cref="ButtonResult.OK"/>。</summary>
    public DelegateCommand ConfirmCommand => _confirmCommand ??= new DelegateCommand(() => Close(ButtonResult.OK));

    /// <summary>取消 / 关闭 / 点击遮罩：返回 <see cref="ButtonResult.Cancel"/>。</summary>
    public DelegateCommand CancelCommand => _cancelCommand ??= new DelegateCommand(() => Close(ButtonResult.Cancel));

    /// <summary>
    /// 请求关闭对话框。由 Prism 的 <c>DialogService</c> 订阅：
    /// 触发后窗口关闭，回调拿到这里的 <see cref="IDialogResult"/>。
    /// </summary>
    public event Action<IDialogResult>? RequestClose;

    /// <inheritdoc />
    public bool CanCloseDialog() => true;

    /// <inheritdoc />
    public void OnDialogClosed()
    {
        // 无需清理：对话框每次都是全新的窗口与 ViewModel。
    }

    /// <inheritdoc />
    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters is null)
        {
            return;
        }

        if (parameters.ContainsKey(DialogServiceExtensions.TitleKey))
        {
            Title = parameters.GetValue<string>(DialogServiceExtensions.TitleKey);
        }

        if (parameters.ContainsKey(DialogServiceExtensions.MessageKey))
        {
            Message = parameters.GetValue<string>(DialogServiceExtensions.MessageKey);
        }

        if (parameters.ContainsKey(DialogServiceExtensions.KindKey))
        {
            Kind = parameters.GetValue<NotificationKind>(DialogServiceExtensions.KindKey);
        }
    }

    private void Close(ButtonResult result) => RequestClose?.Invoke(new DialogResult(result));
}
