# 02 · CommunityToolkit.Mvvm 知识点逐个拆

> 《我是如何构建 WPF 项目》系列 · 第 02 篇
> 上一篇：[项目全貌与一次扫码的完整旅程](01-项目全貌与一次扫码的完整旅程.md) ｜ 下一篇：[这个项目最值钱的六个设计](03-这个项目最值钱的六个设计.md)

---

上一篇是"从整体看"，这一篇换成"从零件看"。每个知识点我都用同一套模板讲：

> **它是什么**（小白版解释） → **项目里怎么用**（真实代码点） → **为什么这么用**（取舍）

---

## 一、MVVM 到底是谁管什么

先把这三个字母拆开：

| 角色 | 在项目里是谁 | 职责 | 绝对不能干的事 |
| --- | --- | --- | --- |
| **M**odel | `Models/Product.cs`、`Order.cs` | 描述数据长什么样 | 不该知道界面的存在 |
| **V**iew | `Views/*.xaml` | 描述界面长什么样 | 不该写业务逻辑（不查数据库、不算钱） |
| **V**iew**M**odel | `ViewModels/*.cs` | 描述"界面当前的状态"和"能做什么操作" | 不该持有控件、不该 `new Window()` |

一句更土的话总结：**View 是"脸"，ViewModel 是"大脑"，Model 是"身体的信息"。大脑指挥身体，脸反映大脑，脸和大脑之间靠"数据绑定"这根神经连着。**

有个特别具体的判断标准，你可以拿它检查自己的代码：

> **ViewModel 里出现 `MessageBox`、`File.WriteAllText`、`new SerialPort()` 这种"只有真机上才有的东西"，就是越界了。**

项目的做法是把这些都抽成接口：`IDialogService`（弹窗）、`IOrderService`（订单）、`IProtocolManager`（硬件），ViewModel 只依赖接口。

---

## 二、`ObservableObject` 与 `[ObservableProperty]`：通知的起点

### 2.1 它是什么

WPF 的数据绑定要能"跟着变"，前提是数据源会**主动喊一嗓子**："我变了！"。这个"喊一嗓子"在 .NET 里叫 `INotifyPropertyChanged` 接口，就一个事件：

```csharp
public event PropertyChangedEventHandler? PropertyChanged;
```

手写的话，每个属性都要写成这样（我早期真的这么写过，写吐了）：

```csharp
private string _name;
public string Name
{
    get => _name;
    set
    {
        if (_name != value)
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
}
```

`CommunityToolkit.Mvvm` 的 `ObservableObject` 帮你把这段模板代码变成了**一个特性**：

```csharp
// CommunityToolkitDemo/Models/CartItem.cs:10-26
public partial class CartItem : ObservableObject
{
    /// <summary>数量：源生成器会生成 Quantity 属性与 QuantityChanged 通知</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(IsMaxQuantity))]
    private int _quantity;
    // ...
}
```

**这里有三个必须记住的"坑"：**

1. **类必须是 `partial`。** 源生成器要在另一个文件里补半个类，不写 `partial` 编译不过。
2. **写的是字段，不是属性。** 你写 `private int _quantity;`（小写、带下划线），编译器生成的是 `public int Quantity { get; set; }`。
3. **生成发生在编译期，IDE 里可能"看不到"。** 有时候需要在 IDE 里重新生成一次（或者放心大胆地用，编译过就没问题）。

### 2.2 一个"手写属性"的例子：什么时候不能用源生成器

```csharp
// CommunityToolkitDemo/Models/CartItem.cs:28-63
private int _maxQuantity = int.MaxValue;

/// <summary>
/// 本行可加入的数量上限，由购物车服务按"是否允许超卖"写入。
/// </summary>
/// <remarks>
/// 把上限放在行项上而不是每次用 <c>Product.Stock</c> 现算，是为了让界面绑定
/// （如"+"按钮的可用状态）与服务的实际约束<b>同源</b>：
/// 若直接用 Stock 判断，在"允许超卖"时服务其实不限量、界面却会显示已达上限，
/// 两者行为不一致。<br/>
/// setter 为 internal：仅同程序集的购物车服务需要修改它。
/// </remarks>
public int MaxQuantity
{
    get => _maxQuantity;
    internal set
    {
        if (SetProperty(ref _maxQuantity, value))
        {
            OnPropertyChanged(nameof(IsMaxQuantity));
        }
    }
}

/// <summary>小计（只读派生属性）</summary>
public decimal Subtotal => Product.Price * Quantity;

/// <summary>数量已达可加购上限（与购物车服务的约束同源，供"+"按钮绑定）。</summary>
public bool IsMaxQuantity => Quantity >= MaxQuantity;
```

**为什么 `MaxQuantity` 是手写的属性，而不是也用 `[ObservableProperty]`？**

因为它的 setter 需要是 `internal`（只允许购物车服务改），而 `[ObservableProperty]` 生成的是 `public set`。这时候就要退回到手写，用基类提供的两个小工具：

- `SetProperty(ref _maxQuantity, value)`：**"值真的变了才通知，并返回 true"**（这就是那个 `if (_name != value)` 判断）；
- `OnPropertyChanged(nameof(IsMaxQuantity))`：手动通知派生属性。

**记住 `SetProperty` 的返回值用法**——它是"变更 + 通知 + 返回是否真变了"三合一，比手写 `if` 更不容易漏。

---

## 三、`[NotifyPropertyChangedFor]`：派生属性自动联动

**问题场景**：`Subtotal = Price × Quantity`。当 `Quantity` 变了，界面上显示 `Subtotal` 的那个 `TextBlock` 不会自动刷新——因为它只在 `Subtotal` 发出通知时才会重新取值，而 `Subtotal` 是个只读计算属性，它自己不喊。

**土办法**：在 `quantity` 的 setter 里手动 `OnPropertyChanged(nameof(Subtotal))`。

**Toolkit 的办法**：贴个标签。

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(Subtotal))]
[NotifyPropertyChangedFor(nameof(IsMaxQuantity))]
private int _quantity;
```

意思是：**`Quantity` 一变，帮我把 `Subtotal` 和 `IsMaxQuantity` 也一起通知一遍。**

在收银台 ViewModel 里，这个用法更密集：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:151-168
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
```

你可以把它读成一句话：**"实收金额一变，`PaidAmount`、`Change` 要重新算，并且结算按钮该重新判断能不能点了。"**

而这三个派生属性全都是只读的表达式：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:182-191
/// <summary>是否为现金支付（只有现金才需要输入实收与计算找零）</summary>
public bool IsCashPayment => PaymentMethod is PaymentMethod.Cash;

/// <summary>实收金额：现金取输入值，其他方式视为刚好收齐</summary>
public decimal PaidAmount => IsCashPayment
    ? (decimal.TryParse(PaidText, out var value) ? value : 0m)
    : Total;

/// <summary>找零</summary>
public decimal Change => Math.Max(PaidAmount - Total, 0m);
```

**注意：派生属性只写 `get`，永远不要在 setter 里改别的属性。** 一旦派生属性有了副作用，"谁改谁"就理不清了。

---

## 四、`[RelayCommand]`：把方法变成按钮

### 4.1 它是什么

WPF 里按钮的 `Command` 属性要的类型是 `ICommand`，它有 `Execute` 和 `CanExecute` 两个方法。手写一个 `ICommand` 实现要 40 行——这是 WPF 最劝退的地方之一。

`[RelayCommand]` 让你**直接写方法**：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:227-249
/// <summary>点击商品卡片加入购物车</summary>
[RelayCommand]
private void AddProduct(Product? product)
{
    if (product is null)
    {
        return;
    }

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
```

编译后会生成 `AddProductCommand`（属性），XAML 里写：

```xml
<Button Command="{Binding AddProductCommand}" CommandParameter="{Binding}" />
```

**命名规则要记住**（我踩过的坑）：方法名 `AddProduct` → 命令名 `AddProductCommand`；异步方法 `CheckoutAsync` 生成的命令名是 **`CheckoutCommand`**（去掉 `Async`）——这个不一致坑过很多人。

### 4.2 项目里用到的四种形态

```csharp
// 1. 无参同步：CommunityToolkitDemo/ViewModels/CashierViewModel.cs:298-299
[RelayCommand]
private void ClearCart() { /* ... */ }

// 2. 带参同步：CommunityToolkitDemo/ViewModels/CashierViewModel.cs:228-229
[RelayCommand]
private void AddProduct(Product? product) { /* ... */ }

// 3. 异步（生成 AsyncRelayCommand）：CommunityToolkitDemo/ViewModels/CashierViewModel.cs:328-329
[RelayCommand]
private async Task ScanAsync() { /* ... */ }

// 4. 带 CanExecute：CommunityToolkitDemo/ViewModels/CashierViewModel.cs:366-367
[RelayCommand(CanExecute = nameof(CanCheckout))]
private async Task CheckoutAsync() { /* ... */ }
```

### 4.3 `CanExecute`：按钮"该亮才亮"

这是 MVVM 里"我最喜欢的部分"。**按钮的可用性不由界面控制，而由业务规则控制。**

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:366-367
[RelayCommand(CanExecute = nameof(CanCheckout))]
private async Task CheckoutAsync() { /* ... */ }

// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:423-427
/// <summary>结算按钮可用性：有商品、未在处理中、现金时实收足够</summary>
private bool CanCheckout()
    => HasItems
       && !IsProcessing
       && (!IsCashPayment || PaidAmount >= Total);
```

`CanExecute` 要求的是一个**返回 `bool` 的无参方法**。上面这条规则读起来就是：

> 购物车里有东西 **且** 当前没有正在结算 **且**（不是现金 **或者** 实收金额够）

**那么"什么时候重新判断"呢？**

- 贴了 `[NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]` 的属性变化时会自动重新判断；
- 但像 `HasItems`（它由 `_summary` 缓存决定，没有对应字段）这种，就得手动喊一声：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:629-647
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
```

**这段注释值得抄下来贴在自己项目里**：派生属性的通知一定要集中在一个入口，否则"改了 `Total` 忘了 `Change`"这种 bug 会反复出现，而且极难排查（现象是"找零显示的是上一次的金额"）。

---

## 五、`OnXxxChanged`：属性变化时的钩子

`[ObservableProperty]` 除了生成属性，还会生成一个**分部方法（partial method）**给你留后门：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:431-454
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
```

命名规则：字段 `_searchText` → 属性 `SearchText` → 钩子 `OnSearchTextChanged`。

**用法心法**：钩子里只干"轻活"（起个计时器、清个状态），重活（查数据库、算钱）要小心——因为它在属性赋值时**同步执行**，写得太重会把 UI 卡住。

这里 `OnSearchTextChanged` 的写法是个经典优化：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:106-113
// 搜索输入防抖：停止输入 250ms 后才真正查询
_searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
_searchDebounceTimer.Tick += (_, _) =>
{
    _searchDebounceTimer.Stop();
    _appliedKeyword = SearchText.Trim();
    _ = RefreshProductsAsync();
};
```

**防抖（Debounce）是什么？** 用户打字"农夫山泉"要敲 5 次，如果每敲一次就查一次，就是 5 次全量过滤。防抖的意思是"**等你停下来 250 毫秒不敲了，我才查一次**"。5 次查询变成 1 次。

这就是"用户感知不到，但性能差别巨大"的优化。（第 04 篇会把这个和虚拟化放一起讲。）

---

## 六、`ObservableRecipient` 与 `IRecipient<T>`：声明式订阅消息

**它是什么？**
`ObservableRecipient` 是 `ObservableObject` 的"加强版"：它既能发通知，又能**接收消息**。

项目里的收银台就继承它：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:24-30
public partial class CashierViewModel
    : ObservableRecipient,
      IRecipient<BarcodeScannedMessage>,
      IRecipient<CartChangedMessage>,
      IRecipient<SettingsChangedMessage>,
      IRecipient<DataLoadedMessage>
```

这行代码的信息量很大：

- 继承 `ObservableRecipient` → 它具备通知能力，且有 `Messenger` 属性和 `IsActive` 开关；
- 实现 `IRecipient<BarcodeScannedMessage>` → **"我要听条码扫描的消息"**；
- 实现 `IRecipient<T>` 只是接口声明，具体行为靠 `Receive` 方法实现。

**但光声明还不够，必须有一步"开机"**：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:115-119
// 收银台需要随时响应扫码枪，因此常驻激活
IsActive = true;

// 构造函数不能 await：首次查询异步发起，结果就绪后由集合通知刷新界面
_ = RefreshProductsAsync();
```

**`IsActive = true` 是这套机制的总开关。** 它的原理是：

- 设为 `true` → 内部调用 `Messenger.RegisterAll(this)`，把本类实现的所有 `IRecipient<T>` 注册进消息总线；
- 设为 `false` → 内部调用 `Messenger.UnregisterAll(this)`，全部退订。

这个"自动注册 / 自动注销"是 `ObservableRecipient` 最值钱的地方——**你不需要手工管理订阅的生命周期**。

---

## 七、`OnActivated` / `OnDeactivated`：页面级的订阅开关

设备监控页是"高频刷新"的重灾区：5 个模拟驱动在后台不停地产生报文。如果你离开这个页面了，界面还在后台刷，那就是纯粹的浪费。

项目用 `OnActivated` / `OnDeactivated` 解决了这个问题：

```csharp
// CommunityToolkitDemo/ViewModels/DeviceMonitorViewModel.cs:185-213
#region 生命周期（OnActivated / OnDeactivated）

/// <summary>进入页面才订阅驱动事件并启动闪烁计时器。</summary>
protected override void OnActivated()
{
    base.OnActivated();

    _manager.FrameReceived += OnFrameReceived;
    _manager.DriverStateChanged += OnDriverStateChanged;

    SyncCardStates();
    _flashTimer.Start();
    _flushTimer.Start();
}

/// <summary>离开页面立即退订，避免后台报文继续驱动界面刷新。</summary>
protected override void OnDeactivated()
{
    _manager.FrameReceived -= OnFrameReceived;
    _manager.DriverStateChanged -= OnDriverStateChanged;

    _flashTimer.Stop();
    _flushTimer.Stop();
    ClearFlashing();

    base.OnDeactivated();
}

#endregion
```

而"谁调用了 `IsActive = true/false`"在导航逻辑里：

```csharp
// CommunityToolkitDemo/ViewModels/MainWindowViewModel.cs:191-219
/// <summary>源生成器产生的分部钩子：导航项变化即切换页面。</summary>
partial void OnSelectedNavItemChanged(NavItem? value)
{
    if (value is null || !PageMap.TryGetValue(value.Key, out var viewModelType))
    {
        return;
    }

    // 页面 ViewModel 均为单例，切换时保留状态（如购物车）
    if (_pageFactory(viewModelType) is not { } viewModel)
    {
        return;
    }

    // ObservableRecipient 知识点：IsActive 控制消息订阅的注册/注销。
    // 设备监控页会产生大量报文，离开时置为非激活以停止订阅，回到页面再恢复。
    // 注意：仅按"当前/目标实例类型"判断，避免为了激活而解析 DeviceMonitorViewModel 导致提前实例化。
    if (CurrentViewModel is DeviceMonitorViewModel leaving && !ReferenceEquals(leaving, viewModel))
    {
        leaving.IsActive = false;
    }

    if (viewModel is DeviceMonitorViewModel entering)
    {
        entering.IsActive = true;
    }

    CurrentViewModel = viewModel;
}
```

**注意注释里那句"避免为了激活而解析 DeviceMonitorViewModel 导致提前实例化"**——这是一个很隐蔽的坑：如果你写成

```csharp
if (_pageFactory(typeof(DeviceMonitorViewModel)) is DeviceMonitorViewModel vm) vm.IsActive = false;
```

那么**每次导航都会顺手把设备监控页创建出来**（而且它是单例，创建后就一直活着），这跟"按需创建"的初衷正好相反。

正确写法是**判断当前实例的类型**，而不是去解析目标类型。

---

## 八、`ObservableValidator` + DataAnnotations：表单校验

**它是什么？**
设置页有 6 个字段要校验（门店名不能空、电话要合法、小票份数 1~3……）。手写这些判断，就是一堆 `if`。`ObservableValidator` 让你**用特性声明规则**。

```csharp
// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:22-26
public partial class SettingsViewModel : ObservableValidator
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IMessenger _messenger;
```

```csharp
// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:43-76
[ObservableProperty]
[NotifyDataErrorInfo]
[Required(ErrorMessage = "门店名称不能为空")]
[MinLength(2, ErrorMessage = "门店名称至少 2 个字符")]
[MaxLength(20, ErrorMessage = "门店名称最多 20 个字符")]
private string _storeName = string.Empty;

[ObservableProperty]
[NotifyDataErrorInfo]
[Required(ErrorMessage = "联系电话不能为空")]
[RegularExpression(@"^(1[3-9]\d{9}|0\d{2,3}-?\d{7,8})$", ErrorMessage = "请输入有效的手机号或座机号")]
private string _phone = string.Empty;

[ObservableProperty]
[NotifyDataErrorInfo]
[Range(1, 3, ErrorMessage = "小票份数需在 1 ~ 3 之间")]
private int _receiptCopies = 1;
```

**三个关键词：**

- `[NotifyDataErrorInfo]`：让 `[ObservableProperty]` 生成的属性在**值变化时自动触发校验并发出 `ErrorsChanged` 通知**（WPF 的 `Validation.ErrorTemplate` 就是靠它显示红框的）；
- 各种 DataAnnotations 特性：`[Required]`、`[MinLength]`、`[MaxLength]`、`[RegularExpression]`、`[Range]`；
- `ErrorMessage`：**写中文**，因为它会直接显示给用户。

然后校验结果可以拿来干两件事：

```csharp
// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:102-105
/// <summary>错误汇总（无错误时提示校验通过）</summary>
public string ValidationSummary => HasErrors
    ? string.Join("；", GetErrors().Select(e => e.ErrorMessage).Distinct())
    : "所有字段校验通过";

// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:155-156
/// <summary>保存按钮可用性由校验结果决定</summary>
private bool CanSave() => !HasErrors;
```

**"保存按钮的可用性由校验结果决定"**——这就是 `CanExecute` 和表单校验的完美结合：有错就灰着，没错了才能点。

保存前再整体校验一遍（防止有字段没被碰过就没触发过校验）：

```csharp
// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:124-134
/// <summary>保存设置：整体校验通过后写回服务并广播消息。</summary>
[RelayCommand(CanExecute = nameof(CanSave))]
private void Save()
{
    ValidateAllProperties();

    if (HasErrors)
    {
        _dialogs.ShowWarning($"表单存在 {GetErrors().Count()} 项校验错误，请修正后再保存。", "无法保存");
        return;
    }
    // ...
}
```

还有一个**需要手工补的坑**，项目里也写清楚了：

```csharp
// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:34-36
// HasErrors 不是 [ObservableProperty] 生成的属性，无法用 [NotifyCanExecuteChangedFor]，
// 因此监听 PropertyChanged 手动刷新保存按钮。
PropertyChanged += OnViewModelPropertyChanged;

// CommunityToolkitDemo/ViewModels/SettingsViewModel.cs:191-198
private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName is nameof(HasErrors))
    {
        OnPropertyChanged(nameof(ValidationSummary));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
```

**这就是 `[NotifyCanExecuteChangedFor]` 的边界**：它只能贴着 `[ObservableProperty]` 字段用，`HasErrors` 这种来自基类的属性就不行，得手动接 `PropertyChanged`。

---

## 九、依赖注入：`App.xaml.cs` 那张"接线图"

DI 在上一篇第 3.3 节已经讲得很细了，这里只补一个"为什么必须用它"的实战理由。

**没有 DI 的时候，你的构造函数会变成这样：**

```csharp
public CashierViewModel()
{
    _catalog = PosRepository.Instance;       // 全局单例
    _cart = new CartService(new PosRepository(), new SettingsService(), Messenger.Default);
    // ...越写越长，而且没法替换
}
```

**有了 DI 之后**，收银台 ViewModel 的构造函数变成了"需求清单"：

```csharp
// CommunityToolkitDemo/ViewModels/CashierViewModel.cs:76-95（节选）
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
    // ...
}
```

**这个构造函数本身，就是这个类最准确的文档。** 你看它注入了什么，就知道它依赖什么——不需要翻遍整个类的正文。

而且注意最后一个参数：`IMessenger messenger) : base(messenger)`——`ObservableRecipient` 的构造函数需要消息总线，所以 `IMessenger` 也从容器里注入，**而不是在类里写 `WeakReferenceMessenger.Default`**。

**为什么要这么绕？** 因为将来做单元测试时，你可以塞一个假的 Messenger 进去，验证"这个方法到底有没有广播消息"。写死了就测不了。

另外两个注册的细节也值得注意：

```csharp
// CommunityToolkitDemo/App.xaml.cs:134-136
// ---------- 基础设施 ----------
// 全局消息总线：整个应用共用同一个 WeakReferenceMessenger 实例
services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
```

```csharp
// CommunityToolkitDemo/App.xaml.cs:177-190
// ---------- 窗口 / 页面 ViewModel ----------
services.AddSingleton<MainWindow>();

// 页面解析器：把"按类型取页面 ViewModel"收窄为一个委托，
// 使 MainWindowViewModel 不必依赖整个 IServiceProvider（服务定位器反模式）。
// 仍由容器解析，因此页面单例语义与惰性创建行为保持不变。
services.AddSingleton<Func<Type, ObservableObject?>>(
    sp => type => sp.GetService(type) as ObservableObject);

// 页面 ViewModel 使用单例，切换页面时保留状态（购物车、日志等）
services.AddSingleton<MainWindowViewModel>();
services.AddSingleton<CashierViewModel>();
services.AddSingleton<DeviceMonitorViewModel>();
services.AddSingleton<SettingsViewModel>();
```

**为什么页面 ViewModel 是 `Singleton` 而不是 `Transient`？**

因为"收银台切到设备监控再切回来，购物车应该还在"。如果是 `Transient`，每次切页面都新建一个 ViewModel，数量、金额、日志全丢了。**单例 = 页面状态在切换间保留。**

---

## 十、`IValueConverter`：绑定时的"翻译官"

### 10.1 它是什么

WPF 绑定有个语法：`{Binding 属性名, Converter={StaticResource 转换器}}`。转换器的作用是**在"数据"和"界面要的东西"之间做翻译**。

最典型的场景：

- 数据是 `bool`（`HasStatusMessage`），界面要的是 `Visibility`（可见/折叠）；
- 数据是 `decimal`（`12.5`），界面要的是 `"¥12.50"`；
- 数据是枚举（`ConnectionState.Connected`），界面要的是颜色（绿色）。

接口就两个方法：

```csharp
public interface IValueConverter
{
    object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
    object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
}
```

- `Convert`：数据 → 界面（`OneWay`/`TwoWay` 都会用）；
- `ConvertBack`：界面 → 数据（**只有 `TwoWay` 才用**；单向绑定时直接 `return Binding.DoNothing;`）。

### 10.2 金额格式化：注意类型兼容

```csharp
// CommunityToolkitDemo/Common/Converters/Converters.cs:80-100
/// <summary>金额格式化：12.5 → ¥12.50；ConverterParameter = "Plain" 时不带货币符号</summary>
public sealed class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double db => (decimal)db,
            int i => i,
            float f => (decimal)f,
            _ => 0m
        };

        var plain = string.Equals(parameter as string, "Plain", StringComparison.OrdinalIgnoreCase);
        return plain ? amount.ToString("N2", culture) : "¥" + amount.ToString("N2", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

注意它用 `switch` **兼容了 4 种数值类型**——因为 XAML 里绑定的可能是 `decimal`、`double` 甚至 `int`。**转换器写得太"挑食"是常见 bug 来源**：一旦类型不匹配，转换会失败然后静默返回空。

### 10.3 `ConvertBack` 的经典用法：枚举 ↔ 单选按钮

```csharp
// CommunityToolkitDemo/Common/Converters/Converters.cs:186-202
/// <summary>
/// 支付方式 ↔ 分段选择（RadioButton）。
/// 正向：当前支付方式等于参数时返回 true（选中态）；
/// 反向：仅当被选中时写回枚举值，未选中的按钮返回 DoNothing 避免互相覆盖。
/// </summary>
public sealed class PaymentMethodToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PaymentMethod method && parameter is not null &&
           string.Equals(method.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null &&
           Enum.TryParse<PaymentMethod>(parameter.ToString(), ignoreCase: true, out var method)
            ? method
            : Binding.DoNothing;
}
```

**这个 `ConvertBack` 里的 `DoNothing` 是精髓。**

三个单选按钮（现金 / 扫码 / 银行卡）绑的是同一个属性。当用户点"扫码"时：

- "扫码"按钮的 `ConvertBack` 返回 `QrCode` → 写回；
- "现金"和"银行卡"按钮因为**被取消选中**，`ConvertBack` 也会被调用。如果它们也返回一个值，就会互相覆盖，导致"点了扫码却变成银行卡"这种玄学 bug。

所以**未选中时返回 `Binding.DoNothing`，表示"我不改"**。

### 10.4 性能坑：转换器里不要 new 画刷

```csharp
// CommunityToolkitDemo/Common/Converters/Converters.cs:10-31
/// <summary>
/// 画刷缓存：转换器里千万不要每次 Convert 都 new 一个画刷。
/// 未冻结的 Freezable 会被 WPF 属性系统登记变更通知，既增加 GC 压力又带来额外订阅开销；
/// 报文日志这类"每来一帧就新建一行"的场景会持续放大这个问题。
/// 统一使用静态只读 + Freeze 的画刷。
/// </summary>
internal static class BrushCache
{
    public static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
```

```csharp
// CommunityToolkitDemo/Common/Converters/Converters.cs:108-122
public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Disconnected = BrushCache.Frozen(0x94, 0xA3, 0xB8);
    private static readonly SolidColorBrush Connecting = BrushCache.Frozen(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush Connected = BrushCache.Frozen(0x16, 0xA3, 0x4A);
    private static readonly SolidColorBrush Faulted = BrushCache.Frozen(0xDC, 0x26, 0x26);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ConnectionState.Connected => Connected,
            ConnectionState.Connecting => Connecting,
            ConnectionState.Faulted => Faulted,
            _ => Disconnected
        };
}
```

**`Freeze()` 是什么？**

`SolidColorBrush` 继承自 `Freezable`。未冻结的画刷是"可变"的，WPF 必须盯着它、给它挂变更通知；**冻结之后就变成只读，WPF 可以直接共享、跳过所有变更跟踪**。在高频刷新（比如每 150 毫秒来一批报文，每行都要上色）的场景里，这个差别很明显。

**一句话记住：转换器里要返回画刷，一律 `static readonly` + `Freeze()`。**

### 10.5 一个"文案同源"的细节

```csharp
// CommunityToolkitDemo/Common/Converters/Converters.cs:128-137
/// <summary>连接状态 → 中文描述（映射与 <c>DeviceCardViewModel.StateText</c> 同源）</summary>
public sealed class ConnectionStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ProtocolDisplay.ConnectionStateText(
            value is ConnectionState state ? state : ConnectionState.Disconnected);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

注意它**没有自己写 `switch`**，而是调用了 `ProtocolDisplay.ConnectionStateText`。为什么？

因为设备卡片上也要显示中文状态（`DeviceCardViewModel.StateText`）。如果转换器里抄一份、ViewModel 里抄一份，将来改文案就会漏改一处。**所以两份都指向 `ProtocolDisplay` 这一个"文案终点"。**

这就是"单一事实来源"在小地方的应用——**同一个含义，只允许有一处定义。**

---

## 十一、附加属性：不继承也能加能力

### 11.1 它是什么

WPF 有个限制：你不能给 `TextBox` 加一个"占位提示"属性，因为你没有它的源码。

附加属性（Attached Property）解决的就是这个：**在别人家的控件上，挂一个自己定义的能力。**

```csharp
// CommunityToolkitDemo/Common/Behaviors/AttachedProps.cs:9-13
/// <summary>
/// 附加属性集合：以"行为"的方式给原生控件补充能力，避免引入第三方行为库。
/// 知识点：依赖属性注册、属性变更回调、事件挂载/卸载。
/// </summary>
public static class AttachedProps
```

项目里一共 5 个，都是很实用的：

| 附加属性 | 作用 | 代码位置 |
| --- | --- | --- |
| `Placeholder` | 输入框占位提示 | `AttachedProps.cs:17-26` |
| `AutoScrollToEnd` | 日志列表自动滚到底部 | `AttachedProps.cs:32-122` |
| `EnterKeyCommand` | 回车触发命令 | `AttachedProps.cs:128-167` |
| `DecimalOnly` | 只允许输入数字和小数点 | `AttachedProps.cs:173-214` |
| `FocusOnLoaded` | 加载后自动聚焦 | `AttachedProps.cs:220-255` |

### 11.2 注册的"标准四件套"

```csharp
// CommunityToolkitDemo/Common/Behaviors/AttachedProps.cs:173-196
#region DecimalOnly —— 只允许输入数字与小数点

public static readonly DependencyProperty DecimalOnlyProperty =
    DependencyProperty.RegisterAttached(
        "DecimalOnly",
        typeof(bool),
        typeof(AttachedProps),
        new PropertyMetadata(false, OnDecimalOnlyChanged));

public static bool GetDecimalOnly(DependencyObject obj) => (bool)obj.GetValue(DecimalOnlyProperty);

public static void SetDecimalOnly(DependencyObject obj, bool value) => obj.SetValue(DecimalOnlyProperty, value);

private static void OnDecimalOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    if (d is not TextBox textBox)
    {
        return;
    }

    textBox.PreviewTextInput -= OnPreviewTextInput;
    if (e.NewValue is true)
    {
        textBox.PreviewTextInput += OnPreviewTextInput;
    }
}

#endregion
```

**这段代码有几个必须记住的点：**

1. **`RegisterAttached` 而不是 `Register`**：`Register` 注册的是"控件自己的属性"，`RegisterAttached` 注册的是"可以挂到任何控件上的属性"。
2. **必须有 `GetXxx` / `SetXxx` 两个静态方法**：这是 WPF 的约定，XAML 解析器靠它读写。
3. **`PropertyMetadata` 的第二个参数是"变更回调"**：这里就是挂事件的地方。
4. **一定要先 `-=` 再 `+=`**：`textBox.PreviewTextInput -= OnPreviewTextInput;` 这行看着多余，其实是**防止重复挂载**——属性可能被反复设置，不先退订就会挂上多份，事件被触发多次。
5. **`if (e.NewValue is true)` 判断**：属性被设成 `false` 时，要**卸载**事件（上面那个 `-=` 已经做了）。

XAML 里用起来就很舒服：

```xml
<TextBox common:AttachedProps.Placeholder="扫描或输入条码"
         common:AttachedProps.DecimalOnly="True"
         common:AttachedProps.EnterKeyCommand="{Binding AddByBarcodeCommand}"
         common:AttachedProps.FocusOnLoaded="True" />
```

### 11.3 一个"从可视树里找 ScrollViewer"的技巧

```csharp
// CommunityToolkitDemo/Common/Behaviors/AttachedProps.cs:96-113
private static ScrollViewer? ResolveScrollViewer(DependencyObject root)
{
    if (root is ScrollViewer viewer)
    {
        return viewer;
    }

    int count = VisualTreeHelper.GetChildrenCount(root);
    for (int i = 0; i < count; i++)
    {
        if (ResolveScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } found)
        {
            return found;
        }
    }

    return null;
}
```

**为什么要这么找？** 因为 `ListBox` 的滚动条不在你自己的 XAML 里，而在它的**默认控件模板（ControlTemplate）**里。模板是运行时才展开的，所以你的元素树里"看不到" `ScrollViewer`——只能在运行时通过 `VisualTreeHelper` 到**可视树（Visual Tree）**里翻。

**"逻辑树"和"可视树"的区别值得单独记一下：**

- 逻辑树：你 XAML 里写了什么（`ListBox` 里有个 `DataTemplate`）；
- 可视树：运行时真正渲染出来的东西（`ListBox` → `Border` → `ScrollViewer` → `ItemsPresenter` → 一堆 `ListBoxItem`……）。

所以附加属性里还配了一个 `Loaded` 事件兜底，等模板展开完再去找：

```csharp
// CommunityToolkitDemo/Common/Behaviors/AttachedProps.cs:52-64
if (e.NewValue is true)
{
    // 可直接挂在 ScrollViewer 上；也可挂在 ListBox 等宿主上，
    // 待模板应用后从可视树里找到内部的 ScrollViewer（适配虚拟化列表）。
    if (element.IsLoaded)
    {
        HookScrollViewer(element);
    }
    else
    {
        element.Loaded += OnAutoScrollTargetLoaded;
    }
}
```

---

## 十二、设计模式：这个项目里真正用到的三种

我不想硬凑设计模式，只讲项目里**真的用了**的三种。

### 12.1 策略模式（Strategy）：`IProtocolDriver`

5 种硬件协议（Modbus / OPC UA / 串口 / Socket / MQTT）对上层来说是"同一件事"：连接、收报文、发指令。

```csharp
// CommunityToolkitDemo/Protocols/IProtocolDriver.cs（契约，示意）
public interface IProtocolDriver
{
    ProtocolType Type { get; }
    string Name { get; }
    ConnectionState State { get; }
    event EventHandler<ProtocolFrame>? FrameReceived;
    event EventHandler<IProtocolDriver>? StateChanged;
    event EventHandler<string>? AlarmRaised;
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendAsync(ProtocolFrame frame, CancellationToken cancellationToken = default);
}
```

上层（设备监控页、收银台）只认这个接口，**根本不知道底下是串口还是 MQTT**。

于是 `ProtocolManager` 的构造函数就变成了"一网打尽"：

```csharp
// CommunityToolkitDemo/Protocols/ProtocolManager.cs:25-36
public ProtocolManager(IEnumerable<IProtocolDriver> drivers)
{
    _drivers = drivers.ToList();
    _driversView = _drivers.AsReadOnly();

    foreach (var driver in _drivers)
    {
        driver.FrameReceived += OnDriverFrameReceived;
        driver.StateChanged += OnDriverStateChanged;
        driver.AlarmRaised += OnDriverAlarmRaised;
    }
}
```

**新增一种协议 → 在 DI 里加一行 → 其他代码零改动。** 这就是策略模式 + 依赖注入的组合威力。

### 12.2 模板方法（Template Method）：`ProtocolDriverBase`

5 种协议的"骨架流程"是一样的（连上 → 循环产数据 → 发指令 → 状态变化通知 → 出错报警），差异只在具体细节。于是把骨架写在基类，把"变化的部分"留成虚方法给子类实现：

```csharp
// CommunityToolkitDemo/Protocols/ProtocolDriverBase.cs:162-190
#region 子类扩展点

/// <summary>握手动作，默认模拟 400ms 建链耗时。</summary>
protected virtual Task OnConnectAsync(CancellationToken cancellationToken)
    => Task.Delay(400, cancellationToken);

/// <summary>断开动作，默认无额外操作。</summary>
protected virtual Task OnDisconnectAsync() => Task.CompletedTask;

/// <summary>
/// 发送动作，默认原样回显一条"发送方向"报文。
/// 真实设备场景中这里会返回"请求 + 响应"两条报文。
/// </summary>
protected virtual Task<IReadOnlyList<ProtocolFrame>> OnSendAsync(ProtocolFrame frame, CancellationToken cancellationToken)
    => Task.FromResult<IReadOnlyList<ProtocolFrame>>(new[] { /* ...原样回显... */ });

/// <summary>每次轮询产生一批仿真报文。</summary>
protected abstract Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken);

#endregion
```

**"模板方法"的核心特征就是：父类定流程，子类填细节。**

`SerialPortSimDriver` 就只填了两个洞——`ProduceAsync`（每 4 个周期回读一次打印机状态）和 `OnSendAsync`（收到 `TRIGGER_SCAN` 才回传条码）：

```csharp
// CommunityToolkitDemo/Protocols/Simulated/SerialPortSimDriver.cs:34-42
protected override Task<IReadOnlyList<ProtocolFrame>> ProduceAsync(CancellationToken cancellationToken)
{
    _tick++;

    // 每 4 个轮询周期回读一次打印机状态，其余周期保持静默
    if (_tick % 4 != 0)
    {
        return Task.FromResult<IReadOnlyList<ProtocolFrame>>(Array.Empty<ProtocolFrame>());
    }
    // ...
}

// CommunityToolkitDemo/Protocols/Simulated/SerialPortSimDriver.cs:76-92
if (frame.Payload.Contains(ScanCommand, StringComparison.OrdinalIgnoreCase))
{
    var product = PickRandomProduct();
    var barcode = product?.Barcode ?? "6921168509256";

    responses.Add(new ProtocolFrame(
        Type,
        ProtocolDirection.In,
        Device.Address,
        $"SCAN EAN13 {barcode}",
        "扫码枪条码",
        new Dictionary<string, string>
        {
            [BarcodeKey] = barcode,
            ["匹配商品"] = product?.Name ?? "未匹配到商品"
        }));
}
```

### 12.3 单一事实来源（Single Source of Truth）：`CartLimits`

这个不是设计模式，但**是我认为项目里最值得抄的一个小文件**：

```csharp
// CommunityToolkitDemo/Services/CartLimits.cs
/// <summary>
/// 购物车数量上限规则（单一事实来源）。
///
/// 为什么单独抽出来？
/// 同一条"能否加购/最多加多少"的规则同时被三处需要：
/// 购物车服务（约束实际数量）、收银台 ViewModel（拦截零库存并给出提示）、
/// 以及购物车行项（驱动"+"按钮的可用状态）。
/// 规则分散在多处时，任何一处漏改都会造成"按钮可点但加不进去"之类的不一致，
/// 因此集中到这里，全应用只保留一份判断。
/// </summary>
public static class CartLimits
{
    /// <summary>允许超库存销售时不再限制数量。</summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>
    /// 在给定"是否允许超卖"策略下，该商品可加入购物车的数量上限。
    /// 不允许超卖时上限即当前库存（库存为负视为 0）。
    /// </summary>
    public static int MaxQuantityFor(Product product, bool allowOversell)
        => allowOversell ? Unlimited : Math.Max(product.Stock, 0);

    /// <summary>
    /// 该商品当前是否允许加入购物车。
    /// 允许超卖时始终可加；不允许超卖时零库存商品不可加购。
    /// </summary>
    public static bool CanAdd(Product product, bool allowOversell)
        => MaxQuantityFor(product, allowOversell) > 0;
}
```

**为什么它值钱？** 因为"上限"这个规则在三个地方被用到：

| 使用方 | 用途 | 代码点 |
| --- | --- | --- |
| `CartService` | 约束实际数量（`Math.Min` / `Math.Clamp`） | `Services/CartService.cs:36`、`:52-58` |
| `CashierViewModel` | 零库存时拦截并给出提示 | `ViewModels/CashierViewModel.cs:239` |
| `CartItem.IsMaxQuantity` | 驱动"+"按钮的可用状态 | `Models/CartItem.cs:63` |

如果这规则抄三份，迟早会出现"界面说还能加，服务说不行"的诡异状态。**抽成一个静态类，全应用只有一份判断——这就是"单一事实来源"。**

---

## 十三、单元测试：为什么必须给 WPF 项目写测试

很多人觉得"WPF 界面程序没法写测试"。其实**能测的部分很多**，只要你的逻辑不写在 `xaml.cs` 里。

项目的测试工程配置：

```xml
<!-- CommunityToolkitDemo/tests/CommunityToolkitDemo.Tests/CommunityToolkitDemo.Tests.csproj:3-27 -->
<PropertyGroup>
  <!-- 与被测主工程保持同一目标框架：主工程是 net10.0-windows + WPF，
       测试要引用其中的 WPF 类型（转换器、画笔），因此测试工程同样开启 UseWPF。
       注意：这里不会把主工程的 App.xaml 入口点带进来，xUnit 使用自己的测试宿主。 -->
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\..\CommunityToolkitDemo.csproj" />
</ItemGroup>
```

另外主工程还要**显式排除测试目录**，这个坑很隐蔽：

```xml
<!-- CommunityToolkitDemo/CommunityToolkitDemo.csproj:38-44 -->
<ItemGroup>
  <!-- tests\ 下是独立的测试工程：必须显式排除，
       否则 SDK 默认的 **\*.cs / **\*.xaml 通配符会把测试代码编译进主程序 -->
  <Compile Remove="tests\**" />
  <None Remove="tests\**" />
  <Page Remove="tests\**" />
</ItemGroup>
```

测试清单（都在 `tests/CommunityToolkitDemo.Tests/`）：

| 测试类 | 测什么 |
| --- | --- |
| `CartLimitsTests` | 数量上限规则（超卖开关的各种组合） |
| `CartServiceTests` | 购物车增删改、广播是否正确 |
| `ConvertersTests` | 转换器的正反向转换 |
| `MarkdownRowReaderTests` / `MarkdownTableParserTests` | Markdown 解析的边界（空表、缺列、截断……） |
| `PosRepositoryIndexTests` | 条码 / 编码 / 分类索引的正确性与失效 |
| `ProductQueryServiceTests` | 分页切片、关键字过滤、取消 |
| `ProtocolDisplayTests` | 协议文案映射 |
| `ProtocolDriverBaseTests` | 驱动基类的骨架行为 |
| `VirtualizingWrapPanelTests` | 虚拟化面板的容器生命周期 |
| `Performance/` 下 | 加载与查询的基准测试 |

### 13.1 但是 WPF 的测试有一个大坑：线程模型

WPF 的视觉树和布局管线要求 **STA（单线程单元）线程**，而 **xUnit 默认测试线程是 MTA**。直接创建 `UIElement` 会抛异常。项目的解法非常干净：

```csharp
// CommunityToolkitDemo/tests/CommunityToolkitDemo.Tests/StaTestRunner.cs:5-45
/// <summary>
/// 在 STA 线程上执行测试体。
/// </summary>
/// <remarks>
/// WPF 的视觉树与布局管线要求 STA 线程，xUnit 默认的测试线程是 MTA，
/// 直接创建 <c>UIElement</c> 会抛 <see cref="InvalidOperationException"/>。
/// 这里为每个测试体单独起一条 STA 线程并等待其完成，
/// 异常原样回抛给 xUnit（保留原始堆栈），避免被吞掉后只看到"测试通过"的假象。
/// </remarks>
internal static class StaTestRunner
{
    /// <summary>在 STA 线程上同步执行 <paramref name="action"/>，并把异常原样抛出。</summary>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}
```

这段 40 行的工具类，有 **3 个值得反复琢磨的细节**：

1. **`SetApartmentState(ApartmentState.STA)`**：这是解决 MTA 限制的关键一行。
2. **`thread.Join()`**：同步等待，测试方法不能提前返回。
3. **`ExceptionDispatchInfo.Capture(captured).Throw()`**：**这一行是"专业"和"业余"的分水岭**。

第 3 点展开说：如果你直接 `throw captured;`，异常堆栈会被"截断"在 throw 这一行，你根本看不到**原始出错的位置**。而 `ExceptionDispatchInfo.Capture(ex).Throw()` 会**保留原始堆栈**，调试时能看到真正出错的那一行。

**记住这个技巧：捕获了异常稍后要重新抛出时，用 `ExceptionDispatchInfo`，不要用 `throw ex;`。**

（顺带一提，`throw ex;` 和 `throw;` 的区别也是面试常考点：`throw ex;` 会重置堆栈，`throw;` 保留。）

### 13.2 一个"和内置控件对齐行为"的测试

`VirtualizingWrapPanelTests` 里有一个特别讲究的用例：验证自定义面板在 `Reset`（集合整体替换）时的行为**和 WPF 内置的 `VirtualizingStackPanel` 完全一致**。

```csharp
// CommunityToolkitDemo/Common/Controls/VirtualizingWrapPanel.cs:243-253
case NotifyCollectionChangedAction.Reset:
    // Reset（整体替换）时框架的 OnItemsChangedInternal → ResetChildren → ClearChildren
    // 已经调用过 generator.RemoveAll() 并清空了 InternalChildren，生成器的位置映射随之失效。
    // 此刻再把容器交回回收池已不可能（Remove/Recycle 的坐标不再有效），
    // 内置的 VirtualizingStackPanel 行为完全一致（见 Reset_DropsContainersLikeBuiltIn 对照测试）。
    // 因此这里只做兜底清理，保证不残留内部子元素；回收复用只在明确受支持的滚动/增量变更路径生效。
                if (InternalChildren.Count > 0)
                {
                    RemoveInternalChildRange(0, InternalChildren.Count);
                }
                break;
```

**这个测试的价值在于：它不是在测"我的实现对不对"，而是在测"我的实现和官方实现像不像"。** 对于自定义控件，这是最聪明的测试策略——**你不需要自己定义"正确"，你只需要对齐官方行为。**

（关于这个自定义面板，第 03、04 篇会更详细地讲。）

---

## 本篇小结

| 知识点 | 一句话记住 | 项目里的关键成员 |
| --- | --- | --- |
| `[ObservableProperty]` | 写字段，生成属性 | `CartItem.Quantity` |
| `[NotifyPropertyChangedFor]` | 派生属性自动联动 | `CashierViewModel.PaidText → Change` |
| `[RelayCommand]` | 方法变命令（异步去掉 `Async`） | `AddProductCommand` |
| `CanExecute` | 按钮亮不亮由业务规则决定 | `CanCheckout()` |
| `OnXxxChanged` | 属性变化钩子（只做轻活） | `OnSearchTextChanged` 防抖 |
| `ObservableRecipient` | `IsActive` = 订阅总开关 | `CashierViewModel`、`DeviceMonitorViewModel` |
| `OnActivated/OnDeactivated` | 进页面订阅、离开退订 | `DeviceMonitorViewModel:185-213` |
| `ObservableValidator` | 特性声明校验规则 | `SettingsViewModel` |
| `IValueConverter` | 数据 ↔ 界面的翻译官 | `Converters.cs` |
| 附加属性 | 给别人家控件加能力 | `AttachedProps.cs` |
| 策略模式 | `IProtocolDriver` + DI 注册 | `ProtocolManager` |
| 模板方法 | 父类定流程、子类填细节 | `ProtocolDriverBase` |
| 单一事实来源 | 同一规则只留一份 | `CartLimits` |
| xUnit + STA | WPF 测试要跑在 STA 线程 | `StaTestRunner` |

下一篇我们挑出这个项目里最值得学的六个设计，一个一个讲透：[这个项目最值钱的六个设计](03-这个项目最值钱的六个设计.md)
