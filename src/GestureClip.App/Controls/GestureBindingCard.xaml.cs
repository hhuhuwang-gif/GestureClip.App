using System.Windows;

namespace GestureClip.App.Controls;

public partial class GestureBindingCard : System.Windows.Controls.UserControl
{
    public static readonly RoutedEvent ChangeActionRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ChangeActionRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GestureBindingCard));

    public event RoutedEventHandler ChangeActionRequested
    {
        add => AddHandler(ChangeActionRequestedEvent, value);
        remove => RemoveHandler(ChangeActionRequestedEvent, value);
    }

    public GestureBindingCard() => InitializeComponent();

    private void ChangeAction_Click(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ChangeActionRequestedEvent, this));
        e.Handled = true;
    }
}
