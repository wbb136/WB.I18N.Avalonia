using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>验证无关编译修改的值缓存和相关键变动的正确失效。</summary>
public sealed class GeneratorIncrementalityTests
{
    private const string SourceModelStep = "WBI18N.SourceModel";

    /// <summary>无关语法树更新后，最终生成模型保持相等，生成源码保持一致。</summary>
    [Fact]
    public void UnrelatedSourceChangeKeepsSourceModelCached()
    {
        const string unrelatedPath = "/src/Unrelated.cs";
        const string initialUnrelated = """
            namespace Other;

            internal sealed class Unrelated
            {
                private const int Value = 1;
            }
            """;
        SyntaxTree initialTree = GeneratorHarness.CreateSyntaxTree(initialUnrelated, unrelatedPath);
        SyntaxTree changedTree = GeneratorHarness.CreateSyntaxTree(
            initialUnrelated.Replace("Value = 1", "Value = 2", StringComparison.Ordinal),
            unrelatedPath);
        CSharpCompilation initial = GeneratorHarness.CreateCompilation(GeneratorHarness.NormalDeclaration).AddSyntaxTrees(initialTree);
        CSharpCompilation changed = initial.ReplaceSyntaxTree(initialTree, changedTree);
        var resource = new InMemoryAdditionalText(
            GeneratorHarness.ResourcePath,
            """<root><data name="Greeting"><value>Hello</value></data></root>""",
            GeneratorHarness.BaseName);

        GeneratorDriver driver = GeneratorHarness.CreateDriver(resource).RunGenerators(initial);
        GeneratorRunResult first = Assert.Single(driver.GetRunResult().Results);
        driver = driver.RunGenerators(changed);
        GeneratorRunResult second = Assert.Single(driver.GetRunResult().Results);

        AssertStableSourceModel(second);
        Assert.Equal(GeneratorHarness.GetGeneratedSource(first, "I18N.g.cs"), GeneratorHarness.GetGeneratedSource(second, "I18N.g.cs"));
        Assert.Equal(GeneratorHarness.GetGeneratedSource(first, "I18NKeys.g.cs"), GeneratorHarness.GetGeneratedSource(second, "I18NKeys.g.cs"));
    }

    /// <summary>只调整译文不刷新生成源码，新增资源键则使生成模型失效。</summary>
    [Fact]
    public void TranslationChangeIsCachedButNewKeyInvalidatesSourceModel()
    {
        var original = new InMemoryAdditionalText(
            GeneratorHarness.ResourcePath,
            """<root><data name="Greeting"><value>Hello</value></data></root>""",
            GeneratorHarness.BaseName);
        var changedTranslation = new InMemoryAdditionalText(
            GeneratorHarness.ResourcePath,
            """<root><data name="Greeting"><value>Bonjour</value></data></root>""",
            GeneratorHarness.BaseName);
        var newKey = new InMemoryAdditionalText(
            GeneratorHarness.ResourcePath,
            """<root><data name="Greeting"><value>Bonjour</value></data><data name="NewKey"><value>新键</value></data></root>""",
            GeneratorHarness.BaseName);
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(GeneratorHarness.NormalDeclaration);
        GeneratorDriver driver = GeneratorHarness.CreateDriver(original).RunGenerators(compilation);
        GeneratorRunResult first = Assert.Single(driver.GetRunResult().Results);

        driver = driver.ReplaceAdditionalText(original, changedTranslation).RunGenerators(compilation);
        GeneratorRunResult second = Assert.Single(driver.GetRunResult().Results);
        AssertStableSourceModel(second);
        Assert.Equal(GeneratorHarness.GetGeneratedSource(first, "I18NKeys.g.cs"), GeneratorHarness.GetGeneratedSource(second, "I18NKeys.g.cs"));

        driver = driver.ReplaceAdditionalText(changedTranslation, newKey).RunGenerators(compilation);
        GeneratorRunResult third = Assert.Single(driver.GetRunResult().Results);
        Assert.Equal(IncrementalStepRunReason.Modified, GetSourceModelReason(third));
        Assert.Contains("public const string NewKey = \"NewKey\";", GeneratorHarness.GetGeneratedSource(third, "I18NKeys.g.cs"), StringComparison.Ordinal);
    }

    /// <summary>读取最终生成模型的跟踪结果，避免把缓存命中误判成输入未执行。</summary>
    private static IncrementalStepRunReason GetSourceModelReason(GeneratorRunResult result)
    {
        IncrementalGeneratorRunStep step = Assert.Single(result.TrackedSteps[SourceModelStep]);
        return Assert.Single(step.Outputs).Reason;
    }

    /// <summary>确认最终模型和依赖它的源码输出步骤都复用或保持相等。</summary>
    private static void AssertStableSourceModel(GeneratorRunResult result)
    {
        IncrementalStepRunReason reason = GetSourceModelReason(result);
        IncrementalStepRunReason[] stableReasons = [IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged];
        Assert.Contains(reason, stableReasons);

        IncrementalGeneratorRunStep emission = Assert.Single(result.TrackedOutputSteps.Values.SelectMany(static steps =>
        {
            return steps;
        }), static step =>
        {
            return step.Inputs.Any(static input =>
            {
                return input.Source.Name == SourceModelStep;
            });
        });
        Assert.Contains(Assert.Single(emission.Outputs).Reason, stableReasons);
    }
}
