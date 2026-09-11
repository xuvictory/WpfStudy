using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace CommunityToolkitDemo.Common;

/// <summary>
/// UI 线程调度助手。
///
/// 协议驱动的仿真数据来自后台线程，而 WPF 的可观察集合只能在 UI 线程上修改，
/// 因此所有"后台线程 → 界面"的更新都必须经过这里。
/// </summary>
public static class UiDispatcher
{
    /// <summary>当前应用的 UI Dispatcher（设计器或单元测试环境下可能为 null）。</summary>
    public static Dispatcher? Current => Application.Current?.Dispatcher;

    /// <summary>若当前已在 UI 线程则直接执行，否则投递到 UI 线程异步执行（不阻塞后台线程）。</summary>
    /// <remarks>
    /// 没有 <see cref="Application"/>（单元测试或 XAML 设计器）时不存在可用 Dispatcher，
    /// 此时<b>在当前线程同步执行</b>：非 UI 场景下仍能直接观察集合变化。
    /// 需要特别注意"此时 action 跑在调用线程上"——若将来出现"后台线程直改 UI 集合"的问题，
    /// 这里会静默放过，因此显式写一条调试日志，便于定位。
    /// </remarks>
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Current;

        if (dispatcher is null)
        {
            Debug.WriteLine("[UiDispatcher] 无 Application，action 将在当前线程同步执行（仅测试/设计器场景）。");
            action();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.InvokeAsync(action);
    }
}
