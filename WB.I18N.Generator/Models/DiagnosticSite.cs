namespace WB.I18N.Generator.Models;

/// <summary>用值保存诊断所在的语法树顺序和源码范围。</summary>
internal readonly record struct DiagnosticSite
{
    /// <summary>记录当前编译中的语法树序号以及节点范围。</summary>
    public DiagnosticSite(int treeIndex, int spanStart, int spanLength)
    {
        TreeIndex = treeIndex;
        SpanStart = spanStart;
        SpanLength = spanLength;
    }

    /// <summary>获取诊断所在语法树的序号，区分使用相同文件路径的树。</summary>
    public int TreeIndex { get; }

    /// <summary>获取声明节点的起始偏移。</summary>
    public int SpanStart { get; }

    /// <summary>获取声明节点的长度。</summary>
    public int SpanLength { get; }
}
