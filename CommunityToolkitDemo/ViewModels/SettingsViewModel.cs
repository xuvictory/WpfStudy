using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;
using CommunityToolkitDemo.Services;

namespace CommunityToolkitDemo.ViewModels;

/// <summary>
/// 设置页 ViewModel。
///
/// 覆盖的 CommunityToolkit 知识点：
/// - <see cref="ObservableValidator"/>：在 <c>[ObservableProperty]</c> 字段上叠加
///   <c>[NotifyDataErrorInfo]</c> + DataAnnotations 特性（Required / MinLength / MaxLength /
///   RegularExpression / Range），属性一改就自动校验；
/// - <c>ValidateAllProperties()</c>：保存前整体校验；
/// - <c>HasErrors</c> / <c>GetErrors()</c>：驱动"保存"按钮可用性与错误汇总。
/// </summary>
public partial class SettingsViewModel : ObservableValidator
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IMessenger _messenger;

    public SettingsViewModel(ISettingsService settings, IDialogService dialogs, IMessenger messenger)
    {
        _settings = settings;
        _dialogs = dialogs;
        _messenger = messenger;

        // HasErrors 不是 [ObservableProperty] 生成的属性，无法用 [NotifyCanExecuteChangedFor]，
        // 因此监听 PropertyChanged 手动刷新保存按钮。
        PropertyChanged += OnViewModelPropertyChanged;

        LoadFrom(_settings.Current);
    }

    #region 表单字段（带校验规则）

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "门店名称不能为空")]
    [MinLength(2, ErrorMessage = "门店名称至少 2 个字符")]
    [MaxLength(20, ErrorMessage = "门店名称最多 20 个字符")]
    private string _storeName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "门店地址不能为空")]
    [MaxLength(50, ErrorMessage = "门店地址最多 50 个字符")]
    private string _storeAddress = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "联系电话不能为空")]
    [RegularExpression(@"^(1[3-9]\d{9}|0\d{2,3}-?\d{7,8})$", ErrorMessage = "请输入有效的手机号或座机号")]
    private string _phone = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "小票抬头不能为空")]
    [MaxLength(30, ErrorMessage = "小票抬头最多 30 个字符")]
    private string _receiptHeader = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(60, ErrorMessage = "小票页脚最多 60 个字符")]
    private string _receiptFooter = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 3, ErrorMessage = "小票份数需在 1 ~ 3 之间")]
    private int _receiptCopies = 1;

    [ObservableProperty]
    private bool _printQrCode = true;

    [ObservableProperty]
    private PaymentMethod _defaultPayment = PaymentMethod.Cash;

    [ObservableProperty]
    private bool _showChangeHint = true;

    [ObservableProperty]
    private bool _allowOversell;

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

        _messenger.Send(new StatusNotificationMessage(
            $"门店参数已保存：{StoreName.Trim()}",
            StatusNotificationMessage.StatusLevel.Success));

        _dialogs.ShowInfo("设置已保存并立即生效（门店名称、默认支付方式等已同步）。", "保存成功");
    }

    /// <summary>保存按钮可用性由校验结果决定</summary>
    private bool CanSave() => !HasErrors;

    /// <summary>恢复出厂默认值</summary>
    [RelayCommand]
    private void Reset()
    {
        if (!_dialogs.Confirm("确定要恢复出厂默认设置吗？", "恢复默认"))
        {
            return;
        }

        _settings.Reset();
        LoadFrom(_settings.Current);

        _messenger.Send(new StatusNotificationMessage(
            "已恢复出厂默认设置",
            StatusNotificationMessage.StatusLevel.Info));
    }

    #endregion

    #region 属性联动

    partial void OnReceiptHeaderChanged(string value) => OnPropertyChanged(nameof(ReceiptPreview));

    partial void OnReceiptFooterChanged(string value) => OnPropertyChanged(nameof(ReceiptPreview));

    partial void OnPrintQrCodeChanged(bool value) => OnPropertyChanged(nameof(ReceiptPreview));

    partial void OnReceiptCopiesChanged(int value) => OnPropertyChanged(nameof(ReceiptPreview));

    #endregion

    #region 私有方法

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HasErrors))
        {
            OnPropertyChanged(nameof(ValidationSummary));
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

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
