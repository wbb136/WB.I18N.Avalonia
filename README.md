# WB.I18N.Avalonia

适用于 .NET 8 和 Avalonia 12.1 的本地化库。通过资源声明生成 C# 入口，并在 AXAML 中随语言、资源键和格式化参数变化更新文案。

## 快速开始

在应用项目中引用 `WB.I18N.Avalonia`，并声明资源位置：

```csharp
using WB.I18N.Avalonia;

namespace MyApp.Localization;

/// <summary>声明应用使用的语言资源。</summary>
[I18NResource("MyApp.Resources.Language")]
public static partial class I18N
{
}
```

```csharp
I18N.Current.LanguageTag = "fr-CA";
string title = I18N.GetString(I18NKeys.ProductTeach);
string message = I18N.Format(I18NKeys.TaskProgress, "A", 1234);
```

把 `ProductTeach`、`TaskProgress` 等键加到默认资源 `Language.resx`。构建时会在 `I18N` 所在命名空间生成 `I18NKeys`，资源键可直接用于 C# 调用和 AXAML。

库首次初始化时采用宿主的 `CultureInfo.CurrentUICulture`。宿主使用不变文化或无法取得对应文化时，`LanguageTag` 为空，直接读取默认资源。应用需要固定启动语言时，可以在创建界面前显式设置 `LanguageTag`。切换语言只影响库内资源查找和格式化。

C# `Format` 和 AXAML 格式参数遇到错误的占位符或不匹配的参数时，返回原始资源模板。

```xml
<!-- 在外层元素声明 xmlns:localization="using:MyApp.Localization"。 -->
<TextBlock Text="{I18N Key={x:Static localization:I18NKeys.ProductTeach}}" />
<TextBlock Text="{I18N Key={Binding StatusKey}}" />
```

`[I18NResource]` 也支持资源类型及独立资源程序集。完整示例见 [samples](samples/README.md)，详细说明见 [使用手册](docs/I18N使用手册.html)。

## 构建与运行

```powershell
dotnet build WB.I18N.Avalonia.sln -c Release
dotnet test WB.I18N.Avalonia.sln -c Release
dotnet run --project samples/WB.I18N.Avalonia.Demo/WB.I18N.Avalonia.Demo.csproj
```

## 性能

在 Windows、.NET 8.0.23、Release 模式下，每项预热 5 万次，执行 5 轮、每轮 50 万次，取中位数：

| 场景 | 优化前 | 优化后 |
| --- | ---: | ---: |
| 固定键 `ProvideValue` | 114.1 ns / 608 B | 45.6 ns / 168 B |
| 双参数绑定格式化分配 | 112 B | 72 B |
| 未声明资源时读取备用文案 | 449.3 ns / 0 B | 7.6 ns / 0 B |

已注册资源的 `GetString` 热键命中约 35–37 ns / 0 B，优化前后基本持平。耗时会随机器负载波动；[压测工程](benchmarks/WB.I18N.Avalonia.Benchmarks/README.md)可复测当前源码。
