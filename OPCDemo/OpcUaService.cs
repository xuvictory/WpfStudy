using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation;

namespace OPCDemo
{
    public class OpcUaService : IDisposable
    {
        private ApplicationConfiguration _config = null!;
        private ISession _session = null!;
        private Subscription _subscription = null!;
        private SessionReconnectHandler? _reconnectHandler;

        public bool IsConnected => _session?.Connected == true;
        public event Action<string, object>? DataChanged;
        public event Action<bool>? ConnectionChanged;
        public event Action<string>? LogReceived;

        public async Task ConnectAsync(string endpointUrl)
        {
            // 1. 构建客户端配置
            _config = new ApplicationConfiguration
            {
                ApplicationName = "WpfOpcUaClient",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 2048
                },
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 }
            };
            await _config.Validate(ApplicationType.Client);

            // 2. 发现端点并选择安全策略 (使用新版API)
            var endpointDescription = CoreClientUtils.SelectEndpoint(_config, endpointUrl, useSecurity: true);
            var endpointConfiguration = EndpointConfiguration.Create(_config);
            var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

            // 3. 创建会话
            _session = await Session.Create(
                _config, endpoint, false, "WpfOpcUaClient",
                30000, new UserIdentity(), null);

            // 4. 绑定事件 (注意方法签名使用 ISession)
            _session.KeepAlive += OnKeepAlive;
            _session.Notification += OnNotification;

            ConnectionChanged?.Invoke(true);
            LogReceived?.Invoke($"已连接 {endpointUrl}");

            // 5. 创建订阅
            CreateSubscription();
        }

        private void CreateSubscription()
        {
            _subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = 500,
                KeepAliveCount = 10,
                LifetimeCount = 30,
                MaxNotificationsPerPublish = 1000
            };

            var item = new MonitoredItem(_subscription.DefaultItem)
            {
                StartNodeId = NodeId.Parse("ns=2;s=Temperature"),
                SamplingInterval = 250,
                QueueSize = 10,
                DiscardOldest = true
            };

            _subscription.AddItem(item);
            _session.AddSubscription(_subscription);
            _subscription.Create();
        }

        // 修正后的 OnNotification 方法
        private void OnNotification(ISession session, Opc.Ua.Client.NotificationEventArgs e)
        {
            foreach (var notificationData in e.NotificationMessage.NotificationData)
            {
                // 从 ExtensionObject 的 Body 属性中直接提取 DataChangeNotification
                var dataChangeNotification = notificationData.Body as DataChangeNotification;

                if (dataChangeNotification != null)
                {
                    foreach (var monitoredItem in dataChangeNotification.MonitoredItems)
                    {
                        var value = monitoredItem.Value?.Value;
                        // 这里可以从 monitoredItem.ClientHandle 或预先映射的字典中获取节点名称
                        DataChanged?.Invoke("Temperature", value!);
                    }
                }
            }
        }

        // 修正后的 OnKeepAlive 方法
        private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsBad(e.Status))
            {
                LogReceived?.Invoke($"心跳失败：{e.Status}");
                _reconnectHandler ??= new SessionReconnectHandler();
                _reconnectHandler.BeginReconnect(session, 1000, OnReconnectComplete);
            }
        }

        private void OnReconnectComplete(object sender, EventArgs e)
        {
            if (_reconnectHandler == null) return;
            // Session 属性返回 ISession，赋值给 _session 字段没有问题
            _session = _reconnectHandler.Session;
            _reconnectHandler.Dispose();
            _reconnectHandler = null;

            ConnectionChanged?.Invoke(true);
            LogReceived?.Invoke("重连成功，订阅已恢复");

            if (_session.SubscriptionCount == 0)
                CreateSubscription();
        }

        public void Dispose()
        {
            _reconnectHandler?.Dispose();
            _subscription?.Delete(true);
            _session?.Close();
            _session?.Dispose();
        }

    }
}
