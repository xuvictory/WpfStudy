using Prism;
using Prism.Events;
using Prism.Mvvm;

namespace PrismDemo.Common.Mvvm;

/// <summary>
/// 所有 ViewModel 的基类：Prism 事件总线的接入点 + 订阅生命周期管理 + 激活状态。
/// </summary>
/// <remarks>
/// 迁移前这些能力由 CommunityToolkit 的 <c>ObservableRecipient</c>（含 <c>IMessenger</c>、
/// <c>IsActive</c>、<c>OnActivated/OnDeactivated</c>）与 <c>IRecipient&lt;T&gt;</c> 提供。
/// Prism 把这些拆成了不同的东西：
/// <list type="bullet">
/// <item><c>IEventAggregator</c> 由容器注入（Prism 默认注册了全局单例），而不是静态的
/// <c>Messenger.Default</c>；</item>
/// <item><c>IsActive</c> 由 <see cref="IActiveAware"/> 表达，区域导航时可被自动设置；</item>
/// <item><c>IRecipient&lt;T&gt;</c> 没有等价物，改用 <c>GetEvent&lt;T&gt;().Subscribe(...)</c> 并持有
/// <see cref="SubscriptionToken"/>。</item>
/// </list>
/// <para>
/// <b>为什么一定要保存 SubscriptionToken？</b>
/// <c>PubSubEvent.Subscribe</c> 默认使用弱引用（<c>keepSubscriberReferenceAlive: false</c>）：
/// 若用匿名 lambda 订阅，闭包对象没有任何强引用，可能在下一次 GC 时被回收，
/// 表现为"事件偶尔收不到"这种极难定位的问题。本基类统一改用
/// <c>keepSubscriberReferenceAlive: true</c> + 显式 <see cref="UnsubscribeAll"/>，
/// 把订阅的存续期交给代码控制，而不是交给 GC。
/// </para>
/// </remarks>
public abstract class ViewModelBase : BindableBase, IActiveAware, IDisposable
{
    /// <summary>随 ViewModel 生命周期存续的订阅</summary>
    private readonly List<SubscriptionToken> _subscriptions = [];

    /// <summary>仅在激活期间有效的订阅（离开页面即退订，回来重新订阅）</summary>
    private readonly List<SubscriptionToken> _activationSubscriptions = [];

    private bool _isActive;
    private bool _disposed;

    /// <param name="eventAggregator">Prism 事件聚合器（由容器注入）</param>
    protected ViewModelBase(IEventAggregator eventAggregator)
    {
        EventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
    }

    /// <summary>Prism 事件聚合器：跨 ViewModel / 服务通信的唯一通道。</summary>
    protected IEventAggregator EventAggregator { get; }

    /// <summary>
    /// 是否处于激活状态（<see cref="IActiveAware"/>）。
    /// 变更时会依次触发 <see cref="OnActivated"/> / <see cref="OnDeactivated"/> 与
    /// <see cref="IsActiveChanged"/>，语义与迁移前的 <c>ObservableRecipient.IsActive</c> 一致。
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (!SetProperty(ref _isActive, value))
            {
                return;
            }

            if (value)
            {
                OnActivated();
            }
            else
            {
                OnDeactivated();
            }

            IsActiveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public event EventHandler? IsActiveChanged;

    /// <summary>进入激活状态时调用（默认无操作）。子类可在此建立"仅在页面可见时才有意义"的订阅。</summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>
    /// 离开激活状态时调用。默认会退订所有通过
    /// <see cref="SubscribeWhileActive{TEvent,TPayload}"/> 建立的订阅。
    /// <b>子类重写时请务必调用 <c>base.OnDeactivated()</c></b>，否则激活期订阅会泄漏。
    /// </summary>
    protected virtual void OnDeactivated() => UnsubscribeWhileActive();

    /// <summary>
    /// 订阅事件，并把订阅凭据交给基类托管（<see cref="UnsubscribeAll"/> 会统一退订）。
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="TPayload">事件载荷类型</typeparam>
    /// <param name="handler">处理载荷的方法组（不建议传 lambda，见类注释）</param>
    protected SubscriptionToken Subscribe<TEvent, TPayload>(Action<TPayload> handler)
        where TEvent : PubSubEvent<TPayload>, new()
    {
        ArgumentNullException.ThrowIfNull(handler);

        var token = EventAggregator.GetEvent<TEvent>()
            .Subscribe(handler, keepSubscriberReferenceAlive: true);
        _subscriptions.Add(token);
        return token;
    }

    /// <summary>
    /// 建立一个"仅在激活期间有效"的订阅：<see cref="OnDeactivated"/> 时会自动退订，
    /// 下次 <see cref="OnActivated"/> 需由子类重新调用本方法订阅。
    /// </summary>
    /// <remarks>
    /// 适用于设备监控页的高频报文/报警这类<b>只在页面可见时才有意义</b>的事件：
    /// 离开页面即停止回调，避免后台持续驱动界面刷新。
    /// </remarks>
    protected SubscriptionToken SubscribeWhileActive<TEvent, TPayload>(Action<TPayload> handler)
        where TEvent : PubSubEvent<TPayload>, new()
    {
        ArgumentNullException.ThrowIfNull(handler);

        var token = EventAggregator.GetEvent<TEvent>()
            .Subscribe(handler, keepSubscriberReferenceAlive: true);
        _activationSubscriptions.Add(token);
        return token;
    }

    /// <summary>发布事件（对应迁移前的 <c>IMessenger.Send</c>）。</summary>
    protected void Publish<TEvent, TPayload>(TPayload payload)
        where TEvent : PubSubEvent<TPayload>, new()
        => EventAggregator.GetEvent<TEvent>().Publish(payload);

    /// <summary>退订全部"仅激活期间有效"的订阅（幂等）。</summary>
    protected void UnsubscribeWhileActive()
    {
        foreach (var token in _activationSubscriptions)
        {
            token.Dispose();
        }

        _activationSubscriptions.Clear();
    }

    /// <summary>退订本 ViewModel 建立的全部订阅（含激活期订阅）。</summary>
    protected void UnsubscribeAll()
    {
        foreach (var token in _subscriptions)
        {
            token.Dispose();
        }

        _subscriptions.Clear();
        UnsubscribeWhileActive();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的扩展点。默认只退订事件；子类可重写并调用 <c>base.DisposeCore()</c>
    /// 以同时清理自身持有的定时器 / 原生事件订阅。
    /// </summary>
    protected virtual void DisposeCore() => UnsubscribeAll();
}
