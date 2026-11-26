using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WB.I18N.Generator;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>创建独立内存编译并运行实际源生成器。</summary>
internal static class GeneratorHarness
{
    public const string ProjectName = "TestProject";
    public const string BaseName = "TestProject.Messages";
    public const string DeclarationPath = "/src/I18N.cs";
    public const string ResourcePath = "/src/Resources/Messages.resx";

    public const string NormalDeclaration = """
        using WB.I18N.Avalonia;

        namespace TestProject;

        [I18NResource("TestProject.Messages")]
        public static partial class I18N
        {
        }
        """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp11);
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    /// <summary>创建支持模块初始化器的指定程序集编译，不加载生成结果。</summary>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = ProjectName,
        string path = DeclarationPath)
    {
        return CSharpCompilation.Create(
            assemblyName,
            [CreateSyntaxTree(source, path)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>以显式文件路径解析测试源码，供诊断验证与增量替换使用。</summary>
    public static SyntaxTree CreateSyntaxTree(string source, string path)
    {
        return CSharpSyntaxTree.ParseText(source, ParseOptions, path);
    }

    /// <summary>创建开启增量步骤追踪的驱动器，并提供资源文件元数据。</summary>
    public static GeneratorDriver CreateDriver(params AdditionalText[] resources)
    {
        return CSharpGeneratorDriver.Create(
            [new I18NResourceGenerator().AsSourceGenerator()],
            resources,
            ParseOptions,
            new ResourceAnalyzerOptionsProvider(),
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    /// <summary>通过稳定的提示文件名返回唯一生成源码。</summary>
    public static string GetGeneratedSource(GeneratorRunResult result, string hintName)
    {
        GeneratedSourceResult source = Assert.Single(result.GeneratedSources, item =>
        {
            return item.HintName == hintName;
        });
        return source.SourceText.ToString();
    }

    /// <summary>返回运行时、Avalonia 和平台程序集的元数据引用，无需加载测试编译。</summary>
    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            typeof(I18NExtension).Assembly.Location,
            typeof(global::Avalonia.Data.Converters.IMultiValueConverter).Assembly.Location,
        };
        string platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        paths.UnionWith(platformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

        foreach (string path in paths)
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        return references.ToImmutable();
    }
}
