using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Metadata;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "WB.I18N.Avalonia")]

namespace WB.I18N.Avalonia;

/// <summary>提供统一的界面语言和资源入口，支持 C# 查值和 XAML 动态绑定。</summary>
public sealed partial class I18NExtension : INotifyPropertyChanged, IMultiValueConverter
{
    private static readonly object ResourceLock = new();
    private static ResourceManager? _resources;
    private static Assembly? _declarationAssembly;
    private static int _entryAssemblyChecked;
    private static CultureInfo _resourceCulture = GetInitialResourceCulture();
    private static event PropertyChangedEventHandler? LanguageChanged;

    /// <summary>获取应用共用的语言绑定源。</summary>
    public static I18NExtension Current { get; } = new();

    /// <summary>获取或设置 .NET 支持的界面语言或地区标签。初始值取自宿主界面语言，不变文化的标签为空。</summary>
    /// <remarks>空白或无效的设置会被忽略。</remarks>
    public string LanguageTag
    {
        get
        {
            return Volatile.Read(ref _resourceCulture).Name;
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(value.Trim());
            }
            catch (CultureNotFoundException)
            {
                return;
            }

            if (string.Equals(culture.Name, LanguageTag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 仅更新本地化用的区域性，避免修改调用线程的全局文化设置。
            Volatile.Write(ref _resourceCulture, culture);
            LanguageChanged?.Invoke(Current, new PropertyChangedEventArgs(nameof(LanguageTag)));
        }
    }

    /// <summary>在首次初始化时取得宿主界面语言的只读区域性，无法取得时读取中性资源。</summary>
    private static CultureInfo GetInitialResourceCulture()
    {
        string languageTag = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageTag);
        }
        catch (CultureNotFoundException)
        {
            // 精简运行环境可能没有对应的文化数据，此时仍可使用默认 resx。
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>界面语言切换时通知绑定重新读取文案。</summary>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            LanguageChanged += value;
        }
        remove
        {
            LanguageChanged -= value;
        }
    }

    /// <summary>获取或设置资源键，支持字符串和 XAML 绑定。</summary>
    public object? Key { get; set; }

    /// <summary>获取或设置缺少资源时使用的备用文案，支持字符串和 XAML 绑定。</summary>
    public object? Fallback { get; set; }

    /// <summary>按占位符顺序获取 XAML 中用于格式化的参数绑定。</summary>
    [Content]
    public Collection<BindingBase> Arguments { get; } = [];

    /// <summary>按键读取当前语言的资源，缺少译文时回退到上级语言、默认资源或备用文案。</summary>
    public static string GetString(string? resourceKey, string? fallback = null)
    {
        return GetString(resourceKey, fallback, Volatile.Read(ref _resourceCulture));
    }

    /// <summary>查找并格式化当前语言的资源模板，无参数或格式错误时返回原始模板。</summary>
    public static string Format(string resourceKey, params object?[]? arguments)
    {
        CultureInfo culture = Volatile.Read(ref _resourceCulture);
        string template = GetString(resourceKey, null, culture);
        if (arguments is null || arguments.Length == 0)
        {
            return template;
        }

        return FormatTemplate(culture, template, arguments);
    }

    /// <summary>从指定语言读取文案，查不到资源时使用备用文案或资源键。</summary>
    private static string GetString(string? resourceKey, string? fallback, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return fallback ?? string.Empty;
        }

        string key = resourceKey.Trim();
        return GetResourceManager()?.GetString(key, culture) ?? fallback ?? key;
    }

    /// <summary>供源生成器注册调用方声明的资源位置。</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterResourceAssembly(Assembly declarationAssembly)
    {
        ArgumentNullException.ThrowIfNull(declarationAssembly);

        // 显式应用声明优先于其引用的语言类库。
        Assembly? applicationAssembly = Assembly.GetEntryAssembly();
        I18NResourceAttribute? applicationResource =
            applicationAssembly?.GetCustomAttribute<I18NResourceAttribute>();
        if (applicationAssembly is not null && applicationResource is not null)
        {
            declarationAssembly = applicationAssembly;
        }

        lock (ResourceLock)
        {
            if (_declarationAssembly == declarationAssembly)
            {
                return;
            }

            if (_declarationAssembly is not null && declarationAssembly != applicationAssembly)
            {
                throw new InvalidOperationException(
                    "发现多个语言资源声明，请在启动项目的 I18N 部分类上指定资源。"
                );
            }

            I18NResourceAttribute resource =
                applicationResource
                ?? declarationAssembly.GetCustomAttribute<I18NResourceAttribute>()
                ?? throw new InvalidOperationException(
                    "请在 I18N 部分类上使用 I18NResource 特性声明语言资源。"
                );
            var manager = new ResourceManager(
                resource.BaseName,
                resource.ResourceAssemblyAnchor?.Assembly ?? declarationAssembly
            );
            _declarationAssembly = declarationAssembly;
            Volatile.Write(ref _resources, manager);
        }

        // 初始化期间已求值的绑定可在资源就绪后重新取值。
        LanguageChanged?.Invoke(Current, new PropertyChangedEventArgs(nameof(LanguageTag)));
    }

    /// <summary>获取已注册的资源，同时兼容旧的启动程序集声明。</summary>
    private static ResourceManager? GetResourceManager()
    {
        ResourceManager? manager = Volatile.Read(ref _resources);
        if (manager is not null)
        {
            return manager;
        }

        // 未声明资源的应用只需检查一次入口程序集；稍后显式注册仍可正常生效。
        if (Volatile.Read(ref _entryAssemblyChecked) != 0)
        {
            return null;
        }

        Assembly? applicationAssembly = Assembly.GetEntryAssembly();
        if (applicationAssembly is null)
        {
            return null;
        }

        if (applicationAssembly.IsDefined(typeof(I18NResourceAttribute), inherit: false))
        {
            RegisterResourceAssembly(applicationAssembly);
        }
        else
        {
            Volatile.Write(ref _entryAssemblyChecked, 1);
        }

        return Volatile.Read(ref _resources);
    }

    /// <summary>向 Avalonia 提供随资源键、备用文案或界面语言更新的绑定。</summary>
    public object ProvideValue()
    {
        // 固定键和备用文案只依赖语言变化，使用单绑定减少每个控件的绑定对象和订阅数量。
        if (Key is not BindingBase && Fallback is not BindingBase && Arguments.Count == 0)
        {
            return new ReflectionBinding(nameof(LanguageTag))
            {
                Source = Current,
                Mode = BindingMode.OneWay,
                Converter = new FixedI18NConverter(Key as string, Fallback as string),
            };
        }

        var binding = new MultiBinding
        {
            Mode = BindingMode.OneWay,
            Converter = Current,
            Bindings =
            [
                new ReflectionBinding(nameof(LanguageTag)) { Source = Current },
                ToBinding(Key),
                ToBinding(Fallback),
            ],
        };

        // 格式化参数也是绑定，任何一个参数变化都会重新格式化。
        foreach (BindingBase argument in Arguments)
        {
            binding.Bindings.Add(argument);
        }

        return binding;
    }

    /// <summary>语言、键或备用文案变化时，从同一套资源重新取值。</summary>
    object IMultiValueConverter.Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        int valueCount = values.Count;
        if (valueCount < 3)
        {
            // 语言、键和备用文案尚未齐全时，保留目标属性已经显示的值。
            return BindingOperations.DoNothing;
        }

        CultureInfo language = Volatile.Read(ref _resourceCulture);
        string text = GetString(values[1] as string, values[2] as string, language);
        if (valueCount == 3)
        {
            return text;
        }

        // 常见的一至三个参数直接使用格式化重载，避免每次绑定更新都分配参数数组。
        object? first = GetFormatArgument(values[3]);
        if (valueCount == 4)
        {
            return FormatTemplate(language, text, first);
        }

        object? second = GetFormatArgument(values[4]);
        if (valueCount == 5)
        {
            return FormatTemplate(language, text, first, second);
        }

        object? third = GetFormatArgument(values[5]);
        if (valueCount == 6)
        {
            return FormatTemplate(language, text, first, second, third);
        }

        var arguments = new object?[valueCount - 3];
        arguments[0] = first;
        arguments[1] = second;
        arguments[2] = third;
        for (int index = 3; index < arguments.Length; index++)
        {
            arguments[index] = GetFormatArgument(values[index + 3]);
        }

        return FormatTemplate(language, text, arguments);
    }

    /// <summary>将尚未绑定的格式参数转换为空值，其余参数原样传递。</summary>
    private static object? GetFormatArgument(object? value)
    {
        if (value == AvaloniaProperty.UnsetValue)
        {
            return null;
        }

        return value;
    }

    /// <summary>使用一个参数格式化，模板无法解析时保留原文。</summary>
    private static string FormatTemplate(CultureInfo culture, string template, object? first)
    {
        try
        {
            return string.Format(culture, template, first);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>使用两个参数格式化，模板无法解析时保留原文。</summary>
    private static string FormatTemplate(CultureInfo culture, string template, object? first, object? second)
    {
        try
        {
            return string.Format(culture, template, first, second);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>使用三个参数格式化，模板无法解析时保留原文。</summary>
    private static string FormatTemplate(CultureInfo culture, string template, object? first, object? second, object? third)
    {
        try
        {
            return string.Format(culture, template, first, second, third);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>使用已有的参数数组格式化，模板或参数格式错误时保留原文。</summary>
    private static string FormatTemplate(CultureInfo culture, string template, object?[] arguments)
    {
        try
        {
            return string.Format(culture, template, arguments);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>保留已有绑定，将固定值包装为绑定源。</summary>
    private static BindingBase ToBinding(object? value)
    {
        return value as BindingBase ?? new ReflectionBinding { Source = value };
    }
}
