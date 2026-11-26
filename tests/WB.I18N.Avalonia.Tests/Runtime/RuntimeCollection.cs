namespace WB.I18N.Avalonia.Tests.Runtime;

/// <summary>串行运行共享语言与资源管理器状态的测试。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RuntimeCollection : ICollectionFixture<RuntimeFixture>
{
    /// <summary>用于标识共享运行时状态的测试集合。</summary>
    public const string Name = "本地化运行时";
}
