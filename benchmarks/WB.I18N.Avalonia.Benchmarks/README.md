# WB.I18N.Avalonia 性能压测

在解决方案根目录运行以下命令，使用 Release 模式和当前项目源码压测。每项串行场景先预热 5 万次，再运行 5 轮、每轮 50 万次，显示中位耗时及每次调用的线程分配量。并发场景使用 4 个工作线程，每轮共 100 万次调用。

```powershell
dotnet run -c Release --project benchmarks/WB.I18N.Avalonia.Benchmarks/WB.I18N.Avalonia.Benchmarks.csproj
```

只复测热键查询与 1、2、3 参数格式化时，运行：

```powershell
dotnet run -c Release --project benchmarks/WB.I18N.Avalonia.Benchmarks/WB.I18N.Avalonia.Benchmarks.csproj -- --focused
```

`Format/n-arg(s)` 与 `Convert/n-arg(s)` 复用计时前准备好的参数、装箱值和绑定列表。耗时和每次分配量包含格式化结果字符串。完整压测中的 `Format/params-2` 另测直接传值时调用方创建数组与数字装箱的成本。对比改动前后时，应在两个版本使用同一份压测代码与资源。

固定键和动态键的 `ProvideValue` 创建成本必须在独立进程测量，防止前一场景的大量分配改变 GC 堆状态：

```powershell
dotnet run -c Release --project benchmarks/WB.I18N.Avalonia.Benchmarks/WB.I18N.Avalonia.Benchmarks.csproj -- --provide-fixed
dotnet run -c Release --project benchmarks/WB.I18N.Avalonia.Benchmarks/WB.I18N.Avalonia.Benchmarks.csproj -- --provide-dynamic
```

没有资源声明的应用会走备用文案。此场景使用独立项目，避免主压测程序集的资源声明影响结果：

```powershell
dotnet run -c Release --project benchmarks/WB.I18N.Avalonia.Benchmarks/NoResource/WB.I18N.Avalonia.NoResource.Benchmarks.csproj
```

对比修改前后数据时，请使用同一机器、相同 .NET 运行时与相同命令。并发耗时受系统调度影响较大，建议多次运行；分配量可用于定位确定的额外对象创建。
