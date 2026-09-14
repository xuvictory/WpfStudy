namespace PrismDemo.Models;

/// <summary>
/// 统一报文模型：屏蔽 Modbus / OPC UA / 串口 / Socket / MQTT 的协议差异，
/// 让 UI 层可以用同一套视图渲染所有协议的数据与日志。
/// </summary>
public sealed class ProtocolFrame
{
    /// <summary>所属协议</summary>
    public ProtocolType Type { get; init; }

    /// <summary>报文方向</summary>
    public ProtocolDirection Direction { get; init; } = ProtocolDirection.In;

    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>从站地址 / 节点 ID / 串口号 / 远端地址 / MQTT 主题</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>报文载荷（可读文本）</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>数据语义描述（如"电子秤重量"）</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>结构化数据中的键值对（可选，供设备监控表格展示）</summary>
    public IReadOnlyDictionary<string, string>? Values { get; init; }

    public ProtocolFrame() { }

    public ProtocolFrame(
        ProtocolType type,
        ProtocolDirection direction,
        string address,
        string payload,
        string description = "",
        IReadOnlyDictionary<string, string>? values = null)
    {
        Type = type;
        Direction = direction;
        Address = address;
        Payload = payload;
        Description = description;
        Values = values;
    }

    public override string ToString() => $"[{Type}] {Address} {Payload}";
}
