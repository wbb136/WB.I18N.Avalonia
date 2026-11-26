using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>验证全新运行时实例初始化时的语言选择。</summary>
[Collection(RuntimeCollection.Name)]
public sealed class DefaultLanguageTests
{
    /// <summary>默认语言捕获首次初始化时的界面文化，且后续线程文化变动不改变库语言。</summary>
    [Theory]
    [InlineData("fr-CA")]
    [InlineData("")]
    public void InitialLanguageCapturesCurrentUICulture(string languageTag)
    {
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        var loadContext = new AssemblyLoadContext(nameof(DefaultLanguageTests), isCollectible: true);

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(languageTag);

            // 单独装载运行时程序集，避免 testhost 中已初始化的静态字段影响首次读取。
            Assembly assembly = loadContext.LoadFromAssemblyPath(typeof(I18NExtension).Assembly.Location);
            Type extension = assembly.GetType(typeof(I18NExtension).FullName!, throwOnError: true)!;
            Assert.Same(loadContext, AssemblyLoadContext.GetLoadContext(extension.Assembly));
            object current = extension.GetProperty(nameof(I18NExtension.Current))!.GetValue(null)!;
            PropertyInfo language = extension.GetProperty(nameof(I18NExtension.LanguageTag))!;
            Assert.Equal(languageTag, Assert.IsType<string>(language.GetValue(current)));
            Assert.Equal(languageTag, CultureInfo.CurrentUICulture.Name);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(languageTag, Assert.IsType<string>(language.GetValue(current)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
            loadContext.Unload();
        }
    }
}
