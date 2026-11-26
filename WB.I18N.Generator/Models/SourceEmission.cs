namespace WB.I18N.Generator.Models;

/// <summary>保存生成源码所需的值，不包含诊断位置。</summary>
internal readonly record struct SourceEmission
{
    /// <summary>记录输出类型信息、程序集特性声明和资源键。</summary>
    public SourceEmission(SourceIdentity identity, string registration, EquatableArray<string> keys = default)
    {
        Identity = identity;
        Registration = registration;
        Keys = keys;
    }

    /// <summary>获取生成类型的命名和可访问性。</summary>
    public SourceIdentity Identity { get; }

    /// <summary>获取程序集特性声明文本，已有等效声明时为空字符串。</summary>
    public string Registration { get; }

    /// <summary>获取生成常量使用的默认资源键。</summary>
    public EquatableArray<string> Keys { get; }
}
