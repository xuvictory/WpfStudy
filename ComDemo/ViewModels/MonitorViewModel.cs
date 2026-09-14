using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace ComDemo.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        private readonly SerialPortService _serial = new();
        private readonly DispatcherTimer _reconnectTimer;

        [ObservableProperty] private string _temperature = "--";
        [ObservableProperty] private string _status = "未连接";
        [ObservableProperty] private string _log = "";
        [ObservableProperty] private string _selectedPort = "COM3";

        public MonitorViewModel()
        {
            // 注意：事件来自后台线程，必须切回UI线程
            _serial.FrameReceived += frame =>
                Application.Current.Dispatcher.Invoke(() => HandleFrame(frame));

            _serial.ConnectionChanged += connected =>
                Application.Current.Dispatcher.Invoke(() =>
                    Status = connected ? "已连接" : "已断开");

            _serial.ErrorOccurred += msg =>
                Application.Current.Dispatcher.Invoke(() =>
                    Log += $"[错误] {msg}\n");

            // 每3秒检查一次，断线自动重连
            _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _reconnectTimer.Tick += (s, e) => TryReconnect();
            _reconnectTimer.Start();
        }

        [RelayCommand]
        private void OpenPort()
        {
            try
            {
                _serial.Open(SelectedPort, 9600);
                Log += $"[{DateTime.Now:HH:mm:ss}] 打开 {SelectedPort} 成功\n";
            }
            catch (Exception ex)
            {
                Log += $"[打开失败] {ex.Message}\n";
            }
        }

        private void TryReconnect()
        {
            if (_serial.IsOpen) return;

            // 动态枚举当前可用串口，防止USB拔插后端口号变化
            var ports = SerialPort.GetPortNames();
            if (ports.Contains(SelectedPort))
            {
                try
                {
                    _serial.Open(SelectedPort, 9600);
                    Log += $"[{DateTime.Now:HH:mm:ss}] 自动重连成功\n";
                }
                catch { /* 失败就等下一次 */ }
            }
        }

        private void HandleFrame(byte[] frame)
        {
            // 假设温度在 frame[6] 和 frame[7]，大端short，实际值除以10
            short raw = (short)((frame[6] << 8) | frame[7]);
            Temperature = $"{raw / 10.0:F1} °C";
            Log += $"[{DateTime.Now:HH:mm:ss}] 温度：{Temperature}\n";
        }

        [RelayCommand]
        private async Task QueryAsync()
        {
            // 示例：发送一条查询帧（实际按设备协议来）
            var cmd = new byte[] { 0xAA, 0x55, 0x00, 0x02, 0x01, 0x00, 0x00, 0x00 };
            await _serial.SendAsync(cmd);
        }
    }
}
