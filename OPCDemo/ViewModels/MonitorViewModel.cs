using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace OPCDemo.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        private readonly OpcUaService _opc = new();

        [ObservableProperty] private string _temperature = "--";
        [ObservableProperty] private string _status = "未连接";
        [ObservableProperty] private string _log = "";

        public MonitorViewModel()
        {
            _opc.DataChanged += (name, value) =>
                Application.Current.Dispatcher.Invoke(() =>
                    Temperature = $"{value} °C");

            _opc.ConnectionChanged += connected =>
                Application.Current.Dispatcher.Invoke(() =>
                    Status = connected ? "已连接" : "已断开");

            _opc.LogReceived += msg =>
                Application.Current.Dispatcher.Invoke(() =>
                    Log += $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                await _opc.ConnectAsync("opc.tcp://192.168.1.100:4840");
            }
            catch (Exception ex)
            {
                Log += $"[连接失败] {ex.Message}\n";
            }
        }
    }
}
