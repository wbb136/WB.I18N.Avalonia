# WB.I18N.Avalonia 示例

`WB.I18N.Avalonia.Demo` 是可运行的 Avalonia 桌面应用，展示生成的 C# 入口与 AXAML 的动态绑定。`TypeDeclaration` 和 `ExternalDeclaration` 两个类库展示另外两种类级资源声明，`ResourcePack` 为后者提供资源。`AssemblyDeclaration` 控制台项目展示旧式程序集级声明。每种声明放在独立项目中，实际应用按自己的资源位置选用一种即可。

## 运行

在 `WB.I18N.Avalonia` 仓库根目录执行：

```powershell
dotnet run --project samples/WB.I18N.Avalonia.Demo/WB.I18N.Avalonia.Demo.csproj
```

构建所有示例项目：

```powershell
dotnet build samples/WB.I18N.Avalonia.Demo/WB.I18N.Avalonia.Demo.csproj
dotnet build samples/WB.I18N.Avalonia.Demo.TypeDeclaration/WB.I18N.Avalonia.Demo.TypeDeclaration.csproj
dotnet build samples/WB.I18N.Avalonia.Demo.ExternalDeclaration/WB.I18N.Avalonia.Demo.ExternalDeclaration.csproj
dotnet build samples/WB.I18N.Avalonia.Demo.AssemblyDeclaration/WB.I18N.Avalonia.Demo.AssemblyDeclaration.csproj
```

## 运行界面涵盖的用法

- `Localization/I18N.cs` 使用 `[I18NResource("WB.I18N.Avalonia.Demo.Resources.Language")]` 声明同程序集资源。
- `DemoViewModel` 使用生成的 `I18N.GetString`（含缺失键备用文案）、`I18N.Format` 和 `I18N.Current.LanguageTag`；语言变化时通知已绑定的 C# 查询结果刷新。
- `MainWindow.axaml` 包含默认 XML 命名空间的 `{I18N ...}` 与显式 `xmlns:i18n` 的 `{i18n:I18N ...}` 写法、固定与动态 `Key`、固定与动态 `Fallback`、绑定的格式化参数，以及按钮触发的语言切换。
- `Language.resx` 提供默认文案，`Language.fr.resx` 提供法语文案，`Language.fr-CA.resx` 仅覆盖部分键。选择加拿大法语时，`RegionalGreeting` 取地区译文，`ParentOnly` 回退到法语，`NeutralOnly` 回退到默认资源。`TaskProgress` 的模板也来自法语，但数字仍按 `fr-CA` 格式化。
- `Language.en.resx` 展示独立的英文译文。

## 另外两种声明

| 项目 | 特性 | 资源位置 |
| --- | --- | --- |
| `WB.I18N.Avalonia.Demo.TypeDeclaration` | `[I18NResource(typeof(Language))]` | `Language` 类型所在程序集，类型完整名称与资源清单名相同 |
| `WB.I18N.Avalonia.Demo.ExternalDeclaration` | `[I18NResource(typeof(ResourceAnchor), "WB.I18N.Avalonia.Demo.ResourcePack.Resources.Language")]` | `WB.I18N.Avalonia.Demo.ResourcePack` 中的资源 |

两个类库都包含可由源生成器补全的 `I18N` 部分类。它们是独立的声明示例，运行中的桌面应用不引用它们，避免多个示例争用同一个进程的全局语言入口。

`WB.I18N.Avalonia.Demo.AssemblyDeclaration` 是旧式程序集级声明的独立控制台示例。它只在入口程序集上使用 `[assembly: I18NResource("...")]`，再调用 `I18NExtension.GetString`；由于没有带特性的分部类，源生成器不创建 `I18N` 包装入口。可通过 `dotnet run --project samples/WB.I18N.Avalonia.Demo.AssemblyDeclaration/WB.I18N.Avalonia.Demo.AssemblyDeclaration.csproj` 验证。
