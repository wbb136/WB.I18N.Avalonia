using System.ComponentModel;
using DemoI18N = WB.I18N.Avalonia.Demo.Localization.I18N;

namespace WB.I18N.Avalonia.Demo.ViewModels;

/// <summary>提供动态资源键、备用文案、格式化参数和 C# 查询示例。</summary>
public sealed class DemoViewModel : INotifyPropertyChanged, IDisposable
{
    private string _statusKey = "StatusReady";
    private string _missingFallback = "动态备用文案";
    private string _taskName = "A";
    private int _itemCount = 1234;

    /// <summary>订阅应用语言切换，以刷新 C# 查询结果。</summary>
    public DemoViewModel()
    {
        DemoI18N.Current.PropertyChanged += OnLanguageChanged;
    }

    #region 绑定数据

    /// <summary>获取当前用于动态查找的资源键。</summary>
    public string StatusKey
    {
        get
        {
            return _statusKey;
        }
    }

    /// <summary>获取演示动态备用文案的缺失资源键。</summary>
    public string MissingKey
    {
        get
        {
            return "MissingDynamic";
        }
    }

    /// <summary>获取资源缺失时使用的动态备用文案。</summary>
    public string MissingFallback
    {
        get
        {
            return _missingFallback;
        }
    }

    /// <summary>获取格式化模板中的任务名称。</summary>
    public string TaskName
    {
        get
        {
            return _taskName;
        }
    }

    /// <summary>获取格式化模板中的任务数量。</summary>
    public int ItemCount
    {
        get
        {
            return _itemCount;
        }
    }

    /// <summary>获取当前应用语言标签。</summary>
    public string CurrentLanguage
    {
        get
        {
            return DemoI18N.Current.LanguageTag;
        }
    }

    /// <summary>演示在 C# 中使用 GetString 查询资源。</summary>
    public string CodeLookup
    {
        get
        {
            return DemoI18N.GetString("Title");
        }
    }

    /// <summary>演示在 C# 中为缺失资源键提供备用文案。</summary>
    public string CodeFallback
    {
        get
        {
            return DemoI18N.GetString("MissingFromCode", "C# 备用文案");
        }
    }

    /// <summary>演示在 C# 中使用 Format 格式化资源。</summary>
    public string CodeFormat
    {
        get
        {
            return DemoI18N.Format("TaskProgress", _taskName, _itemCount);
        }
    }

    /// <summary>通知 Avalonia 重新读取发生变化的绑定属性。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region 交互操作

    /// <summary>通过生成的 Current 入口切换应用语言。</summary>
    public void ChangeLanguage(string languageTag)
    {
        DemoI18N.Current.LanguageTag = languageTag;
    }

    /// <summary>在准备与完成两个资源键之间切换。</summary>
    public void ToggleStatus()
    {
        _statusKey = _statusKey == "StatusReady" ? "StatusCompleted" : "StatusReady";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusKey)));
    }

    /// <summary>修改格式化参数并更新 C# 示例结果。</summary>
    public void AdvanceTask()
    {
        _taskName = _taskName == "A" ? "B" : "A";
        _itemCount += 1111;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CodeFormat)));
    }

    /// <summary>切换资源缺失时显示的动态备用文案。</summary>
    public void ToggleFallback()
    {
        _missingFallback = _missingFallback == "动态备用文案" ? "更新后的备用文案" : "动态备用文案";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MissingFallback)));
    }

    /// <summary>释放静态语言事件的订阅。</summary>
    public void Dispose()
    {
        DemoI18N.Current.PropertyChanged -= OnLanguageChanged;
    }

    /// <summary>语言改变后刷新 C# 查询及当前语言显示。</summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DemoI18N.Current.LanguageTag))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CodeLookup)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CodeFormat)));
        }
    }

    #endregion
}
