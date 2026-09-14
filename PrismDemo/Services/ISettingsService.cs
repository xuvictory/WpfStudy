using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>
/// 设置服务：保存当前运行参数，并在保存成功后通过消息总线通知订阅方。
///
/// 本 Demo 不落盘，设置保存在内存中（重启恢复默认值），
/// 重点演示"设置页 → 其他页面"的跨页联动。
/// </summary>
public interface ISettingsService
{
    /// <summary>当前生效的设置</summary>
    PosSettings Current { get; }

    /// <summary>保存并广播 <see cref="Events.SettingsChangedEvent"/></summary>
    void Save(PosSettings settings);

    /// <summary>恢复出厂默认值</summary>
    void Reset();
}
