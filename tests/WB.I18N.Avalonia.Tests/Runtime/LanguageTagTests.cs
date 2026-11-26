using System.ComponentModel;
using System.Globalization;

namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>验证语言变更、通知去重和线程文化隔离。</summary>
[Collection(RuntimeCollection.Name)]
public sealed class LanguageTagTests : RuntimeTestScope
{
    /// <summary>变更语言应规范化标签，只通知一次，并保留调用线程文化。</summary>
    [Fact]
    public void LanguageChangeCanonicalizesAndNotifiesOnce()
    {
        I18NExtension current = I18NExtension.Current;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        var changes = new List<string?>();
        PropertyChangedEventHandler handler = (sender, args) =>
        {
            Assert.Same(current, sender);
            changes.Add(args.PropertyName);
            Assert.Equal("fr-CA", current.LanguageTag);
        };

        current.PropertyChanged += handler;
        try
        {
            current.LanguageTag = "  fr-ca  ";
            current.LanguageTag = "FR-ca";
            current.LanguageTag = "fr-CA";

            Assert.Equal("fr-CA", current.LanguageTag);
            Assert.Equal(nameof(I18NExtension.LanguageTag), Assert.Single(changes));
            Assert.Same(originalCulture, CultureInfo.CurrentCulture);
            Assert.Same(originalUICulture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            current.PropertyChanged -= handler;
        }
    }

    /// <summary>空值、空白和非法标签均不改变语言或发出通知。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("en US")]
    public void InvalidLanguageIsIgnored(string? languageTag)
    {
        I18NExtension current = I18NExtension.Current;
        int notifications = 0;
        PropertyChangedEventHandler handler = (_, _) =>
        {
            notifications++;
        };

        current.PropertyChanged += handler;
        try
        {
            current.LanguageTag = languageTag!;
            Assert.Equal("en", current.LanguageTag);
            Assert.Equal(0, notifications);
        }
        finally
        {
            current.PropertyChanged -= handler;
        }
    }
}
