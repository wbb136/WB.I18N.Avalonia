using System.Diagnostics;
using WB.I18N.Avalonia;

namespace Benchmark;

/// <summary>测量入口程序集未声明语言资源时重复获取备用文案的成本。</summary>
internal static class NoResourceProgram
{
    private const int Iterations = 1_000_000;
    private const int Runs = 5;
    private static long _sink;

    /// <summary>校验无资源回退行为，再测量相同调用的中位耗时和分配。</summary>
    private static void Main()
    {
        string value = I18NExtension.GetString("NoSuchKey", "Fallback");
        if (value != "Fallback")
        {
            throw new InvalidOperationException("未正确使用备用文案。");
        }

        for (int index = 0; index < 50_000; index++)
        {
            _sink += I18NExtension.GetString("NoSuchKey", "Fallback").Length;
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
                checksum += I18NExtension.GetString("NoSuchKey", "Fallback").Length;
            }

            long ended = Stopwatch.GetTimestamp();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            _sink += checksum;
            times[run] = (ended - started) * 1_000_000_000.0 / Stopwatch.Frequency / Iterations;
            allocations[run] = (allocatedAfter - allocatedBefore) / (double)Iterations;
        }

        Array.Sort(times);
        Array.Sort(allocations);
        Console.WriteLine($"NoResource/fallback {times[Runs / 2]:F1} ns/op {allocations[Runs / 2]:F1} B/op; sink={_sink}");
    }
}
