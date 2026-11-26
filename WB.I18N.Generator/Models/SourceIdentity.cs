namespace WB.I18N.Generator.Models;

/// <summary>保存生成两个源码文件时共用的类型声明文本。</summary>
internal readonly record struct SourceIdentity
{
    /// <summary>记录从符号提取的类型名、命名空间和访问级别。</summary>
    public SourceIdentity(string name, string keysName, string namespaceDeclaration, string accessibility)
    {
        Name = name;
        KeysName = keysName;
        NamespaceDeclaration = namespaceDeclaration;
        Accessibility = accessibility;
    }

    /// <summary>获取分部类型名，保留语法要求的标识符转义。</summary>
    public string Name { get; }

    /// <summary>获取资源键常量类型名。</summary>
    public string KeysName { get; }

    /// <summary>获取生成文件中的命名空间声明。</summary>
    public string NamespaceDeclaration { get; }

    /// <summary>获取与原声明一致的访问级别。</summary>
    public string Accessibility { get; }
}
