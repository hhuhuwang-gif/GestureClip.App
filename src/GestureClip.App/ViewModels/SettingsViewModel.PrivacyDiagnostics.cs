using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Diagnostics;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Core.Gestures;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Workstation;
using GestureClip.Features.Gestures;
using GestureClip.Features.Workstation;
using GestureClip.App.Services;
using GestureClip.Infrastructure.Paths;
using System.Windows.Data;
using System.Windows.Threading;

namespace GestureClip.App.ViewModels;

public sealed partial class SettingsViewModel
{

    private async Task LoadBlacklistAsync()
    {
        var items = await _appBlacklistService.GetAllAsync(CancellationToken.None);
        AppBlacklistItems.Clear();
        foreach (var item in items)
        {
            AppBlacklistItems.Add(new AppBlacklistItemViewModel(item, UpdateBlacklistItemAsync, DeleteBlacklistItemAsync));
        }
    }


    private async Task AddBlacklistItemAsync()
    {
        await _appBlacklistService.AddAsync(NewBlacklistProcessName, blockClipboard: true, blockGesture: true, CancellationToken.None);
        NewBlacklistProcessName = "";
        await LoadBlacklistAsync();
    }


    private async Task UpdateBlacklistItemAsync(AppBlacklistItemViewModel item)
    {
        await _appBlacklistService.UpdateAsync(item.Id, item.BlockClipboard, item.BlockGesture, CancellationToken.None);
    }


    private async Task DeleteBlacklistItemAsync(AppBlacklistItemViewModel item)
    {
        await _appBlacklistService.DeleteAsync(item.Id, CancellationToken.None);
        AppBlacklistItems.Remove(item);
    }


    private async Task RefreshDiagnosticsAsync()
    {
        Diagnostics = await _diagnosticsService.GetSnapshotAsync(CancellationToken.None);
    }


    private async Task CopyDiagnosticsAsync()
    {
        var report = await _diagnosticsService.BuildReportAsync(CancellationToken.None);
        _clipboardService.SuppressCaptureFor(TimeSpan.FromMilliseconds(1000));
        await _clipboardWriter.SetTextAsync(report, CancellationToken.None);
    }


    private async Task ExportDiagnosticsAsync()
    {
        var packagePath = await _diagnosticsService.ExportPackageAsync(CancellationToken.None);
        LastDiagnosticsExportText = $"已导出：{packagePath}";
        var directory = Path.GetDirectoryName(packagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            OpenDirectory(directory);
        }
    }


    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }


    private void LoadChangelogText()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "CHANGELOG.md")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CHANGELOG.md"))
            };

            foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var text = File.ReadAllText(path);
                ChangelogText = text.Length > 12000 ? text[..12000] + "\n\n…(已截断，完整内容见 CHANGELOG.md)" : text;
                OnPropertyChanged(nameof(ChangelogText));
                return;
            }

            ChangelogText =
                "## 本机未找到 CHANGELOG.md\n\n" +
                "发布包会附带 CHANGELOG.md。你也可以点“检查更新”查看 GitHub 最新版本说明。";
            OnPropertyChanged(nameof(ChangelogText));
        }
        catch
        {
            ChangelogText = "更新日志读取失败。可打开 GitHub Release 页面查看。";
            OnPropertyChanged(nameof(ChangelogText));
        }
    }

}
