using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WB.I18N.Generator;

/// <summary>保存一个默认资源文件及其提供的字符串键。</summary>
internal sealed class ResourceKeySource
{
    private const string BaseNameOption = "build_metadata.AdditionalFiles.WBI18NResourceBaseName";
    private const string AssemblyOption = "build_metadata.AdditionalFiles.WBI18NResourceAssembly";

    /// <summary>创建资源键读取结果，供生成阶段与资源声明匹配。</summary>
    private ResourceKeySource(
        string baseName,
        string? assemblyName,
        string path,
        ImmutableArray<string> keys,
        string? error
    )
    {
        BaseName = baseName;
        AssemblyName = assemblyName;
        Path = path;
        Keys = keys;
        Error = error;
    }

    /// <summary>获取不含扩展名的资源清单名。</summary>
    public string BaseName { get; }

    /// <summary>获取资源所在程序集的短名称，由项目提供。</summary>
    public string? AssemblyName { get; }

    /// <summary>获取资源文件路径，用于构建诊断。</summary>
    public string Path { get; }

    /// <summary>获取默认资源中定义的字符串键。</summary>
    public ImmutableArray<string> Keys { get; }

    /// <summary>获取读取失败的原因，未失败时为空。</summary>
    public string? Error { get; }

    /// <summary>读取构建系统标记的默认资源，仅提取字符串条目的名称。</summary>
    public static ResourceKeySource? Read(
        AdditionalText file,
        AnalyzerConfigOptions options,
        CancellationToken cancellationToken
    )
    {
        if (!options.TryGetValue(BaseNameOption, out string? baseName) || string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        options.TryGetValue(AssemblyOption, out string? assemblyName);
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = null;
        }

        string? text = file.GetText(cancellationToken)?.ToString();
        if (text is null)
        {
            return new ResourceKeySource(baseName!, assemblyName, file.Path, ImmutableArray<string>.Empty, "无法读取文件内容");
        }

        // 只读取 XML 元数据，不解析资源值或引用的外部文件。
        try
        {
            using (var input = new StringReader(text))
            {
                using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                {
                    XElement root = XElement.Load(reader);
                    var keys = ImmutableArray.CreateBuilder<string>();
                    foreach (XElement entry in root.Elements("data"))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string? key = (string?)entry.Attribute("name");
                        string? type = (string?)entry.Attribute("type");
                        if (!string.IsNullOrWhiteSpace(key) && entry.Attribute("mimetype") is null &&
                            (type is null || type == "System.String" || type.StartsWith("System.String,", StringComparison.Ordinal)))
                        {
                            keys.Add(key!);
                        }
                    }

                    return new ResourceKeySource(baseName!, assemblyName, file.Path, keys.ToImmutable(), null);
                }
            }
        }
        catch (XmlException exception)
        {
            return new ResourceKeySource(baseName!, assemblyName, file.Path, ImmutableArray<string>.Empty, exception.Message);
        }
    }
}
