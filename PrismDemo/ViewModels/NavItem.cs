namespace PrismDemo.ViewModels;

/// <summary>左侧导航项定义。</summary>
/// <param name="Key">页面标识（与 <see cref="Events.NavigateEvent.Pages"/> 及区域注册名一致）</param>
/// <param name="Title">显示名称</param>
/// <param name="Glyph">Segoe MDL2 图标字形</param>
public sealed record NavItem(string Key, string Title, string Glyph);
