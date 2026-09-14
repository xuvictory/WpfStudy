using PrismDemo.Common.Collection;
using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>
/// 内存仓储：所有业务数据的唯一存放点。
/// Markdown 数据源在启动时填充它，业务服务读写它，UI 绑定它。
///
/// 订单集合使用 <see cref="RangeObservableCollection{T}"/>，
/// 这样新订单写入后订单列表会自动刷新（自带集合变更通知），且加载时可一次性批量替换。
/// </summary>
public sealed class PosRepository : IProductCatalog, IDeviceCatalog
{
    /// <summary>"全部"虚拟分类的 Id 取值。</summary>
    /// <remarks>字段名从 AllCategoryId 改为 AllCategoryValue，是为了把该名字让给
    /// <see cref="IProductCatalog.AllCategoryId"/> 契约成员（同一类型内不能出现同名的字段与属性）。</remarks>
    private const string AllCategoryValue = "all";

    /// <summary>条码索引。与旧线性扫描的匹配规则一致：只收录非空白条码，按 <see cref="StringComparison.Ordinal"/> 比较。</summary>
    private Dictionary<string, Product>? _productsByBarcode;

    /// <summary>编码索引。按 <see cref="StringComparison.OrdinalIgnoreCase"/> 比较，与旧线性扫描一致。</summary>
    private Dictionary<string, Product>? _productsByCode;

    /// <summary>
    /// 分类索引：CategoryId → 该分类下的商品（保持 <see cref="Products"/> 中的原始顺序）。
    /// </summary>
    /// <remarks>
    /// 服务于批次 3 的分类切片查询（当前无调用方）。它与条码/编码索引共用同一套
    /// 惰性构建与失效机制，避免在查询路径上做 O(N) 扫描。
    /// </remarks>
    private Dictionary<string, List<Product>>? _productsByCategory;

    /// <summary>建立索引时的商品条数，用于判断索引是否仍然与 <see cref="Products"/> 一致。</summary>
    private int _indexedProductCount = -1;

    /// <summary>带"全部"虚拟分类的分类视图缓存。</summary>
    private IReadOnlyList<ProductCategory>? _categoriesWithAllCache;

    /// <summary>构建分类视图缓存时的分类条数，用于判断缓存是否仍然有效。</summary>
    private int _categoriesWithAllCount = -1;

    /// <summary>商品分类</summary>
    public List<ProductCategory> Categories { get; } = new();

    /// <summary>
    /// 商品主数据（存储唯一落点）。
    /// </summary>
    /// <remarks>
    /// 批次 3 起，<see cref="IProductCatalog"/> 已不再暴露本集合：表现层的商品浏览统一走
    /// <see cref="IProductQuery"/>，因此这里保留具体 <see cref="List{T}"/> 只是为了仓储自身
    /// 维护（批量替换）与测试取样，不再承担"作为 UI 过滤数据源"的职责。
    /// 派生索引的失效检测依赖本集合的条数变化，直接增删元素会被索引自动感知。
    /// </remarks>
    public List<Product> Products { get; } = new();

    /// <summary>外设与协议配置</summary>
    public List<DeviceInfo> Devices { get; } = new();

    /// <summary>订单（历史 + 本次运行新增）</summary>
    /// <remarks>
    /// 使用支持批量替换的集合类型：加载时一次性重建只发一次变更通知，
    /// 避免逐条 Add 造成订单列表反复刷新（详见 <see cref="RangeObservableCollection{T}"/>）。
    /// </remarks>
    public RangeObservableCollection<Order> Orders { get; } = new();

    /// <summary>订单历史上限：超出后丢弃最旧的，避免长时间运行内存无界增长</summary>
    public const int MaxOrders = 2000;

    /// <summary>写入新订单：插到最前（界面按时间倒序）并裁剪超出上限的旧订单。</summary>
    public void AddOrder(Order order)
    {
        Orders.Insert(0, order);

        while (Orders.Count > MaxOrders)
        {
            Orders.RemoveAt(Orders.Count - 1);
        }
    }

    /// <summary>
    /// 原子替换全部数据：解析完成后一次性写入，避免加载中途出现"部分数据"的不一致状态。
    /// 就地清空并填充现有集合，保证已绑定的集合实例（如商品视图）继续有效。
    /// </summary>
    public void ReplaceAll(
        IEnumerable<ProductCategory> categories,
        IEnumerable<Product> products,
        IEnumerable<Order> orders,
        IEnumerable<DeviceInfo> devices)
    {
        Categories.Clear();
        Categories.AddRange(categories);

        Products.Clear();
        Products.AddRange(products);

        // 一次性替换并套用与 AddOrder 相同的上限：
        // 旧实现逐条 Add 会发出 N 次集合变更通知，且没有裁剪 —— 重置后订单数可能超过
        // MaxOrders，与 AddOrder 的护栏语义不一致。orders.md 按时间倒序，保留前 N 条即"丢弃最旧的"。
        Orders.ReplaceAll(orders.Take(MaxOrders));

        Devices.Clear();
        Devices.AddRange(devices);

        // 数据已整体替换：显式失效派生索引与分类视图缓存。
        // 仅凭"条数是否变化"不足以覆盖"重新加载了同样条数的数据"这一场景。
        InvalidateDerivedCaches();
    }

    /// <summary>带"全部"虚拟分类的分类列表，供收银台分类标签使用（<see cref="IProductCatalog"/> 契约成员）。</summary>
    /// <remarks>结果缓存：分类标签在界面构建与刷新时会被反复读取，每次重建都会分配一个新列表。</remarks>
    public IReadOnlyList<ProductCategory> CategoriesWithAll
    {
        get
        {
            var cached = _categoriesWithAllCache;
            if (cached is not null && _categoriesWithAllCount == Categories.Count)
            {
                return cached;
            }

            var list = new List<ProductCategory>(Categories.Count + 1)
            {
                new() { Id = AllCategoryValue, Name = "全部", Short = "全" }
            };
            list.AddRange(Categories);

            _categoriesWithAllCache = list;
            _categoriesWithAllCount = Categories.Count;
            return list;
        }
    }

    /// <summary>"全部"虚拟分类的 Id（<see cref="IProductCatalog"/> 契约成员）。</summary>
    public string AllCategoryId => AllCategoryValue;

    /// <summary>当前商品总数（<see cref="IProductCatalog"/> 契约成员）。</summary>
    public int ProductCount => Products.Count;

    /// <summary>按序号取商品，越界返回 <c>null</c>（<see cref="IProductCatalog"/> 契约成员）。</summary>
    public Product? GetProductAt(int index)
        => index >= 0 && index < Products.Count ? Products[index] : null;

    /// <summary>"全部"虚拟分类的 Id（静态便捷访问）。</summary>
    /// <remarks>保留此静态属性以兼容既有调用方；新代码建议依赖 <see cref="IProductCatalog.AllCategoryId"/>。</remarks>
    public static string AllCategory => AllCategoryValue;

    /// <summary>按条码查找商品（扫码枪场景）。</summary>
    /// <remarks>走条码索引：扫码是 POS 最高频的查询路径，10w 商品下必须为 O(1)。</remarks>
    public Product? FindByBarcode(string barcode)
    {
        var index = GetBarcodeIndex();
        return index.TryGetValue(barcode.Trim(), out var product) ? product : null;
    }

    /// <summary>按商品编码查找。</summary>
    /// <remarks>走编码索引，与条码索引共用同一份一致性检测。</remarks>
    public Product? FindByCode(string code)
    {
        var index = GetCodeIndex();

        if (code is not null && index.TryGetValue(code, out var product))
        {
            return product;
        }

        // 索引刻意跳过 Code 为 null 的商品（Dictionary 不接受 null 键）。
        // 旧线性实现中 string.Equals(p.Code, null) 会命中第一条 Code 为 null 的商品，
        // 这里用一次兜底扫描保留该语义（极少触发，不影响高频路径）。
        return code is null ? Products.FirstOrDefault(p => p.Code is null) : null;
    }

    /// <summary>
    /// 惰性构建/获取条码索引。
    /// </summary>
    /// <remarks>
    /// <see cref="Products"/> 是公开可变列表，调用方（如测试）可以直接 Add/Remove。
    /// 这里用"条数是否变化"作为轻量一致性检测：条数不同即重建索引。
    /// 条数相同但内容被就地替换的场景由 <see cref="ReplaceAll"/> 显式失效兜底。
    /// </remarks>
    private Dictionary<string, Product> GetBarcodeIndex()
    {
        EnsureProductIndexes();
        return _productsByBarcode!;
    }

    /// <summary>惰性构建/获取编码索引，一致性检测说明见 <see cref="GetBarcodeIndex"/>。</summary>
    private Dictionary<string, Product> GetCodeIndex()
    {
        EnsureProductIndexes();
        return _productsByCode!;
    }

    /// <summary>按当前 <see cref="Products"/> 内容（必要时）重建条码/编码索引。</summary>
    private void EnsureProductIndexes()
    {
        if (_productsByBarcode is not null
            && _productsByCode is not null
            && _indexedProductCount == Products.Count)
        {
            return;
        }

        var byBarcode = new Dictionary<string, Product>(Products.Count, StringComparer.Ordinal);
        var byCode = new Dictionary<string, Product>(Products.Count, StringComparer.OrdinalIgnoreCase);
        var byCategory = new Dictionary<string, List<Product>>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in Products)
        {
            // TryAdd 而非索引器赋值：保持与旧 FirstOrDefault「首个命中」一致的语义。
            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                byBarcode.TryAdd(product.Barcode, product);
            }

            if (product.Code is not null)
            {
                byCode.TryAdd(product.Code, product);
            }

            if (product.CategoryId is not null)
            {
                if (!byCategory.TryGetValue(product.CategoryId, out var bucket))
                {
                    bucket = new List<Product>();
                    byCategory[product.CategoryId] = bucket;
                }

                bucket.Add(product);
            }
        }

        _productsByBarcode = byBarcode;
        _productsByCode = byCode;
        _productsByCategory = byCategory;
        _indexedProductCount = Products.Count;
    }

    /// <summary>
    /// 取某分类下的全部商品（分类索引切片，O(1) 定位）。
    /// </summary>
    /// <param name="categoryId">分类 Id；等于 <see cref="AllCategoryId"/>（忽略大小写）时返回全部商品。</param>
    /// <remarks>
    /// <see cref="IProductCatalog"/> 契约成员，由 <see cref="ProductQueryService"/> 消费；
    /// 返回值是只读视图，调用方不得修改。当 <paramref name="categoryId"/> 为"全部"时
    /// 返回的是仓储自身的商品集合（只读形态），因此调用方不应长期持有。
    /// </remarks>
    public IReadOnlyList<Product> GetProductsInCategory(string categoryId)
    {
        EnsureProductIndexes();

        if (string.Equals(categoryId, AllCategoryValue, StringComparison.OrdinalIgnoreCase))
        {
            return Products;
        }

        return _productsByCategory!.TryGetValue(categoryId, out var bucket)
            ? bucket
            : Array.Empty<Product>();
    }

    /// <summary>
    /// 失效全部派生缓存：索引与分类视图。
    /// </summary>
    /// <remarks>
    /// <see cref="ReplaceAll"/> 会就地清空再填充集合，条数可能与替换前完全相同，
    /// 仅靠条数检测无法识别，因此必须在替换后显式失效。
    /// </remarks>
    private void InvalidateDerivedCaches()
    {
        _productsByBarcode = null;
        _productsByCode = null;
        _productsByCategory = null;
        _indexedProductCount = -1;
        _categoriesWithAllCache = null;
        _categoriesWithAllCount = -1;
    }

    /// <summary>
    /// 按设备 Id 取配置；Markdown 未提供时返回一份兜底配置，
    /// 保证即使数据文件缺失协议层依然可用。
    /// </summary>
    public DeviceInfo GetDeviceOrDefault(string id, ProtocolType protocol, string name, string address)
        => Devices.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
           ?? new DeviceInfo { Id = id, Name = name, Protocol = protocol, Address = address };
}
