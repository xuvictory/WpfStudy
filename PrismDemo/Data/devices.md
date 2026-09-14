# 外设与协议配置（模拟）

> 本文件驱动 5 个模拟协议驱动：每种协议对应一台外设，
> 由 `ProtocolManager` 按 `IntervalMs` 周期生成仿真报文，用于演示上位机通信链路。
>
> 字段说明：
> - `Protocol`：ModbusTcp / OpcUa / SerialPort / Socket / Mqtt
> - `Address`：ModbusTcp 为 IP:端口；OpcUa 为节点 ID；SerialPort 为 COM 口；Socket 为远端地址；Mqtt 为主题
> - `IntervalMs`：仿真数据推送周期（毫秒）
> - `FaultRate`：模拟故障概率（0~1），命中时驱动进入 Faulted 并广播报警
> - `Metrics`：推送的数据指标（用顿号分隔）

| Id | Name | Protocol | Address | IntervalMs | FaultRate | Metrics | Description |
| -- | ---- | -------- | ------- | ---------- | --------- | ------- | ----------- |
| scale | 电子秤 | ModbusTcp | 192.168.1.20:502 | 1200 | 0.02 | 重量 | 读取从站 1 的保持寄存器 40001，模拟生鲜称重 |
| env | 冷藏柜环境传感器 | OpcUa | ns=2;s=Store.ColdRoom.Temp | 2000 | 0.03 | 温度、湿度 | 订阅 OPC UA 节点，监控冷藏柜温湿度 |
| scanner | 扫码枪与小票打印机 | SerialPort | COM3 | 1600 | 0.05 | 条码、打印状态 | 串口读取扫码枪条码，回写小票打印指令 |
| customer | 客显屏 | Socket | 127.0.0.1:8899 | 1800 | 0.02 | 显示金额、钱箱信号 | TCP 长连接向客显屏推送应收金额，回传钱箱开合 |
| cloud | 云端订单通道 | Mqtt | pos/store001/order | 2500 | 0.04 | 上报序号、在线设备数 | 通过 MQTT 主题上报订单与门店心跳 |
