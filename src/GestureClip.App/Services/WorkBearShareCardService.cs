using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GestureClip.Core.Abstractions;
using MediaColor = System.Windows.Media.Color;

namespace GestureClip.App.Services;

public sealed class WorkBearShareCardService : IWorkBearShareCardService
{
    private readonly IWorkstationDashboardService _dashboardService;

    public WorkBearShareCardService(IWorkstationDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<string> GenerateTodayCardAsync(CancellationToken cancellationToken)
    {
        var report = await _dashboardService.GenerateDailyReportAsync(DateTimeOffset.Now, cancellationToken);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = Path.Combine(desktop, $"WorkBear-Report-{report.Date:yyyyMMdd}.png");

        await SaveCardOnStaThreadAsync(path, report, cancellationToken);

        return path;
    }

    private static Task SaveCardOnStaThreadAsync(string path, GestureClip.Core.Workstation.WorkBearDailyReport report, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                const int width = 1080;
                const int height = 1440;
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new LinearGradientBrush(MediaColor.FromRgb(15, 23, 42), MediaColor.FromRgb(30, 64, 175), 90), null, new Rect(0, 0, width, height));
                    dc.DrawRoundedRectangle(new SolidColorBrush(MediaColor.FromArgb(235, 255, 255, 255)), null, new Rect(70, 70, width - 140, height - 140), 42, 42);
                    DrawText(dc, "GestureClip / 工位小熊", 112, 130, 46, Colors.Black, FontWeights.Bold);
                    DrawText(dc, report.Date.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture), 112, 205, 26, MediaColor.FromRgb(71, 85, 105), FontWeights.SemiBold);
                    DrawText(dc, "今日牛马生存报告", 112, 285, 64, MediaColor.FromRgb(30, 64, 175), FontWeights.Bold);

                    DrawMetric(dc, "今日工资", $"￥{report.TodayEarned:0.00}", 112, 405);
                    DrawMetric(dc, "少点次数", $"{report.EstimatedSavedClicks} 次", 560, 405);
                    DrawMetric(dc, "摸鱼价值", $"￥{report.FishingValue:0.00}", 112, 585);
                    DrawMetric(dc, "工作时长", FormatDuration(report.WorkDuration), 560, 585);
                    DrawMetric(dc, "今日评级", report.Rating, 112, 765);

                    DrawText(dc, report.BearLine, 112, 930, 34, MediaColor.FromRgb(15, 23, 42), FontWeights.SemiBold, 840);
                    DrawText(dc, "不包含剪贴板内容 / 图片内容 / 浏览器内容。所有数据本地生成，不上传，不自动分享。", 112, 1240, 24, MediaColor.FromRgb(100, 116, 139), FontWeights.Normal, 840);
                }

                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(path);
                encoder.Save(stream);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    private static void DrawMetric(DrawingContext dc, string label, string value, double x, double y)
    {
        dc.DrawRoundedRectangle(new SolidColorBrush(MediaColor.FromRgb(241, 245, 249)), null, new Rect(x, y, 380, 132), 28, 28);
        DrawText(dc, label, x + 32, y + 26, 24, MediaColor.FromRgb(100, 116, 139), FontWeights.SemiBold);
        DrawText(dc, value, x + 32, y + 66, 38, MediaColor.FromRgb(15, 23, 42), FontWeights.Bold, 320);
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        double x,
        double y,
        double size,
        MediaColor color,
        FontWeight weight,
        double maxWidth = 900)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("zh-CN"),
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            1.0)
        {
            MaxTextWidth = maxWidth,
            MaxTextHeight = 260,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(formatted, new System.Windows.Point(x, y));
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalHours >= 1 ? $"{(int)value.TotalHours}小时{value.Minutes}分" : $"{Math.Max(0, value.Minutes)}分钟";
}

