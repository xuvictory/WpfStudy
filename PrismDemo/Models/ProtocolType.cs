namespace PrismDemo.Models;

/// <summary>支持的通信协议类型。</summary>
public enum ProtocolType
{
    /// <summary>Modbus TCP —— 用于读取电子秤 / 秤台寄存器</summary>
    ModbusTcp,

    /// <summary>OPC UA —— 用于读取门店环境与设备节点</summary>
    OpcUa,

    /// <summary>串口 —— 用于扫码枪、钱箱、小票打印机</summary>
    SerialPort,

    /// <summary>Socket —— 用于 TCP 长连接外设（如客显屏）</summary>
    Socket,

    /// <summary>MQTT —— 用于云端订单与门店状态上报</summary>
    Mqtt
}

/// <summary>连接的运行状态。</summary>
public enum ConnectionState
{
    /// <summary>未连接</summary>
    Disconnected,

    /// <summary>连接中</summary>
    Connecting,

    /// <summary>已连接</summary>
    Connected,

    /// <summary>故障（如模拟的通信异常）</summary>
    Faulted
}

/// <summary>报文方向。</summary>
public enum ProtocolDirection
{
    /// <summary>接收（设备 → 上位机）</summary>
    In,

    /// <summary>发送（上位机 → 设备）</summary>
    Out
}
