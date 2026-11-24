using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace WB.I18N.Generator;

/// <summary>根据调用方的资源声明生成本地化入口和程序集资源标记。</summary>
[Generator(LanguageNames.CSharp)]
public sealed class I18NResourceGenerator : IIncrementalGenerator
{
    private const string AttributeName = "WB.I18N.Avalonia.I18NResourceAttribute";
    private const string Category = "Localization";

    private static readonly DiagnosticDescriptor InvalidDeclaration = new(
        "WBI18N001",
        "无效的 I18N 类声明",
        "I18NResource 必须标记在非文件局部、非泛型、非嵌套的 static partial class 上",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor MultipleDeclarations = new(
        "WBI18N002",
        "重复的 I18N 资源声明",
        "一个程序集只能声明一个带 I18NResource 特性的 I18N 类",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor ConflictingRegistration = new(
        "WBI18N003",
        "I18N 资源声明不一致",
        "I18N 类与程序集上的 I18NResource 声明指向不同资源，请统一声明",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor InvalidRegistration = new(
        "WBI18N004",
        "无效的 I18N 资源位置",
        "I18NResource 必须提供非空的资源名称或有效的资源类型",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>注册类级资源特性和程序集级资源特性的增量读取规则。</summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) =>
            {
                return node is TypeDeclarationSyntax;
            },
            static (attributeContext, _) =>
            {
                return attributeContext;
            }
        );

        var assemblyRegistration = context.CompilationProvider.Select(static (compilation, _) =>
        {
            return compilation.Assembly.GetAttributes().FirstOrDefault(static attribute =>
            {
                return attribute.AttributeClass?.ToDisplayString() == AttributeName;
            });
        });

        context.RegisterSourceOutput(
            declarations.Collect().Combine(assemblyRegistration),
            static (productionContext, input) =>
            {
                Generate(productionContext, input.Left, input.Right);
            }
        );
    }

    /// <summary>检查声明并生成调用入口，保留兼容的现有程序集标记。</summary>
    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<GeneratorAttributeSyntaxContext> declarations,
        AttributeData? assemblyRegistration
    )
    {
        if (declarations.IsDefaultOrEmpty)
        {
            return;
        }

        if (declarations.Length > 1)
        {
            foreach (GeneratorAttributeSyntaxContext declaration in declarations)
            {
                context.ReportDiagnostic(Diagnostic.Create(MultipleDeclarations, declaration.TargetNode.GetLocation()));
            }

            return;
        }

        GeneratorAttributeSyntaxContext resource = declarations[0];
        Location location = resource.TargetNode.GetLocation();
        if (resource.TargetNode is not ClassDeclarationSyntax classDeclaration ||
            resource.TargetSymbol is not INamedTypeSymbol { IsStatic: true, Arity: 0, ContainingType: null } target ||
            classDeclaration.Modifiers.Any(SyntaxKind.FileKeyword) ||
            !classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidDeclaration, location));
            return;
        }

        string? classArguments = GetRegistrationArguments(resource.Attributes[0]);
        if (classArguments is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidRegistration, location));
            return;
        }

        if (assemblyRegistration is not null && GetRegistrationArguments(assemblyRegistration) != classArguments)
        {
            context.ReportDiagnostic(Diagnostic.Create(ConflictingRegistration, location));
            return;
        }

        string registration = assemblyRegistration is null
            ? $"[assembly: global::WB.I18N.Avalonia.I18NResourceAttribute({classArguments})]"
            : string.Empty;
        context.AddSource("I18N.g.cs", SourceText.From(CreateSource(target, registration), Encoding.UTF8));
    }

    /// <summary>把经编译器解析的特性参数还原为有效的程序集声明参数。</summary>
    private static string? GetRegistrationArguments(AttributeData attribute)
    {
        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
        if (arguments.Length == 1 && arguments[0].Value is string localBaseName &&
            !string.IsNullOrWhiteSpace(localBaseName))
        {
            return SymbolDisplay.FormatLiteral(localBaseName, quote: true);
        }

        if (arguments.Length == 1 && arguments[0].Value is ITypeSymbol resourceType &&
            resourceType.TypeKind != TypeKind.Error)
        {
            string typeName = resourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"typeof({typeName})";
        }

        if (arguments.Length == 2 && arguments[0].Value is ITypeSymbol anchor &&
            anchor.TypeKind != TypeKind.Error && arguments[1].Value is string externalBaseName &&
            !string.IsNullOrWhiteSpace(externalBaseName))
        {
            string typeName = anchor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string baseName = SymbolDisplay.FormatLiteral(externalBaseName, quote: true);
            return $"typeof({typeName}), {baseName}";
        }

        return null;
    }

    /// <summary>生成和调用方类型匹配的分部声明，并转发到唯一的本地化入口。</summary>
    private static string CreateSource(INamedTypeSymbol target, string registration)
    {
        string namespaceDeclaration = target.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : "namespace " + target.ContainingNamespace.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                    SymbolDisplayGlobalNamespaceStyle.Omitted
                )
            ) + ";";
        string name = target.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string accessibility = target.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";

        return $$"""
            // <auto-generated/>
            #nullable enable

            {{registration}}

            {{namespaceDeclaration}}

            {{accessibility}} static partial class {{name}}
            {
                /// <summary>在程序集初始化时注册该程序集声明的语言资源。</summary>
                [global::System.Runtime.CompilerServices.ModuleInitializerAttribute]
                internal static void RegisterI18NResources()
                {
                    global::WB.I18N.Avalonia.I18NExtension.RegisterResourceAssembly(typeof({{name}}).Assembly);
                }

                /// <summary>获取当前应用共享的语言状态。</summary>
                public static global::WB.I18N.Avalonia.I18NExtension Current
                {
                    get
                    {
                        return global::WB.I18N.Avalonia.I18NExtension.Current;
                    }
                }

                /// <summary>查找当前语言的资源文案。</summary>
                public static string GetString(string? resourceKey, string? fallback = null)
                {
                    return global::WB.I18N.Avalonia.I18NExtension.GetString(resourceKey, fallback);
                }

                /// <summary>查找并格式化当前语言的资源模板。</summary>
                public static string Format(string resourceKey, params object?[]? arguments)
                {
                    return global::WB.I18N.Avalonia.I18NExtension.Format(resourceKey, arguments);
                }
            }
            """;
    }
}
