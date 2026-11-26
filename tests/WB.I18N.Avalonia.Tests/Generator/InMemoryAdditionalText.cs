using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>提供带资源清单元数据的内存输入，模拟 MSBuild 传入的资源。</summary>
internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText? _text;

    /// <summary>创建指定文件路径、内容和资源元数据的输入。</summary>
    public InMemoryAdditionalText(string path, string? text, string? baseName = null, string? assemblyName = null)
    {
        Path = path;
        Options = new ResourceAnalyzerOptions(baseName, assemblyName);
        if (text is not null)
        {
            _text = SourceText.From(text);
        }
    }

    /// <summary>获取生成器观察到的输入路径。</summary>
    public override string Path { get; }

    /// <summary>获取该文件对应的资源清单名、程序集名等元数据。</summary>
    public ResourceAnalyzerOptions Options { get; }

    /// <summary>返回文本内容，空值用于模拟读取失败。</summary>
    public override SourceText? GetText(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _text;
    }
}
