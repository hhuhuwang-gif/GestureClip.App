using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GestureClip.App.Converters;
using Microsoft.Extensions.Logging;

namespace GestureClip.App.Diagnostics;

/// <summary>
/// `--trace-mem` 诊断：周期性把内存构成写进日志，用来判断常驻内存花在托管堆、
/// 程序集映射还是窗口/位图这类原生资源上。默认关闭，关闭时零开销。
/// </summary>
internal static class MemoryTrace
{
    private static DispatcherTimer? _timer;

    public static void Start(ILogger logger, TimeSpan interval)
    {
        if (_timer is not null)
        {
            return;
        }

        Report(logger, "startup");

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval
        };
        _timer.Tick += (_, _) => Report(logger, "periodic");
        _timer.Start();
    }

    public static void Report(ILogger logger, string reason)
    {
        using var process = Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();

        logger.LogInformation(
            "MemoryTrace {Reason} ws={WorkingSet}MB private={Private}MB managed={Managed}MB heap={Heap}MB fragmented={Fragmented}MB windows={Windows} images={Images} handles={Handles} threads={Threads} gen0={Gen0} gen2={Gen2}",
            reason,
            ToMb(process.WorkingSet64),
            ToMb(process.PrivateMemorySize64),
            ToMb(GC.GetTotalMemory(forceFullCollection: false)),
            ToMb(gcInfo.HeapSizeBytes),
            ToMb(gcInfo.FragmentedBytes),
            System.Windows.Application.Current?.Windows.Count ?? 0,
            ImageBase64ToSourceConverter.CachedImageCount,
            process.HandleCount,
            process.Threads.Count,
            GC.CollectionCount(0),
            GC.CollectionCount(2));
    }

    private static long ToMb(long bytes) => bytes / (1024 * 1024);
}
