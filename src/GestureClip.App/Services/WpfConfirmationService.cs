using System.Windows;
using GestureClip.Core.Abstractions;

namespace GestureClip.App.Services;

public sealed class WpfConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message)
    {
        return System.Windows.MessageBox.Show(
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning) == MessageBoxResult.OK;
    }
}
