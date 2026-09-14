using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace ComDemo
{
    public class SerialPortService : IDisposable
    {
        private SerialPort _port;
        private CancellationTokenSource _cts;
        private readonly List<byte> _cache = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public event Action<byte[]> FrameReceived;
        public event Action<bool> ConnectionChanged;
        public event Action<string> ErrorOccurred;

        public bool IsOpen => _port?.IsOpen == true;

        public void Open(string portName, int baudRate = 9600)
        {
            Close();

            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
                Handshake = Handshake.None
            };

            _port.Open();
            _cts = new CancellationTokenSource();
            ConnectionChanged?.Invoke(true);

            _ = ReadLoopAsync(_cts.Token); // 启动异步读取循环
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            var buffer = new byte[1024];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    int n = await _port.BaseStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (n <= 0) continue;

                    _cache.AddRange(buffer.Take(n));
                    ParseCache();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke($"接收异常：{ex.Message}");
                    break;
                }
            }

            ConnectionChanged?.Invoke(false);
        }

        private void ParseCache()
        {
            while (_cache.Count >= 6)
            {
                // 找帧头 0xAA 0x55
                int headerIdx = -1;
                for (int i = 0; i < _cache.Count - 1; i++)
                {
                    if (_cache[i] == 0xAA && _cache[i + 1] == 0x55)
                    {
                        headerIdx = i;
                        break;
                    }
                }

                if (headerIdx < 0) { _cache.Clear(); return; }
                if (headerIdx > 0) _cache.RemoveRange(0, headerIdx);
                if (_cache.Count < 6) return;

                int bodyLen = (_cache[2] << 8) | _cache[3]; // 大端长度
                int totalLen = 2 + 2 + bodyLen + 2;          // 头+长度+体+CRC
                if (_cache.Count < totalLen) return;          // 半包，等下次

                var frame = _cache.Take(totalLen).ToArray();
                _cache.RemoveRange(0, totalLen);

                if (CheckCrc(frame)) // 实际项目一定要实现CRC16校验
                    FrameReceived?.Invoke(frame);
            }
        }

        public async Task SendAsync(byte[] data)
        {
            if (!IsOpen) throw new InvalidOperationException("串口未打开");

            await _sendLock.WaitAsync();
            try
            {
                await _port.BaseStream.WriteAsync(data, 0, data.Length);
                await _port.BaseStream.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Close()
        {
            try
            {
                _cts?.Cancel();
                _port?.Close();
                _port?.Dispose();
            }
            catch { /* 忽略关闭异常 */ }

            _port = null;
            _cts = null;
            _cache.Clear();
            ConnectionChanged?.Invoke(false);
        }

        public void Dispose() => Close();

        private bool CheckCrc(byte[] frame)
        {
            // 这里用CRC16 Modbus算法，网上查表法很多，实际项目必须校验
            return true;
        }
    }
}
