using System.Runtime.ExceptionServices;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// 在 STA 线程上执行测试体。
/// </summary>
/// <remarks>
/// WPF 的视觉树与布局管线要求 STA 线程，xUnit 默认的测试线程是 MTA，
/// 直接创建 <c>UIElement</c> 会抛 <see cref="InvalidOperationException"/>。
/// 这里为每个测试体单独起一条 STA 线程并等待其完成，
/// 异常原样回抛给 xUnit（保留原始堆栈），避免被吞掉后只看到"测试通过"的假象。
/// </remarks>
internal static class StaTestRunner
{
    /// <summary>在 STA 线程上同步执行 <paramref name="action"/>，并把异常原样抛出。</summary>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}
