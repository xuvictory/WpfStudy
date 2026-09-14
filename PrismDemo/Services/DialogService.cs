using PrismDemo.Common.Dialogs;
using PrismDialogService = Prism.Services.Dialogs.IDialogService;

namespace PrismDemo.Services;

/// <summary>
/// 基于 Prism <see cref="PrismDialogService"/> 的对话框服务实现。
/// </summary>
/// <remarks>
/// 迁移前本类直接 <c>new DialogWindow()</c> 并调用 <c>ShowDialog()</c>；
/// 现在把"显示对话框"这件事交给 Prism：
/// <list type="bullet">
/// <item>对话框的宿主窗口由 <c>RegisterDialogWindow&lt;NotificationDialogWindow&gt;</c> 指定，
/// 因而外观（遮罩 + 双层阴影 + 无边框圆角）完全保留；</item>
/// <item>对话框内容由 <c>RegisterDialog&lt;NotificationDialogView&gt;</c> 注册，
/// 其 ViewModel 实现 Prism 的 <c>IDialogAware</c>，负责按参数渲染并回传结果。</item>
/// </list>
/// 本类只做一层薄封装，让业务 ViewModel 继续面向 <see cref="IDialogService"/> 编程，
/// 而不必了解 Prism 对话框的参数键与回调约定。
/// </remarks>
public sealed class DialogService : IDialogService
{
    private readonly PrismDialogService _dialogs;

    public DialogService(PrismDialogService dialogs)
        => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    /// <inheritdoc />
    public void ShowInfo(string message, string title = "提示")
        => _dialogs.ShowInfo(message, title);

    /// <inheritdoc />
    public void ShowWarning(string message, string title = "警告")
        => _dialogs.ShowWarning(message, title);

    /// <inheritdoc />
    public void ShowError(string message, string title = "错误")
        => _dialogs.ShowError(message, title);

    /// <inheritdoc />
    public bool Confirm(string message, string title = "确认操作")
        => _dialogs.Confirm(message, title);
}
