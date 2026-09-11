using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Common.Collection;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Protocols;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>
/// 收银台 ViewModel：商品浏览 → 加购 → 结算支付 → 驱动外设。
///
/// 覆盖的 CommunityToolkit 知识点：
/// - <see cref="ObservableRecipient"/> + <c>IRecipient&lt;T&gt;</c>：接收扫码/购物车消息；
/// - <c>[ObservableProperty]</c> + <c>[NotifyPropertyChangedFor]</c>：金额联动；
/// - <c>[NotifyCanExecuteChangedFor]</c>：实收金额/支付方式变化自动刷新结算按钮可用性；
/// - <c>[RelayCommand]</c>（同步/异步/带参/CanExecute）与 <c>OnXxxChanged</c> 分部钩子。
/// </summary>
public partial class CashierViewModel
    : ObservableRecipient,
      IRecipient<BarcodeScannedMessage>,
      IRecipient<CartChangedMessage>,
      IRecipient<SettingsChangedMessage>,
      IRecipient<DataLoadedMessage>
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
    /// 购物车汇总缓存：由 <see cref="CartChangedMessage"/> 携带的数据刷新，
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

    public CashierViewModel(
        IProductCatalog catalog,
        IProductQuery productQuery,
        ICartService cart,
        IOrderService orders,
        IDialogService dialogs,
        IProtocolManager protocols,
        ISettingsService settings,
        CheckoutCoordinator checkoutCoordinator,
        IMessenger messenger) : base(messenger)
    {
        _catalog = catalog;
        _productQuery = productQuery;
        _cart = cart;
        _orders = orders;
        _dialogs = dialogs;
        _protocols = protocols;
        _settings = settings;
        _checkoutCoordinator = checkoutCoordinator;

        // 初始汇总：构造后即可直接绑定，无需等待第一次购物车变更消息
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

        // 收银台需要随时响应扫码枪，因此常驻激活
        IsActive = true;

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
    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    /// <summary>商品搜索关键字（名称/编码/条码）</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>扫码/手动录入的条码</summary>
    [ObservableProperty]
    private string _barcodeInput = string.Empty;

    /// <summary>支付方式：变化时刷新按钮可用性与找零显示</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCashPayment))]
    [NotifyPropertyChangedFor(nameof(Change))]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;

    /// <summary>现金实收金额（文本，便于输入校验；非现金时自动等于应收）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaidAmount))]
    [NotifyPropertyChangedFor(nameof(Change))]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    private string _paidText = string.Empty;

    /// <summary>结算进行中（防止重复点击）</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    private bool _isProcessing;

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
    [RelayCommand]
    private void AddProduct(Product? product)
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
            Messenger.Send(new StatusNotificationMessage(
                $"{product.Name} 当前无库存，无法加入购物车",
                StatusNotificationMessage.StatusLevel.Warning));
            return;
        }

        _cart.Add(product);
        NotifyCartChanged();
    }

    /// <summary>购物车 +（受库存上限约束）</summary>
    [RelayCommand]
    private void IncreaseQuantity(CartItem? item)
    {
        if (item is not null && !_cart.Increase(item))
        {
            // 上限取自行项（已按"是否允许超卖"换算）：不允许超卖时等于库存，
            // 允许超卖时为 Unlimited，此时不会走到这里。
            var limitText = item.MaxQuantity == CartLimits.Unlimited
                ? "库存上限"
                : $"库存上限 {item.MaxQuantity}";

            Messenger.Send(new StatusNotificationMessage(
                $"{item.Name} 已达{limitText}",
                StatusNotificationMessage.StatusLevel.Warning));
        }

        NotifyCartChanged();
    }

    /// <summary>购物车 -（数量归零自动移除）</summary>
    [RelayCommand]
    private void DecreaseQuantity(CartItem? item)
    {
        if (item is null)
        {
            return;
        }

        _cart.Decrease(item);
        NotifyCartChanged();
    }

    /// <summary>移除单个商品</summary>
    [RelayCommand]
    private void RemoveItem(CartItem? item)
    {
        if (item is null)
        {
            return;
        }

        _cart.Remove(item);
        NotifyCartChanged();
    }

    /// <summary>清空购物车（需确认）</summary>
    [RelayCommand]
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

    /// <summary>手动录入/扫码框提交（回车触发）</summary>
    [RelayCommand]
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
    [RelayCommand]
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

            Messenger.Send(new StatusNotificationMessage(
                sent ? "已向扫码枪下发扫码指令…" : "扫码枪未连接，请先到「设备监控」页连接串口设备",
                sent ? StatusNotificationMessage.StatusLevel.Info : StatusNotificationMessage.StatusLevel.Warning));
        }
        catch (Exception ex)
        {
            Messenger.Send(new StatusNotificationMessage(
                $"下发扫码指令失败：{ex.Message}",
                StatusNotificationMessage.StatusLevel.Warning));
        }
        finally
        {
            _isScanning = false;
        }
    }

    /// <summary>现金"收整"：把实收金额设置为应收金额</summary>
    [RelayCommand]
    private void FillExactAmount() => PaidText = Total.ToString("F2");

    /// <summary>
    /// 结算：校验金额 → 推送客显屏 → 生成订单 → 打印小票 / 开钱箱 / 云端上报。
    /// 可执行条件由 <see cref="CanCheckout"/> 决定（源生成器生成 CheckoutCommand）。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCheckout))]
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

            // 2) 生成订单并落库（内部会清空购物车 → 广播 CartChangedMessage）
            var order = _orders.Checkout(PaymentMethod, paid);
            if (order is null)
            {
                return;
            }

            // 3) 打印小票 / 现金时开钱箱 / 云端上报：交给结算编排器并行执行
            var followUp = await _checkoutCoordinator.CompleteAsync(order, IsCashPayment);

            // 4) 广播支付完成 + 全局提示
            Messenger.Send(new PaymentCompletedMessage(order));
            Messenger.Send(new StatusNotificationMessage(
                $"订单 {order.OrderNo} 结算成功，收款 ¥{order.Total:F2}",
                StatusNotificationMessage.StatusLevel.Success));

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

    #region 属性变化钩子（源生成器分部方法）

    partial void OnSelectedCategoryChanged(ProductCategory? value)
    {
        // 分类切换即时生效（无需防抖）
        _appliedKeyword = SearchText.Trim();
        _ = RefreshProductsAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        // 输入防抖：停止输入后再过滤，避免每敲一个字符就触发全量重排
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    /// <summary>支付方式切换：非现金直接把实收置为应收，避免用户重复输入。</summary>
    partial void OnPaymentMethodChanged(PaymentMethod value)
    {
        if (value is not PaymentMethod.Cash)
        {
            PaidText = string.Empty;
        }
    }

    #endregion

    #region 消息接收

    /// <summary>购物车变更（由服务广播）→ 用消息携带的汇总刷新金额与按钮状态。</summary>
    /// <remarks>
    /// 直接使用消息里的 <see cref="CartSummary"/>，而不是回读购物车服务：
    /// 消息本身就是那次变更的快照，回读等于把同一份数据算两遍，
    /// 并且在"服务已再次变更"时还可能读到比消息更新的状态。
    /// </remarks>
    public void Receive(CartChangedMessage message) => ApplyCartSummary(message.Value);

    /// <summary>扫码枪扫到条码 → 自动加购并跳回收银台。</summary>
    public void Receive(BarcodeScannedMessage message)
    {
        AddByBarcodeCore(message.Value);
        Messenger.Send(new NavigateMessage(NavigateMessage.Pages.Cashier));
    }

    /// <summary>设置保存后同步：找零提示开关、行项可加购上限与默认支付方式。</summary>
    public void Receive(SettingsChangedMessage message)
    {
        OnPropertyChanged(nameof(ShowChangeHint));

        // 「允许超卖」变化会改变每行的可加购上限（决定"+"按钮是否可用），
        // 需要同步到已存在的行项，否则界面上的可用状态会停留在旧设置。
        foreach (var item in _cart.Items)
        {
            item.MaxQuantity = CartLimits.MaxQuantityFor(item.Product, message.Value.AllowOversell);
        }

        // 购物车为空时，把默认支付方式同步到结算面板
        if (_cart.Items.Count == 0)
        {
            PaymentMethod = message.Value.DefaultPayment;
        }
    }

    /// <summary>Markdown 数据加载完成 → 重建分类并刷新商品视图（启动时窗口先于数据出现）。</summary>
    public void Receive(DataLoadedMessage message)
    {
        RebuildCategories();
        _ = RefreshProductsAsync();

        if (!message.Value.Succeeded)
        {
            Messenger.Send(new StatusNotificationMessage(
                $"商品数据加载异常：{string.Join("；", message.Value.Errors)}",
                StatusNotificationMessage.StatusLevel.Warning));
        }
    }

    /// <summary>回到收银台时按最新设置重置默认支付方式（空车时）。</summary>
    protected override void OnActivated()
    {
        base.OnActivated();

        if (_cart.Items.Count == 0)
        {
            PaymentMethod = _settings.Current.DefaultPayment;
        }
    }

    #endregion

    #region 私有方法

    // 分类全选标识统一取自目录契约，避免依赖具体仓储的静态成员
    private string CurrentCategoryId => SelectedCategory?.Id ?? _catalog.AllCategoryId;

    /// <summary>当前是否选中"全部"分类（忽略大小写）。</summary>
    private bool IsAllCategorySelected
        => string.Equals(CurrentCategoryId, _catalog.AllCategoryId, StringComparison.OrdinalIgnoreCase);

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

            OnPropertyChanged(nameof(ProductCount));
            OnPropertyChanged(nameof(HasProducts));
            OnPropertyChanged(nameof(HasMoreProducts));
            OnPropertyChanged(nameof(ProductSummary));
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
            Messenger.Send(new StatusNotificationMessage(
                $"扫码成功：{product!.Name} 已加入购物车",
                StatusNotificationMessage.StatusLevel.Success));
        }
        else
        {
            Messenger.Send(new StatusNotificationMessage(text, StatusNotificationMessage.StatusLevel.Warning));
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

        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(KindCount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(PaidAmount));
        OnPropertyChanged(nameof(Change));
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    #endregion
}
