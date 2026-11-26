# WB.I18N.Avalonia

在声明语言资源的 .NET 8 项目里安装 `WB.I18N.Avalonia`，加入一个静态分部类声明资源位置。分部类和资源放在同一程序集时，特性只需资源名。

```csharp
using WB.I18N.Avalonia;

[I18NResource("MyApp.Resources.Language")]
public static partial class I18N
{
}
```

调用方可以自行选择声明方式。只有 `.resx` 时，直接传资源名即可。从 2.1.0 起，已有 `Language.Designer.cs` 等强类型资源类时，也可以使用 `[I18NResource(typeof(MyApp.Resources.Language))]`。该类的全名应与默认 `.resx` 的清单名一致，省略 `.resources` 后缀。类型可来自当前项目或引用的资源项目。

资源类型与默认资源的名称不同时，可显式指定资源名，例如 `[I18NResource(typeof(SharedTexts.ResourceAnchor), "SharedTexts.Resources.Language")]`。`ResourceAnchor` 可使用该类库中已有的类型。分部类放在类库时，应用引用该类库并在初始化时通过 `I18N.Current` 使用共享语言状态。

生成的 C# 入口有 `I18N.GetString(key)`、`I18N.Format(key, args)` 和 `I18N.Current.LanguageTag`。库首次初始化时采用宿主的 `CultureInfo.CurrentUICulture`。宿主使用不变文化或无法取得对应文化时，`LanguageTag` 为空，直接读取默认资源。应用需要固定启动语言时，可以在创建界面前显式设置语言，例如 `zh`、`en`、`fr`、`fr-CA` 或 `ja-JP`。设置时会去除首尾空白并规范标签大小写，空值和 .NET 无法识别的值会被忽略。

构建时会根据中性 `.resx` 中的键生成与调用入口同命名空间的常量类。类名由声明类型的名字加上 `Keys` 组成，例如 `I18NKeys`。在 IDE 中键入 `I18NKeys.` 即可选键。

```csharp
string title = I18N.GetString(I18NKeys.ProductTeach);
string progress = I18N.Format(I18NKeys.TaskProgress, "A", 1234);
```

包含特殊字符的键会转换为有效的 C# 标识符，同名时会生成可区分的成员名。常量值始终保留 `.resx` 中的原始键。新增语言时，在中性资源中先声明需要生成的键。

中性 `.resx` 在其他项目时，可以把它作为 `AdditionalFiles` 加到声明 `I18N` 的项目。`WBI18NResourceBaseName` 填特性中对应的中性资源清单名，`WBI18NResourceAssembly` 填提供资源的程序集名称。

```xml
<ItemGroup>
  <AdditionalFiles Include="..\SharedTexts\Resources\Language.resx"
                   WBI18NResourceBaseName="SharedTexts.Resources.Language"
                   WBI18NResourceAssembly="SharedTexts" />
</ItemGroup>
```

在原资源文件旁添加对应语言的 `.resx`，例如 `Language.fr.resx` 或 `Language.fr-CA.resx`，即可提供新增译文。资源按具体语言、上级语言和默认 `.resx` 的顺序回退。`I18N.Format` 使用当前所选语言的数字和日期格式。C# 和 AXAML 格式化在占位符有误或参数不匹配时返回原始模板。设置 `LanguageTag` 仅改变本库的语言状态。

在 AXAML 中将 `localization` 映射到 `I18N` 所在的命名空间，可以通过 `{I18N Key={x:Static localization:I18NKeys.ProductTeach}}` 引用生成的键。动态键可使用 `{I18N Key={Binding ResourceKey}}`。界面语言或已绑定的资源键发生变化时，文案会自动更新。
