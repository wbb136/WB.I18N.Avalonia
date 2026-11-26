namespace WB.I18N.Generator.Models;

/// <summary>保存类级资源声明的源码生成信息和独立的诊断位置。</summary>
internal readonly record struct ResourceDeclaration
{
    /// <summary>记录类声明形状、资源匹配条件和定位信息。</summary>
    public ResourceDeclaration(
        DiagnosticSite site,
        SourceIdentity? identity = null,
        string? registrationArguments = null,
        string? resourceBaseName = null,
        string? resourceAssembly = null
    )
    {
        Site = site;
        Identity = identity;
        RegistrationArguments = registrationArguments;
        ResourceBaseName = resourceBaseName;
        ResourceAssembly = resourceAssembly;
    }

    /// <summary>获取诊断需要绑定的声明位置。</summary>
    public DiagnosticSite Site { get; }

    /// <summary>获取有效类声明的类型信息，无效声明时为空。</summary>
    public SourceIdentity? Identity { get; }

    /// <summary>获取用于比较及输出的程序集特性参数文本。</summary>
    public string? RegistrationArguments { get; }

    /// <summary>获取用于匹配默认资源文件的清单名。</summary>
    public string? ResourceBaseName { get; }

    /// <summary>获取资源所在程序集的短名称。</summary>
    public string? ResourceAssembly { get; }
}
