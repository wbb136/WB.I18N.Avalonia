using Avalonia.Controls;
using Avalonia.Interactivity;
using WB.I18N.Avalonia.Demo.ViewModels;

namespace WB.I18N.Avalonia.Demo.Views;

/// <summary>展示 C# 与 AXAML 本地化调用以及动态语言切换。</summary>
public partial class MainWindow : Window
{
    private readonly DemoViewModel _viewModel;

    /// <summary>创建演示窗口并连接视图模型。</summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new DemoViewModel();
        DataContext = _viewModel;
        Closed += OnClosed;
    }

    /// <summary>切换到中文资源。</summary>
    private void OnChineseClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ChangeLanguage("zh");
    }

    /// <summary>切换到英文资源。</summary>
    private void OnEnglishClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ChangeLanguage("en");
    }

    /// <summary>切换到法语资源。</summary>
    private void OnFrenchClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ChangeLanguage("fr");
    }

    /// <summary>切换到加拿大法语资源。</summary>
    private void OnCanadianFrenchClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ChangeLanguage("fr-CA");
    }

    /// <summary>更新绑定的资源键。</summary>
    private void OnStatusClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleStatus();
    }

    /// <summary>更新绑定的格式化参数。</summary>
    private void OnTaskClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.AdvanceTask();
    }

    /// <summary>更新绑定的备用文案。</summary>
    private void OnFallbackClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleFallback();
    }

    /// <summary>解除共享语言事件订阅。</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }
}
