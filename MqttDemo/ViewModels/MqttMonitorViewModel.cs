using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace MqttDemo.ViewModels
{
    public partial class DeviceStatus : ObservableObject
    {
        public string DeviceId { get; set; }
        public string Line { get; set; }
        [ObservableProperty] private double _temperature;
        [ObservableProperty] private bool _isOnline;
        [ObservableProperty] private DateTime _lastUpdate;
    }

    public partial class MqttMonitorViewModel : ObservableObject
    {
        private readonly MqttService _mqtt = new();

        [ObservableProperty] private string _status = "未连接";
        [ObservableProperty] private string _log = "";
        public ObservableCollection<DeviceStatus> Devices { get; } = new();

        public MqttMonitorViewModel()
        {
            // MQTT回调在后台线程，必须切回UI线程
            _mqtt.MessageReceived += (topic, payload) =>
                Application.Current.Dispatcher.Invoke(() =>
                    HandleMessage(topic, payload));

            _mqtt.ConnectionChanged += connected =>
                Application.Current.Dispatcher.Invoke(() =>
                    Status = connected ? "已连接" : "已断开");

            _mqtt.LogReceived += msg =>
                Application.Current.Dispatcher.Invoke(() =>
                    Log += $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                await _mqtt.ConnectAsync("192.168.1.200");
                await _mqtt.SubscribeAsync("factory/+/+/+/status", qos: 1);
                await _mqtt.SubscribeAsync("factory/+/+/+/temperature", qos: 0);
            }
            catch (Exception ex)
            {
                Log += $"[连接失败] {ex.Message}\n";
            }
        }

        private void HandleMessage(string topic, string payload)
        {
            // 主题格式：factory/{line}/{deviceId}/{dataType}
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            var line = parts[1];
            var deviceId = parts[2];
            var dataType = parts[3];

            var device = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (device == null)
            {
                device = new DeviceStatus { DeviceId = deviceId, Line = line };
                Devices.Add(device);
            }

            try
            {
                switch (dataType)
                {
                    case "temperature":
                        var tempData = JsonSerializer.Deserialize<TempPayload>(payload);
                        device.Temperature = tempData.Value;
                        device.IsOnline = true;
                        device.LastUpdate = DateTime.Now;
                        break;

                    case "status":
                        var statusData = JsonSerializer.Deserialize<StatusPayload>(payload);
                        device.IsOnline = statusData.Status == "online";
                        break;
                }
            }
            catch (JsonException ex)
            {
                Log += $"[解析失败] {topic}: {ex.Message}\n";
            }
        }
    }

    public record TempPayload(double Value);
    public record StatusPayload(string Status);
}
