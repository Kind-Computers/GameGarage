
// Copyright 2024 by Fredric Echols and Kind Computers.
// All rights reserved.

using GameGarage.Resources;
using GameGarage.Tools;
using GameGarage.Utilities;
using System.Windows;
using System.Windows.Controls;

namespace GameGarage.Interface
{
    struct VerifierTaskList
    {
        public bool VerifyStability;
        public bool VerifyDrives;
        public bool VerifyWindowsArchive;
        public bool VerifyWindowsFiles;
        public bool VerifyDeviceDrivers;
        public bool OptimizeDrives;

        public int NumTasks
        {
            get
            {
                return (VerifyStability ? 1 : 0) +
                       (VerifyDrives ? 1 : 0) +
                       (VerifyWindowsArchive ? 1 : 0) +
                       (VerifyWindowsFiles ? 1 : 0) +
                       (VerifyDeviceDrivers ? 1 : 0) +
                       (OptimizeDrives ? 1 : 0);
            }
        }
    }

    internal class VerifierTask
    {
        public VerifierTask(VerifierTaskList TaskList, MainWindow Window)
        {
            this.TaskList = TaskList;
            this.AppWindow = Window;
        }

        public (Task VerificationTask, CancellationTokenSource CancelTokenSource) StartVerification()
        {
            var cancellation = new CancellationTokenSource();
            return (Task.Run(() => StartVerificationInternal(cancellation.Token)), cancellation);
        }
        private void StartVerificationInternal(CancellationToken CancelToken)
        {
            using (ScopedTaskProgress TaskbarProgress = new(AppWindow.TaskbarProgressBar, TaskList.NumTasks))
            {
                // Stability verification
                if ((CancelToken.IsCancellationRequested == false) &&
                    (TaskList.VerifyStability == true))
                {
                    RunTaskInfo StabilityVerifierTaskInfo = new(
                        // Main UI elements
                        AppWindow.VerifyStabilityLabel,
                        AppWindow.VerifyStabilityCheckBox,
                        AppWindow.VerifyStabilityProgressBar,
                        // Log tab
                        AppWindow.VerifyStabilityLogHeader,
                        // Scan log
                        AppWindow.VerifyStabilityScanLogHeader,
                        AppWindow.VerifyStabilityScanLogListView,
                        // Repair log
                        null,
                        null);

                    using (new ScopedElementVisibility([AppWindow.StabilityTestNotification], null))
                    {
#pragma warning disable 420
                        RunTask(StabilityVerifierTaskInfo, ref StabilityVerification, TaskbarProgress, CancelToken);
#pragma warning restore 420
                    }
                }

                // Drive verification
                if ((CancelToken.IsCancellationRequested == false) &&
                    (TaskList.VerifyDrives == true))
                {
                    RunTaskInfo DriveVerifierTaskInfo = new(
                        // Main UI elements
                        AppWindow.VerifyDrivesLabel,
                        AppWindow.VerifyDrivesCheckBox,
                        AppWindow.VerifyDrivesProgressBar,
                        // Log tab
                        AppWindow.VerifyDrivesLogHeader,
                        // Scan log
                        AppWindow.VerifyDrivesScanLogHeader,
                        AppWindow.VerifyDrivesScanLogListView,
                        // Repair log
                        AppWindow.VerifyDrivesRepairLogHeader,
                        AppWindow.VerifyDrivesRepairLogListView);

#pragma warning disable 420
                    RunTask(DriveVerifierTaskInfo, ref DriveVerification, TaskbarProgress, CancelToken);
#pragma warning restore 420
                }

                // Windows archive verification
                if ((CancelToken.IsCancellationRequested == false) &&
                    (TaskList.VerifyWindowsArchive == true))
                {
                    RunTaskInfo WindowsImageVerifierTaskInfo = new(
                        // Main UI elements
                        AppWindow.VerifyWindowsArchiveLabel,
                        AppWindow.VerifyWindowsArchiveCheckBox,
                        AppWindow.VerifyWindowsArchiveProgressBar,
                        // Log tab
                        AppWindow.VerifyWindowsArchiveLogHeader,
                        // Scan log
                        AppWindow.VerifyWindowsArchiveScanLogHeader,
                        AppWindow.VerifyWindowsArchiveScanLogListView,
                        // Repair log
                        AppWindow.VerifyWindowsArchiveRepairLogHeader,
                        AppWindow.VerifyWindowsArchiveRepairLogListView);

#pragma warning disable 420
                    RunTask(WindowsImageVerifierTaskInfo, ref WindowsImageVerification, TaskbarProgress, CancelToken);
#pragma warning restore 420
                }

                // Windows files verification
                if ((CancelToken.IsCancellationRequested == false) &&
                    (TaskList.VerifyWindowsFiles == true))
                {
                    RunTaskInfo WindowsFilesVerifierTaskInfo = new(
                        // Main UI elements
                        AppWindow.VerifyWindowsFilesLabel,
                        AppWindow.VerifyWindowsFilesCheckBox,
                        AppWindow.VerifyWindowsFilesProgressBar,
                        // Log tab
                        AppWindow.VerifyWindowsFilesLogHeader,
                        // Scan log
                        AppWindow.VerifyWindowsFilesScanLogHeader,
                        AppWindow.VerifyWindowsFilesScanLogListView,
                        // Repair log
                        AppWindow.VerifyWindowsFilesRepairLogHeader,
                        AppWindow.VerifyWindowsFilesRepairLogListView);

#pragma warning disable 420
                    RunTask(WindowsFilesVerifierTaskInfo, ref WindowsFilesVerification, TaskbarProgress, CancelToken);
#pragma warning restore 420
                }

                // Device driver verification
                if ((CancelToken.IsCancellationRequested == false) &&
                    (TaskList.VerifyDeviceDrivers == true))
                {
                    RunTaskInfo DeviceDriverVerifierTaskInfo = new(
                       // Main UI elements
                       AppWindow.VerifyDeviceDriversLabel,
                       AppWindow.VerifyDeviceDriversCheckBox,
                       AppWindow.VerifyDeviceDriversProgressBar,
                       // Log tab
                       AppWindow.VerifyDeviceDriversLogHeader,
                       // Scan log
                       AppWindow.VerifyDeviceDriversScanLogHeader,
                       AppWindow.VerifyDeviceDriversScanLogListView,
                       // Repair log
                       null,
                       null);

#pragma warning disable 420
                    RunTask(DeviceDriverVerifierTaskInfo, ref DeviceDriverVerification, TaskbarProgress, CancelToken);
#pragma warning restore 420
                }

                // Drive optimization
                if ((CancelToken.IsCancellationRequested == false) &&
                    (TaskList.OptimizeDrives == true))
                {
                    RunTaskInfo OptimizeDrivesTaskInfo = new(
                        // Main UI elements
                        AppWindow.OptimizeDrivesLabel,
                        AppWindow.OptimizeDrivesCheckBox,
                        AppWindow.OptimizeDrivesProgressBar,
                        // Log tab
                        AppWindow.OptimizeDrivesLogHeader,
                        // Scan log
                        AppWindow.OptimizeDrivesScanLogHeader,
                        AppWindow.OptimizeDrivesScanLogListView,
                        // Repair log
                        null,
                        null);

#pragma warning disable 420
                    RunTask(OptimizeDrivesTaskInfo, ref DriveOptimization, TaskbarProgress, CancelToken);
#pragma warning restore 420
                }
            }
        }

        class RunTaskInfo(
            // Main UI elements
            Label StatusLabel,
            CheckBox CheckBox,
            ProgressBar ProgressBar,
            // Log tab
            TabItem LogHeader,
            // Scan log
            TabItem ScanLogHeader,
            ListView ScanLogListView,
            // Repair log
            TabItem? RepairLogHeader,
            ListView? RepairLogListView)
        {
            // Main UI elements
            public Label StatusLabel = StatusLabel;
            public CheckBox CheckBox = CheckBox;
            public ProgressBar ProgressBar = ProgressBar;

            // Log tab
            public TabItem LogHeader = LogHeader;

            // Scan log
            public TabItem ScanLogHeader = ScanLogHeader;
            public ListView ScanLogListView = ScanLogListView;

            // Repair log
            public TabItem? RepairLogHeader = RepairLogHeader;
            public ListView? RepairLogListView = RepairLogListView;
        }

        private static void RunTask<T>(RunTaskInfo TaskInfo,
                                ref T? ToolInstance,
                                ScopedTaskProgress TaskbarProgress,
                                CancellationToken CancelToken) where T : VerifierBase
        {
            VerifierBase.VerifierResult VerifyResult = VerifierBase.VerifierResult.Error;
            int toolActive = 1;

            try
            {
                // Reset status and clear logs
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // TODO: Use data binding instead!!!
                    TaskInfo.StatusLabel.Content = UiText.Get("Running");
                    TaskInfo.ScanLogListView.Items.Clear();
                    TaskInfo.RepairLogListView?.Items.Clear();
                    TaskInfo.ScanLogHeader.IsSelected = true;
                    TaskInfo.ProgressBar.Value = 0;

                    // TODO: Use styles instead!!!
                    (TaskInfo.CheckBox.Parent as Border)!.Background = BlueBrush;
                    (TaskInfo.StatusLabel.Parent as Border)!.Background = BlueBrush;
                    (TaskInfo.ProgressBar.Parent as Border)!.Background = BlueBrush;
                    TaskInfo.LogHeader.Background = BlueBrush;
                });

                bool HasStartedRepair = false;

                // Delegate to update the UI log
                AppendToLogAction AppendToLogDelegate = (String Line, bool IsNewLine, bool LastLineWasNewLine, bool Highlight, bool IsRepair) =>
                    {
                        // Invoke the control update on the main thread
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            // Update the log
                            GUIUtilities.UpdateLogListView(IsRepair
                                ? (TaskInfo.RepairLogListView ?? TaskInfo.ScanLogListView)
                                : TaskInfo.ScanLogListView
                                , Line, IsNewLine, LastLineWasNewLine, Highlight);

                            // Update UI when switching to repair mode
                            if ((IsRepair == true) && (HasStartedRepair == false))
                            {
                                HasStartedRepair = true;

                                // Switch log tab to repair log
                                if (TaskInfo.RepairLogHeader != null)
                                {
                                    TaskInfo.RepairLogHeader.IsSelected = true;
                                }

                                // Set status
                                TaskInfo.StatusLabel.Content = UiText.Get("Repairing");
                            }

                            // Set current tab header to bold
                            TaskInfo.ScanLogHeader.FontWeight = (IsRepair ? FontWeights.Regular : FontWeights.Bold);
                            if (TaskInfo.RepairLogHeader != null)
                            {
                                TaskInfo.RepairLogHeader.FontWeight = (IsRepair ? FontWeights.Bold : FontWeights.Regular);
                            }
                        });
                    };

                // Delegate to update the UI progress bar
                EventHandler<double> UpdateProgressDelegate = (object? Sender, double ProgressPercent) =>
                    {
                        // Invoke the control update on the main thread
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            if (Volatile.Read(ref toolActive) == 0) return;
                            TaskInfo.ProgressBar.Value = Math.Clamp(ProgressPercent, 0, 100);
                            TaskbarProgress.SetTaskProgress(ProgressPercent);
                        });
                    };

                // Initialize the tool
                Interlocked.Exchange(ref ToolInstance, (T)Activator.CreateInstance(typeof(T),
                    AppendToLogDelegate, UpdateProgressDelegate, CancelToken, new WpfUserInteraction())!);

                // Highlight the current log tab while running
                using (new ScopedElementBold([
                        TaskInfo.LogHeader,
                        TaskInfo.CheckBox,
                        TaskInfo.StatusLabel], null))
                {
                    // Start the tool
                    ToolInstance.Run();
                    VerifyResult = ToolInstance.LastVerifierResult;
                }
            }
            finally
            {
                Interlocked.Exchange(ref toolActive, 0);
                // End of verification phase
                Interlocked.Exchange(ref ToolInstance, null);

                // Set UI status
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (VerifyResult is VerifierBase.VerifierResult.Scanned or VerifierBase.VerifierResult.Repaired) TaskInfo.ProgressBar.Value = 100;
                    TaskInfo.StatusLabel.Content = UiText.Get(VerifyResult switch { VerifierBase.VerifierResult.Scanned => "Passed", VerifierBase.VerifierResult.Repair_Scheduled_On_Reboot => "Scheduled", VerifierBase.VerifierResult.Not_Repaired => "NotRepaired", _ => VerifyResult.ToString() });

                    // TODO: Use styles instead!!!
                    var ResultColor = GetColorForResult(VerifyResult);
                    (TaskInfo.CheckBox.Parent as Border)!.Background = ResultColor;
                    (TaskInfo.StatusLabel.Parent as Border)!.Background = ResultColor;
                    (TaskInfo.ProgressBar.Parent as Border)!.Background = ResultColor;
                    TaskInfo.LogHeader.Background = ResultColor;

                    // Update the total progress in the taskbar
                    TaskbarProgress.TaskCompleted();
                });
            }
        }

        // TODO: Use styles instead!!!
        private static System.Windows.Media.SolidColorBrush GetColorForResult(VerifierBase.VerifierResult Result)
        {
            var ColorBrush = (Result switch
                {
                    VerifierBase.VerifierResult.Scanned => GreenBrush,
                    VerifierBase.VerifierResult.Cancelled => BlueBrush,
                    VerifierBase.VerifierResult.Repaired => GreenBrush,
                    VerifierBase.VerifierResult.Repair_Scheduled_On_Reboot => YellowBrush,
                    VerifierBase.VerifierResult.Not_Repaired => YellowBrush,
                    VerifierBase.VerifierResult.Error => RedBrush, VerifierBase.VerifierResult.IssuesFound => RedBrush, _ => YellowBrush
                });

            return ColorBrush;
        }

        // Verifier instances (volatile assures aquire-release semantics)
        public volatile StabilityVerifier? StabilityVerification;
        public volatile DriveVerifier? DriveVerification;
        public volatile WindowsImageVerifier? WindowsImageVerification;
        public volatile WindowsFilesVerifier? WindowsFilesVerification;
        public volatile DeviceDriverVerifier? DeviceDriverVerification;
        public volatile DriveOptimizer? DriveOptimization;

        private readonly MainWindow AppWindow;
        private VerifierTaskList TaskList;

        // TODO: Use styles instead!!!
        // Colors
        static readonly System.Windows.Media.SolidColorBrush GreenBrush = FrozenBrush(0xDE, 0xFF, 0xDC);
        static readonly System.Windows.Media.SolidColorBrush BlueBrush = FrozenBrush(0xD6, 0xE2, 0xFF);
        static readonly System.Windows.Media.SolidColorBrush YellowBrush = FrozenBrush(0xFF, 0xF4, 0xCE);
        static readonly System.Windows.Media.SolidColorBrush RedBrush = FrozenBrush(0xFF, 0xDD, 0xDD);
        private static System.Windows.Media.SolidColorBrush FrozenBrush(byte r, byte g, byte b) { var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r,g,b)); brush.Freeze(); return brush; }
    }
}
