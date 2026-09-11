using GameGarage.Resources;
using GameGarage.Utilities;
using System.Diagnostics;
using System.Windows;

namespace GameGarage.Interface;

public partial class AboutWindow : Window
{
    public AboutWindow() { InitializeComponent(); DataContext = this; }
    public string Version => ProductInfo.Version;
    public string License => ProductInfo.License;
    private void Project_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://github.com/Kind-Computers/GameGarage") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, UiText.Format("LaunchError", ex.Message), UiText.Get("AppTitle")); }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
