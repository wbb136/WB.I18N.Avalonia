using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>只向生成器提供每个资源文件的分析器配置。</summary>
internal sealed class ResourceAnalyzerOptionsProvider : AnalyzerConfigOptionsProvider
{
    private static readonly ResourceAnalyzerOptions Empty = new();

    /// <summary>获取编译全局配置，资源测试不需要全局设置。</summary>
    public override AnalyzerConfigOptions GlobalOptions
    {
        get
        {
            return Empty;
        }
    }

    /// <summary>获取语法树配置，资源测试不需要源码文件设置。</summary>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return Empty;
    }

    /// <summary>从内存资源输入取回与对应 AdditionalFile 绑定的元数据。</summary>
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        if (textFile is InMemoryAdditionalText resource)
        {
            return resource.Options;
        }

        return Empty;
    }
}
