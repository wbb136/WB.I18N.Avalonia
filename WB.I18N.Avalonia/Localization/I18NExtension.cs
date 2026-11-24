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
    private static CultureInfo _resourceCulture = CultureInfo.GetCultureInfo("zh");
    private static event PropertyChangedEventHandler? LanguageChanged;

    /// <summary>获取应用共用的语言绑定源。</summary>
    public static I18NExtension Current { get; } = new();

    /// <summary>获取或设置 .NET 支持的界面语言或地区标签，例如 fr 或 fr-CA。无效值会被忽略。</summary>
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

    /// <summary>查找当前语言的资源模板，并使用传入的参数填充占位符。</summary>
    public static string Format(string resourceKey, params object?[]? arguments)
    {
        CultureInfo culture = Volatile.Read(ref _resourceCulture);
        string template = GetString(resourceKey, null, culture);
        return arguments is { Length: > 0 }
            ? string.Format(culture, template, arguments)
            : template;
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
        CultureInfo language = Volatile.Read(ref _resourceCulture);
        string text = GetString(values[1] as string, values[2] as string, language);
        if (values.Count == 3)
        {
            return text;
        }

        // 常见的一至三个参数直接使用格式化重载，避免每次绑定更新都分配参数数组。
        if (values.Count == 4)
        {
            object? argument = values[3] == AvaloniaProperty.UnsetValue ? null : values[3];
            return string.Format(language, text, argument);
        }

        if (values.Count == 5)
        {
            object? first = values[3] == AvaloniaProperty.UnsetValue ? null : values[3];
            object? second = values[4] == AvaloniaProperty.UnsetValue ? null : values[4];
            return string.Format(language, text, first, second);
        }

        if (values.Count == 6)
        {
            object? first = values[3] == AvaloniaProperty.UnsetValue ? null : values[3];
            object? second = values[4] == AvaloniaProperty.UnsetValue ? null : values[4];
            object? third = values[5] == AvaloniaProperty.UnsetValue ? null : values[5];
            return string.Format(language, text, first, second, third);
        }

        var arguments = new object?[values.Count - 3];
        for (int index = 0; index < arguments.Length; index++)
        {
            object? value = values[index + 3];
            arguments[index] = value == AvaloniaProperty.UnsetValue ? null : value;
        }

        return string.Format(language, text, arguments);
    }

    /// <summary>保留已有绑定，将固定值包装为绑定源。</summary>
    private static BindingBase ToBinding(object? value)
    {
        return value as BindingBase ?? new ReflectionBinding { Source = value };
    }
}
