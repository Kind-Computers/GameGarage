using GameGarage.Core;
using GameGarage.Tools;
using System.Windows;

namespace GameGarage.Interface;

/// <summary>All modal interaction belongs to the Windows shell, not diagnostic adapters.</summary>
internal sealed class WpfUserInteraction : IWindowsUserInteraction
{
    private static T OnUi<T>(Func<T> action) => Application.Current.Dispatcher.Invoke(action);
    public bool Confirm(string title, string message) => OnUi(() =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);
    public void Notify(string title, string message, DiagnosticStatus status) => OnUi(() =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK,
            status is DiagnosticStatus.Error or DiagnosticStatus.IssuesFound ? MessageBoxImage.Warning : MessageBoxImage.Information));
    public (string Filename, int Index)? SelectWindowsImage() => OnUi<(string, int)?>(() =>
    {
        var dialog = new WindowsImageSelection { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? (dialog.SelectedWindowsImageFilename, dialog.SelectedWindowsImageIndex) : null;
    });
    public void ShowDrivers(IReadOnlyList<DeviceDriverInfo> drivers) => OnUi(() =>
        new DeviceDriverWindow(drivers) { Owner = Application.Current.MainWindow }.ShowDialog());
    public void RequestExit() => Application.Current.Dispatcher.BeginInvoke(() => Application.Current.MainWindow.Close());
}
