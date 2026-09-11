using GameGarage.Resources;
using GameGarage.Utilities;
using System.Windows;

namespace GameGarage;

internal static class AppConsole
{
    [STAThread]
    static int Main(string[] args)
    {
        using var console = new ConsoleUtilities.AttachToConsole(AttachToParent: false);
        try
        {
            App.Main();
            return 0;
        }
        catch (Exception ex)
        {
            var message = UiText.Format("StartupError", ex.Message);
            Console.Error.WriteLine(message);
            MessageBox.Show(message, UiText.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return 2;
        }
    }
}
