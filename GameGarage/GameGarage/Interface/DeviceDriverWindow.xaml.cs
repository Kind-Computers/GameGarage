using GameGarage.Resources;
using GameGarage.Utilities;
using GameGarage.Tools;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GameGarage.Interface;

public partial class DeviceDriverWindow : Window
{
    public DeviceDriverWindow(IEnumerable<DeviceDriverInfo> drivers)
    {
        InitializeComponent();
        foreach (var driver in drivers) UnsignedDrivers.Add(driver);
        DataContext = this;
    }
    private void UnsignedDriversListView_CopyItem_Click(object sender, RoutedEventArgs e)
    {
        if (UnsignedDriversListView.SelectedItem is DeviceDriverInfo driver) GUIUtilities.CopyText(driver.ToString());
    }
    private void UnsignedDriversListView_CopyAll_Click(object sender, RoutedEventArgs e) =>
        GUIUtilities.CopyText(string.Join(Environment.NewLine, UnsignedDrivers));
    private void Open(ProcessStartInfo info)
    {
        try { Process.Start(info); }
        catch (Exception ex) { MessageBox.Show(this, UiText.Format("LaunchError", ex.Message), UiText.Get("AppTitle")); }
    }
    private void DeviceManagerLink_Click(object sender, RoutedEventArgs e) =>
        Open(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mmc.exe"), "devmgmt.msc") { UseShellExecute = true });
    private void WindowsUpdateLink_Click(object sender, RoutedEventArgs e) =>
        Open(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true });
    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
    public ObservableCollection<DeviceDriverInfo> UnsignedDrivers { get; } = [];
}
