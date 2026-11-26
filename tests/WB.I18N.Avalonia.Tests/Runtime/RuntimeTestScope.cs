namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>隔离每条运行时测试对应用语言状态的修改。</summary>
public abstract class RuntimeTestScope : IDisposable
{
    private readonly string _originalLanguage = I18NExtension.Current.LanguageTag;

    /// <summary>在用例执行前将语言统一设置为英语。</summary>
    protected RuntimeTestScope()
    {
        I18NExtension.Current.LanguageTag = "en";
    }

    /// <summary>还原用例开始前的语言状态。</summary>
    public void Dispose()
    {
        I18NExtension.Current.LanguageTag = _originalLanguage;
        GC.SuppressFinalize(this);
    }
}
