namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>验证区域资源、父区域资源和缺省文案的读取顺序。</summary>
[Collection(RuntimeCollection.Name)]
public sealed class ResourceLookupTests : RuntimeTestScope
{
    /// <summary>从 fr-CA 开始，依次尝试父区域、中性资源、备用文案和资源键。</summary>
    [Theory]
    [InlineData("Regional", "备用", "地区文案")]
    [InlineData("Parent", "备用", "父区域文案")]
    [InlineData("Neutral", "备用", "中性文案")]
    [InlineData("Missing", "备用", "备用")]
    [InlineData("  Missing  ", null, "Missing")]
    [InlineData("  Regional  ", null, "地区文案")]
    [InlineData(null, "备用", "备用")]
    [InlineData("  ", null, "")]
    public void LookupFallsBackInOrder(string? key, string? fallback, string expected)
    {
        I18NExtension.Current.LanguageTag = "fr-CA";

        Assert.Equal(expected, I18NExtension.GetString(key, fallback));
    }
}
