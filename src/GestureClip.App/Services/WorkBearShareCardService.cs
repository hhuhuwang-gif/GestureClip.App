using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using MediaColor = System.Windows.Media.Color;

namespace GestureClip.App.Services;

public sealed class WorkBearShareCardService : IWorkBearShareCardService
{
    public const string StyleClassic = "经典蓝";
    public const string StyleMinimal = "简洁白";
    public const string StyleRoast = "吐槽风";
    public const string StyleData = "数据风";

    private readonly IWorkstationDashboardService _dashboardService;
    private readonly ISettingsService? _settingsService;

    public WorkBearShareCardService(IWorkstationDashboardService dashboardService, ISettingsService? settingsService = null)
    {
        _dashboardService = dashboardService;
        _settingsService = settingsService;
    }

    public Task<string> GenerateTodayCardAsync(CancellationToken cancellationToken)
    {
        var style = _settingsService?.Get(SettingKeys.WorkBearReportCardStyle, StyleClassic) ?? StyleClassic;
        return GenerateTodayCardAsync(style, cancellationToken);
    }

    public async Task<string> GenerateTodayCardAsync(string style, CancellationToken cancellationToken)
    {
        var report = await _dashboardService.GenerateDailyReportAsync(DateTimeOffset.Now, cancellationToken);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var safeStyle = NormalizeStyle(style);
        var path = Path.Combine(desktop, $"WorkBear-Report-{report.Date:yyyyMMdd}-{safeStyle}.png");

        await SaveCardOnStaThreadAsync(path, report, safeStyle, cancellationToken);
        return path;
    }

    public void OpenCardFolder(string cardPath)
    {
        try
        {
            if (File.Exists(cardPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{cardPath}\"",
                    UseShellExecute = true
                });
                return;
            }

            var dir = Path.GetDirectoryName(cardPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Best effort open; callers show path text.
        }
    }

    private static string NormalizeStyle(string? style) => style switch
    {
        StyleMinimal or "Minimal" or "简洁" => StyleMinimal,
        StyleRoast or "Roast" or "吐槽" => StyleRoast,
        StyleData or "Data" or "数据" => StyleData,
        _ => StyleClassic
    };

    private static Task SaveCardOnStaThreadAsync(
        string path,
        WorkBearDailyReport report,
        string style,
        CancellationToken cancellationToken)
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
                    DrawCard(dc, width, height, report, style);
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

    private static void DrawCard(DrawingContext dc, int width, int height, WorkBearDailyReport report, string style)
    {
        var (bgStart, bgEnd, panel, accent, textPrimary, textMuted) = style switch
        {
            StyleMinimal => (
                MediaColor.FromRgb(245, 245, 247),
                MediaColor.FromRgb(255, 255, 255),
                MediaColor.FromArgb(250, 255, 255, 255),
                MediaColor.FromRgb(0, 113, 227),
                MediaColor.FromRgb(29, 29, 31),
                MediaColor.FromRgb(110, 110, 115)),
            StyleRoast => (
                MediaColor.FromRgb(40, 20, 30),
                MediaColor.FromRgb(90, 30, 50),
                MediaColor.FromArgb(240, 255, 248, 245),
                MediaColor.FromRgb(255, 59, 48),
                MediaColor.FromRgb(40, 20, 20),
                MediaColor.FromRgb(120, 80, 80)),
            StyleData => (
                MediaColor.FromRgb(15, 23, 42),
                MediaColor.FromRgb(17, 94, 89),
                MediaColor.FromArgb(240, 248, 250, 252),
                MediaColor.FromRgb(16, 185, 129),
                MediaColor.FromRgb(15, 23, 42),
                MediaColor.FromRgb(71, 85, 105)),
            _ => (
                MediaColor.FromRgb(15, 23, 42),
                MediaColor.FromRgb(30, 64, 175),
                MediaColor.FromArgb(235, 255, 255, 255),
                MediaColor.FromRgb(30, 64, 175),
                MediaColor.FromRgb(15, 23, 42),
                MediaColor.FromRgb(100, 116, 139))
        };

        dc.DrawRectangle(new LinearGradientBrush(bgStart, bgEnd, 90), null, new Rect(0, 0, width, height));
        dc.DrawRoundedRectangle(new SolidColorBrush(panel), null, new Rect(70, 70, width - 140, height - 140), 42, 42);

        DrawText(dc, "GestureClip / 工位小熊", 112, 130, 42, textPrimary, FontWeights.Bold);
        DrawText(dc, report.Date.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture) + $" · {style}", 112, 200, 24, textMuted, FontWeights.SemiBold);

        var title = style == StyleRoast ? "今日牛马生存吐槽报告" : "今日牛马生存报告";
        DrawText(dc, title, 112, 275, style == StyleMinimal ? 52 : 60, accent, FontWeights.Bold);

        DrawMetric(dc, "今日工资", $"￥{report.TodayEarned:0.00}", 112, 405, accent, textPrimary, textMuted, style);
        DrawMetric(dc, "少点次数", $"{report.EstimatedSavedClicks} 次", 560, 405, accent, textPrimary, textMuted, style);
        DrawMetric(dc, "摸鱼价值", $"￥{report.FishingValue:0.00}", 112, 585, accent, textPrimary, textMuted, style);
        DrawMetric(dc, "工作时长", FormatDuration(report.WorkDuration), 560, 585, accent, textPrimary, textMuted, style);
        DrawMetric(dc, "今日评级", report.Rating, 112, 765, accent, textPrimary, textMuted, style);

        var line = style == StyleRoast
            ? (string.IsNullOrWhiteSpace(report.BearLine) ? "能下班就不加班，这叫职业素养。" : report.BearLine)
            : report.BearLine;
        DrawText(dc, line, 112, 930, 32, textPrimary, FontWeights.SemiBold, 840);
        DrawText(dc, "不包含剪贴板内容 / 图片内容 / 浏览器内容。所有数据本地生成，不上传，不自动分享。", 112, 1240, 22, textMuted, FontWeights.Normal, 840);
    }

    private static void DrawMetric(
        DrawingContext dc,
        string label,
        string value,
        double x,
        double y,
        MediaColor accent,
        MediaColor textPrimary,
        MediaColor textMuted,
        string style)
    {
        var fill = style == StyleData
            ? MediaColor.FromRgb(236, 253, 245)
            : style == StyleRoast
                ? MediaColor.FromRgb(255, 241, 242)
                : MediaColor.FromRgb(241, 245, 249);
        dc.DrawRoundedRectangle(new SolidColorBrush(fill), null, new Rect(x, y, 380, 132), 28, 28);
        if (style == StyleData)
        {
            dc.DrawRectangle(new SolidColorBrush(accent), null, new Rect(x, y, 10, 132));
        }

        DrawText(dc, label, x + 32, y + 26, 24, textMuted, FontWeights.SemiBold);
        DrawText(dc, value, x + 32, y + 66, 36, textPrimary, FontWeights.Bold, 320);
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
