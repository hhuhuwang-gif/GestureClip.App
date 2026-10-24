using System.Windows;
using System.Windows.Input;
using GestureClip.App.ViewModels;

namespace GestureClip.App.Controls;

public partial class GestureActionPicker : System.Windows.Controls.UserControl
{
    public event EventHandler? CloseRequested;

    public GestureActionPicker() => InitializeComponent();

    public void Show(GestureBindingCardViewModel card)
        => DataContext = new GestureActionPickerViewModel(card);

    public void FocusSearch() => SearchBox.Focus();

    private void ChooseAction_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not GestureActionPickerViewModel model ||
            sender is not FrameworkElement { Tag: GestureActionOptionViewModel option }) return;
        model.Card.SelectedActionOption = option;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void Picker_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocusWithin && ActionList.Items.Count > 0)
        {
            ActionList.SelectedIndex = 0;
            ActionList.ItemContainerGenerator.ContainerFromIndex(0)?.SetValue(FocusableProperty, true);
            (ActionList.ItemContainerGenerator.ContainerFromIndex(0) as UIElement)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ActionList.SelectedItem is GestureActionOptionViewModel option &&
                 DataContext is GestureActionPickerViewModel model)
        {
            model.Card.SelectedActionOption = option;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}
