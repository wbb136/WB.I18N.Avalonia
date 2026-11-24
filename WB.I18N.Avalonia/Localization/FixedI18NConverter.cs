using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WB.I18N.Avalonia;

/// <summary>为固定资源键创建单向语言绑定，并保留创建绑定时的键和备用文案。</summary>
internal sealed class FixedI18NConverter : IValueConverter
{
    private readonly string? _key;
    private readonly string? _fallback;

    /// <summary>保存绑定创建时的固定资源键和备用文案。</summary>
    public FixedI18NConverter(string? key, string? fallback)
    {
        _key = key;
        _fallback = fallback;
    }

    /// <summary>语言变化时从当前资源读取固定键的文案。</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return I18NExtension.GetString(_key, _fallback);
    }

    /// <summary>本地化绑定只向界面提供文案。</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
