using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows;

namespace SocketDemo.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        private readonly TcpCommService _comm = new();
        private readonly HeartbeatManager _heartbeat;

        [ObservableProperty] private string _temperature = "--";
        [ObservableProperty] private string _connectionStatus = "未连接";
        [ObservableProperty] private string _logText = "";

        public MonitorViewModel()
        {
            _heartbeat = new HeartbeatManager(_comm, "192.168.1.100", 502);

            // 注意：事件回调来自后台线程，必须切回UI线程
            _comm.LogReceived += msg =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Temperature = msg;
                    LogText += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
                });

            _comm.ConnectionChanged += connected =>
                Application.Current.Dispatcher.Invoke(() =>
                    ConnectionStatus = connected ? "已连接" : "已断开");
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                await _comm.ConnectAsync("192.168.1.100", 502);
                _heartbeat.Start();
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"连接失败：{ex.Message}";
            }
        }
    }
}
