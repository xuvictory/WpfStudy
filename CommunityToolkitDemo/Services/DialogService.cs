using System.Windows;
using CommunityToolkitDemo.Views;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// 基于自定义 <see cref="DialogWindow"/> 的对话框服务实现。
/// 统一应用主页面视觉风格，替代原生 MessageBox。
/// </summary>
public sealed class DialogService : IDialogService
{
    public void ShowInfo(string message, string title = "提示")
        => Show(message, title, DialogWindow.DialogType.Info);

    public void ShowWarning(string message, string title = "警告")
        => Show(message, title, DialogWindow.DialogType.Warning);

    public void ShowError(string message, string title = "错误")
        => Show(message, title, DialogWindow.DialogType.Error);

    public bool Confirm(string message, string title = "确认操作")
        => Show(message, title, DialogWindow.DialogType.Confirm);

    private static bool Show(string message, string title, DialogWindow.DialogType type)
    {
        var owner = Application.Current?.MainWindow;
        return DialogWindow.Show(owner, message, title, type);
    }
}
