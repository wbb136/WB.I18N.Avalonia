using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>验证资源声明诊断的编号、生成行为及精确源码位置。</summary>
public sealed class GeneratorDiagnosticTests
{
    /// <summary>资源声明只允许符合约定的顶层非泛型静态分部类。</summary>
    [Theory]
    [InlineData("public partial class I18N")]
    [InlineData("public static class I18N")]
    [InlineData("public static partial class I18N<T>")]
    [InlineData("file static partial class I18N")]
    public void ReportsWBI18N001ForInvalidDeclaration(string declaration)
    {
        string source = $$"""
            using WB.I18N.Avalonia;

            [I18NResource("TestProject.Messages")]
            {{declaration}}
            {
            }
            """;
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(source);
        GeneratorRunResult result = Run(compilation);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        AssertDiagnostic("WBI18N001", diagnostic, Assert.Single(compilation.SyntaxTrees));
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>即使声明文件路径相同，也在每个声明自己的语法树上报错。</summary>
    [Fact]
    public void ReportsWBI18N002ForEachDeclaration()
    {
        SyntaxTree other = GeneratorHarness.CreateSyntaxTree(
            GeneratorHarness.NormalDeclaration.Replace("namespace TestProject;", "namespace Other;", StringComparison.Ordinal),
            GeneratorHarness.DeclarationPath);
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(GeneratorHarness.NormalDeclaration).AddSyntaxTrees(other);
        GeneratorRunResult result = Run(compilation);

        Assert.Equal(2, result.Diagnostics.Length);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            Diagnostic diagnostic = Assert.Single(result.Diagnostics, item =>
            {
                return ReferenceEquals(item.Location.SourceTree, tree);
            });
            AssertDiagnostic("WBI18N002", diagnostic, tree);
        }

        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>程序集资源冲突时保留实际语法树及 #line 映射的位置。</summary>
    [Fact]
    public void ReportsWBI18N003AtMappedLocation()
    {
        const string source = """
            using WB.I18N.Avalonia;

            [assembly: I18NResource("Other.Messages")]

            namespace TestProject;

            #line 250 "MappedResource.cs"
            [I18NResource("TestProject.Messages")]
            public static partial class I18N
            {
            }
            #line default
            """;
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(source);
        GeneratorDriver driver = GeneratorHarness.CreateDriver().RunGenerators(compilation);
        GeneratorRunResult result = Assert.Single(driver.GetRunResult().Results);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        SyntaxTree originalTree = Assert.Single(compilation.SyntaxTrees);
        AssertDiagnostic("WBI18N003", diagnostic, originalTree);
        Assert.Equal("MappedResource.cs", Path.GetFileName(diagnostic.Location.GetMappedLineSpan().Path));
        Assert.Equal(249, diagnostic.Location.GetMappedLineSpan().StartLinePosition.Line);
        Assert.Equal(GeneratorHarness.DeclarationPath, diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(result.GeneratedSources);

        // 使用同一个驱动移动声明，确保增量诊断不再引用上一次编译的语法树。
        string moved = "\n" + source.Replace("#line 250", "#line 270", StringComparison.Ordinal);
        SyntaxTree movedTree = GeneratorHarness.CreateSyntaxTree(moved, GeneratorHarness.DeclarationPath);
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(originalTree, movedTree));
        result = Assert.Single(driver.GetRunResult().Results);
        diagnostic = Assert.Single(result.Diagnostics);

        AssertDiagnostic("WBI18N003", diagnostic, movedTree);
        Assert.Equal(269, diagnostic.Location.GetMappedLineSpan().StartLinePosition.Line);
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>空白清单名和无法解析的资源类型均报告资源声明错误。</summary>
    [Theory]
    [InlineData("\"   \"")]
    [InlineData("typeof(MissingResourceType)")]
    public void ReportsWBI18N004ForInvalidResourceLocation(string argument)
    {
        string source = $$"""
            using WB.I18N.Avalonia;

            [I18NResource({{argument}})]
            public static partial class I18N
            {
            }
            """;
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(source);
        GeneratorRunResult result = Run(compilation);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        AssertDiagnostic("WBI18N004", diagnostic, Assert.Single(compilation.SyntaxTrees));
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>资源文件损坏或不可读取时保留访问入口，停止生成键常量。</summary>
    [Theory]
    [InlineData("<root><data>")]
    [InlineData(null)]
    public void ReportsWBI18N005ForUnreadableDefaultResource(string? text)
    {
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(GeneratorHarness.NormalDeclaration);
        var resource = new InMemoryAdditionalText(GeneratorHarness.ResourcePath, text, GeneratorHarness.BaseName);
        GeneratorRunResult result = Run(compilation, resource);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        AssertDiagnostic("WBI18N005", diagnostic, Assert.Single(compilation.SyntaxTrees));
        Assert.Contains(GeneratorHarness.ResourcePath, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("I18N.g.cs", Assert.Single(result.GeneratedSources).HintName);
    }

    /// <summary>运行生成器并提取它自身产生的结果。</summary>
    private static GeneratorRunResult Run(CSharpCompilation compilation, params AdditionalText[] resources)
    {
        GeneratorDriver driver = GeneratorHarness.CreateDriver(resources).RunGenerators(compilation);
        return Assert.Single(driver.GetRunResult().Results);
    }

    /// <summary>确认诊断挂在实际类节点的源码位置，包含物理和映射行列。</summary>
    private static void AssertDiagnostic(string id, Diagnostic diagnostic, SyntaxTree sourceTree)
    {
        ClassDeclarationSyntax declaration = Assert.Single(
            sourceTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>());
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Same(sourceTree, diagnostic.Location.SourceTree);
        Assert.Equal(declaration.Span, diagnostic.Location.SourceSpan);
        Assert.Equal(declaration.GetLocation().GetLineSpan(), diagnostic.Location.GetLineSpan());
        Assert.Equal(declaration.GetLocation().GetMappedLineSpan(), diagnostic.Location.GetMappedLineSpan());
    }
}
