namespace CommunityToolkitDemo.Services;

/// <summary>
/// Markdown 数据源加载服务：把 Data/*.md 解析为内存仓储中的实体集合。
/// </summary>
public interface IMarkdownDataService
{
    /// <summary>数据文件所在目录（运行目录下的 Data 文件夹）</summary>
    string DataDirectory { get; }

    /// <summary>加载过程中的错误信息（文件缺失、字段非法等）</summary>
    IReadOnlyList<string> LoadErrors { get; }

    /// <summary>加载（或重新加载）全部 Markdown 数据源。</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
