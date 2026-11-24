using WB.I18N.Avalonia;

[assembly: I18NResource("WB.I18N.Avalonia.Demo.AssemblyDeclaration.Resources.Language")]

namespace WB.I18N.Avalonia.Demo.AssemblyDeclaration;

/// <summary>展示旧式程序集级资源声明与运行时直接查值。</summary>
internal static class Program
{
    /// <summary>读取入口程序集声明的资源并输出结果。</summary>
    private static void Main()
    {
        Console.WriteLine(I18NExtension.GetString("Example"));
    }
}
