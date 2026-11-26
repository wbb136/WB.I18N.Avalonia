using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>验证公开格式化入口和 Avalonia 转换器的正常与错误路径。</summary>
[Collection(RuntimeCollection.Name)]
public sealed class FormattingTests : RuntimeTestScope
{
    /// <summary>公开入口与转换器均能按参数个数格式化模板。</summary>
    [Theory]
    [InlineData("OneArgument", 1, "甲")]
    [InlineData("TwoArguments", 2, "甲/乙")]
    [InlineData("ThreeArguments", 3, "甲/乙/丙")]
    [InlineData("FourArguments", 4, "甲/乙/丙/丁")]
    public void FormatsEachSupportedArgumentCount(string key, int count, string expected)
    {
        object?[] nameParts = ["甲", "乙", "丙", "丁"];
        object?[] arguments = nameParts[..count];

        Assert.Equal(expected, I18NExtension.Format(key, arguments));
        Assert.Equal(expected, Convert(key, null, arguments));
    }

    /// <summary>语法错误与缺少占位参数均应回传原始模板。</summary>
    [Theory]
    [InlineData("BadTemplate", 1, "格式 {0")]
    [InlineData("BadTemplate", 2, "格式 {0")]
    [InlineData("BadTemplate", 3, "格式 {0")]
    [InlineData("BadTemplate", 4, "格式 {0")]
    [InlineData("MissingArgument", 1, "缺少 {4}")]
    [InlineData("MissingArgument", 2, "缺少 {4}")]
    [InlineData("MissingArgument", 3, "缺少 {4}")]
    [InlineData("MissingArgument", 4, "缺少 {4}")]
    public void ReturnsTemplateWhenFormattingFails(string key, int count, string template)
    {
        object?[] nameParts = ["甲", "乙", "丙", "丁"];
        object?[] arguments = nameParts[..count];

        Assert.Equal(template, I18NExtension.Format(key, arguments));
        Assert.Equal(template, Convert(key, null, arguments));
    }

    /// <summary>未提供参数时保留模板，缺少资源时仍使用原备用文案。</summary>
    [Fact]
    public void ReturnsOriginalTextWithoutArguments()
    {
        Assert.Equal("格式 {0", I18NExtension.Format("BadTemplate"));
        Assert.Equal("格式 {0", I18NExtension.Format("BadTemplate", null));
        Assert.Equal("{0}/{1}", Convert("TwoArguments", null, []));
        Assert.Equal("备用 {0", Convert("Missing", "备用 {0", ["甲"]));
    }

    /// <summary>绑定未设置值应按空值格式化，不应显示 Avalonia 哨兵。</summary>
    [Theory]
    [InlineData("OneArgument", 1, "")]
    [InlineData("TwoArguments", 2, "/")]
    [InlineData("ThreeArguments", 3, "//")]
    [InlineData("FourArguments", 4, "///")]
    public void TreatsUnsetBindingArgumentsAsNull(string key, int count, string expected)
    {
        var arguments = new object?[count];
        Array.Fill(arguments, AvaloniaProperty.UnsetValue);

        Assert.Equal(expected, Convert(key, null, arguments));
    }

    /// <summary>格式化按所选界面语言处理参数，包括使用父区或中性资源模板时。</summary>
    [Fact]
    public void FormatsWithSelectedLanguage()
    {
        I18NExtension.Current.LanguageTag = "fr-CA";
        CultureInfo culture = CultureInfo.GetCultureInfo("fr-CA");
        const decimal value = 1234.5m;
        string expected = string.Format(culture, "{0:N1}", value);

        Assert.Equal(expected, I18NExtension.Format("Number", value));
        Assert.Equal(expected, Convert("Number", null, [value]));
    }

    /// <summary>输入尚未齐全时保留控件原值，且不访问不存在的参数索引。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IncompleteBindingLeavesTargetUnchanged(int count)
    {
        object?[] values = ["en", "Regional"];
        var converter = (IMultiValueConverter)I18NExtension.Current;

        object? result = converter.Convert(values[..count], typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Same(BindingOperations.DoNothing, result);
    }

    /// <summary>按语言、键、备用文案和占位参数的真实顺序调用转换器。</summary>
    private static object? Convert(string key, string? fallback, object?[] arguments)
    {
        object?[] values = [I18NExtension.Current.LanguageTag, key, fallback, .. arguments];
        var converter = (IMultiValueConverter)I18NExtension.Current;

        return converter.Convert(values, typeof(string), null, CultureInfo.InvariantCulture);
    }
}
