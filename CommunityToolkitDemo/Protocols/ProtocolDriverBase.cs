using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Protocols;

/// <summary>
/// 协议驱动公共基类：统一实现连接生命周期、后台仿真循环、状态与事件的转发。
///
/// 线程模型：
/// - 仿真循环跑在后台线程（<c>Task.Run</c> + <c>Task.Delay</c>），空闲时零 CPU 占用；
/// - 所有事件都在后台线程触发，订阅方（ViewModel）负责用 Dispatcher 切回 UI 线程；
/// - 断开时通过 CancellationToken 取消循环，避免资源泄漏。
/// </summary>
public abstract class ProtocolDriverBase : IProtocolDriver
{
    private readonly object _sync = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    /// <summary>
    /// 状态字段，按底层类型 <see cref="int"/> 存放。
    /// </summary>
    /// <remarks>
    /// 为什么不用 <see cref="ConnectionState"/> 直接加 volatile？
    /// <see cref="Volatile"/> 的泛型重载要求 <c>T : class</c>（枚举不满足），
    /// 而 <c>volatile</c> 关键字也不允许用于枚举字段。
    /// 因此这里以 int 存放，再配合 <see cref="Volatile"/> 读写：
    /// <see cref="State"/> 会被后台仿真循环线程写入、被 UI 线程读取，
    /// 枚举底层是 int、读写本身原子，但没有内存屏障时读线程可能长期看到旧值
    ///（JIT 可以把字段缓存在寄存器中）。
    /// </remarks>
    private int _state = (int)ConnectionState.Disconnected;
    private bool _disposed;

    protected ProtocolDriverBase(DeviceInfo device)
    {
        Device = device;
    }

    public DeviceInfo Device { get; }

    public string Name => Device.Name;

    public ProtocolType Type => Device.Protocol;

    /// <summary>当前连接状态（跨线程安全读）。</summary>
    /// <remarks>
    /// 说明：判断与写入之间没有加锁，两个线程同时切换状态时理论上可能各触发一次
    /// <see cref="StateChanged"/>。这里的订阅方都是幂等的状态同步逻辑，重复一次无害，
    /// 因此为避免在热路径上加锁而保留当前实现。
    /// </remarks>
    public ConnectionState State
    {
        get => (ConnectionState)Volatile.Read(ref _state);
        private set
        {
            if (Volatile.Read(ref _state) == (int)value)
            {
                return;
            }

            Volatile.Write(ref _state, (int)value);
            StateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<ProtocolFrame>? FrameReceived;

    public event EventHandler<ConnectionState>? StateChanged;

    public event EventHandler<string>? AlarmRaised;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (State is ConnectionState.Connected or ConnectionState.Connecting)
        {
            return;
        }

        State = ConnectionState.Connecting;

        try
        {
            await OnConnectAsync(cancellationToken).ConfigureAwait(false);
            State = ConnectionState.Connected;

            // 先用局部变量持有本次的 CTS，再交给后台任务闭包捕获。
            // 若闭包直接读取 _loopCts 字段，在"连接后立刻断开"的竞态下，
            // DisconnectAsync 已把字段置为 null，而后台任务才刚启动，
            // 读取 _loopCts.Token 会抛 NullReferenceException。捕获局部变量可彻底避免。
            var loopCts = new CancellationTokenSource();
            lock (_sync)
            {
                _loopCts?.Dispose();
                _loopCts = loopCts;
                _loopTask = Task.Run(() => RunLoopAsync(loopCts.Token), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            State = ConnectionState.Disconnected;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Faulted;
            RaiseAlarm($"连接失败：{ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        Task? loop;
        CancellationTokenSource? cts;

        lock (_sync)
        {
            cts = _loopCts;
            loop = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        cts?.Dispose();

        await OnDisconnectAsync().ConfigureAwait(false);
        State = ConnectionState.Disconnected;
    }

    public async Task SendAsync(ProtocolFrame frame, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (State is not ConnectionState.Connected)
        {
            throw new InvalidOperationException($"[{Name}] 当前未连接，无法发送报文。");
        }

        var responses = await OnSendAsync(frame, cancellationToken).ConfigureAwait(false);
        foreach (var response in responses)
        {
            FrameReceived?.Invoke(this, response);
        }
    }

    #region 子类扩展点

    /// <summary>握手动作，默认模拟 400ms 建链耗时。</summary>
    protected virtual Task OnConnectAsync(CancellationToken cancellationToken)
        => Task.Delay(400, cancellationToken);

    /// <summary>断开动作，默认无额外操作。</summary>
    protected virtual Task OnDisconnectAsync() => Task.CompletedTask;

    /// <summary>
    /// 发送动作，默认原样回显一条"发送方向"报文。
    /// 真实设备场景中这里会返回"请求 + 响应"两条报文。
    /// </summary>
    protected virtual Task<IReadOnlyList<ProtocolFrame>> OnSendAsync(ProtocolFrame frame, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ProtocolFrame>>(new[]
        {
            new ProtocolFrame(
                frame.Type,
                ProtocolDirection.Out,
                frame.Address,
                frame.Payload,
                frame.Description,
                frame.Values)
        });

    /// <summary>每次轮询产生一批仿真报文。</summary>
    protected abstract Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken);

    #endregion

    #region 内部实现

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(Device.IntervalMs, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    break;
                }

                await TickAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            State = ConnectionState.Faulted;
            RaiseAlarm($"轮询异常：{ex.Message}");
        }
    }

    private async Task TickAsync(CancellationToken token)
    {
        // ---- 模拟通信故障并自动重连，用于演示报警链路 ----
        if (Device.FaultRate > 0 && Random.Shared.NextDouble() < Device.FaultRate)
        {
            State = ConnectionState.Faulted;
            RaiseAlarm("通信超时，正在重连…");

            await Task.Delay(1400, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                State = ConnectionState.Connected;
            }

            return;
        }

        var frames = await ProduceAsync(token).ConfigureAwait(false);
        foreach (var frame in frames)
        {
            FrameReceived?.Invoke(this, frame);
        }
    }

    /// <summary>供子类主动上报报警。</summary>
    protected void RaiseAlarm(string message) => AlarmRaised?.Invoke(this, message);

    protected void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        CancellationTokenSource? cts;
        lock (_sync)
        {
            cts = _loopCts;
            _loopCts = null;
            _loopTask = null;
        }

        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已在别处释放
            }

            cts.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
