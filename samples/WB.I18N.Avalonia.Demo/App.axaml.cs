using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WB.I18N.Avalonia.Demo.Views;

namespace WB.I18N.Avalonia.Demo;

/// <summary>加载主题并创建本地化演示窗口。</summary>
public partial class App : Application
{
    /// <summary>加载应用样式。</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>在桌面应用生命周期中设置主窗口。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
