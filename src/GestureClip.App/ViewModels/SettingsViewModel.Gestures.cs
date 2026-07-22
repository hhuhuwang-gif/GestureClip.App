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

    private async Task ApplyGestureEnabledAsync(bool enabled)
    {
        await _featureToggleService.SetGestureEnabledAsync(enabled, CancellationToken.None);
    }


    private async Task ApplyEdgeTriggerEnabledAsync(bool enabled)
    {
        await _settingsService.SetAsync(SettingKeys.EdgeTriggerEnabled, enabled, CancellationToken.None);
        if (enabled)
        {
            await _edgeTriggerService.StartAsync(CancellationToken.None);
        }
        else
        {
            await _edgeTriggerService.StopAsync(CancellationToken.None);
        }
    }


    private void SetEdgeTriggerAction(ref BuiltInGestureAction field, BuiltInGestureAction value, string settingKey)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(settingKey switch
        {
            SettingKeys.EdgeTriggerTopLeftAction => nameof(EdgeTriggerTopLeftAction),
            SettingKeys.EdgeTriggerTopRightAction => nameof(EdgeTriggerTopRightAction),
            SettingKeys.EdgeTriggerBottomRightAction => nameof(EdgeTriggerBottomRightAction),
            SettingKeys.EdgeTriggerBottomLeftAction => nameof(EdgeTriggerBottomLeftAction),
            SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction => nameof(EdgeTriggerLeftEdgeLeftButtonAction),
            SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction => nameof(EdgeTriggerLeftEdgeMiddleButtonAction),
            SettingKeys.EdgeTriggerLeftEdgeXButton1Action => nameof(EdgeTriggerLeftEdgeXButton1Action),
            SettingKeys.EdgeTriggerLeftEdgeXButton2Action => nameof(EdgeTriggerLeftEdgeXButton2Action),
            SettingKeys.EdgeTriggerTopRightWheelAction => nameof(EdgeTriggerTopRightWheelAction),
            SettingKeys.EdgeTriggerSlideLeftAction => nameof(EdgeTriggerSlideLeftAction),
            SettingKeys.EdgeTriggerSlideRightAction => nameof(EdgeTriggerSlideRightAction),
            SettingKeys.EdgeTriggerSlideTopAction => nameof(EdgeTriggerSlideTopAction),
            SettingKeys.EdgeTriggerSlideBottomAction => nameof(EdgeTriggerSlideBottomAction),
            _ => null
        });
        _ = SaveEdgeTriggerSettingAndRefreshAsync(settingKey, value);
    }


    private void SetEdgeTriggerEnabled(ref bool field, bool value, string settingKey)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(settingKey switch
        {
            SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled => nameof(EdgeTriggerLeftEdgeLeftButtonEnabled),
            SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled => nameof(EdgeTriggerLeftEdgeMiddleButtonEnabled),
            SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled => nameof(EdgeTriggerLeftEdgeXButton1Enabled),
            SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled => nameof(EdgeTriggerLeftEdgeXButton2Enabled),
            SettingKeys.EdgeTriggerTopRightWheelEnabled => nameof(EdgeTriggerTopRightWheelEnabled),
            SettingKeys.EdgeTriggerSlideLeftEnabled => nameof(EdgeTriggerSlideLeftEnabled),
            SettingKeys.EdgeTriggerSlideRightEnabled => nameof(EdgeTriggerSlideRightEnabled),
            SettingKeys.EdgeTriggerSlideTopEnabled => nameof(EdgeTriggerSlideTopEnabled),
            SettingKeys.EdgeTriggerSlideBottomEnabled => nameof(EdgeTriggerSlideBottomEnabled),
            _ => null
        });
        _ = SaveEdgeTriggerSettingAndRefreshAsync(settingKey, value);
    }


    private void SetEdgeTriggerInt(ref int field, int value, string settingKey, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        _ = SaveEdgeTriggerSettingAndRefreshAsync(settingKey, value);
    }


    private async Task SaveEdgeTriggerSettingAndRefreshAsync<T>(string settingKey, T value)
    {
        await _settingsService.SetAsync(settingKey, value, CancellationToken.None);
        _edgeTriggerService.RefreshSettings();
    }


    private async Task ApplyBrowserEdgePresetAsync()
    {
        await ApplyEdgePresetAsync(
            enableLeftMiddle: true,
            leftMiddleAction: BuiltInGestureAction.Refresh,
            enableX1: true,
            x1Action: BuiltInGestureAction.SendAltLeft,
            enableX2: true,
            x2Action: BuiltInGestureAction.SendAltRight,
            enableWheel: true,
            wheelAction: BuiltInGestureAction.TaskSwitcher);
    }


    private async Task ApplySystemEdgePresetAsync()
    {
        await ApplyEdgePresetAsync(
            enableLeftMiddle: true,
            leftMiddleAction: BuiltInGestureAction.ShowDesktop,
            enableX1: true,
            x1Action: BuiltInGestureAction.SwitchApp,
            enableX2: true,
            x2Action: BuiltInGestureAction.TaskSwitcher,
            enableWheel: true,
            wheelAction: BuiltInGestureAction.VolumeUp);
    }


    private async Task ApplyEdgePresetAsync(
        bool enableLeftMiddle,
        BuiltInGestureAction leftMiddleAction,
        bool enableX1,
        BuiltInGestureAction x1Action,
        bool enableX2,
        BuiltInGestureAction x2Action,
        bool enableWheel,
        BuiltInGestureAction wheelAction)
    {
        EdgeTriggerEnabled = true;
        EdgeTriggerLeftEdgeMiddleButtonEnabled = enableLeftMiddle;
        EdgeTriggerLeftEdgeMiddleButtonAction = leftMiddleAction;
        EdgeTriggerLeftEdgeXButton1Enabled = enableX1;
        EdgeTriggerLeftEdgeXButton1Action = x1Action;
        EdgeTriggerLeftEdgeXButton2Enabled = enableX2;
        EdgeTriggerLeftEdgeXButton2Action = x2Action;
        EdgeTriggerTopRightWheelEnabled = enableWheel;
        EdgeTriggerTopRightWheelAction = wheelAction;
        EdgeTriggerSlideLeftEnabled = true;
        EdgeTriggerSlideLeftAction = BuiltInGestureAction.SwitchApp;
        EdgeTriggerSlideRightEnabled = true;
        EdgeTriggerSlideRightAction = BuiltInGestureAction.TaskSwitcher;
        EdgeTriggerSlideBottomEnabled = true;
        EdgeTriggerSlideBottomAction = BuiltInGestureAction.PasteAndEnter;
        await _settingsService.SetAsync(SettingKeys.EdgeTriggerEnabled, true, CancellationToken.None);
    }




    private void UpdateGestureSettingsSnapshot()
    {
        _gestureSettingsProvider.Update(new GestureSettings(
            _gestureEnabled,
            _gestureShowOverlay,
            _gestureCloseWindowEnabled,
            _gestureDebugEnabled,
            _selectedGesturePreset,
            new GestureOptions(_gestureTriggerThreshold, 16, 2000, 2),
            _gestureLeftButtonEnabled,
            _gestureMiddleButtonEnabled,
            _gestureXButton1Enabled,
            _gestureXButton2Enabled,
            _gestureRightButtonEnabled));
    }


    private void RefreshGestureBindingCards()
    {
        var previousPattern = SelectedGestureBindingCard?.Pattern;
        GestureBindingCards.Clear();
        PrimaryGestureBindingCards.Clear();
        AdvancedGestureBindingCards.Clear();
        var bindings = _gesturePresetProvider.GetBindings(_selectedGesturePreset);
        var patterns = GesturePatterns
            .Concat(bindings.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(pattern => Array.IndexOf(GesturePatterns, pattern) >= 0 ? Array.IndexOf(GesturePatterns, pattern) : int.MaxValue)
            .ThenBy(pattern => pattern, StringComparer.Ordinal);

        foreach (var pattern in patterns)
        {
            var action = bindings.TryGetValue(pattern, out var mappedAction) ? mappedAction : BuiltInGestureAction.None;
            var card = CreateGestureBindingCard(pattern, action);
            GestureBindingCards.Add(card);
            if (card.IsCommon || card.IsBound)
            {
                PrimaryGestureBindingCards.Add(card);
            }
            else
            {
                AdvancedGestureBindingCards.Add(card);
            }
        }

        SelectedGestureBindingCard = PrimaryGestureBindingCards.FirstOrDefault(card => card.Pattern == previousPattern)
            ?? GestureBindingCards.FirstOrDefault(card => card.Pattern == previousPattern)
            ?? PrimaryGestureBindingCards.FirstOrDefault()
            ?? GestureBindingCards.FirstOrDefault();
        NotifyGestureBindingEmptyStatesChanged();
    }


    private GestureBindingCardViewModel CreateGestureBindingCard(string pattern, BuiltInGestureAction action)
    {
        return new GestureBindingCardViewModel(
            pattern,
            DirectionText(pattern),
            GestureName(pattern),
            PrimaryGesturePatterns.Contains(pattern),
            action,
            GestureActionOptions,
            ApplyGestureBindingAsync,
            DeleteGestureBindingAsync,
            ResolveLeftButtonEnhancedAction);
    }


    private BuiltInGestureAction ResolveLeftButtonEnhancedAction(string pattern)
    {
        var map = _gesturePresetProvider.GetLeftButtonEnhancedBindings();
        return map.TryGetValue(pattern, out var action) ? action : BuiltInGestureAction.None;
    }


    private void RefreshLeftButtonEnhancedBindings()
    {
        LeftButtonEnhancedBindings.Clear();
        foreach (var pair in _gesturePresetProvider.GetLeftButtonEnhancedBindings()
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            LeftButtonEnhancedBindings.Add(CreateLeftButtonEnhancedBinding(pair.Key, pair.Value));
        }

        LeftButtonEnhancedStatusText = LeftButtonEnhancedBindings.Count == 0
            ? "当前没有左键增强动作。可点“添加一条”，例如：下划 + 左键 = 智能粘贴。"
            : $"已配置 {LeftButtonEnhancedBindings.Count} 条左键增强。按住右键画手势时再点左键即可触发。";
        OnPropertyChanged(nameof(LeftButtonEnhancedStatusText));
        foreach (var card in GestureBindingCards)
        {
            card.RefreshLeftButtonModifierDisplay();
        }
    }


    private LeftButtonEnhancedBindingViewModel CreateLeftButtonEnhancedBinding(string pattern, BuiltInGestureAction action)
    {
        var item = new LeftButtonEnhancedBindingViewModel(
            pattern,
            action,
            GestureActionOptions,
            SaveLeftButtonEnhancedBindingsAsync);
        item.DeleteRequested += (_, _) =>
        {
            LeftButtonEnhancedBindings.Remove(item);
        };
        return item;
    }


    private void AddLeftButtonEnhancedBinding()
    {
        var pattern = "D";
        if (LeftButtonEnhancedBindings.Any(item => string.Equals(item.Pattern, pattern, StringComparison.Ordinal)))
        {
            pattern = "U";
        }

        if (LeftButtonEnhancedBindings.Any(item => string.Equals(item.Pattern, pattern, StringComparison.Ordinal)))
        {
            pattern = "L";
        }

        var item = CreateLeftButtonEnhancedBinding(pattern, BuiltInGestureAction.SmartPaste);
        LeftButtonEnhancedBindings.Add(item);
        _ = SaveLeftButtonEnhancedBindingsAsync();
    }


    private async Task ResetLeftButtonEnhancedBindingsAsync()
    {
        if (!_confirmationService.Confirm(
            "恢复默认左键增强",
            "要恢复默认左键增强吗？\n\n默认：\n下划 + 左键 → 智能粘贴（干净粘贴）\n上划 + 左键 → 全选\n\n你现在的自定义增强会被替换。"))
        {
            return;
        }

        _gesturePresetProvider.UpdateLeftButtonEnhancedBindings(GesturePresetProvider.DefaultLeftButtonEnhanced);
        await _settingsService.SetAsync(
            SettingKeys.GestureLeftButtonEnhancedJson,
            JsonSerializer.Serialize(GesturePresetProvider.DefaultLeftButtonEnhanced),
            CancellationToken.None);
        RefreshLeftButtonEnhancedBindings();
        LeftButtonEnhancedStatusText = "已恢复默认左键增强。";
        OnPropertyChanged(nameof(LeftButtonEnhancedStatusText));
    }


    private async Task SaveLeftButtonEnhancedBindingsAsync()
    {
        var map = LeftButtonEnhancedBindings
            .Where(item => !string.IsNullOrWhiteSpace(item.Pattern) && item.Action != BuiltInGestureAction.None)
            .GroupBy(item => item.Pattern.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Action, StringComparer.Ordinal);

        _gesturePresetProvider.UpdateLeftButtonEnhancedBindings(map);
        await _settingsService.SetAsync(
            SettingKeys.GestureLeftButtonEnhancedJson,
            JsonSerializer.Serialize(map),
            CancellationToken.None);

        LeftButtonEnhancedStatusText = map.Count == 0
            ? "已保存：当前没有左键增强动作。"
            : $"已保存 {map.Count} 条左键增强。";
        OnPropertyChanged(nameof(LeftButtonEnhancedStatusText));
        foreach (var card in GestureBindingCards)
        {
            card.RefreshLeftButtonModifierDisplay();
        }
    }


    private async Task ApplyGestureBindingAsync(GestureBindingCardViewModel card)
    {
        if (ReferenceEquals(card, SelectedGestureBindingCard))
        {
            OnPropertyChanged(nameof(SelectedGestureBindingActionName));
            OnPropertyChanged(nameof(SelectedGestureBindingShortcutText));
            OnPropertyChanged(nameof(SelectedGestureBindingEmptyText));
            RefreshGestureCardBuckets(card);
        }

        await SaveGestureBindingsAsync();
    }


    private void RefreshGestureCardBuckets(GestureBindingCardViewModel card)
    {
        var shouldBePrimary = card.IsCommon || card.IsBound;
        var inPrimary = PrimaryGestureBindingCards.Contains(card);
        if (shouldBePrimary && !inPrimary)
        {
            AdvancedGestureBindingCards.Remove(card);
            PrimaryGestureBindingCards.Add(card);
        }
        else if (!shouldBePrimary && inPrimary)
        {
            PrimaryGestureBindingCards.Remove(card);
            AdvancedGestureBindingCards.Add(card);
        }
    }


    private async Task DeleteGestureBindingAsync(GestureBindingCardViewModel card)
    {
        if (!_confirmationService.Confirm(
            "删除这个手势",
            $"要删除这个手势吗？{Environment.NewLine}{Environment.NewLine}" +
            $"手势：{card.GestureName}（{card.Pattern}）{Environment.NewLine}" +
            $"方向：{DirectionText(card.Pattern)}{Environment.NewLine}" +
            $"当前动作：{card.ActionName}{Environment.NewLine}{Environment.NewLine}" +
            "删除后，这个手势不会再触发任何动作。"))
        {
            return;
        }

        var index = GestureBindingCards.IndexOf(card);
        GestureBindingCards.Remove(card);
        PrimaryGestureBindingCards.Remove(card);
        AdvancedGestureBindingCards.Remove(card);
        if (ReferenceEquals(card, SelectedGestureBindingCard))
        {
            SelectedGestureBindingCard = GestureBindingCards.Count == 0
                ? null
                : GestureBindingCards[Math.Min(index, GestureBindingCards.Count - 1)];
        }

        NotifyGestureBindingEmptyStatesChanged();
        await SaveGestureBindingsAsync();
    }


    public Task DeleteSelectedGestureBindingAsync()
    {
        return SelectedGestureBindingCard is null
            ? Task.CompletedTask
            : DeleteGestureBindingAsync(SelectedGestureBindingCard);
    }


    private async Task ApplyRecommendedGestureBindingsAsync()
    {
        if (!_confirmationService.Confirm(
            "添加推荐手势",
            $"要添加推荐手势吗？{Environment.NewLine}{Environment.NewLine}" +
            $"已有的自定义手势不会被删除。{Environment.NewLine}" +
            "如果某个推荐手势已经存在，会自动跳过。"))
        {
            RecommendedGestureStatusText = "已取消，现有手势没有变化。";
            return;
        }

        var added = 0;
        var skipped = 0;
        GestureBindingCardViewModel? firstAdded = null;

        foreach (var recommended in RecommendedGestureBindings)
        {
            var existing = GestureBindingCards.FirstOrDefault(card =>
                string.Equals(card.Pattern, recommended.Pattern, StringComparison.Ordinal));
            if (existing is not null && existing.IsBound)
            {
                skipped++;
                continue;
            }

            if (existing is not null)
            {
                existing.SetSelectedActionWithoutSaving(recommended.Action);
                RefreshGestureCardBuckets(existing);
                firstAdded ??= existing;
                added++;
                continue;
            }

            var card = CreateGestureBindingCard(recommended.Pattern, recommended.Action);
            GestureBindingCards.Add(card);
            if (card.IsCommon || card.IsBound)
            {
                PrimaryGestureBindingCards.Add(card);
            }
            else
            {
                AdvancedGestureBindingCards.Add(card);
            }

            firstAdded ??= card;
            added++;
        }

        if (added == 0)
        {
            RecommendedGestureStatusText = "推荐手势已经都在列表里。";
            return;
        }

        NotifyGestureBindingEmptyStatesChanged();
        if (firstAdded is not null)
        {
            SelectedGestureBindingCard = firstAdded;
        }

        RecommendedGestureStatusText = skipped > 0
            ? $"已添加 {added} 个推荐手势，已跳过 {skipped} 个已存在手势。"
            : $"已添加 {added} 个推荐手势。";
        await SaveGestureBindingsAsync();
    }


    private async Task SaveGestureBindingsAsync()
    {
        var bindings = GestureBindingCards.ToDictionary(item => item.Pattern, item => item.SelectedAction, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(bindings.ToDictionary(
            pair => pair.Key,
            pair => new CustomBindingDto(pair.Value, ShortcutText(pair.Value), pair.Value != BuiltInGestureAction.None),
            StringComparer.Ordinal));
        _gesturePresetProvider.UpdateCustomBindings(bindings);
        _selectedGesturePreset = GesturePreset.Custom;
        UpdateGestureSettingsSnapshot();
        OnPropertyChanged(nameof(SelectedGesturePresetOption));
        await _settingsService.SetAsync(SettingKeys.GestureCustomBindingsJson, json, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.GesturePreset, GesturePreset.Custom, CancellationToken.None);
    }


    private async Task AddCustomGestureBindingAsync()
    {
        var pattern = NormalizeGesturePattern(NewGesturePattern);
        if (!IsValidGesturePattern(pattern))
        {
            return;
        }

        if (!_newGestureAddConfirmationPending)
        {
            _newGestureAddConfirmationPending = true;
            RecordGestureStatusText = $"将添加 {DirectionText(pattern)} → {GestureActionText.Name(NewGestureAction)}，再点一次确认。";
            OnPropertyChanged(nameof(NewGestureAddButtonText));
            return;
        }

        var existing = GestureBindingCards.FirstOrDefault(card => string.Equals(card.Pattern, pattern, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.SelectedAction = NewGestureAction;
            NewGesturePattern = "";
            await ApplyGestureBindingAsync(existing);
            SelectGestureBindingAfterRefresh(pattern);
            return;
        }

        var card = CreateGestureBindingCard(pattern, NewGestureAction);
        GestureBindingCards.Add(card);
        if (card.IsCommon || card.IsBound)
        {
            PrimaryGestureBindingCards.Add(card);
        }
        else
        {
            AdvancedGestureBindingCards.Add(card);
        }
        NewGesturePattern = "";
        await ApplyGestureBindingAsync(card);
        NotifyGestureBindingEmptyStatesChanged();
        SelectGestureBindingAfterRefresh(pattern);
    }


    private void NotifyGestureBindingEmptyStatesChanged()
    {
        OnPropertyChanged(nameof(GestureBindingEmptyStateText));
        OnPropertyChanged(nameof(CustomGestureEmptyStateText));
    }


    private void SelectGestureBindingAfterRefresh(string pattern)
    {
        RefreshGestureBindingCards();
        SelectedGestureBindingCard = GestureBindingCards.FirstOrDefault(card => string.Equals(card.Pattern, pattern, StringComparison.Ordinal))
            ?? SelectedGestureBindingCard;
    }


    private void SetNewGesturePattern(object? parameter)
    {
        if (parameter is string pattern)
        {
            NewGesturePattern = pattern;
        }
    }


    private void SetNewGestureAction(object? parameter)
    {
        if (TryParseGestureAction(parameter, out var action))
        {
            NewGestureAction = action;
        }
    }


    private void SetNewGestureTemplate(object? parameter)
    {
        if (parameter is not string text)
        {
            return;
        }

        var parts = text.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        NewGesturePattern = parts[0];
        if (parts.Length == 2 && TryParseGestureAction(parts[1], out var action))
        {
            NewGestureAction = action;
        }
    }


    private void SetSelectedGestureAction(object? parameter)
    {
        if (SelectedGestureBindingCard is not null && TryParseGestureAction(parameter, out var action))
        {
            SelectedGestureBindingCard.SelectedAction = action;
        }
    }


    private void SelectGestureBinding(object? parameter)
    {
        var pattern = parameter switch
        {
            GestureBindingCardViewModel card => card.Pattern,
            string text => text,
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        SelectedGestureBindingSelectionKey = pattern;
    }


    private static bool TryParseGestureAction(object? parameter, out BuiltInGestureAction action)
    {
        if (parameter is BuiltInGestureAction actionValue)
        {
            action = actionValue;
            return true;
        }

        if (parameter is string text && Enum.TryParse(text, ignoreCase: true, out BuiltInGestureAction parsed))
        {
            action = parsed;
            return true;
        }

        action = BuiltInGestureAction.None;
        return false;
    }


    private void AppendGestureDirection(object? parameter)
    {
        if (parameter is not string direction || direction.Length != 1)
        {
            return;
        }

        var normalized = NormalizeGesturePattern(direction);
        if (!IsValidGesturePattern(normalized))
        {
            return;
        }

        if (NewGesturePattern.Length >= 8)
        {
            return;
        }

        NewGesturePattern += normalized;
    }


    private void RemoveLastGestureDirection()
    {
        if (NewGesturePattern.Length == 0)
        {
            return;
        }

        NewGesturePattern = NewGesturePattern[..^1];
    }


    private void ClearNewGesturePattern()
    {
        NewGesturePattern = "";
        RecordGestureStatusText = "按住左键在方框里画一次。";
    }


    private void ResetNewGestureAddConfirmation()
    {
        if (!_newGestureAddConfirmationPending)
        {
            return;
        }

        _newGestureAddConfirmationPending = false;
    }


    public void SetNewGesturePatternFromRecordedPoints(IReadOnlyList<GesturePoint> points)
    {
        var pattern = RecognizeDesignerGesturePattern(points);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            RecordGestureStatusText = "没识别出来，画得稍微长一点。";
            return;
        }

        NewGesturePattern = pattern;
        RecordGestureStatusText = $"识别为 {DirectionText(pattern)}，可以直接添加。";
    }


    private static string RecognizeDesignerGesturePattern(IReadOnlyList<GesturePoint> points)
    {
        const int designerTriggerThreshold = 20;
        const int designerDirectionThreshold = 18;

        if (points.Count < 2 || TotalDistance(points) < designerTriggerThreshold)
        {
            return "";
        }

        var directions = new List<char>();
        var anchor = points[0];
        foreach (var point in points.Skip(1))
        {
            var dx = point.X - anchor.X;
            var dy = point.Y - anchor.Y;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < designerDirectionThreshold)
            {
                continue;
            }

            var direction = Math.Abs(dx) >= Math.Abs(dy)
                ? dx < 0 ? 'L' : 'R'
                : dy < 0 ? 'U' : 'D';

            if (directions.Count == 0 || directions[^1] != direction)
            {
                directions.Add(direction);
                if (directions.Count > 8)
                {
                    break;
                }
            }

            anchor = point;
        }

        return directions.Count == 0 ? "" : new string(directions.ToArray());
    }


    private static double TotalDistance(IReadOnlyList<GesturePoint> points)
    {
        var total = 0d;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }

        return total;
    }


    private void RefreshGestureDiagnostics()
    {
        _gestureDiagnostics = _mouseGestureService.Diagnostics;
        OnPropertyChanged(nameof(GestureHookStatus));
        OnPropertyChanged(nameof(GestureRuntimeStateText));
        OnPropertyChanged(nameof(LastGesturePattern));
        OnPropertyChanged(nameof(LastGestureAction));
        OnPropertyChanged(nameof(LastGestureError));
        OnPropertyChanged(nameof(LastGestureEventTime));
        OnPropertyChanged(nameof(GestureEnvironmentStatus));
        OnPropertyChanged(nameof(EdgeTriggerLastSource));
        OnPropertyChanged(nameof(EdgeTriggerLastPosition));
        OnPropertyChanged(nameof(EdgeTriggerLastAction));
        OnPropertyChanged(nameof(EdgeTriggerLastReason));
        OnPropertyChanged(nameof(EdgeTriggerLastEventTime));
        OnPropertyChanged(nameof(EdgeTriggerCooldownText));
        OnPropertyChanged(nameof(HotkeyStatusText));
        _ = RefreshDiagnosticsAsync();
        var toggles = _featureToggleService.GetSnapshot();
        if (_clipboardCaptureEnabled != toggles.ClipboardCaptureEnabled)
        {
            _clipboardCaptureEnabled = toggles.ClipboardCaptureEnabled;
            OnPropertyChanged(nameof(ClipboardCaptureEnabled));
        }

        if (_gestureEnabled != toggles.GestureEnabled)
        {
            _gestureEnabled = toggles.GestureEnabled;
            OnPropertyChanged(nameof(GestureEnabled));
        }
    }

}
