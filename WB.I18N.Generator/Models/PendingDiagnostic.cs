namespace WB.I18N.Generator.Models;

/// <summary>保存待报告的诊断编号、定位信息和消息参数。</summary>
internal readonly record struct PendingDiagnostic
{
    /// <summary>记录诊断及其可选的资源错误信息。</summary>
    public PendingDiagnostic(string id, DiagnosticSite site, string? resourcePath = null, string? error = null)
    {
        Id = id;
        Site = site;
        ResourcePath = resourcePath;
        Error = error;
    }

    /// <summary>获取诊断编号。</summary>
    public string Id { get; }

    /// <summary>获取声明节点的位置值。</summary>
    public DiagnosticSite Site { get; }

    /// <summary>获取报告资源读取错误时使用的文件路径。</summary>
    public string? ResourcePath { get; }

    /// <summary>获取资源读取失败的原因。</summary>
    public string? Error { get; }
}
