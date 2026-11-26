using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>为内存资源模拟分析器可见的 MSBuild 元数据。</summary>
internal sealed class ResourceAnalyzerOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _settings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>保存本资源可选的清单名和资源程序集名。</summary>
    public ResourceAnalyzerOptions(string? baseName = null, string? assemblyName = null)
    {
        if (baseName is not null)
        {
            _settings.Add("build_metadata.AdditionalFiles.WBI18NResourceBaseName", baseName);
        }

        if (assemblyName is not null)
        {
            _settings.Add("build_metadata.AdditionalFiles.WBI18NResourceAssembly", assemblyName);
        }
    }

    /// <summary>按 Roslyn 请求的键查找当前文件元数据。</summary>
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        return _settings.TryGetValue(key, out value);
    }
}
