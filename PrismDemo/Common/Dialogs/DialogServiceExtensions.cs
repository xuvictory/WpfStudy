using Prism.Services.Dialogs;
using PrismDialogService = Prism.Services.Dialogs.IDialogService;

namespace PrismDemo.Common.Dialogs;

/// <summary>
/// 为 Prism 的 <see cref="PrismDialogService"/> 补充"信息 / 警告 / 错误 / 确认"四个语义化入口。
/// </summary>
/// <remarks>
/// Prism 的对话框 API 是"名字 + 参数 + 回调"三段式，调用起来比较啰嗦：
/// <code>
/// dialogs.ShowDialog("NotificationDialog", parameters, result => { ... });
/// </code>
/// 这里把它收敛成一行，同时让"哪一类提示用哪种外观"这件事只在扩展方法里决定。
/// </remarks>
public static class DialogServiceExtensions
{
    /// <summary>
    /// 通知对话框在容器中的注册名（<c>App.RegisterTypes</c> 的 <c>RegisterDialog</c> 与之对应）。
    /// </summary>
    public const string NotificationDialogName = "NotificationDialog";

    /// <summary>参数键：正文。</summary>
    public const string MessageKey = "message";

    /// <summary>参数键：标题。</summary>
    public const string TitleKey = "title";

    /// <summary>参数键：通知类型（<see cref="NotificationKind"/>）。</summary>
    public const string KindKey = "kind";

    /// <summary>普通信息提示</summary>
    public static void ShowInfo(this PrismDialogService dialogs, string message, string title = "提示")
        => Show(dialogs, message, title, NotificationKind.Info);

    /// <summary>警告提示</summary>
    public static void ShowWarning(this PrismDialogService dialogs, string message, string title = "警告")
        => Show(dialogs, message, title, NotificationKind.Warning);

    /// <summary>错误提示</summary>
    public static void ShowError(this PrismDialogService dialogs, string message, string title = "错误")
        => Show(dialogs, message, title, NotificationKind.Error);

    /// <summary>
    /// 确认对话框，返回用户是否点击"确定"。
    /// </summary>
    /// <remarks>
    /// 这里刻意保留"同步返回 bool"的调用形态（迁移前 <c>DialogWindow.Show</c> 也是同步返回），
    /// 使既有业务判断 <c>if (!_dialogs.Confirm(...)) return;</c> 不需要改写成回调金字塔。
    /// Prism 的 <c>ShowDialog</c> 会以<b>模态</b>方式显示窗口，回调在返回前完成，
    /// 因此这个包装是可靠的。
    /// </remarks>
    public static bool Confirm(this PrismDialogService dialogs, string message, string title = "确认操作")
        => Show(dialogs, message, title, NotificationKind.Confirm);

    /// <summary>按通知类型构造 Prism 对话框参数（对话框 ViewModel 从这些键读值）。</summary>
    internal static IDialogParameters CreateParameters(string message, string title, NotificationKind kind)
        => new DialogParameters
        {
            { MessageKey, message },
            { TitleKey, title },
            { KindKey, kind }
        };

    private static bool Show(PrismDialogService dialogs, string message, string title, NotificationKind kind)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var confirmed = false;
        dialogs.ShowDialog(
            NotificationDialogName,
            CreateParameters(message, title, kind),
            result => confirmed = result.Result == ButtonResult.OK);

        return confirmed;
    }
}
