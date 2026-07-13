using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;

namespace GestureClip.App.ViewModels;

public sealed class QuickActionCenterViewModel : INotifyPropertyChanged
{
    private readonly IAssistantActionCatalog _catalog;
    private readonly IAssistantActionExecutor _executor;
    private readonly IClipboardTextReader _clipboardTextReader;
    private string _searchText = "";
    private string _statusText = "复制一段文字，选择动作后回车执行。";
    private string _previewText = "";
    private string _inputHint = "";
    private AssistantActionDefinition? _selectedAction;
    private bool _isBusy;

    public QuickActionCenterViewModel(
        IAssistantActionCatalog catalog,
        IAssistantActionExecutor executor,
        IClipboardTextReader clipboardTextReader)
    {
        _catalog = catalog;
        _executor = executor;
        _clipboardTextReader = clipboardTextReader;

        AllActions = catalog.GetActions()
            .Where(x => x.PrivacyLevel == AssistantPrivacyLevel.LocalOnly)
            .ToList();

        FilteredActions = new ObservableCollection<AssistantActionDefinition>(AllActions);
        SelectedAction = FilteredActions.FirstOrDefault();

        RunSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(AssistantOutputKind.Clipboard), _ => CanRun());
        RunAndPasteCommand = new AsyncRelayCommand(_ => RunSelectedAsync(AssistantOutputKind.Paste), _ => CanRunSelectedTextAction());
        PreviewSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(AssistantOutputKind.Preview), _ => CanRunSelectedTextAction());
        RefreshInputCommand = new RelayCommand(_ => RefreshInputHint());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<AssistantActionDefinition> AllActions { get; }

    public ObservableCollection<AssistantActionDefinition> FilteredActions { get; }

    public ICommand RunSelectedCommand { get; }

    public ICommand RunAndPasteCommand { get; }

    public ICommand PreviewSelectedCommand { get; }

    public ICommand RefreshInputCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public AssistantActionDefinition? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (ReferenceEquals(_selectedAction, value))
            {
                return;
            }

            _selectedAction = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDescription));
            OnPropertyChanged(nameof(CanPasteSelected));
        }
    }

    public string SelectedDescription =>
        SelectedAction?.Description ?? "选择一个本地动作。不会上传内容。";

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string PreviewText
    {
        get => _previewText;
        private set
        {
            if (_previewText == value)
            {
                return;
            }

            _previewText = value;
            OnPropertyChanged();
        }
    }

    public string InputHint
    {
        get => _inputHint;
        private set
        {
            if (_inputHint == value)
            {
                return;
            }

            _inputHint = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public bool CanPasteSelected =>
        SelectedAction is { RequiredInput: AssistantInputKind.ClipboardText or AssistantInputKind.SelectedText };

    public string OpenQuickActionHotkeyHint { get; private set; } = "Ctrl + Shift + Q";

    public void OnOpened(string? hotkeyHint = null)
    {
        if (!string.IsNullOrWhiteSpace(hotkeyHint))
        {
            OpenQuickActionHotkeyHint = hotkeyHint;
            OnPropertyChanged(nameof(OpenQuickActionHotkeyHint));
        }

        SearchText = "";
        ApplyFilter();
        SelectedAction = FilteredActions.FirstOrDefault();
        RefreshInputHint();
        StatusText = $"本地动作面板 · 不上传 · {OpenQuickActionHotkeyHint} 开关";
        PreviewText = "";
    }

    public void MoveSelection(int delta)
    {
        if (FilteredActions.Count == 0)
        {
            return;
        }

        var index = SelectedAction is null ? 0 : FilteredActions.IndexOf(SelectedAction);
        if (index < 0)
        {
            index = 0;
        }

        index = Math.Clamp(index + delta, 0, FilteredActions.Count - 1);
        SelectedAction = FilteredActions[index];
    }

    public Task RunDefaultAsync()
    {
        if (SelectedAction is null)
        {
            return Task.CompletedTask;
        }

        return SelectedAction.RequiredInput == AssistantInputKind.None
            ? RunSelectedAsync(AssistantOutputKind.None)
            : RunSelectedAsync(AssistantOutputKind.Clipboard);
    }

    private bool CanRun() => !IsBusy && SelectedAction is not null;

    private bool CanRunSelectedTextAction() =>
        CanRun() && SelectedAction is { RequiredInput: AssistantInputKind.ClipboardText or AssistantInputKind.SelectedText };

    private void ApplyFilter()
    {
        var keyword = SearchText.Trim();
        IEnumerable<AssistantActionDefinition> query = AllActions;
        if (!string.IsNullOrEmpty(keyword))
        {
            query = AllActions.Where(action =>
                action.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                action.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                action.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                action.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();
        FilteredActions.Clear();
        foreach (var action in list)
        {
            FilteredActions.Add(action);
        }

        if (SelectedAction is null || !FilteredActions.Contains(SelectedAction))
        {
            SelectedAction = FilteredActions.FirstOrDefault();
        }
    }

    private void RefreshInputHint()
    {
        var text = _clipboardTextReader.TryReadText();
        if (string.IsNullOrEmpty(text))
        {
            InputHint = "当前剪贴板：无文本";
            return;
        }

        var oneLine = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (oneLine.Length > 80)
        {
            oneLine = oneLine[..80] + "…";
        }

        InputHint = $"当前剪贴板：{text.Length} 字 · {oneLine}";
    }

    private async Task RunSelectedAsync(AssistantOutputKind outputKind)
    {
        if (SelectedAction is null || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "执行中…";
            RefreshInputHint();

            var request = new AssistantActionRequest(SelectedAction.Id, OutputOverride: outputKind);
            var result = await _executor.ExecuteAsync(request, CancellationToken.None);
            StatusText = result.Message ?? (result.Success ? "完成" : "失败");
            PreviewText = result.PreviewText ?? "";
            if (result.Success && SelectedAction.RequiredInput == AssistantInputKind.None)
            {
                PreviewText = "";
            }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
