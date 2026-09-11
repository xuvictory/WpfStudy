namespace CommunityToolkitDemo.Services;

/// <summary>
/// 对话框服务：把 MessageBox 封装成可注入接口。
///
/// 好处：ViewModel 不直接依赖 WPF 静态方法（便于单元测试与替换实现），
/// 也让"提示/确认"这类 UI 行为集中在一处。
/// </summary>
public interface IDialogService
{
    /// <summary>普通信息提示</summary>
    void ShowInfo(string message, string title = "提示");

    /// <summary>警告提示</summary>
    void ShowWarning(string message, string title = "警告");

    /// <summary>错误提示</summary>
    void ShowError(string message, string title = "错误");

    /// <summary>确认对话框，返回用户是否点击"确定"</summary>
    bool Confirm(string message, string title = "确认操作");
}
