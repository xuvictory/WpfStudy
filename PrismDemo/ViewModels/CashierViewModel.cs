using System.Collections.ObjectModel;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Events;
using PrismDemo.Common.Collection;
using PrismDemo.Common.Commands;
using PrismDemo.Common.Mvvm;
using PrismDemo.Events;
using PrismDemo.Models;
using PrismDemo.Protocols;
using PrismDemo.Services;

namespace PrismDemo.ViewModels;

/// <summary>
/// 收银台 ViewModel：商品浏览 → 加购 → 结算支付 → 驱动外设。
///
/// 覆盖的 Prism 知识点：
/// - <c>IEventAggregator</c>：订阅扫码 / 购物车 / 设置 / 数据加载 / 页面导航五类事件；
/// - <c>DelegateCommand</c> / <c>DelegateCommand&lt;T&gt;</c>：同步命令（含带参命令）；
/// - <see cref="AsyncDelegateCommand"/>：Prism 8 缺失、由本项目补齐的异步命令；
/// - 手写属性 + <c>RaisePropertyChanged</c>：金额联动；
/// - setter 内联动：实收金额 / 支付方式变化时刷新结算按钮可用性（取代原 <c>[NotifyCanExecuteChangedFor]</c>）。
/// </summary>
public class CashierViewModel : ViewModelBase
{
    /// <summary>
    /// 商品目录（只读契约）：收银台只需要"看商品/分类"，不需要订单与设备能力，
    /// 因此依赖最小接口而非整个仓储，避免表现层被数据层实现细节绑定。
    /// </summary>
    private readonly IProductCatalog _catalog;

    private readonly ICartService _cart;
    private readonly IOrderService _orders;
    private readonly IDialogService _dialogs;
    private readonly IProtocolManager _protocols;
    private readonly ISettingsService _settings;

    /// <summary>结算外设编排（打印/开钱箱/上报）：设备协调细节不放在本类</summary>
    private readonly CheckoutCoordinator _checkoutCoordinator;

    /// <summary>
    /// 购物车汇总缓存：由 <see cref="CartChangedEvent"/> 携带的数据刷新，
    /// 派生属性据此计算，避免每次属性读取都穿透到购物车服务。
    /// </summary>
    private CartSummary _summary;

    /// <summary>
    /// 商品查询服务（表现层"看商品"的唯一入口）：
    /// 分类切片走仓储索引，关键字过滤在大数据量时于后台线程执行，结果受上限约束。
    /// </summary>
    private readonly IProductQuery _productQuery;

    /// <summary>当前展示的商品（批量替换只发一次 Reset 通知，避免逐条重排）</summary>
    private readonly RangeObservableCollection<Product> _products = new();

    /// <summary>上一次查询的取消源：用户连续输入/切分类时取消尚未完成的旧查询</summary>
    private CancellationTokenSource? _queryCts;

    /// <summary>当前结果集是否因上限被截断（截断时界面提示"请输入关键字缩小范围"）</summary>
    private bool _hasMoreProducts;

    /// <summary>搜索输入防抖计时器</summary>
    private readonly DispatcherTimer _searchDebounceTimer;

    /// <summary>已生效的搜索关键字（防抖后）</summary>
    private string _appliedKeyword = string.Empty;

    /// <summary>扫码指令下发中（防止重复触发）</summary>
    private bool _isScanning;

    private ProductCategory? _selectedCategory;
    private string _searchText = string.Empty;
    private string _barcodeInput = string.Empty;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private string _paidText = string.Empty;
    private bool _isProcessing;

    // 命令对象延迟创建：与迁移前源生成器的 lazy 语义保持一致
    // （构造过程中属性 setter 就可能触发 RaiseCanExecuteChanged，饿汉式创建容易踩空引用）
    private DelegateCommand<Product>? _addProductCommand;
    private DelegateCommand<CartItem>? _increaseQuantityCommand;
    private DelegateCommand<CartItem>? _decreaseQuantityCommand;
    private DelegateCommand<CartItem>? _removeItemCommand;
    private DelegateCommand? _clearCartCommand;
    private DelegateCommand? _addByBarcodeCommand;
    private AsyncDelegateCommand? _scanCommand;
    private DelegateCommand? _fillExactAmountCommand;
    private AsyncDelegateCommand? _checkoutCommand;

    public CashierViewModel(
        IProductCatalog catalog,
        IProductQuery productQuery,
        ICartService cart,
        IOrderService orders,
        IDialogService dialogs,
        IProtocolManager protocols,
        ISettingsService settings,
        CheckoutCoordinator checkoutCoordinator,
        IEventAggregator eventAggregator) : base(eventAggregator)
    {
        _catalog = catalog;
        _productQuery = productQuery;
        _cart = cart;
        _orders = orders;
        _dialogs = dialogs;
        _protocols = protocols;
        _settings = settings;
        _checkoutCoordinator = checkoutCoordinator;

        // 初始汇总：构造后即可直接绑定，无需等待第一次购物车变更事件
        _summary = _cart.Summary;

        Categories = new ObservableCollection<ProductCategory>(_catalog.CategoriesWithAll);

        // 购物车集合直接复用服务里的实例：服务改动，界面自动刷新（ObservableCollection 通知）
        CartItems = _cart.Items;

        _selectedCategory = Categories.FirstOrDefault();

        // 搜索输入防抖：停止输入 250ms 后才真正查询
        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            _appliedKeyword = SearchText.Trim();
            _ = RefreshProductsAsync();
        };

        // 收银台需要随时响应扫码枪，订阅的生命周期与应用一致
        SubscribeEvents();

        // 首次进入收银台：等价于原来"构造时 IsActive = true → OnActivated()"的效果
        ApplyDefaultPaymentIfCartEmpty();

        // 构造函数不能 await：首次查询异步发起，结果就绪后由集合通知刷新界面
        _ = RefreshProductsAsync();
    }

    #region 绑定数据

    /// <summary>顶部横向分类标签</summary>
    public ObservableCollection<ProductCategory> Categories { get; }

    /// <summary>
    /// 当前分类/关键字过滤后的商品。
    /// </summary>
    /// <remarks>
    /// 暴露具体集合类型而非 <c>ICollectionView</c>：过滤已下沉到 <see cref="IProductQuery"/>，
    /// 不再需要 WPF 视图层参与，界面只需一个可观察集合。
    /// </remarks>
    public ObservableCollection<Product> Products => _products;

    /// <summary>购物车行项</summary>
    public ObservableCollection<CartItem> CartItems { get; }

    /// <summary>选中分类：变化时重新过滤商品</summary>
    public ProductCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            // 分类切换即时生效（无需防抖）
            _appliedKeyword = SearchText.Trim();
            _ = RefreshProductsAsync();
        }
    }

    /// <summary>商品搜索关键字（名称/编码/条码）</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            // 输入防抖：停止输入后再过滤，避免每敲一个字符就触发全量重排
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }
    }

    /// <summary>扫码/手动录入的条码</summary>
    public string BarcodeInput
    {
        get => _barcodeInput;
        set => SetProperty(ref _barcodeInput, value);
    }

    /// <summary>
    /// 支付方式：变化时刷新按钮可用性与找零显示，非现金时清空实收输入。
    /// </summary>
    public PaymentMethod PaymentMethod
    {
        get => _paymentMethod;
        set
        {
            if (!SetProperty(ref _paymentMethod, value))
            {
                return;
            }

            // 原 [NotifyPropertyChangedFor] / [NotifyCanExecuteChangedFor] 的效果
            RaisePropertyChanged(nameof(IsCashPayment));
            RaisePropertyChanged(nameof(Change));
            CheckoutCommand.RaiseCanExecuteChanged();

            // 原 OnPaymentMethodChanged：非现金直接把实收置为应收，避免用户重复输入
            if (value is not PaymentMethod.Cash)
            {
                PaidText = string.Empty;
            }
        }
    }

    /// <summary>现金实收金额（文本，便于输入校验；非现金时自动等于应收）</summary>
    public string PaidText
    {
        get => _paidText;
        set
        {
            if (!SetProperty(ref _paidText, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(PaidAmount));
            RaisePropertyChanged(nameof(Change));
            CheckoutCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>结算进行中（防止重复点击）</summary>
    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (SetProperty(ref _isProcessing, value))
            {
                CheckoutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>应收合计（取自购物车汇总缓存）</summary>
    public decimal Total => _summary.Total;

    /// <summary>商品总件数（取自购物车汇总缓存）</summary>
    public int ItemCount => _summary.ItemCount;

    /// <summary>商品种类数（取自购物车汇总缓存）</summary>
    public int KindCount => _summary.KindCount;

    /// <summary>购物车是否为空（取自购物车汇总缓存）</summary>
    public bool HasItems => _summary.KindCount > 0;

    /// <summary>是否为现金支付（只有现金才需要输入实收与计算找零）</summary>
    public bool IsCashPayment => PaymentMethod is PaymentMethod.Cash;

    /// <summary>实收金额：现金取输入值，其他方式视为刚好收齐</summary>
    public decimal PaidAmount => IsCashPayment
        ? (decimal.TryParse(PaidText, out var value) ? value : 0m)
        : Total;

    /// <summary>找零</summary>
    public decimal Change => Math.Max(PaidAmount - Total, 0m);

    /// <summary>分类栏/搜索无结果时展示空态</summary>
    public bool HasProducts => _products.Count > 0;

    /// <summary>当前结果是否因上限被截断（还有更多未返回）</summary>
    public bool HasMoreProducts => _hasMoreProducts;

    /// <summary>当前过滤后的商品数（底部统计用）</summary>
    public int ProductCount => _products.Count;

    /// <summary>底部统计文案：结果被截断时引导用户用关键字缩小范围。</summary>
    public string ProductSummary
    {
        get
        {
            if (!_hasMoreProducts)
            {
                return $"当前分类共 {_products.Count:N0} 个商品 · 数据来自 Data/products.md";
            }

            // "全部"分类的截断是产品取舍（10w 条滚动无意义），这里给出总量提示；
            // 分类内截断则只提示缩小范围，避免误报"总量"。
            return IsAllCategorySelected
                ? $"已显示前 {_products.Count:N0} 个商品 · 共 {_productQuery.TotalCount:N0} 个，请输入关键字缩小范围"
                : $"已显示前 {_products.Count:N0} 个商品，请输入关键字缩小范围";
        }
    }

    /// <summary>是否显示找零提示（由设置页控制）</summary>
    public bool ShowChangeHint => _settings.Current.ShowChangeHint;

    #endregion

    #region 命令

    /// <summary>点击商品卡片加入购物车</summary>
    public DelegateCommand<Product> AddProductCommand
        => _addProductCommand ??= new DelegateCommand<Product>(AddProduct);

    /// <summary>购物车 +（受库存上限约束）</summary>
    public DelegateCommand<CartItem> IncreaseQuantityCommand
        => _increaseQuantityCommand ??= new DelegateCommand<CartItem>(IncreaseQuantity);

    /// <summary>购物车 -（数量归零自动移除）</summary>
    public DelegateCommand<CartItem> DecreaseQuantityCommand
        => _decreaseQuantityCommand ??= new DelegateCommand<CartItem>(DecreaseQuantity);

    /// <summary>移除单个商品</summary>
    public DelegateCommand<CartItem> RemoveItemCommand
        => _removeItemCommand ??= new DelegateCommand<CartItem>(RemoveItem);

    /// <summary>清空购物车（需确认）</summary>
    public DelegateCommand ClearCartCommand
        => _clearCartCommand ??= new DelegateCommand(ClearCart);

    /// <summary>手动录入/扫码框提交（回车触发）</summary>
    public DelegateCommand AddByBarcodeCommand
        => _addByBarcodeCommand ??= new DelegateCommand(AddByBarcode);

    /// <summary>触发串口扫码枪扫一次条码</summary>
    public AsyncDelegateCommand ScanCommand
        => _scanCommand ??= new AsyncDelegateCommand(ScanAsync);

    /// <summary>现金"收整"：把实收金额设置为应收金额</summary>
    public DelegateCommand FillExactAmountCommand
        => _fillExactAmountCommand ??= new DelegateCommand(FillExactAmount);

    /// <summary>
    /// 结算：校验金额 → 推送客显屏 → 生成订单 → 打印小票 / 开钱箱 / 云端上报。
    /// 可用性由 <see cref="CanCheckout"/> 决定；异步命令在执行期间自动禁用，防止重复提交。
    /// </summary>
    public AsyncDelegateCommand CheckoutCommand
        => _checkoutCommand ??= new AsyncDelegateCommand(CheckoutAsync, CanCheckout);

    #endregion

    #region 命令实现

    private void AddProduct(Product product)
    {
        if (product is null)
        {
            return;
        }

        // 零库存且未开启"允许超卖"时不可加购。
        // 这里先拦一道是为了给出明确的用户提示（服务内部的拒绝是静默的），
        // 同时也保证即使将来有别的调用方绕过 UI，规则仍然由 CartLimits 统一兜底。
        if (!CartLimits.CanAdd(product, _settings.Current.AllowOversell))
        {
            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                $"{product.Name} 当前无库存，无法加入购物车",
                StatusLevel.Warning));
            return;
        }

        _cart.Add(product);
        NotifyCartChanged();
    }

    private void IncreaseQuantity(CartItem item)
    {
        if (item is not null && !_cart.Increase(item))
        {
            // 上限取自行项（已按"是否允许超卖"换算）：不允许超卖时等于库存，
            // 允许超卖时为 Unlimited，此时不会走到这里。
            var limitText = item.MaxQuantity == CartLimits.Unlimited
                ? "库存上限"
                : $"库存上限 {item.MaxQuantity}";

            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                $"{item.Name} 已达{limitText}",
                StatusLevel.Warning));
        }

        NotifyCartChanged();
    }

    private void DecreaseQuantity(CartItem item)
    {
        if (item is null)
        {
            return;
        }

        _cart.Decrease(item);
        NotifyCartChanged();
    }

    private void RemoveItem(CartItem item)
    {
        if (item is null)
        {
            return;
        }

        _cart.Remove(item);
        NotifyCartChanged();
    }

    private void ClearCart()
    {
        if (_cart.Items.Count == 0)
        {
            return;
        }

        if (_dialogs.Confirm($"确定要清空购物车中的 {_cart.KindCount} 种商品吗？", "清空购物车"))
        {
            _cart.Clear();
            NotifyCartChanged();
        }
    }

    private void AddByBarcode()
    {
        var code = BarcodeInput.Trim();
        if (code.Length == 0)
        {
            return;
        }

        BarcodeInput = string.Empty;
        AddByBarcodeCore(code);
    }

    /// <summary>触发串口扫码枪扫一次条码（带重入保护与异常兜底）。</summary>
    private async Task ScanAsync()
    {
        if (_isScanning)
        {
            return;
        }

        _isScanning = true;

        try
        {
            var sent = await _protocols.RequestBarcodeScanAsync();

            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                sent ? "已向扫码枪下发扫码指令…" : "扫码枪未连接，请先到「设备监控」页连接串口设备",
                sent ? StatusLevel.Info : StatusLevel.Warning));
        }
        catch (Exception ex)
        {
            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                $"下发扫码指令失败：{ex.Message}",
                StatusLevel.Warning));
        }
        finally
        {
            _isScanning = false;
        }
    }

    private void FillExactAmount() => PaidText = Total.ToString("F2");

    private async Task CheckoutAsync()
    {
        var total = Total;
        var paid = PaidAmount;

        if (IsCashPayment && paid < total)
        {
            _dialogs.ShowWarning($"实收金额不足，还差 ¥{total - paid:F2}", "金额校验");
            return;
        }

        IsProcessing = true;

        try
        {
            // 1) 客显屏显示应收金额（必须在生成订单前，让顾客先看到金额）
            var displayPushed = await _protocols.PushDisplayAmountAsync(total);

            // 2) 生成订单并落库（内部会清空购物车 → 广播 CartChangedEvent）
            var order = _orders.Checkout(PaymentMethod, paid);
            if (order is null)
            {
                return;
            }

            // 3) 打印小票 / 现金时开钱箱 / 云端上报：交给结算编排器并行执行
            var followUp = await _checkoutCoordinator.CompleteAsync(order, IsCashPayment);

            // 4) 广播支付完成 + 全局提示
            Publish<PaymentCompletedEvent, Order>(order);
            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                $"订单 {order.OrderNo} 结算成功，收款 ¥{order.Total:F2}",
                StatusLevel.Success));

            _dialogs.ShowInfo(
                $"收款成功！\n\n" +
                $"订单号：{order.OrderNo}\n" +
                $"商品件数：{order.ItemCount} 件\n" +
                $"应收：¥{order.Total:F2}\n" +
                $"实收：¥{order.Paid:F2}\n" +
                $"找零：¥{order.Change:F2}\n\n" +
                CheckoutCoordinator.BuildDeviceNote(displayPushed, followUp),
                "结算完成");

            PaidText = string.Empty;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"结算失败：{ex.Message}", "结算异常");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>结算按钮可用性：有商品、未在处理中、现金时实收足够</summary>
    private bool CanCheckout()
        => HasItems
           && !IsProcessing
           && (!IsCashPayment || PaidAmount >= Total);

    #endregion

    #region 事件订阅

    private void SubscribeEvents()
    {
        Subscribe<CartChangedEvent, CartSummary>(OnCartChanged);
        Subscribe<BarcodeScannedEvent, string>(OnBarcodeScanned);
        Subscribe<SettingsChangedEvent, PosSettings>(OnSettingsChanged);
        Subscribe<DataLoadedEvent, DataLoaded>(OnDataLoaded);
        Subscribe<NavigateEvent, string>(OnPageNavigated);
    }

    /// <summary>购物车变更（由服务广播）→ 用事件载荷携带的汇总刷新金额与按钮状态。</summary>
    /// <remarks>
    /// 直接使用载荷里的 <see cref="CartSummary"/>，而不是回读购物车服务：
    /// 载荷本身就是那次变更的快照，回读等于把同一份数据算两遍，
    /// 并且在"服务已再次变更"时还可能读到比事件更新的状态。
    /// </remarks>
    private void OnCartChanged(CartSummary summary) => ApplyCartSummary(summary);

    /// <summary>扫码枪扫到条码 → 自动加购并跳回收银台。</summary>
    private void OnBarcodeScanned(string barcode)
    {
        AddByBarcodeCore(barcode);
        Publish<NavigateEvent, string>(NavigateEvent.Pages.Cashier);
    }

    /// <summary>设置保存后同步：找零提示开关、行项可加购上限与默认支付方式。</summary>
    private void OnSettingsChanged(PosSettings settings)
    {
        RaisePropertyChanged(nameof(ShowChangeHint));

        // 「允许超卖」变化会改变每行的可加购上限（决定"+"按钮是否可用），
        // 需要同步到已存在的行项，否则界面上的可用状态会停留在旧设置。
        foreach (var item in _cart.Items)
        {
            item.MaxQuantity = CartLimits.MaxQuantityFor(item.Product, settings.AllowOversell);
        }

        // 购物车为空时，把默认支付方式同步到结算面板
        if (_cart.Items.Count == 0)
        {
            PaymentMethod = settings.DefaultPayment;
        }
    }

    /// <summary>Markdown 数据加载完成 → 重建分类并刷新商品视图（启动时窗口先于数据出现）。</summary>
    private void OnDataLoaded(DataLoaded payload)
    {
        RebuildCategories();
        _ = RefreshProductsAsync();

        if (!payload.Succeeded)
        {
            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                $"商品数据加载异常：{string.Join("；", payload.Errors)}",
                StatusLevel.Warning));
        }
    }

    /// <summary>回到收银台时按最新设置重置默认支付方式（空车时）。</summary>
    private void OnPageNavigated(string pageKey)
    {
        if (pageKey == NavigateEvent.Pages.Cashier)
        {
            ApplyDefaultPaymentIfCartEmpty();
        }
    }

    #endregion

    #region 私有方法

    // 分类全选标识统一取自目录契约，避免依赖具体仓储的静态成员
    private string CurrentCategoryId => SelectedCategory?.Id ?? _catalog.AllCategoryId;

    /// <summary>当前是否选中"全部"分类（忽略大小写）。</summary>
    private bool IsAllCategorySelected
        => string.Equals(CurrentCategoryId, _catalog.AllCategoryId, StringComparison.OrdinalIgnoreCase);

    /// <summary>空车时把默认支付方式同步到结算面板。</summary>
    private void ApplyDefaultPaymentIfCartEmpty()
    {
        if (_cart.Items.Count == 0)
        {
            PaymentMethod = _settings.Current.DefaultPayment;
        }
    }

    /// <summary>数据（重新）加载完成后重建分类标签，并尽量保持原选中分类。</summary>
    private void RebuildCategories()
    {
        var currentId = CurrentCategoryId;

        Categories.Clear();
        foreach (var category in _catalog.CategoriesWithAll)
        {
            Categories.Add(category);
        }

        // 原选中项属于重建前的实例，需要指向新列表中同 Id 的项（找不到则回到"全部"）
        SelectedCategory = Categories.FirstOrDefault(c =>
                               string.Equals(c.Id, currentId, StringComparison.OrdinalIgnoreCase))
                           ?? Categories.FirstOrDefault();
    }

    /// <summary>
    /// 重新查询商品并刷新派生计数。
    /// </summary>
    /// <remarks>
    /// 与原 <c>ListCollectionView.Refresh</c> 的差异：
    /// <list type="bullet">
    /// <item>过滤下沉到 <see cref="IProductQuery"/>（分类走索引切片，关键字在大数据量时后台执行），
    /// UI 线程不再承担 10w 项逐个谓词回调；</item>
    /// <item>新查询会取消尚未完成的旧查询，避免"慢的旧结果"覆盖"快的新结果"；</item>
    /// <item>结果一次性 <see cref="RangeObservableCollection{T}.ReplaceAll"/>，只发一次 Reset。</item>
    /// </list>
    /// </remarks>
    private async Task RefreshProductsAsync()
    {
        var cts = new CancellationTokenSource();
        var previous = _queryCts;
        _queryCts = cts;

        // 只 Cancel 不 Dispose：旧查询由它自己的 finally 释放（避免与在途任务竞争释放）
        previous?.Cancel();

        var request = new ProductQueryRequest(
            CurrentCategoryId,
            _appliedKeyword,
            Offset: 0,
            Limit: IsAllCategorySelected && _appliedKeyword.Length == 0
                ? ProductQueryService.AllCategoryDefaultLimit
                : ProductQueryService.MaxResultLimit);

        try
        {
            var result = await _productQuery.QueryAsync(request, cts.Token);

            // 已被更新的查询取代：丢弃过期结果，避免回退到旧数据
            if (!ReferenceEquals(_queryCts, cts))
            {
                return;
            }

            _hasMoreProducts = result.HasMore;
            _products.ReplaceAll(result.Items);

            RaisePropertyChanged(nameof(ProductCount));
            RaisePropertyChanged(nameof(HasProducts));
            RaisePropertyChanged(nameof(HasMoreProducts));
            RaisePropertyChanged(nameof(ProductSummary));
        }
        catch (OperationCanceledException)
        {
            // 被后续查询取消：属正常流程，静默忽略
        }
        finally
        {
            if (ReferenceEquals(_queryCts, cts))
            {
                _queryCts = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>加入购物车并给出全局提示。</summary>
    private void AddByBarcodeCore(string barcode)
    {
        if (_cart.TryAddByBarcode(barcode, out var product, out var text))
        {
            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(
                $"扫码成功：{product!.Name} 已加入购物车",
                StatusLevel.Success));
        }
        else
        {
            Publish<StatusNotificationEvent, StatusNotification>(new StatusNotification(text, StatusLevel.Warning));
        }

        NotifyCartChanged();
    }

    /// <summary>购物车变更后统一刷新：从服务取最新汇总并集中通知。</summary>
    private void NotifyCartChanged() => ApplyCartSummary(_cart.Summary);

    /// <summary>
    /// 应用购物车汇总：更新缓存并通知全部派生属性。
    /// </summary>
    /// <remarks>
    /// 派生属性的通知集中在这一个入口，避免散落在各处调用时漏通知某一项
    ///（例如只通知了 Total 却忘记 Change，界面就会显示旧找零）。
    /// </remarks>
    private void ApplyCartSummary(CartSummary summary)
    {
        _summary = summary;

        RaisePropertyChanged(nameof(Total));
        RaisePropertyChanged(nameof(ItemCount));
        RaisePropertyChanged(nameof(KindCount));
        RaisePropertyChanged(nameof(HasItems));
        RaisePropertyChanged(nameof(PaidAmount));
        RaisePropertyChanged(nameof(Change));
        CheckoutCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region 生命周期

    /// <summary>释放资源：退订全局事件、停止输入防抖计时器、取消在途的商品查询。</summary>
    protected override void DisposeCore()
    {
        _searchDebounceTimer.Stop();
        _queryCts?.Cancel();

        base.DisposeCore();
    }

    #endregion
}
