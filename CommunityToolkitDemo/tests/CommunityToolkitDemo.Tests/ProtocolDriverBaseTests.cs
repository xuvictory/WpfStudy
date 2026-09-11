using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Protocols;
using Xunit;

namespace CommunityToolkitDemo.Tests;

/// <summary>
/// <see cref="ProtocolDriverBase"/> 的连接状态机回归。
///
/// 这里用最小可用驱动替身（不继承任何真实模拟驱动），
/// 目的只是验证基类生命周期：状态迁移、事件、发送前置校验与释放语义。
/// </summary>
public sealed class ProtocolDriverBaseTests
{
    private sealed class TestDriver : ProtocolDriverBase
    {
        public TestDriver() : base(new DeviceInfo
        {
            Id = "test",
            Name = "测试设备",
            Protocol = ProtocolType.SerialPort,
            Address = "COM1",
            // 轮询周期设为极大值：测试期间不会真正触发 ProduceAsync，
            // 状态变化完全由被测代码显式驱动，避免用例受后台循环干扰。
            IntervalMs = 60_000,
            FaultRate = 0
        })
        {
        }

        protected override Task OnConnectAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task OnDisconnectAsync() => Task.CompletedTask;

        protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProtocolFrame>>(Array.Empty<ProtocolFrame>());
    }

    [Fact]
    public void NewDriver_IsDisconnected()
    {
        using var driver = new TestDriver();

        Assert.Equal(ConnectionState.Disconnected, driver.State);
        Assert.Equal("测试设备", driver.Name);
        Assert.Equal(ProtocolType.SerialPort, driver.Type);
    }

    [Fact]
    public async Task ConnectAsync_TransitionsThroughConnectingToConnected()
    {
        using var driver = new TestDriver();
        var states = new List<ConnectionState>();
        driver.StateChanged += (_, state) => states.Add(state);

        await driver.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, driver.State);
        Assert.Equal(new[] { ConnectionState.Connecting, ConnectionState.Connected }, states);
    }

    [Fact]
    public async Task ConnectAsync_IsIdempotent_WhenAlreadyConnected()
    {
        using var driver = new TestDriver();
        await driver.ConnectAsync();

        var raised = 0;
        driver.StateChanged += (_, _) => raised++;

        await driver.ConnectAsync();

        Assert.Equal(0, raised);
        Assert.Equal(ConnectionState.Connected, driver.State);
    }

    [Fact]
    public async Task SendAsync_BeforeConnect_Throws()
    {
        using var driver = new TestDriver();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => driver.SendAsync(new ProtocolFrame(ProtocolType.SerialPort, ProtocolDirection.Out, "COM1", "PING")));
    }

    [Fact]
    public async Task SendAsync_WhenConnected_EmitsOutboundFrame()
    {
        using var driver = new TestDriver();
        await driver.ConnectAsync();

        ProtocolFrame? received = null;
        driver.FrameReceived += (_, frame) => received = frame;

        await driver.SendAsync(new ProtocolFrame(ProtocolType.SerialPort, ProtocolDirection.Out, "COM1", "PING"));

        Assert.NotNull(received);
        Assert.Equal(ProtocolDirection.Out, received!.Direction);
        Assert.Equal("PING", received.Payload);
    }

    [Fact]
    public async Task DisconnectAsync_ReturnsToDisconnected()
    {
        using var driver = new TestDriver();
        await driver.ConnectAsync();

        await driver.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, driver.State);
    }

    [Fact]
    public async Task DisposedDriver_RejectsFurtherSends()
    {
        var driver = new TestDriver();
        await driver.ConnectAsync();
        driver.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => driver.SendAsync(new ProtocolFrame(ProtocolType.SerialPort, ProtocolDirection.Out, "COM1", "PING")));

        // Dispose 幂等：重复调用不应抛异常
        driver.Dispose();
    }
}
