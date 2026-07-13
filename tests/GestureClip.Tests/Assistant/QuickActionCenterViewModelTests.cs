using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;
using GestureClip.Features.Assistant;
using Xunit;

namespace GestureClip.Tests.Assistant;

public sealed class QuickActionCenterViewModelTests
{
    [Fact]
    public void Search_filters_actions_by_name_and_category()
    {
        var vm = CreateViewModel();

        vm.SearchText = "json";

        Assert.All(vm.FilteredActions, action =>
            Assert.True(
                action.DisplayName.Contains("JSON", StringComparison.OrdinalIgnoreCase) ||
                action.Category.Contains("JSON", StringComparison.OrdinalIgnoreCase) ||
                action.Id.Contains("json", StringComparison.OrdinalIgnoreCase)));
        Assert.NotEmpty(vm.FilteredActions);
    }

    [Fact]
    public void MoveSelection_wraps_within_filtered_list()
    {
        var vm = CreateViewModel();
        vm.SearchText = "";
        var first = vm.FilteredActions[0];
        vm.SelectedAction = first;

        vm.MoveSelection(1);
        Assert.Equal(vm.FilteredActions[1], vm.SelectedAction);

        vm.MoveSelection(-1);
        Assert.Equal(first, vm.SelectedAction);
    }

    [Fact]
    public async Task RunDefaultAsync_executes_selected_action()
    {
        var executor = new RecordingExecutor();
        var vm = CreateViewModel(executor);
        vm.SelectedAction = vm.AllActions.First(x => x.Id == BuiltInAssistantActionCatalog.TrimId);

        await vm.RunDefaultAsync();

        Assert.Equal(BuiltInAssistantActionCatalog.TrimId, executor.LastRequest?.ActionId);
        Assert.Equal(AssistantOutputKind.Clipboard, executor.LastRequest?.OutputOverride);
        Assert.Contains("完成", vm.StatusText + executor.ResultMessage);
    }

    private static QuickActionCenterViewModel CreateViewModel(IAssistantActionExecutor? executor = null)
    {
        return new QuickActionCenterViewModel(
            new BuiltInAssistantActionCatalog(),
            executor ?? new RecordingExecutor(),
            new FakeClipboardTextReader("  sample  "));
    }

    private sealed class RecordingExecutor : IAssistantActionExecutor
    {
        public AssistantActionRequest? LastRequest { get; private set; }
        public string ResultMessage { get; private set; } = "完成";

        public Task<AssistantActionResult> ExecuteAsync(AssistantActionRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new AssistantActionResult(true, PreviewText: "sample", Message: ResultMessage));
        }
    }

    private sealed class FakeClipboardTextReader(string? text) : IClipboardTextReader
    {
        public string? TryReadText() => text;
        public string? TryReadImagePngBase64() => null;
    }
}
