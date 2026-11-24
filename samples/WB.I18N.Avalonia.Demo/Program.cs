using Avalonia;

namespace WB.I18N.Avalonia.Demo;

/// <summary>启动本地化功能演示应用。</summary>
internal static class Program
{
    /// <summary>创建 Avalonia 桌面应用并进入消息循环。</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>配置供运行时和设计器使用的应用构建器。</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
