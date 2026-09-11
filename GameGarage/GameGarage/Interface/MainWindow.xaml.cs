using GameGarage.Resources;
using GameGarage.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace GameGarage.Interface;

public partial class MainWindow : Window
{
    private Task? runningTask;
    private CancellationTokenSource? cancellation;
    private readonly Stopwatch elapsed = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool closeWhenFinished;
    private AboutWindow? about;

    public MainWindow()
    {
        InitializeComponent();
        InitializePresentation();
        timer.Tick += (_, _) => ElapsedStatus.Text = UiText.Format("Elapsed", elapsed.Elapsed);
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!Environment.Is64BitProcess || Environment.OSVersion.Version.Build < 22000)
        {
            MessageBox.Show(this, UiText.Get("WindowsRequired"), UiText.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }
        if (CultureInfo.InstalledUICulture.TwoLetterISOLanguageName != "en")
        {
            MessageBox.Show(this, UiText.Get("EnglishOnly"), UiText.Get("UnsupportedLocale"), MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }
        StartTuneUpButton.Focus();
    }

    private async void StartTuneUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (runningTask is { IsCompleted: false }) return;
        var tools = new VerifierTaskList
        {
            VerifyStability = VerifyStabilityCheckBox.IsChecked == true,
            VerifyDrives = VerifyDrivesCheckBox.IsChecked == true,
            VerifyWindowsArchive = VerifyWindowsArchiveCheckBox.IsChecked == true,
            VerifyWindowsFiles = VerifyWindowsFilesCheckBox.IsChecked == true,
            VerifyDeviceDrivers = VerifyDeviceDriversCheckBox.IsChecked == true,
            OptimizeDrives = OptimizeDrivesCheckBox.IsChecked == true
        };
        if (tools.NumTasks == 0)
        {
            MessageBox.Show(this, UiText.Get("NoTools"), UiText.Get("NoToolsTitle"));
            return;
        }
        bool sessionFailed = false;
        ResetRowsForSession();
        SetRunning(true);
        elapsed.Restart();
        timer.Start();
        SessionStatus.Text = UiText.Get("SessionRunning");
        try
        {
            var taskInfo = new VerifierTask(tools, this).StartVerification();
            runningTask = taskInfo.VerificationTask;
            cancellation = taskInfo.CancelTokenSource;
            await runningTask;
            SessionStatus.Text = UiText.Get(cancellation.IsCancellationRequested ? "SessionCancelled" : "SessionComplete");
        }
        catch (OperationCanceledException) { SessionStatus.Text = UiText.Get("SessionCancelled"); }
        catch (Exception ex)
        {
            sessionFailed = true;
            SessionStatus.Text = UiText.Format("SessionError", ex.Message);
        }
        finally
        {
            timer.Stop();
            elapsed.Stop();
            ElapsedStatus.Text = UiText.Format("Elapsed", elapsed.Elapsed);
            FinishQueuedRows(cancellation?.IsCancellationRequested == true);
            FinishVerificationFeedback(cancellation?.IsCancellationRequested == true, sessionFailed);
            SetRunning(false);
            cancellation?.Dispose();
            cancellation = null;
            runningTask = null;
            if (closeWhenFinished) Close();
        }
    }


    private (CheckBox Selection, Label Status, ProgressBar Progress, TabItem Details, ListView Log, ListView? Repairs)[] ToolRows =>
    [
        (VerifyStabilityCheckBox, VerifyStabilityLabel, VerifyStabilityProgressBar, VerifyStabilityLogHeader, VerifyStabilityScanLogListView, null),
        (VerifyDrivesCheckBox, VerifyDrivesLabel, VerifyDrivesProgressBar, VerifyDrivesLogHeader, VerifyDrivesScanLogListView, VerifyDrivesRepairLogListView),
        (VerifyWindowsArchiveCheckBox, VerifyWindowsArchiveLabel, VerifyWindowsArchiveProgressBar, VerifyWindowsArchiveLogHeader, VerifyWindowsArchiveScanLogListView, VerifyWindowsArchiveRepairLogListView),
        (VerifyWindowsFilesCheckBox, VerifyWindowsFilesLabel, VerifyWindowsFilesProgressBar, VerifyWindowsFilesLogHeader, VerifyWindowsFilesScanLogListView, VerifyWindowsFilesRepairLogListView),
        (VerifyDeviceDriversCheckBox, VerifyDeviceDriversLabel, VerifyDeviceDriversProgressBar, VerifyDeviceDriversLogHeader, VerifyDeviceDriversScanLogListView, null),
        (OptimizeDrivesCheckBox, OptimizeDrivesLabel, OptimizeDrivesProgressBar, OptimizeDrivesLogHeader, OptimizeDrivesScanLogListView, null)
    ];

    internal void ResetRowsForSession()
    {
        foreach (var row in ToolRows)
        {
            row.Status.Content = UiText.Get(row.Selection.IsChecked == true ? "Queued" : "NotSelected");
            row.Progress.Value = 0;
            row.Log.Items.Clear();
            row.Repairs?.Items.Clear();
            var background = PresentationBrush("TileBrush");
            ((Border)row.Selection.Parent).Background = background;
            ((Border)row.Status.Parent).Background = background;
            ((Border)row.Progress.Parent).Background = background;
            row.Details.Background = background;
        }
        ResetVerificationFeedback();
    }
    internal void FinishQueuedRows(bool cancelled)
    {
        foreach (var row in ToolRows)
            if (Equals(row.Status.Content, UiText.Get("Queued")))
                row.Status.Content = UiText.Get(cancelled ? "Cancelled" : "NotRun");
    }

    private void SetRunning(bool running)
    {
        foreach (var element in new UIElement[] { StartTuneUpButton, VerifyStabilityCheckBox, VerifyDrivesCheckBox,
            VerifyWindowsArchiveCheckBox, VerifyWindowsFilesCheckBox, VerifyDeviceDriversCheckBox, OptimizeDrivesCheckBox })
            element.IsEnabled = !running;
        CancelTuneUpButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        CancelTuneUpButton.IsEnabled = running;
        SetPresentationRunning(running);
    }

    private void RequestCancellation()
    {
        cancellation?.Cancel();
        CancelTuneUpButton.IsEnabled = false;
        SessionStatus.Text = UiText.Get("SessionCancelling");
        ShowCancellationFeedback();
    }
    private void CancelTuneUpButton_Click(object sender, RoutedEventArgs e) => RequestCancellation();
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (runningTask is { IsCompleted: false })
        {
            e.Cancel = true;
            closeWhenFinished = true;
            RequestCancellation();
        }
        else
        {
            timer.Stop();
            about?.Close();
        }
    }

    private static ListView? LogFromMenu(object sender) =>
        sender is MenuItem item && item.Parent is ContextMenu menu ? menu.PlacementTarget as ListView : sender as ListView;
    private static string LogText(object item) => item is ListViewItem row ? row.Content?.ToString() ?? "" : item.ToString() ?? "";
    private void LogListView_CopyLine_Click(object sender, RoutedEventArgs e)
    {
        if (LogFromMenu(sender)?.SelectedItem is { } item) GUIUtilities.CopyText(LogText(item));
    }
    private void LogListView_CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (LogFromMenu(sender) is { } list)
            GUIUtilities.CopyText(string.Join(Environment.NewLine, list.Items.Cast<object>().Select(LogText)));
    }
    private void LogListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            LogListView_CopyLine_Click(sender, e);
            e.Handled = true;
        }
    }

    private void HelpHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (about is null)
        {
            about = new AboutWindow { Owner = this };
            about.Closed += (_, _) => about = null;
            about.Show();
        }
        else about.Activate();
    }
}
