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

    private async Task ApplyClipboardCaptureEnabledAsync(bool enabled)
    {
        await _featureToggleService.SetClipboardCaptureEnabledAsync(enabled, CancellationToken.None);
    }


    public async Task RefreshClipboardStatsAsync()
    {
        ClipboardItemCount = await _clipboardRepository.GetCountAsync(CancellationToken.None);
    }


    public async Task ClearAllClipboardItemsAsync()
    {
        if (!_confirmationService.Confirm(
            "清空全部剪贴板历史",
            "这会删除所有剪贴板历史，包括固定项。是否继续？"))
        {
            await RefreshClipboardStatsAsync();
            return;
        }

        await _clipboardRepository.ClearAllAsync(CancellationToken.None);
        await RefreshAfterClipboardCleanupAsync();
    }


    public async Task ClearUnpinnedClipboardItemsAsync()
    {
        if (!_confirmationService.Confirm(
            "清空非固定项",
            "这会删除所有非固定剪贴板历史，固定项会保留。是否继续？"))
        {
            await RefreshClipboardStatsAsync();
            return;
        }

        await _clipboardRepository.ClearUnpinnedAsync(CancellationToken.None);
        await RefreshAfterClipboardCleanupAsync();
    }


    public async Task ApplyClipboardCleanupAsync()
    {
        if (!_confirmationService.Confirm(
            "立即执行剪贴板清理",
            "这会根据最大保存数量和保留天数删除旧的非固定剪贴板记录。是否继续？"))
        {
            await RefreshClipboardStatsAsync();
            return;
        }

        await _clipboardRepository.CleanupAsync(ClipboardMaxItems, ClipboardRetentionDays, CancellationToken.None);
        await RefreshAfterClipboardCleanupAsync();
    }


    private async Task RefreshAfterClipboardCleanupAsync()
    {
        await RefreshClipboardStatsAsync();
        await _clipboardOverlayService.RefreshAsync();
    }


    private async Task ApplyOpenClipboardHotkeyAsync(string hotkeyText)
    {
        await _settingsService.SetAsync(SettingKeys.HotkeyOpenClipboardOverlayKey, hotkeyText, CancellationToken.None);
        RestartGlobalHotkeys();
        HotkeyCaptureStatusText = $"已保存「打开历史」：{hotkeyText}（{HotkeyStatusText}）";
    }


    private async Task ApplyOpenQuickActionHotkeyAsync(string hotkeyText)
    {
        await _settingsService.SetAsync(SettingKeys.HotkeyOpenQuickActionCenterKey, hotkeyText, CancellationToken.None);
        RestartGlobalHotkeys();
        HotkeyCaptureStatusText = $"已保存「快捷动作」：{hotkeyText}（{HotkeyStatusText}）";
    }


    private async Task ApplyPastePlainTextHotkeyAsync(string hotkeyText)
    {
        await _settingsService.SetAsync(SettingKeys.HotkeyPastePlainTextKey, hotkeyText, CancellationToken.None);
        RestartGlobalHotkeys();
        HotkeyCaptureStatusText = $"已保存「纯文本粘贴」：{hotkeyText}（{HotkeyStatusText}）";
    }


    private void RestartGlobalHotkeys()
    {
        _globalHotkeyService.Stop();
        _globalHotkeyService.Start();
        OnPropertyChanged(nameof(HotkeyStatusText));
        if (_globalHotkeyService.Status.State == HotkeyRegistrationState.Failed)
        {
            var failed = HotkeyDefinition.ParseOrDefault(
                _settingsService.Get(SettingKeys.HotkeyOpenClipboardOverlayKey, HotkeyDefinition.DefaultOpenClipboardOverlay));
            var tips = HotkeyDefinition.SuggestAlternatives(failed, 4)
                .Select(h => h.DisplayText)
                .ToArray();
            HotkeyCaptureStatusText = tips.Length == 0
                ? HotkeyStatusText
                : $"{HotkeyStatusText}。可尝试：{string.Join(" / ", tips)}";
        }
    }

    public void ApplySuggestedHotkey(string displayText)
    {
        if (string.IsNullOrWhiteSpace(displayText))
        {
            return;
        }

        OpenClipboardHotkeyText = displayText;
    }


    private async Task ApplyClipboardEdgePresetAsync()
    {
        await ApplyEdgePresetAsync(
            enableLeftMiddle: true,
            leftMiddleAction: BuiltInGestureAction.OpenClipboardOverlay,
            enableX1: true,
            x1Action: BuiltInGestureAction.PasteLatestClipboardItem,
            enableX2: true,
            x2Action: BuiltInGestureAction.Copy,
            enableWheel: false,
            wheelAction: BuiltInGestureAction.TaskSwitcher);
    }

}
