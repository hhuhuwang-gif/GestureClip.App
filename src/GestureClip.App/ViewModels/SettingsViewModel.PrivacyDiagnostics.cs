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


    private async Task LoadSmartPasteRulesAsync()
    {
        var items = await _appSmartPasteRuleService.GetAllAsync(CancellationToken.None);
        AppSmartPasteRules.Clear();
        foreach (var item in items)
        {
            AppSmartPasteRules.Add(new AppSmartPasteRuleViewModel(item, UpdateSmartPasteRuleAsync, DeleteSmartPasteRuleAsync));
        }
    }

    private async Task AddSmartPasteRuleAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewSmartPasteProcessName))
            {
                SmartPasteRulesStatusText = "请先填写进程名，例如 notepad.exe。";
                return;
            }

            await _appSmartPasteRuleService.SetAsync(
                NewSmartPasteProcessName,
                NewSmartPasteStrategy,
                note: null,
                CancellationToken.None);
            var name = NewSmartPasteProcessName;
            NewSmartPasteProcessName = "";
            await LoadSmartPasteRulesAsync();
            SmartPasteRulesStatusText = $"已添加规则：{name} → {NewSmartPasteStrategy}。";
        }
        catch (Exception ex)
        {
            SmartPasteRulesStatusText = $"添加失败：{ex.Message}";
        }
    }

    private async Task AddForegroundSmartPasteRuleAsync()
    {
        try
        {
            var processName = _foregroundAppService.GetCurrent().ProcessName;
            if (string.IsNullOrWhiteSpace(processName))
            {
                SmartPasteRulesStatusText = "无法获取当前前台进程。请切换到目标窗口后再点一次。";
                return;
            }

            NewSmartPasteProcessName = processName;
            await _appSmartPasteRuleService.SetAsync(
                processName,
                NewSmartPasteStrategy,
                note: "前台窗口一键添加",
                CancellationToken.None);
            await LoadSmartPasteRulesAsync();
            SmartPasteRulesStatusText = $"已为前台进程 {processName} 设置 {NewSmartPasteStrategy}。";
        }
        catch (Exception ex)
        {
            SmartPasteRulesStatusText = $"添加前台规则失败：{ex.Message}";
        }
    }

    private async Task UpdateSmartPasteRuleAsync(AppSmartPasteRuleViewModel item)
    {
        await _appSmartPasteRuleService.SetAsync(item.ProcessName, item.Strategy, item.Note, CancellationToken.None);
        SmartPasteRulesStatusText = $"已更新 {item.ProcessName}。";
    }

    private async Task DeleteSmartPasteRuleAsync(AppSmartPasteRuleViewModel item)
    {
        await _appSmartPasteRuleService.DeleteAsync(item.ProcessName, CancellationToken.None);
        AppSmartPasteRules.Remove(item);
        SmartPasteRulesStatusText = $"已删除 {item.ProcessName}。";
    }
}
