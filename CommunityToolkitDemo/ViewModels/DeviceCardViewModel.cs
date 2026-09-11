using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Protocols;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>
/// 单张协议设备卡片。
///
/// 每张卡片对应一个 <see cref="IProtocolDriver"/>，只负责"展示 + 连接开关"，
/// 具体动作仍由 <see cref="IProtocolManager"/> 执行——界面与协议实现彻底解耦。
/// </summary>
public partial class DeviceCardViewModel : ObservableObject
{
    private readonly IProtocolManager _manager;

    public DeviceCardViewModel(IProtocolDriver driver, IProtocolManager manager)
    {
        Driver = driver;
        _manager = manager;
        _state = driver.State;
    }

    public IProtocolDriver Driver { get; }

    public string Name => Driver.Name;

    public ProtocolType Type => Driver.Type;

    /// <summary>连接地址：IP:Port / COMx / 节点 ID / MQTT 主题</summary>
    public string Address => Driver.Device.Address;

    public string Description => Driver.Device.Description;

    public string IntervalText => $"轮询 {Driver.Device.IntervalMs} ms";

    public string FaultRateText => $"故障率 {Driver.Device.FaultRate:P0}";

    /// <summary>连接状态（由 DeviceMonitorViewModel 在驱动状态变化时写入）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(IsFaulted))]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private ConnectionState _state;

    /// <summary>连接/断开执行中</summary>
    [ObservableProperty]
    private bool _isBusy;

    public bool IsConnected => State is ConnectionState.Connected;

    public bool IsFaulted => State is ConnectionState.Faulted;

    /// <summary>开关按钮文字</summary>
    public string ToggleText => IsConnected ? "断开" : "连接";

    /// <summary>状态中文（与 XAML 转换器共用同一份映射，避免两处文案不一致）</summary>
    public string StateText => ProtocolDisplay.ConnectionStateText(State);

    /// <summary>该协议的最新指标（与实时数据表共享同一批实例）</summary>
    public ObservableCollection<LiveMetricItem> Metrics { get; } = new();

    /// <summary>连接 / 断开开关</summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        IsBusy = true;
        try
        {
            await _manager.ToggleAsync(Driver);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
