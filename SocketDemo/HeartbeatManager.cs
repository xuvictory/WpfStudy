using System;
using System.Collections.Generic;
using System.Text;

namespace SocketDemo
{
    public class HeartbeatManager
    {
        private readonly TcpCommService _comm;
        private readonly System.Timers.Timer _heartbeatTimer;
        private readonly string _ip;
        private readonly int _port;
        private int _reconnectAttempts;
        private const int MaxReconnect = 10;

        public HeartbeatManager(TcpCommService comm, string ip, int port)
        {
            _comm = comm;
            _ip = ip;
            _port = port;
            _heartbeatTimer = new System.Timers.Timer(5000); // 5秒心跳
            _heartbeatTimer.Elapsed += async (s, e) => await CheckAndReconnectAsync();
        }

        public void Start() => _heartbeatTimer.Start();

        private async Task CheckAndReconnectAsync()
        {
            if (_comm.IsConnected) return;

            if (_reconnectAttempts < MaxReconnect)
            {
                _reconnectAttempts++;
                try
                {
                    await _comm.ConnectAsync(_ip, _port);
                    _reconnectAttempts = 0;
                }
                catch { /* 记录日志，等下次重试 */ }
            }
        }
    }
}
