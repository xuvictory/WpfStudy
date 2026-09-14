using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using Prism.Mvvm;

namespace PrismDemo.Common.Mvvm;

/// <summary>
/// 支持 DataAnnotations 校验的 ViewModel 基类，实现 <see cref="INotifyDataErrorInfo"/>，
/// 可直接被 WPF 的 <c>Validation.Errors</c> / <c>AdornedElementPlaceholder</c> 使用。
/// </summary>
/// <remarks>
/// 迁移前该能力由 CommunityToolkit 的 <c>ObservableValidator</c> + <c>[NotifyDataErrorInfo]</c> 提供，
/// 而 Prism 8 没有等价基类。本类保留原 API 形状与行为，使设置页的校验体验完全不变：
/// <list type="bullet">
/// <item>属性 setter 内调用 <see cref="ValidateProperty"/>，值一变即重新校验该属性；</item>
/// <item><see cref="HasErrors"/> 发生变化时额外抛出 <c>PropertyChanged(HasErrors)</c>，
/// 供外部（设置页）刷新校验摘要与"保存"按钮可用性 —— 这一点与 <c>ObservableValidator</c> 一致；</item>
/// <item>无论校验通过与否都抛出 <see cref="ErrorsChanged"/>，这样校验通过时界面上的红框能立即消失。</item>
/// </list>
/// </remarks>
public abstract class ValidatableBindableBase : BindableBase, INotifyDataErrorInfo
{
    private static readonly PropertyChangedEventArgs HasErrorsChangedEventArgs = new(nameof(HasErrors));

    /// <summary>属性名 → 该属性的校验错误集合（无错误的属性不会出现在字典里）</summary>
    private readonly Dictionary<string, List<ValidationResult>> _errors = new(StringComparer.Ordinal);

    /// <summary>是否存在校验错误</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <inheritdoc />
    IEnumerable INotifyDataErrorInfo.GetErrors(string? propertyName) => GetErrors(propertyName);

    /// <summary>
    /// 获取校验错误。<paramref name="propertyName"/> 为空时返回全部属性的错误。
    /// </summary>
    public IEnumerable<ValidationResult> GetErrors(string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return _errors.Values.SelectMany(static list => list).ToArray();
        }

        return _errors.TryGetValue(propertyName, out var list)
            ? list.ToArray()
            : [];
    }

    /// <summary>
    /// 校验单个属性：在属性 setter 内调用，只校验<b>新值</b>本身（不依赖对象其他状态）。
    /// </summary>
    /// <param name="value">属性的新值</param>
    /// <param name="propertyName">属性名（默认由调用方成员名推断）</param>
    protected void ValidateProperty(object? value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        var attributes = GetValidationAttributes(propertyName);
        if (attributes.Length == 0)
        {
            // 没有校验特性的属性无需参与校验（也避免误把旧错误清掉）
            return;
        }

        var results = new List<ValidationResult>();
        var context = new ValidationContext(this) { MemberName = propertyName };
        var valid = Validator.TryValidateValue(value, context, results, attributes);

        SetErrors(propertyName, valid && results.Count == 0 ? null : results);
    }

    /// <summary>
    /// 校验所有带 DataAnnotations 特性的公开属性。
    /// 用于"载入配置后重新做一次整体校验"这类场景。
    /// </summary>
    protected void ValidateAllProperties()
    {
        foreach (var property in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            if (property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any() == false)
            {
                continue;
            }

            ValidateProperty(property.GetValue(this), property.Name);
        }
    }

    private ValidationAttribute[] GetValidationAttributes(string propertyName)
    {
        var property = GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property is null
            ? []
            : property.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
    }

    /// <summary>写入某属性的错误集合并抛出通知（不比较内容是否变化，保证界面能及时清除红框）。</summary>
    private void SetErrors(string propertyName, List<ValidationResult>? results)
    {
        var hadErrors = HasErrors;

        if (results is null || results.Count == 0)
        {
            _errors.Remove(propertyName);
        }
        else
        {
            _errors[propertyName] = results;
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));

        if (hadErrors != HasErrors)
        {
            OnPropertyChanged(HasErrorsChangedEventArgs);
        }
    }
}
