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
        var action = SelectedAction;
        if (action is null || IsBusy) return;
        try
        {
            IsBusy = true;
            StatusText = "执行中…";
            RefreshInputHint();
            var request = new AssistantActionRequest(action.Id, OutputOverride: outputKind);
            var result = await _executor.ExecuteAsync(request, CancellationToken.None);
            ApplyResult(action, result);
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

    private void ApplyResult(AssistantActionDefinition action, AssistantActionResult result)
    {
        var message = result.Message ?? (result.Success ? "完成" : "失败");
        StatusText = $"{action.DisplayName}：{message}";
        if (!ReferenceEquals(SelectedAction, action))
        {
            return;
        }

        PreviewText = result.Success && action.RequiredInput == AssistantInputKind.None
            ? ""
            : result.PreviewText ?? "";
    }
}
