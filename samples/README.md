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

- 启动时采用宿主的界面语言。不变文化下显示「中性资源」，并读取默认 `Language.resx`。仍可通过示例按钮切换到指定语言。
- `Localization/I18N.cs` 使用 `[I18NResource("WB.I18N.Avalonia.Demo.Resources.Language")]` 声明同程序集资源。
- `DemoViewModel` 使用 `I18N.GetString(I18NKeys.ProductTeach)` 和 `I18N.Format(I18NKeys.TaskProgress, ...)`。状态切换也通过 `I18NKeys.StatusReady` 和 `I18NKeys.StatusCompleted` 提供动态键。语言变化时会通知已绑定的 C# 查询结果刷新。
- `I18NKeys` 从 `Language.resx` 生成。特殊字符会映射为有效的 C# 标识符，同名时生成可区分的成员，常量内容保留原始资源键。语言或地区资源文件为现有键提供翻译。
- `MainWindow.axaml` 把 `{x:Static localization:I18NKeys.ProductTeach}` 用作固定键，并展示动态 `Key`、固定与动态 `Fallback`、绑定的格式化参数以及语言切换。备用文案示例故意使用没有资源的键。
- `Language.resx` 提供默认文案，`Language.fr.resx` 提供法语文案，`Language.fr-CA.resx` 仅覆盖部分键。选择加拿大法语时，`RegionalGreeting` 取地区译文，`ParentOnly` 回退到法语，`NeutralOnly` 回退到默认资源。`TaskProgress` 的模板也来自法语，但数字仍按 `fr-CA` 格式化。
- `Language.en.resx` 展示独立的英文译文。

## 另外两种声明

| 项目 | 特性 | 资源位置 |
| --- | --- | --- |
| `WB.I18N.Avalonia.Demo.TypeDeclaration` | `[I18NResource(typeof(Language))]` | `Language` 类型所在程序集，类型完整名称与资源清单名相同 |
| `WB.I18N.Avalonia.Demo.ExternalDeclaration` | `[I18NResource(typeof(ResourceAnchor), "WB.I18N.Avalonia.Demo.ResourcePack.Resources.Language")]` | `WB.I18N.Avalonia.Demo.ResourcePack` 中的资源 |

两个类库都包含可由源生成器补全的 `I18N` 部分类。它们是独立的声明示例，运行中的桌面应用不引用它们，避免多个示例争用同一个进程的全局语言入口。

`ExternalDeclaration` 将 `ResourcePack` 的中性 `.resx` 作为 `AdditionalFiles` 传给生成器，配置中包含 `WBI18NResourceBaseName` 和 `WBI18NResourceAssembly`。

`WB.I18N.Avalonia.Demo.AssemblyDeclaration` 是旧式程序集级声明的独立控制台示例。它只在入口程序集上使用 `[assembly: I18NResource("...")]`，再调用 `I18NExtension.GetString`；由于没有带特性的分部类，源生成器不创建 `I18N` 包装入口。可通过 `dotnet run --project samples/WB.I18N.Avalonia.Demo.AssemblyDeclaration/WB.I18N.Avalonia.Demo.AssemblyDeclaration.csproj` 验证。
