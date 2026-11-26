using WB.I18N.Avalonia;

[assembly: I18NResource("WB.I18N.Avalonia.Tests.Runtime.Resources.Messages")]

namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>为运行时测试一次性注册独立资源，并建立可恢复的语言基线。</summary>
public sealed class RuntimeFixture
{
    /// <summary>注册测试程序集资源，避免将 testhost 当作语言资源入口。</summary>
    public RuntimeFixture()
    {
        I18NExtension.RegisterResourceAssembly(typeof(RuntimeFixture).Assembly);
        I18NExtension.Current.LanguageTag = "en";
    }
}
