using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GestureClip.App.ViewModels;

public sealed class GestureActionPickerViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<GestureActionOptionViewModel> _options;
    private string _searchText = "";

    public GestureActionPickerViewModel(GestureBindingCardViewModel card)
    {
        Card = card;
        _options = card.ActionOptions;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public GestureBindingCardViewModel Card { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredOptions));
            OnPropertyChanged(nameof(ResultText));
        }
    }

    public IReadOnlyList<GestureActionOptionViewModel> FilteredOptions
    {
        get
        {
            var query = SearchText.Trim();
            return query.Length == 0 ? _options : _options.Where(option =>
                option.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
    }

    public string ResultText => FilteredOptions.Count == 0
        ? "没有匹配动作，请更换关键词"
        : $"{FilteredOptions.Count} 个动作 · 选择后立即应用 · Esc 关闭";

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
