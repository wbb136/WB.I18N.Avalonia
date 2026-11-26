using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace WB.I18N.Avalonia.Tests.Generator;

/// <summary>验证资源键的可编译名称、原值、重名排序和资源筛选。</summary>
public sealed class ResourceKeyTests
{
    /// <summary>特殊字符和关键字得到合法且确定的字段名，原始键可用于查值。</summary>
    [Fact]
    public void SanitizesKeysAndReservesExistingIdentifiers()
    {
        const string entries = """
            <root>
              <data name="A-B"><value>符号</value></data>
              <data name="A_B_2"><value>已占编号</value></data>
              <data name="A_B"><value>合法名称</value></data>
              <data name="A-B"><value>重复键</value></data>
              <data name="class"><value>关键字</value></data>
              <data name="_class"><value>已占名称</value></data>
              <data name="3rd"><value>数字开头</value></data>
              <data name="I18NKeys"><value>与包含类型同名</value></data>
              <data name="你好"><value>国际化名称</value></data>
              <data name="Name &quot;Quote&quot;"><value>转义值</value></data>
              <data name="Typed" type="System.String, mscorlib"><value>强类型字符串</value></data>
              <data name="NonString" type="System.Int32, mscorlib"><value>123</value></data>
              <data name="Binary" mimetype="application/x-test"><value>不属于文本</value></data>
            </root>
            """;
        var neutral = new InMemoryAdditionalText(GeneratorHarness.ResourcePath, entries, GeneratorHarness.BaseName);
        var satellite = new InMemoryAdditionalText(
            "/src/Resources/Messages.fr.resx",
            """<root><data name="OnlyTranslated"><value>fr</value></data></root>""");
        var otherBaseName = new InMemoryAdditionalText("/src/Other.resx", "<broken", "Other.Messages");
        var nonResx = new InMemoryAdditionalText("/src/Ignored.txt", "<broken", GeneratorHarness.BaseName);
        CSharpCompilation compilation = GeneratorHarness.CreateCompilation(GeneratorHarness.NormalDeclaration);
        GeneratorDriver driver = GeneratorHarness.CreateDriver(neutral, satellite, otherBaseName, nonResx);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updated, out _);
        GeneratorRunResult result = Assert.Single(driver.GetRunResult().Results);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(updated.GetDiagnostics(), diagnostic =>
        {
            return diagnostic.Severity == DiagnosticSeverity.Error;
        });
        INamedTypeSymbol keys = Assert.IsAssignableFrom<INamedTypeSymbol>(updated.GetTypeByMetadataName("TestProject.I18NKeys"));

        var expected = new Dictionary<string, string>
        {
            ["A_B"] = "A_B",
            ["A_B_2"] = "A_B_2",
            ["A_B_3"] = "A-B",
            ["_class"] = "_class",
            ["_class_2"] = "class",
            ["_3rd"] = "3rd",
            ["I18NKeys_2"] = "I18NKeys",
            ["你好"] = "你好",
            ["Name__Quote_"] = "Name \"Quote\"",
            ["Typed"] = "Typed",
        };
        IFieldSymbol[] fields = keys.GetMembers().OfType<IFieldSymbol>().ToArray();
        Assert.Equal(expected.Count, fields.Length);
        foreach (IFieldSymbol field in fields)
        {
            Assert.True(field.IsConst);
            Assert.Equal(expected[field.Name], field.ConstantValue);
        }
    }
}
