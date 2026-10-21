using GestureClip.Core.Assistant;

namespace GestureClip.App.ViewModels;

public sealed partial class QuickActionCenterViewModel
{
    private void ApplyFilter()
    {
        var keyword = SearchText.Trim();
        var matches = string.IsNullOrEmpty(keyword)
            ? AllActions
            : AllActions.Where(action => Matches(action, keyword)).ToList();
        FilteredActions.Clear();
        foreach (var action in matches)
        {
            FilteredActions.Add(action);
        }

        if (SelectedAction is null || !FilteredActions.Contains(SelectedAction))
        {
            SelectedAction = FilteredActions.FirstOrDefault();
        }
        OnPropertyChanged(nameof(HasFilteredActions));
    }

    private static bool Matches(AssistantActionDefinition action, string keyword) =>
        action.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        action.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        action.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        action.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private void RefreshInputHint()
    {
        var text = _clipboardTextReader.TryReadText();
        if (string.IsNullOrEmpty(text))
        {
            InputHint = "当前剪贴板：无文本";
            return;
        }

        var oneLine = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (oneLine.Length > 80) oneLine = oneLine[..80] + "…";
        InputHint = $"当前剪贴板：{text.Length} 字 · {oneLine}";
    }

    private async Task RunSelectedAsync(AssistantOutputKind outputKind)
    {
        if (SelectedAction is null || IsBusy) return;
        try
        {
            IsBusy = true;
            StatusText = "执行中…";
            RefreshInputHint();
            var request = new AssistantActionRequest(SelectedAction.Id, OutputOverride: outputKind);
            var result = await _executor.ExecuteAsync(request, CancellationToken.None);
            ApplyResult(result);
            RefreshInputHint();
        }
        catch (Exception)
        {
            StatusText = "执行失败，请重试。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(AssistantActionResult result)
    {
        StatusText = result.Message ?? (result.Success ? "完成" : "失败");
        PreviewText = result.PreviewText ?? "";
        if (result.Success && SelectedAction?.RequiredInput == AssistantInputKind.None)
        {
            PreviewText = "";
        }
    }
}
