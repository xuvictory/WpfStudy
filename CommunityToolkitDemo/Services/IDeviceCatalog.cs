using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// 设备配置只读契约：协议驱动构造时读取自身连接参数的唯一入口。
///
/// 设计说明：驱动属于传输层，本不该感知"商品/订单"等业务数据。
/// 之前它们注入整个 <see cref="PosRepository"/>，只为了调用其中这一个方法，
/// 等于让传输层拿到全部业务数据的访问权。切出该接口后，驱动只依赖设备配置这一件事，
/// 既收窄了权限，也让驱动更容易被单独测试与替换。
/// </summary>
public interface IDeviceCatalog
{
    /// <summary>
    /// 按设备 Id 取配置；数据源未提供时返回一份兜底配置，
    /// 保证即使数据文件缺失协议层依然可用。
    /// </summary>
    DeviceInfo GetDeviceOrDefault(string id, ProtocolType protocol, string name, string address);
}
