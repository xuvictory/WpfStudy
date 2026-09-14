using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace SocketDemo
{
    public class TcpCommService
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly byte[] _buffer = new byte[4096];
        private readonly List<byte> _cache = new(); // 粘包处理缓存

        public bool IsConnected => _client?.Connected ?? false;
        public event Action<string> LogReceived;
        public event Action<bool> ConnectionChanged;

        public async Task ConnectAsync(string ip, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);
            _stream = _client.GetStream();
            ConnectionChanged?.Invoke(true);
            _ = ReceiveLoopAsync(); // 启动接收循环
        }

        private async Task ReceiveLoopAsync()
        {
            _cts = new CancellationTokenSource();
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(
                        _buffer, 0, _buffer.Length, _cts.Token);
                    if (bytesRead == 0) break; // 服务端关闭

                    _cache.AddRange(_buffer.Take(bytesRead));
                    ProcessCache(); // 从缓存中提取完整帧
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke($"接收异常：{ex.Message}");
                    break;
                }
            }
            ConnectionChanged?.Invoke(false);
        }

        private void ProcessCache()
        {
            // 简化版拆包：查找0xAA 0x55帧头，按长度字段提取完整帧
            while (_cache.Count >= 6)
            {
                int headerIdx = -1;
                for (int i = 0; i < _cache.Count - 1; i++)
                {
                    if (_cache[i] == 0xAA && _cache[i + 1] == 0x55)
                    { headerIdx = i; break; }
                }
                if (headerIdx < 0) { _cache.Clear(); return; }
                if (headerIdx > 0) _cache.RemoveRange(0, headerIdx);
                if (_cache.Count < 6) return;

                int bodyLen = (_cache[2] << 8) | _cache[3]; // 大端长度
                int totalLen = 2 + 2 + bodyLen + 2;          // 头+长度+体+校验
                if (_cache.Count < totalLen) return;          // 半包，等下次

                var frame = _cache.Take(totalLen).ToArray();
                _cache.RemoveRange(0, totalLen);
                ParseFrame(frame); // 交给业务层解析
            }
        }

        private void ParseFrame(byte[] frame)
        {
            // 根据协议解析温度值等数据
            // 示例：frame[6]和frame[7]是温度（大端short）
            short temp = (short)((frame[6] << 8) | frame[7]);
            LogReceived?.Invoke($"温度：{temp / 10.0}°C");
        }
    }
}
