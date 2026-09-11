using GameGarage.Resources;
using GameGarage.Utilities;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GameGarage.Interface;

public partial class WindowsImageSelection : Window
{
    private CancellationTokenSource? enumeration;
    private int generation;
    private bool closed;
    public WindowsImageSelection() { InitializeComponent(); DataContext = this; }
    private void Window_Loaded(object sender, RoutedEventArgs e) =>
        CurrentWindowsVersionTextBox.Text = SystemUtilities.WindowsProductName;
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { DefaultExt = ".wim", Filter = UiText.Get("ImageFileFilter"), CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) WindowsImageFileTextBox.Text = dialog.FileName;
    }
    private async void WindowsImageFileTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (OkButton is null) return;
        enumeration?.Cancel();
        var localCancellation = new CancellationTokenSource();
        enumeration = localCancellation;
        var localGeneration = ++generation;
        var filename = WindowsImageFileTextBox.Text;
        WindowsImages.Clear();

        OkButton.IsEnabled = false;
        ImageStatus.Text = UiText.Get("AvailableImages");
        try
        {
            await Task.Delay(300, localCancellation.Token);

            ImageStatus.Text = UiText.Get("ImageLoading");
            var images = await Task.Run(() => File.Exists(filename) ? WindowsImageUtilities.GetWindowsImages(filename, localCancellation.Token) : new List<WindowsImageInfo>(), localCancellation.Token);
            if (closed || generation != localGeneration || localCancellation.IsCancellationRequested) return;
            foreach (var image in images) WindowsImages.Add(image);
            ImageStatus.Text = UiText.Get(images.Count == 0 ? "ImageNoEntries" : "AvailableImages");
            if (images.Count > 0)
            {
                AvailableImagesListView.SelectedIndex = WindowsImageUtilities.GetClosestMatchIndex(CurrentWindowsVersionTextBox.Text, images);
                AvailableImagesListView.ScrollIntoView(AvailableImagesListView.SelectedItem);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!closed && generation == localGeneration) ImageStatus.Text = UiText.Format("ImageReadError", ex.Message);
        }
        finally
        {
            if (ReferenceEquals(enumeration, localCancellation)) enumeration = null;
            localCancellation.Dispose();
        }
    }
    private void AvailableImagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OkButton is not null) OkButton.IsEnabled = AvailableImagesListView.SelectedItem is WindowsImageInfo;
    }
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (AvailableImagesListView.SelectedItem is not WindowsImageInfo image) return;
        SelectedWindowsImageFilename = WindowsImageFileTextBox.Text;
        SelectedWindowsImageIndex = image.Index; // WIM indices need not be contiguous or equal to row numbers.
        DialogResult = true;
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        closed = true;
        generation++;
        if (OkButton is null) return;
        enumeration?.Cancel();
    }
    private void AvailableImagesListView_CopyItem_Click(object sender, RoutedEventArgs e)
    {
        if (AvailableImagesListView.SelectedItem is WindowsImageInfo image) GUIUtilities.CopyText(image.ToString());
    }
    private void AvailableImagesListView_CopyAll_Click(object sender, RoutedEventArgs e) =>
        GUIUtilities.CopyText(string.Join(Environment.NewLine, WindowsImages));
    public string SelectedWindowsImageFilename = "";
    public int SelectedWindowsImageIndex = -1;
    public ObservableCollection<WindowsImageInfo> WindowsImages { get; } = [];
}
