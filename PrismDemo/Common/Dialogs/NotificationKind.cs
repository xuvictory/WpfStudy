namespace PrismDemo.Common.Dialogs;

/// <summary>
/// 通知对话框的外观类型：决定图标字形、配色与按钮风格。
/// </summary>
/// <remarks>
/// 迁移前该类型是 <c>DialogWindow</c> 上的私有枚举；改为 Prism 的
/// <c>IDialogService</c> 后，类型需要跨"调用方 → IDialogParameters → 对话框 ViewModel"传递，
/// 因此提升为公开枚举。
/// </remarks>
public enum NotificationKind
{
    /// <summary>普通信息（蓝色圆形 "i"）</summary>
    Info,

    /// <summary>警告（琥珀色三角 "!"）</summary>
    Warning,

    /// <summary>错误（红色圆形 "×"）</summary>
    Error,

    /// <summary>确认询问（主色圆形 "?"，同时显示"取消"按钮，关闭遮罩等价于取消）</summary>
    Confirm
}
