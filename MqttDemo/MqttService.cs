using MQTTnet;
using MQTTnet.Protocol;          // 必须引用，MqttQualityOfServiceLevel 所在命名空间
using System.Text;
using System.Text.Json;

namespace MqttDemo
{
    public class MqttService : IDisposable
    {
        private IMqttClient _client = null!;
        private MqttClientOptions _options = null!;
        private CancellationTokenSource? _reconnectCts;

        public bool IsConnected => _client?.IsConnected == true;

        public event Action<string, string>? MessageReceived;
        public event Action<bool>? ConnectionChanged;
        public event Action<string>? LogReceived;

        public async Task ConnectAsync(string brokerIp, int port = 1883,
            string? clientId = null)
        {
            // 5.x：使用 MqttClientFactory（不再是 MqttFactory）
            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            clientId ??= $"wpf-monitor-{Guid.NewGuid():N}";

            var willPayload = JsonSerializer.SerializeToUtf8Bytes(
                new { status = "offline" });

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerIp, port)
                .WithClientId(clientId)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .WithCleanSession(false)
                .WithWillTopic($"factory/wpf/{clientId}/status")
                .WithWillPayload(willPayload)
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithWillRetain(true)
                .Build();

            // 注册消息接收
            _client.ApplicationMessageReceivedAsync += e =>
            {
                var topic = e.ApplicationMessage.Topic;

                // 5.x：使用 ConvertPayloadToString() 替代手动解构 PayloadSegment
                var payload = e.ApplicationMessage.ConvertPayloadToString();

                MessageReceived?.Invoke(topic, payload);
                return Task.CompletedTask;
            };

            // 注册断线事件（5.x 没有 ManagedClient，需要自己实现重连）
            _client.DisconnectedAsync += async e =>
            {
                ConnectionChanged?.Invoke(false);
                LogReceived?.Invoke($"连接断开：{e.Exception?.Message}，5秒后重连...");

                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    await ConnectAsync(brokerIp, port, clientId);
                    await SubscribeAsync("factory/#");
                    LogReceived?.Invoke("重连成功，订阅已恢复");
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke($"重连失败：{ex.Message}");
                }
            };

            await _client.ConnectAsync(_options);
            ConnectionChanged?.Invoke(true);
            LogReceived?.Invoke($"已连接 {brokerIp}:{port}");
        }

        public async Task SubscribeAsync(string topic, int qos = 1)
        {
            if (!IsConnected) return;

            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos))
                .Build();

            await _client.SubscribeAsync(options);
            LogReceived?.Invoke($"已订阅：{topic}");
        }

        public async Task PublishAsync(string topic, object payload, int qos = 1)
        {
            if (!IsConnected) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(JsonSerializer.Serialize(payload))
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                .Build();

            await _client.PublishAsync(message);
        }

        public void Dispose()
        {
            _client?.DisconnectAsync().Wait(3000);
            _client?.Dispose();
        }
    }
}