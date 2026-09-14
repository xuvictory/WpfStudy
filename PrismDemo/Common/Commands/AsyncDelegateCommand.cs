using System.Windows.Input;

namespace PrismDemo.Common.Commands;

/// <summary>
/// 异步命令：<c>Execute</c> 执行期间自动禁用自身，防止用户重复点击造成并发重入。
/// </summary>
/// <remarks>
/// 为什么需要它？
/// Prism 8 的 <c>Prism.Commands</c> 只提供同步的 <c>DelegateCommand</c> / <c>DelegateCommand&lt;T&gt;</c>
/// （<c>AsyncDelegateCommand</c> 是 Prism 9 才加入的）。迁移前本项目的异步命令由
/// CommunityToolkit 的 <c>AsyncRelayCommand</c> 承担，其关键语义有两条：
/// <list type="number">
/// <item>执行期间 <c>CanExecute</c> 返回 false —— 界面上按钮自动置灰，无法重入；</item>
/// <item>执行结束后重新查询一次 <c>CanExecute</c>，让按钮恢复可用。</item>
/// </list>
/// 本类精确复刻这两条语义，新增的 <see cref="IsRunning"/> 便于界面/日志观察执行状态。
/// </remarks>
public sealed class AsyncDelegateCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    /// <param name="execute">要执行的异步方法</param>
    /// <param name="canExecute">额外的可用性判断；与"是否正在执行"是"与"的关系</param>
    public AsyncDelegateCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>当前是否正在执行（执行期间命令不可用）</summary>
    public bool IsRunning => _isRunning;

    /// <summary>可用性变化通知，WPF 命令系统据此重新查询 <see cref="CanExecute"/></summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 手动触发可用性重新查询。
    /// 对应原 CommunityToolkit 的 <c>NotifyCanExecuteChanged()</c>。
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    /// <summary>
    /// 以 <c>async void</c> 启动异步执行。
    /// </summary>
    /// <remarks>
    /// 这里刻意<b>不</b>吞掉异常：与 <c>AsyncRelayCommand</c> 一致，
    /// 异常会沿 SynchronizationContext 回到 UI 线程，
    /// 最终由 <c>App.OnDispatcherUnhandledException</c> 统一记录并提示；
    /// 各 ViewModel 自身的 try/catch 仍是第一道防线。
    /// </remarks>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }
}
