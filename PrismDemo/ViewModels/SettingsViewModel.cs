using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Prism.Commands;
using Prism.Events;
using PrismDemo.Common.Mvvm;
using PrismDemo.Events;
using PrismDemo.Models;
using PrismDemo.Services;

namespace PrismDemo.ViewModels;

/// <summary>
/// 设置页 ViewModel。
///
/// 覆盖的 Prism 知识点：
/// - <see cref="ValidatableBindableBase"/>：本项目为 Prism 8 补齐的校验基类，
///   在 <see cref="Prism.Mvvm.BindableBase"/> 之上实现 <c>INotifyDataErrorInfo</c>，
///   属性 setter 一改就重新校验（等价于原 <c>ObservableValidator</c> + <c>[NotifyDataErrorInfo]</c>）；
/// - 校验特性写在<b>属性</b>上：DataAnnotations 的 Required / MinLength / MaxLength /
///   RegularExpression / Range 全部原样保留；
/// - <c>ValidateAllProperties()</c>：保存前整体校验；
/// - <c>HasErrors</c> / <c>GetErrors()</c>：驱动"保存"按钮可用性与错误汇总；
/// - <c>IEventAggregator</c>：保存 / 恢复默认后广播 <see cref="SettingsChangedEvent"/>。
/// </summary>
public class SettingsViewModel : ValidatableBindableBase
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IEventAggregator _eventAggregator;

    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private string _phone = string.Empty;
    private string _receiptHeader = string.Empty;
    private string _receiptFooter = string.Empty;
    private int _receiptCopies = 1;
    private bool _printQrCode = true;
    private PaymentMethod _defaultPayment = PaymentMethod.Cash;
    private bool _showChangeHint = true;
    private bool _allowOversell;

    private DelegateCommand? _saveCommand;
    private DelegateCommand? _resetCommand;

    public SettingsViewModel(ISettingsService settings, IDialogService dialogs, IEventAggregator eventAggregator)
    {
        _settings = settings;
        _dialogs = dialogs;
        _eventAggregator = eventAggregator;

        // HasErrors 的变化同样会抛出 PropertyChanged（见 ValidatableBindableBase），
        // 这里监听它来刷新错误汇总与"保存"按钮可用性。
        PropertyChanged += OnViewModelPropertyChanged;

        LoadFrom(_settings.Current);
    }

    #region 表单字段（带校验规则）

    [Required(ErrorMessage = "门店名称不能为空")]
    [MinLength(2, ErrorMessage = "门店名称至少 2 个字符")]
    [MaxLength(20, ErrorMessage = "门店名称最多 20 个字符")]
    public string StoreName
    {
        get => _storeName;
        set
        {
            if (!SetProperty(ref _storeName, value))
            {
                return;
            }

            ValidateProperty(value, nameof(StoreName));
        }
    }

    [Required(ErrorMessage = "门店地址不能为空")]
    [MaxLength(50, ErrorMessage = "门店地址最多 50 个字符")]
    public string StoreAddress
    {
        get => _storeAddress;
        set
        {
            if (!SetProperty(ref _storeAddress, value))
            {
                return;
            }

            ValidateProperty(value, nameof(StoreAddress));
        }
    }

    [Required(ErrorMessage = "联系电话不能为空")]
    [RegularExpression(@"^(1[3-9]\d{9}|0\d{2,3}-?\d{7,8})$", ErrorMessage = "请输入有效的手机号或座机号")]
    public string Phone
    {
        get => _phone;
        set
        {
            if (!SetProperty(ref _phone, value))
            {
                return;
            }

            ValidateProperty(value, nameof(Phone));
        }
    }

    [Required(ErrorMessage = "小票抬头不能为空")]
    [MaxLength(30, ErrorMessage = "小票抬头最多 30 个字符")]
    public string ReceiptHeader
    {
        get => _receiptHeader;
        set
        {
            if (!SetProperty(ref _receiptHeader, value))
            {
                return;
            }

            ValidateProperty(value, nameof(ReceiptHeader));
            RaisePropertyChanged(nameof(ReceiptPreview));
        }
    }

    [MaxLength(60, ErrorMessage = "小票页脚最多 60 个字符")]
    public string ReceiptFooter
    {
        get => _receiptFooter;
        set
        {
            if (!SetProperty(ref _receiptFooter, value))
            {
                return;
            }

            ValidateProperty(value, nameof(ReceiptFooter));
            RaisePropertyChanged(nameof(ReceiptPreview));
        }
    }

    [Range(1, 3, ErrorMessage = "小票份数需在 1 ~ 3 之间")]
    public int ReceiptCopies
    {
        get => _receiptCopies;
        set
        {
            if (!SetProperty(ref _receiptCopies, value))
            {
                return;
            }

            ValidateProperty(value, nameof(ReceiptCopies));
            RaisePropertyChanged(nameof(ReceiptPreview));
        }
    }

    public bool PrintQrCode
    {
        get => _printQrCode;
        set
        {
            if (SetProperty(ref _printQrCode, value))
            {
                RaisePropertyChanged(nameof(ReceiptPreview));
            }
        }
    }

    public PaymentMethod DefaultPayment
    {
        get => _defaultPayment;
        set => SetProperty(ref _defaultPayment, value);
    }

    public bool ShowChangeHint
    {
        get => _showChangeHint;
        set => SetProperty(ref _showChangeHint, value);
    }

    public bool AllowOversell
    {
        get => _allowOversell;
        set => SetProperty(ref _allowOversell, value);
    }

    #endregion

    #region 只读绑定

    /// <summary>支付方式下拉数据源</summary>
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } = new[]
    {
        PaymentMethod.Cash,
        PaymentMethod.QrCode,
        PaymentMethod.BankCard
    };

    /// <summary>错误汇总（无错误时提示校验通过）</summary>
    public string ValidationSummary => HasErrors
        ? string.Join("；", GetErrors().Select(e => e.ErrorMessage).Distinct())
        : "所有字段校验通过";

    /// <summary>小票预览：抬头 / 页脚 / 份数 / 二维码开关变化时实时刷新</summary>
    public string ReceiptPreview =>
        $"{ReceiptHeader}\n" +
        "------------------------------\n" +
        "可乐 500ml     x1      ¥3.50\n" +
        "农夫山泉 550ml x2      ¥4.00\n" +
        "------------------------------\n" +
        "合计                   ¥7.50\n" +
        "现金 收 ¥10.00   找零 ¥2.50\n" +
        (PrintQrCode ? "[二维码]\n" : string.Empty) +
        $"— 打印份数 {ReceiptCopies} —\n" +
        $"{ReceiptFooter}";

    #endregion

    #region 命令

    /// <summary>保存设置：整体校验通过后写回服务并广播事件。</summary>
    public DelegateCommand SaveCommand => _saveCommand ??= new DelegateCommand(Save, CanSave);

    /// <summary>恢复出厂默认值</summary>
    public DelegateCommand ResetCommand => _resetCommand ??= new DelegateCommand(Reset);

    private void Save()
    {
        ValidateAllProperties();

        if (HasErrors)
        {
            _dialogs.ShowWarning($"表单存在 {GetErrors().Count()} 项校验错误，请修正后再保存。", "无法保存");
            return;
        }

        _settings.Save(new PosSettings(
            StoreName.Trim(),
            StoreAddress.Trim(),
            Phone.Trim(),
            ReceiptHeader.Trim(),
            ReceiptFooter.Trim(),
            ReceiptCopies,
            PrintQrCode,
            DefaultPayment,
            ShowChangeHint,
            AllowOversell));

        // 设置服务内部会广播 SettingsChangedEvent，
        // 这里只额外推送一条"全局提示条"消息。
        Publish(new StatusNotification(
            $"门店参数已保存：{StoreName.Trim()}",
            StatusLevel.Success));

        _dialogs.ShowInfo("设置已保存并立即生效（门店名称、默认支付方式等已同步）。", "保存成功");
    }

    /// <summary>保存按钮可用性由校验结果决定</summary>
    private bool CanSave() => !HasErrors;

    private void Reset()
    {
        if (!_dialogs.Confirm("确定要恢复出厂默认设置吗？", "恢复默认"))
        {
            return;
        }

        _settings.Reset();
        LoadFrom(_settings.Current);

        Publish(new StatusNotification("已恢复出厂默认设置", StatusLevel.Info));
    }

    #endregion

    #region 私有方法

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HasErrors))
        {
            RaisePropertyChanged(nameof(ValidationSummary));
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>向全局提示条推送一条状态（对应迁移前的 <c>IMessenger.Send</c>）。</summary>
    private void Publish(StatusNotification notification)
        => _eventAggregator.GetEvent<StatusNotificationEvent>().Publish(notification);

    /// <summary>把设置值填回表单并重新校验。</summary>
    private void LoadFrom(PosSettings settings)
    {
        StoreName = settings.StoreName;
        StoreAddress = settings.StoreAddress;
        Phone = settings.Phone;
        ReceiptHeader = settings.ReceiptHeader;
        ReceiptFooter = settings.ReceiptFooter;
        ReceiptCopies = settings.ReceiptCopies;
        PrintQrCode = settings.PrintQrCode;
        DefaultPayment = settings.DefaultPayment;
        ShowChangeHint = settings.ShowChangeHint;
        AllowOversell = settings.AllowOversell;

        ValidateAllProperties();
    }

    #endregion
}
