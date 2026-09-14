using NModbus;
using NModbus.Device;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ModbusDemo
{
    public class ModbusTcpService : IDisposable
    {
        private TcpClient _tcpClient;
        private IModbusMaster _master;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public bool IsConnected => _tcpClient?.Connected == true;
        public event Action<string> LogReceived;
        public event Action<bool> ConnectionChanged;

        public async Task ConnectAsync(string ip, int port = 502)
        {
            try
            {
                _tcpClient = new TcpClient();
                // 连接超时通过 ConnectAsync 的 CancellationToken 控制
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _tcpClient.ConnectAsync(ip, port, cts.Token);

                // 用 ModbusFactory 创建主站（新版 API）
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);

                // 在 Modbus 层设置超时，而不是 TcpClient 层
                _master.Transport.ReadTimeout = 3000;
                _master.Transport.WriteTimeout = 3000;
                _master.Transport.Retries = 0;

                ConnectionChanged?.Invoke(true);
                LogReceived?.Invoke($"已连接 {ip}:{port}");
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"连接失败：{ex.Message}");
                ConnectionChanged?.Invoke(false);
                throw;
            }
        }

        /// 读保持寄存器（带异常处理）
        public async Task<ushort[]> ReadHoldingRegistersAsync(
            byte slaveId, ushort startAddress, ushort count)
        {
            if (!IsConnected) throw new InvalidOperationException("未连接");

            await _lock.WaitAsync();
            try
            {
                // NModbus的同步方法放到Task.Run里，不阻塞UI
                return await Task.Run(() =>
                    _master.ReadHoldingRegisters(slaveId, startAddress, count));
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"读寄存器异常[{slaveId}]: {ex.Message}");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// 读线圈
        public async Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count)
        {
            await _lock.WaitAsync();
            try
            {
                return await Task.Run(() =>
                    _master.ReadCoils(slaveId, startAddress, count));
            }
            finally { _lock.Release(); }
        }

        public void Disconnect()
        {
            try { _master?.Dispose(); _tcpClient?.Close(); } catch { }
            _tcpClient = null;
            _master = null;
            ConnectionChanged?.Invoke(false);
        }

        public void Dispose() => Disconnect();
    }
}
