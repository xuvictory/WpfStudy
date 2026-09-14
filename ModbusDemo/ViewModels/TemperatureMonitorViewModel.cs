using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace ModbusDemo.ViewModels
{
    public partial class TemperatureMonitorViewModel : ObservableObject
    {
        private readonly ModbusTcpService _modbus = new();
        private readonly DispatcherTimer _pollTimer;
        private readonly DispatcherTimer _reconnectTimer;
        private readonly string _ip = "192.168.1.100";

        [ObservableProperty] private string _status = "未连接";
        [ObservableProperty] private string _log = "";
        public ObservableCollection<DeviceData> Devices { get; } = new();

        public TemperatureMonitorViewModel()
        {
            // 初始化3台设备
            Devices.Add(new DeviceData { SlaveId = 1, Name = "温控表-烘箱A" });
            Devices.Add(new DeviceData { SlaveId = 2, Name = "温控表-烘箱B" });
            Devices.Add(new DeviceData { SlaveId = 3, Name = "温控表-烘箱C" });

            _modbus.LogReceived += msg =>
                Application.Current.Dispatcher.Invoke(() =>
                    Log += $"[{DateTime.Now:HH:mm:ss}] {msg}\n");

            _modbus.ConnectionChanged += connected =>
                Application.Current.Dispatcher.Invoke(() =>
                    Status = connected ? "已连接" : "已断开");

            // 每500ms轮询一次所有设备
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += async (s, e) => await PollAllDevicesAsync();

            // 每3秒检查连接状态
            _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _reconnectTimer.Tick += async (s, e) => await TryReconnectAsync();
            _reconnectTimer.Start();
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                await _modbus.ConnectAsync(_ip);
                _pollTimer.Start();
            }
            catch (Exception ex)
            {
                Log += $"[连接失败] {ex.Message}\n";
            }
        }

        private async Task PollAllDevicesAsync()
        {
            foreach (var device in Devices)
            {
                try
                {
                    var regs = await _modbus.ReadHoldingRegistersAsync(
                        device.SlaveId, 0, 1);

                    // 16位整数温度，值/10
                    short raw = (short)regs[0];
                    device.Temperature = raw / 10.0;
                    device.IsOnline = true;
                    device.LastUpdate = DateTime.Now;
                }
                catch
                {
                    device.IsOnline = false;
                }
            }
        }

        private async Task TryReconnectAsync()
        {
            if (_modbus.IsConnected) return;
            try
            {
                await _modbus.ConnectAsync(_ip);
                Log += $"[{DateTime.Now:HH:mm:ss}] 自动重连成功\n";
                _pollTimer.Start();
            }
            catch { /* 下次再试 */ }
        }
    }

    public partial class DeviceData : ObservableObject
    {
        public byte SlaveId { get; set; }
        public string Name { get; set; }
        [ObservableProperty] private double _temperature;
        [ObservableProperty] private bool _isOnline;
        [ObservableProperty] private DateTime _lastUpdate;
    }
}
