using System.IO;
using System.Text;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Services;

/// <summary>
/// Markdown 数据加载服务实现。
///
/// 数据流：Data/*.md → MarkdownTableParser（通用表格解析）→ 字段映射 → PosRepository（内存仓储）。
/// 整个解析过程容错：单个文件失败不会影响其它文件，错误汇总在 <see cref="LoadErrors"/> 中。
/// </summary>
public sealed class MarkdownDataService : IMarkdownDataService
{
    private const string CategoriesFile = "categories.md";
    private const string ProductsFile = "products.md";
    private const string OrdersFile = "orders.md";
    private const string DevicesFile = "devices.md";

    /// <summary>
    /// 单个数据文件的字节数上限（64 MB）。
    /// </summary>
    /// <remarks>
    /// 数据文件来自应用目录，理论上可被替换，因此保留一道"整文件跳过"的兜底校验。
    /// 阈值由 8 MB 提高到 64 MB：解析已改为流式逐行读取，内存占用与文件体积基本解耦，
    /// 真正的内存风险已转为"结果集规模"（由 <see cref="MaxDataRows"/> 约束）。
    /// 原先 8 MB 的阈值恰好落在目标规模之下 —— 10 万行商品文件实测约 8.4 MB，
    /// 会被整体跳过，表现为"商品列表为空"而不是"加载变慢"。
    /// </remarks>
    private const long MaxDataFileBytes = 64L * 1024 * 1024;

    /// <summary>
    /// 单个数据文件的最大数据行数（100 万行）。
    /// </summary>
    /// <remarks>
    /// 流式解析后，行数直接决定实体数量（10 万行商品约 23 MB 稳态内存，100 万行约 230 MB），
    /// 因此用它替代"文件体积"作为规模护栏。触发时保留已读取部分，并在
    /// <see cref="LoadErrors"/> 中给出明确提示，而不是静默丢弃整个文件。
    /// </remarks>
    private const int MaxDataRows = 1_000_000;

    private readonly PosRepository _repository;
    private readonly List<string> _errors = new();

    public MarkdownDataService(PosRepository repository)
    {
        _repository = repository;
        DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    }

    public string DataDirectory { get; }

    public IReadOnlyList<string> LoadErrors => _errors;

    /// <summary>把单个文件的错误并入汇总列表（按调用顺序追加，保证 LoadErrors 顺序稳定）。</summary>
    private void CollectError(string? error)
    {
        if (error is not null)
        {
            _errors.Add(error);
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // 放在开头清理：即使中途被取消或抛错，也不会残留上一次加载的错误信息。
        _errors.Clear();

        // 4 个文件互不依赖，先把任务全部启动（真正的并行发生在 IO 等待期间），
        // 再逐个 await 取结果。虽然写法是顺序 await，但 4 个任务已同时在进行，
        // 总耗时仍由最慢的那个决定，与 Task.WhenAll 等价，
        // 区别只在于这里能直接拿到各自的强类型结果（Task.WhenAll 对不同类型的
        // Task<T> 只能退化为 Task[]，反而要回退到 .Result 取值）。
        var categoriesTask = ReadAsync<ProductCategory>(CategoriesFile, MapCategory, cancellationToken);
        var productsTask = ReadAsync<Product>(ProductsFile, MapProduct, cancellationToken);
        var ordersTask = ReadAsync<Order>(OrdersFile, MapOrder, cancellationToken);
        var devicesTask = ReadAsync<DeviceInfo>(DevicesFile, MapDevice, cancellationToken);

        var categories = await categoriesTask;
        var products = await productsTask;
        var orders = await ordersTask;
        var devices = await devicesTask;

        // 错误统一在这里汇总，而不是让各并行任务直接写 _errors：
        // 那样只有在"续体恰好回到同一个同步上下文"时才安全（当前依赖 UI 线程调用），
        // 一旦调用方改成 Task.Run 就会产生并发写。各任务只返回自己的结果，
        // 主流程按固定顺序合并，LoadErrors 的顺序也因此稳定可预期。
        CollectError(categories.Error);
        CollectError(products.Error);
        CollectError(orders.Error);
        CollectError(devices.Error);

        // 全部解析完成后再一次性替换，避免加载中途出现"部分数据"的不一致状态。
        _repository.ReplaceAll(categories.Items, products.Items, orders.Items, devices.Items);
    }

    #region 通用读取

    /// <summary>单个文件的读取结果：数据与（可选的）错误信息，避免并行任务共享可变状态。</summary>
    private readonly record struct ReadResult<T>(IReadOnlyList<T> Items, string? Error);

    private async Task<ReadResult<T>> ReadAsync<T>(
        string fileName,
        Func<MarkdownRow, T?> map,
        CancellationToken cancellationToken)
        where T : class
    {
        var path = Path.Combine(DataDirectory, fileName);

        try
        {
            if (!File.Exists(path))
            {
                return new ReadResult<T>(Array.Empty<T>(), $"缺少数据文件：{fileName}");
            }

            // 兜底校验：整文件跳过，避免被替换进来的异常大文件被整体载入
            var length = new FileInfo(path).Length;
            if (length > MaxDataFileBytes)
            {
                return new ReadResult<T>(
                    Array.Empty<T>(),
                    $"数据文件过大（{length / 1024 / 1024} MB），已跳过：{fileName}");
            }

            // 解析是 CPU 密集操作：await 的续体默认回到调用方的同步上下文（此处是 UI 线程），
            // 若在续体里直接解析，10 万行的耗时（实测约 0.9 s）会整段冻结界面。
            // 因此把"打开文件流 + 流式解析 + 映射实体"整体放进线程池，主流程只 await 结果。
            var (items, truncated) = await Task.Run(
                () => ParseFile<T>(path, map),
                cancellationToken);

            var error = truncated
                ? $"数据行数超过上限（{MaxDataRows:N0}），已截断：{fileName}"
                : null;
            return new ReadResult<T>(items, error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ReadResult<T>(Array.Empty<T>(), $"解析 {fileName} 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 在工作线程上执行的"打开文件流 → 流式解析 → 映射实体"。
    /// </summary>
    /// <remarks>
    /// 使用同步 IO 而非 <c>ReadLineAsync</c>：调用方已经在线程池上，
    /// 逐行 await 只会为每一行增加一次状态机调度开销，反而更慢；
    /// <see cref="FileOptions.SequentialScan"/> 则提示操作系统按顺序预读，提升大文件吞吐。
    /// </remarks>
    private static (IReadOnlyList<T> Items, bool Truncated) ParseFile<T>(string path, Func<MarkdownRow, T?> map)
        where T : class
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var items = MarkdownTableParser.ParseFirstTable<T>(reader, map, MaxDataRows, out var truncated);
        return (items, truncated);
    }

    #endregion

    #region 字段映射

    private static ProductCategory? MapCategory(MarkdownRow row)
    {
        var id = row.GetString("Id");
        return string.IsNullOrWhiteSpace(id)
            ? null
            : new ProductCategory
            {
                Id = id,
                Name = row.GetString("Name"),
                Short = row.GetString("Short")
            };
    }

    private static Product? MapProduct(MarkdownRow row)
    {
        var code = row.GetString("Code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return new Product
        {
            Code = code,
            Name = row.GetString("Name"),
            CategoryId = row.GetString("CategoryId"),
            Price = row.GetDecimal("Price"),
            Barcode = row.GetString("Barcode"),
            Stock = row.GetInt("Stock"),
            WarnStock = row.GetInt("WarnStock", 10),
            Unit = row.GetString("Unit") is { Length: > 0 } unit ? unit : "件",
            Spec = row.GetString("Spec")
        };
    }

    private static Order? MapOrder(MarkdownRow row)
    {
        var orderNo = row.GetString("OrderNo");
        if (string.IsNullOrWhiteSpace(orderNo))
        {
            return null;
        }

        return new Order
        {
            OrderNo = orderNo,
            CreatedAt = row.GetDateTime("CreatedAt", DateTime.Now),
            Payment = row.GetEnum<PaymentMethod>("Payment") ?? PaymentMethod.Cash,
            Total = row.GetDecimal("Total"),
            Paid = row.GetDecimal("Paid"),
            ItemCount = row.GetInt("ItemCount"),
            Cashier = row.GetString("Cashier")
        };
    }

    private static DeviceInfo? MapDevice(MarkdownRow row)
    {
        var id = row.GetString("Id");
        var protocol = row.GetEnum<ProtocolType>("Protocol");
        if (string.IsNullOrWhiteSpace(id) || protocol is null)
        {
            return null;
        }

        return new DeviceInfo
        {
            Id = id,
            Name = row.GetString("Name"),
            Protocol = protocol.Value,
            Address = row.GetString("Address"),
            IntervalMs = row.GetInt("IntervalMs", 1500),
            FaultRate = row.GetDouble("FaultRate", 0.03),
            Metrics = row.GetString("Metrics"),
            Description = row.GetString("Description")
        };
    }

    #endregion
}
