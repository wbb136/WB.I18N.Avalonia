using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using WB.I18N.Generator.Models;

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

    private static readonly DiagnosticDescriptor InvalidKeySource = new(
        "WBI18N005",
        "无法读取本地化资源键",
        "无法从“{0}”生成本地化资源键：{1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>注册值语义输入，并分别输出生成的源码和定位到当前编译的诊断。</summary>
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
                return ProjectDeclaration(attributeContext);
            }
        ).WithTrackingName("WBI18N.Declarations");

        var assemblyRegistration = context.CompilationProvider.Select(static (compilation, _) =>
        {
            AttributeData? attribute = compilation.Assembly.GetAttributes().FirstOrDefault(static attribute =>
            {
                return attribute.AttributeClass?.ToDisplayString() == AttributeName;
            });
            if (attribute is null)
            {
                return (HasAttribute: false, Arguments: (string?)null);
            }

            return (HasAttribute: true, Arguments: GetRegistrationArguments(attribute));
        }).WithTrackingName("WBI18N.AssemblyRegistration");

        var keySources = context.AdditionalTextsProvider
            .Where(static file =>
            {
                return file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase);
            })
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (input, cancellationToken) =>
            {
                return ResourceKeySource.Read(input.Left, input.Right.GetOptions(input.Left), cancellationToken);
            })
            .Where(static source =>
            {
                return source is not null;
            })
            .WithTrackingName("WBI18N.ResourceKeys");

        var declarationValues = declarations.Collect().Select(static (items, _) =>
        {
            return new EquatableArray<ResourceDeclaration>(items);
        });
        var resourceValues = keySources.Collect().Select(static (items, _) =>
        {
            return new EquatableArray<ResourceKeySource?>(items);
        });
        var output = declarationValues.Combine(assemblyRegistration).Combine(resourceValues)
            .Select(static (input, _) =>
            {
                return CreateOutput(input.Left.Left.Items, input.Left.Right, input.Right.Items);
            });

        var sources = output.SelectMany(static (result, _) =>
        {
            if (result.Source.HasValue)
            {
                return ImmutableArray.Create(result.Source.Value);
            }

            return ImmutableArray<SourceEmission>.Empty;
        }).WithTrackingName("WBI18N.SourceModel");

        context.RegisterSourceOutput(sources, static (productionContext, source) =>
        {
            EmitSources(productionContext, source);
        });

        // 只有待报告的诊断才关联当前编译；生成源码不依赖语法树或诊断位置。
        var pendingDiagnostics = output.SelectMany(static (result, _) =>
        {
            return result.Diagnostics.Items;
        }).WithTrackingName("WBI18N.Diagnostics");
        var diagnostics = pendingDiagnostics.Combine(context.CompilationProvider).Select(static (input, _) =>
        {
            return BindDiagnostic(input.Left, (CSharpCompilation)input.Right);
        });

        context.RegisterSourceOutput(diagnostics, static (productionContext, diagnostic) =>
        {
            productionContext.ReportDiagnostic(diagnostic);
        });
    }

    /// <summary>从 Roslyn 的符号和声明节点提取可跨编译比较的值。</summary>
    private static ResourceDeclaration ProjectDeclaration(GeneratorAttributeSyntaxContext context)
    {
        SyntaxNode node = context.TargetNode;
        var compilation = (CSharpCompilation)context.SemanticModel.Compilation;
        var site = new DiagnosticSite(compilation.SyntaxTrees.IndexOf(node.SyntaxTree), node.SpanStart, node.Span.Length);
        var classDeclaration = node as ClassDeclarationSyntax;
        var target = context.TargetSymbol as INamedTypeSymbol;
        if (classDeclaration is null || target is null || !target.IsStatic || target.Arity != 0 ||
            target.ContainingType is not null || classDeclaration.Modifiers.Any(SyntaxKind.FileKeyword) ||
            !classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return new ResourceDeclaration(site);
        }

        string accessibility = "internal";
        if (target.DeclaredAccessibility == Accessibility.Public)
        {
            accessibility = "public";
        }

        var identity = new SourceIdentity(
            target.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            target.Name + "Keys",
            GetNamespaceDeclaration(target),
            accessibility
        );

        AttributeData attribute = context.Attributes[0];
        string? registrationArguments = GetRegistrationArguments(attribute);
        if (registrationArguments is null)
        {
            return new ResourceDeclaration(site, identity);
        }

        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
        var resourceType = arguments[0].Value as INamedTypeSymbol;
        string? resourceBaseName;
        if (arguments.Length == 2)
        {
            resourceBaseName = arguments[1].Value as string;
        }
        else
        {
            resourceBaseName = arguments[0].Value as string ?? GetResourceTypeName(resourceType);
        }

        string resourceAssembly = (resourceType?.ContainingAssembly ?? target.ContainingAssembly).Name;
        return new ResourceDeclaration(site, identity, registrationArguments, resourceBaseName, resourceAssembly);
    }

    /// <summary>按现有诊断优先级确定源码和诊断，只保留生成所需的值。</summary>
    private static (SourceEmission? Source, EquatableArray<PendingDiagnostic> Diagnostics) CreateOutput(
        ImmutableArray<ResourceDeclaration> declarations,
        (bool HasAttribute, string? Arguments) assemblyRegistration,
        ImmutableArray<ResourceKeySource?> keySources
    )
    {
        if (declarations.IsDefaultOrEmpty)
        {
            return (null, default);
        }

        if (declarations.Length > 1)
        {
            var duplicates = ImmutableArray.CreateBuilder<PendingDiagnostic>(declarations.Length);
            foreach (ResourceDeclaration declaration in declarations)
            {
                duplicates.Add(new PendingDiagnostic(MultipleDeclarations.Id, declaration.Site));
            }

            return (null, new EquatableArray<PendingDiagnostic>(duplicates.MoveToImmutable()));
        }

        ResourceDeclaration resource = declarations[0];
        if (!resource.Identity.HasValue)
        {
            return (null, CreateDiagnostic(InvalidDeclaration.Id, resource.Site));
        }

        if (resource.RegistrationArguments is null)
        {
            return (null, CreateDiagnostic(InvalidRegistration.Id, resource.Site));
        }

        if (assemblyRegistration.HasAttribute && assemblyRegistration.Arguments != resource.RegistrationArguments)
        {
            return (null, CreateDiagnostic(ConflictingRegistration.Id, resource.Site));
        }

        string registration = string.Empty;
        if (!assemblyRegistration.HasAttribute)
        {
            registration = $"[assembly: global::WB.I18N.Avalonia.I18NResourceAttribute({resource.RegistrationArguments})]";
        }

        // 选择与声明的资源名称和程序集匹配的默认资源文件，保持输入顺序以报告首个读取错误。
        var keys = ImmutableArray.CreateBuilder<string>();
        foreach (ResourceKeySource? source in keySources)
        {
            if (source is null ||
                !string.Equals(source.BaseName, resource.ResourceBaseName, StringComparison.Ordinal) ||
                (source.AssemblyName is not null &&
                    !string.Equals(source.AssemblyName, resource.ResourceAssembly, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (source.Error is not null)
            {
                var diagnostic = new PendingDiagnostic(InvalidKeySource.Id, resource.Site, source.Path, source.Error);
                return (
                    new SourceEmission(resource.Identity.Value, registration),
                    new EquatableArray<PendingDiagnostic>(ImmutableArray.Create(diagnostic))
                );
            }

            keys.AddRange(source.Keys.Items);
        }

        var emission = new SourceEmission(resource.Identity.Value, registration, new EquatableArray<string>(keys.ToImmutable()));
        return (emission, default);
    }

    /// <summary>记录不带额外消息参数的声明诊断。</summary>
    private static EquatableArray<PendingDiagnostic> CreateDiagnostic(string id, DiagnosticSite site)
    {
        return new EquatableArray<PendingDiagnostic>(ImmutableArray.Create(new PendingDiagnostic(id, site)));
    }

    /// <summary>在当前语法树上恢复诊断位置，保留源码文件定位和行映射。</summary>
    private static Diagnostic BindDiagnostic(PendingDiagnostic diagnostic, CSharpCompilation compilation)
    {
        DiagnosticSite site = diagnostic.Site;
        SyntaxTree tree = compilation.SyntaxTrees[site.TreeIndex];
        Location location = Location.Create(tree, new TextSpan(site.SpanStart, site.SpanLength));
        switch (diagnostic.Id)
        {
            case "WBI18N001":
                return Diagnostic.Create(InvalidDeclaration, location);
            case "WBI18N002":
                return Diagnostic.Create(MultipleDeclarations, location);
            case "WBI18N003":
                return Diagnostic.Create(ConflictingRegistration, location);
            case "WBI18N004":
                return Diagnostic.Create(InvalidRegistration, location);
            case "WBI18N005":
                return Diagnostic.Create(InvalidKeySource, location, diagnostic.ResourcePath, diagnostic.Error);
            default:
                throw new InvalidOperationException($"未定义的本地化诊断编号：{diagnostic.Id}");
        }
    }

    /// <summary>按原有顺序生成调用入口和可选的资源键常量。</summary>
    private static void EmitSources(SourceProductionContext context, SourceEmission source)
    {
        context.AddSource("I18N.g.cs", SourceText.From(CreateSource(source), Encoding.UTF8));
        if (source.Keys.Items.IsEmpty)
        {
            return;
        }

        string keysSource = ResourceKeyEmitter.CreateSource(
            source.Identity.KeysName,
            source.Identity.Accessibility,
            source.Identity.NamespaceDeclaration,
            source.Keys.Items
        );
        context.AddSource("I18NKeys.g.cs", SourceText.From(keysSource, Encoding.UTF8));
    }

    /// <summary>获取资源类型用于定位资源清单的名称，嵌套类型使用运行时的加号分隔符。</summary>
    private static string? GetResourceTypeName(INamedTypeSymbol? type)
    {
        if (type is null || type.IsGenericType)
        {
            return null;
        }

        if (type.ContainingType is not null)
        {
            return GetResourceTypeName(type.ContainingType) + "+" + type.MetadataName;
        }

        return type.ContainingNamespace.IsGlobalNamespace
            ? type.MetadataName
            : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
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
    private static string CreateSource(SourceEmission source)
    {
        string namespaceDeclaration = source.Identity.NamespaceDeclaration;
        string name = source.Identity.Name;
        string accessibility = source.Identity.Accessibility;
        string registration = source.Registration;

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

    /// <summary>生成供调用入口和键常量类型共用的命名空间声明。</summary>
    private static string GetNamespaceDeclaration(INamedTypeSymbol target)
    {
        return target.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : "namespace " + target.ContainingNamespace.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                    SymbolDisplayGlobalNamespaceStyle.Omitted
                )
            ) + ";";
    }
}
