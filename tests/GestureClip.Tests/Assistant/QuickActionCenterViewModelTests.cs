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

    [Fact]
    public void Empty_search_result_clears_selection_and_disables_actions()
    {
        var vm = CreateViewModel();

        vm.SearchText = "definitely-no-such-action";

        Assert.False(vm.HasFilteredActions);
        Assert.Null(vm.SelectedAction);
        Assert.False(vm.RunSelectedCommand.CanExecute(null));
        Assert.False(vm.PreviewSelectedCommand.CanExecute(null));
        Assert.False(vm.RunAndPasteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Preview_command_uses_preview_output_without_changing_selection()
    {
        var executor = new RecordingExecutor();
        var vm = CreateViewModel(executor);
        var selected = vm.AllActions.First(x => x.Id == BuiltInAssistantActionCatalog.TrimId);
        vm.SelectedAction = selected;

        vm.PreviewSelectedCommand.Execute(null);
        await WaitForAsync(() => executor.LastRequest is not null);

        Assert.Equal(AssistantOutputKind.Preview, executor.LastRequest?.OutputOverride);
        Assert.Same(selected, vm.SelectedAction);
    }

    [Fact]
    public async Task Running_action_disables_other_action_buttons()
    {
        var executor = new BlockingExecutor();
        var vm = CreateViewModel(executor);

        vm.PreviewSelectedCommand.Execute(null);
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(vm.RunSelectedCommand.CanExecute(null));
        Assert.False(vm.RunAndPasteCommand.CanExecute(null));
        executor.Release.SetResult();
        await WaitForAsync(() => !vm.IsBusy);
        Assert.True(vm.RunSelectedCommand.CanExecute(null));
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

    private sealed class BlockingExecutor : IAssistantActionExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AssistantActionResult> ExecuteAsync(
            AssistantActionRequest request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new AssistantActionResult(true, PreviewText: "done", Message: "完成");
        }
    }

    private sealed class FakeClipboardTextReader(string? text) : IClipboardTextReader
    {
        public string? TryReadText() => text;
        public string? TryReadImagePngBase64() => null;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        Assert.True(condition());
    }
}
