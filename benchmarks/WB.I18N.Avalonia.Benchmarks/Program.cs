using System.Diagnostics;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using WB.I18N.Avalonia;

[assembly: I18NResource("Benchmark.Messages")]

namespace Benchmark;

/// <summary>测量本地化读取、格式化、绑定转换和语言切换的吞吐与分配。</summary>
internal static class Program
{
    private const int Iterations = 500_000;
    private const int Runs = 5;
    private static long _sink;
    private static object? _lastResult;

    /// <summary>注册压测资源并依次运行每个稳定场景。</summary>
    private static void Main(string[] args)
    {
        I18NExtension.RegisterResourceAssembly(typeof(Program).Assembly);
        I18NExtension.Current.LanguageTag = "en";
        Console.WriteLine($"Runtime: {Environment.Version}; Iterations: {Iterations}; Runs: {Runs}");

        // 单独进程测量绑定创建，避免前一个大量分配的场景影响 GC 堆大小。
        if (args.Length > 0)
        {
            if (args[0] == "--provide-fixed")
            {
                var fixedExtension = new I18NExtension
                {
                    Key = "Greeting",
                };
                MeasureCreation("ProvideValue/fixed", () =>
                {
                    return fixedExtension.ProvideValue();
                });
                return;
            }

            if (args[0] == "--provide-dynamic")
            {
                var dynamicExtension = new I18NExtension
                {
                    Key = new ReflectionBinding("ResourceKey"),
                };
                MeasureCreation("ProvideValue/dynamic", () =>
                {
                    return dynamicExtension.ProvideValue();
                });
                return;
            }

            if (args[0] == "--focused")
            {
                MeasureFocused();
                Console.WriteLine($"Sink: {_sink}");
                return;
            }

            throw new ArgumentException("未知压测场景。", nameof(args));
        }

        MeasureFocused();
        Measure("GetString/miss", () =>
        {
            return I18NExtension.GetString("NotFound");
        });
        Measure("GetString/spaces", () =>
        {
            return I18NExtension.GetString(" Greeting ");
        });
        // 单独保留直接传值的调用方式，记录调用方创建 params 数组和装箱的成本。
        Measure("Format/params-2", () =>
        {
            return I18NExtension.Format("Template", "Alice", 1234);
        });

        var converter = (IMultiValueConverter)I18NExtension.Current;
        object?[] plainValues = ["en", "Greeting", null];
        Measure("Convert/plain", () =>
        {
            return converter.Convert(plainValues, typeof(string), null, CultureInfo.InvariantCulture)?.ToString() ?? string.Empty;
        });

        MeasureParallel("Parallel/hit", worker =>
        {
            return I18NExtension.GetString("Greeting");
        });
        MeasureParallel("Parallel/mixed-switch", worker =>
        {
            if (worker == 0)
            {
                I18NExtension.Current.LanguageTag = I18NExtension.Current.LanguageTag == "en" ? "fr" : "en";
            }

            return I18NExtension.GetString("Greeting");
        });

        int switchIndex = 0;
        Measure("Language/switch", () =>
        {
            switchIndex++;
            I18NExtension.Current.LanguageTag = (switchIndex & 1) == 0 ? "en" : "fr";
            return I18NExtension.Current.LanguageTag;
        });
        Console.WriteLine($"Sink: {_sink}");
    }

    /// <summary>单独测量热键命中及一到三个格式化参数的场景。</summary>
    private static void MeasureFocused()
    {
        Measure("GetString/hit", () =>
        {
            return I18NExtension.GetString("Greeting");
        });

        MeasureFormatting("1-arg", "TemplateOne", ["Alice"]);
        MeasureFormatting("2-args", "Template", ["Alice", 1234]);
        MeasureFormatting("3-args", "TemplateThree", ["Alice", 1234, "A"]);
    }

    /// <summary>使用同一份预创建参数分别测量 C# 格式化与多绑定转换。</summary>
    private static void MeasureFormatting(string scenario, string key, object?[] arguments)
    {
        var converter = (IMultiValueConverter)I18NExtension.Current;
        CultureInfo bindingCulture = CultureInfo.InvariantCulture;
        // 前三项对应语言、资源键与备用文案。参数数组和装箱都在采样前准备。
        var values = new object?[arguments.Length + 3];
        values[0] = I18NExtension.Current.LanguageTag;
        values[1] = key;
        Array.Copy(arguments, 0, values, 3, arguments.Length);

        Measure($"Format/{scenario}", () =>
        {
            return I18NExtension.Format(key, arguments);
        });

        Measure($"Convert/{scenario}", () =>
        {
            return (string)converter.Convert(values, typeof(string), null, bindingCulture)!;
        });
    }

    /// <summary>预热后记录中位耗时与每次调用的线程分配字节数。</summary>
    private static void Measure(string name, Func<string> action)
    {
        for (int index = 0; index < 50_000; index++)
        {
            _sink += action().Length;
        }

        var times = new double[Runs];
        var allocations = new double[Runs];
        for (int run = 0; run < Runs; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            long checksum = 0;
            for (int index = 0; index < Iterations; index++)
            {
                checksum += action().Length;
            }

            long ended = Stopwatch.GetTimestamp();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            _sink += checksum;
            times[run] = (ended - started) * 1_000_000_000.0 / Stopwatch.Frequency / Iterations;
            allocations[run] = (allocatedAfter - allocatedBefore) / (double)Iterations;
        }

        Array.Sort(times);
        Array.Sort(allocations);
        Console.WriteLine($"{name,-20} {times[Runs / 2],9:F1} ns/op {allocations[Runs / 2],8:F1} B/op");
    }

    /// <summary>测量重复构造 Avalonia 绑定的耗时和线程分配量。</summary>
    private static void MeasureCreation(string name, Func<object> action)
    {
        for (int index = 0; index < 50_000; index++)
        {
            Volatile.Write(ref _lastResult, action());
        }

        var times = new double[Runs];
        var allocations = new double[Runs];
        for (int run = 0; run < Runs; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int index = 0; index < Iterations; index++)
            {
                Volatile.Write(ref _lastResult, action());
            }

            long ended = Stopwatch.GetTimestamp();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            times[run] = (ended - started) * 1_000_000_000.0 / Stopwatch.Frequency / Iterations;
            allocations[run] = (allocatedAfter - allocatedBefore) / (double)Iterations;
        }

        Array.Sort(times);
        Array.Sort(allocations);
        Console.WriteLine($"{name,-20} {times[Runs / 2],9:F1} ns/op {allocations[Runs / 2],8:F1} B/op");
    }

    /// <summary>同时运行四个工作线程，测量共享资源管理器的读取和语言切换。</summary>
    private static void MeasureParallel(string name, Func<int, string> action)
    {
        const int workers = 4;
        const int iterationsPerWorker = 250_000;
        var times = new double[Runs];
        for (int run = 0; run < Runs; run++)
        {
            long started = Stopwatch.GetTimestamp();
            Parallel.For(0, workers, worker =>
            {
                long checksum = 0;
                for (int index = 0; index < iterationsPerWorker; index++)
                {
                    checksum += action(worker).Length;
                }

                Interlocked.Add(ref _sink, checksum);
            });
            long ended = Stopwatch.GetTimestamp();
            times[run] = (ended - started) * 1_000_000_000.0 / Stopwatch.Frequency / (workers * iterationsPerWorker);
        }

        Array.Sort(times);
        Console.WriteLine($"{name,-20} {times[Runs / 2],9:F1} ns/op (4 workers)");
    }
}
