using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>验证固定键单绑定及动态键、备用文案和参数的多绑定。</summary>
[Collection(RuntimeCollection.Name)]
public sealed class BindingTests : RuntimeTestScope
{
    /// <summary>固定键使用单绑定，并按最新界面语言更新文案。</summary>
    [Fact]
    public void FixedKeyKeepsSingleBindingAndTracksLanguage()
    {
        var extension = new I18NExtension
        {
            Key = "Regional",
            Fallback = "备用",
        };

        var binding = Assert.IsType<ReflectionBinding>(extension.ProvideValue());
        IValueConverter converter = Assert.IsAssignableFrom<IValueConverter>(binding.Converter);
        Assert.Equal("FixedI18NConverter", converter.GetType().Name);
        Assert.Same(I18NExtension.Current, binding.Source);
        Assert.Equal(nameof(I18NExtension.LanguageTag), binding.Path);
        Assert.Equal(BindingMode.OneWay, binding.Mode);
        Assert.Equal("中性地区文案", converter.Convert("en", typeof(string), null, CultureInfo.InvariantCulture));

        I18NExtension.Current.LanguageTag = "fr-CA";
        Assert.Equal("地区文案", converter.Convert("fr-CA", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Same(AvaloniaProperty.UnsetValue, converter.ConvertBack("任意", typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>动态键、动态备用文案和绑定参数分别要求使用多绑定。</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void DynamicInputsKeepBindingsInOrder(bool dynamicKey, bool dynamicFallback, bool hasArgument)
    {
        var key = new ReflectionBinding("ResourceKey");
        var fallback = new ReflectionBinding("FallbackText");
        var argument = new ReflectionBinding("Argument");
        var extension = new I18NExtension
        {
            Key = "OneArgument",
            Fallback = "静态备用",
        };

        if (dynamicKey)
        {
            extension.Key = key;
        }

        if (dynamicFallback)
        {
            extension.Fallback = fallback;
        }

        if (hasArgument)
        {
            extension.Arguments.Add(argument);
        }

        var binding = Assert.IsType<MultiBinding>(extension.ProvideValue());
        Assert.Equal(BindingMode.OneWay, binding.Mode);
        Assert.Same(I18NExtension.Current, binding.Converter);
        var language = Assert.IsType<ReflectionBinding>(binding.Bindings[0]);
        Assert.Same(I18NExtension.Current, language.Source);
        Assert.Equal(nameof(I18NExtension.LanguageTag), language.Path);
        AssertInputBinding(binding.Bindings[1], key, "OneArgument", dynamicKey);
        AssertInputBinding(binding.Bindings[2], fallback, "静态备用", dynamicFallback);

        if (hasArgument)
        {
            Assert.Equal(4, binding.Bindings.Count);
            Assert.Same(argument, binding.Bindings[3]);
        }
        else
        {
            Assert.Equal(3, binding.Bindings.Count);
        }
    }

    /// <summary>动态输入保留绑定本身，固定输入从包装绑定读取源对象。</summary>
    private static void AssertInputBinding(BindingBase actual, BindingBase original, string constant, bool isDynamic)
    {
        if (isDynamic)
        {
            Assert.Same(original, actual);
            return;
        }

        Assert.Equal(constant, Assert.IsType<ReflectionBinding>(actual).Source);
    }
}
