using System.Collections.ObjectModel;
using Prism.Mvvm;
using PrismDemo.Common.Commands;
using PrismDemo.Models;
using PrismDemo.Protocols;

namespace PrismDemo.ViewModels;

/// <summary>
/// 单张协议设备卡片。
///
/// 每张卡片对应一个 <see cref="IProtocolDriver"/>，只负责"展示 + 连接开关"，
/// 具体动作仍由 <see cref="IProtocolManager"/> 执行——界面与协议实现彻底解耦。
/// </summary>
public class DeviceCardViewModel : BindableBase
{
    private readonly IProtocolManager _manager;
    private ConnectionState _state;
    private bool _isBusy;

    public DeviceCardViewModel(IProtocolDriver driver, IProtocolManager manager)
    {
        Driver = driver;
        _manager = manager;
        _state = driver.State;
        ToggleCommand = new AsyncDelegateCommand(ToggleAsync);
    }

    public IProtocolDriver Driver { get; }

    public string Name => Driver.Name;

    public ProtocolType Type => Driver.Type;

    /// <summary>连接地址：IP:Port / COMx / 节点 ID / MQTT 主题</summary>
    public string Address => Driver.Device.Address;

    public string Description => Driver.Device.Description;

    public string IntervalText => $"轮询 {Driver.Device.IntervalMs} ms";

    public string FaultRateText => $"故障率 {Driver.Device.FaultRate:P0}";

    /// <summary>连接 / 断开开关（异步命令，执行期间自动禁用防止重入）</summary>
    public AsyncDelegateCommand ToggleCommand { get; }

    /// <summary>
    /// 连接状态（由 DeviceMonitorViewModel 在驱动状态变化时写入）。
    /// 状态一变，卡片上的多处文案/配色都要跟着刷新。
    /// </summary>
    public ConnectionState State
    {
        get => _state;
        set
        {
            if (!SetProperty(ref _state, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsConnected));
            RaisePropertyChanged(nameof(IsFaulted));
            RaisePropertyChanged(nameof(ToggleText));
            RaisePropertyChanged(nameof(StateText));
        }
    }

    /// <summary>连接/断开执行中</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsConnected => State is ConnectionState.Connected;

    public bool IsFaulted => State is ConnectionState.Faulted;

    /// <summary>开关按钮文字</summary>
    public string ToggleText => IsConnected ? "断开" : "连接";

    /// <summary>状态中文（与 XAML 转换器共用同一份映射，避免两处文案不一致）</summary>
    public string StateText => ProtocolDisplay.ConnectionStateText(State);

    /// <summary>该协议的最新指标（与实时数据表共享同一批实例）</summary>
    public ObservableCollection<LiveMetricItem> Metrics { get; } = new();

    /// <summary>连接 / 断开开关的实际动作</summary>
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
