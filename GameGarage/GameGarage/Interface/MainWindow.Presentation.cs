using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using GameGarage.Resources;
using GameGarage.Tools;

namespace GameGarage.Interface;

public partial class MainWindow
{
    private readonly Dictionary<VerificationArea, VerificationCheckResult> finishedChecks = [];
    private readonly Dictionary<string, object> defaultPalette = [];
    private VerificationEffects? verificationEffects;
    private VerificationArea? activeArea;
    private VerificationArea? summaryFocus;
    private int requestedChecks;

    internal bool IsVerificationEffectActive => verificationEffects?.IsActive == true;

    private void InitializePresentation()
    {
        foreach (string key in new[] { "CanvasBrush", "PanelBrush", "TileBrush", "BorderBrush", "TextBrush",
            "MutedBrush", "AccentBrush", "ButtonInkBrush", "ActiveBrush", "GoodBrush", "ReviewBrush",
            "IssueBrush", "TrackBrush", "VerifyButtonBrush" })
            defaultPalette[key] = Resources[key];
        verificationEffects = new(VerificationEffectLayer, StartTuneUpButton);
        ApplyContrastPalette();
        AnnounceGuide(announce: false);
        SystemParameters.StaticPropertyChanged += PresentationSettingsChanged;
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) verificationEffects.Stop(); };
        Closed += (_, _) =>
        {
            SystemParameters.StaticPropertyChanged -= PresentationSettingsChanged;
            verificationEffects.Dispose();
        };
    }

    private void PresentationSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SystemParameters.HighContrast) or nameof(SystemParameters.ClientAreaAnimation))) return;
        void Update()
        {
            if (Dispatcher.HasShutdownStarted) return;
            ApplyContrastPalette();
            if (!SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) verificationEffects?.Stop();
        }
        if (Dispatcher.CheckAccess()) Update();
        else if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke((Action)Update);
    }

    private void ApplyContrastPalette()
    {
        foreach (var (key, value) in defaultPalette) Resources[key] = value;
        if (SystemParameters.HighContrast)
        {
            foreach (string key in new[] { "CanvasBrush", "PanelBrush", "TileBrush", "ActiveBrush",
                "GoodBrush", "ReviewBrush", "IssueBrush", "TrackBrush" }) Resources[key] = SystemColors.WindowBrush;
            Resources["TextBrush"] = SystemColors.WindowTextBrush;
            Resources["MutedBrush"] = SystemColors.WindowTextBrush;
            Resources["BorderBrush"] = SystemColors.WindowTextBrush;
            Resources["AccentBrush"] = SystemColors.HighlightBrush;
            Resources["VerifyButtonBrush"] = SystemColors.HighlightBrush;
            Resources["ButtonInkBrush"] = SystemColors.HighlightTextBrush;
        }
        foreach (var row in ToolRows)
        {
            var area = AreaFor(row.Selection);
            var brush = activeArea == area ? PresentationBrush("ActiveBrush")
                : finishedChecks.TryGetValue(area, out var outcome) ? ResultBrush(outcome.Result) : PresentationBrush("TileBrush");
            ((Border)row.Selection.Parent).Background = brush;
            ((Border)row.Status.Parent).Background = brush;
            ((Border)row.Progress.Parent).Background = brush;
            row.Details.Background = brush;
        }
    }

    internal Brush PresentationBrush(string key) => (Brush)Resources[key];
    internal Brush ResultBrush(VerifierBase.VerifierResult result) => PresentationBrush(result switch
    {
        VerifierBase.VerifierResult.Scanned or VerifierBase.VerifierResult.Repaired => "GoodBrush",
        VerifierBase.VerifierResult.IssuesFound or VerifierBase.VerifierResult.Error => "IssueBrush",
        VerifierBase.VerifierResult.Cancelled => "TileBrush",
        _ => "ReviewBrush"
    });

    internal VerificationArea AreaFor(CheckBox selection)
    {
        if (selection == VerifyStabilityCheckBox) return VerificationArea.Memory;
        if (selection == VerifyDrivesCheckBox) return VerificationArea.Drives;
        if (selection == VerifyWindowsArchiveCheckBox) return VerificationArea.WindowsImage;
        if (selection == VerifyWindowsFilesCheckBox) return VerificationArea.WindowsFiles;
        if (selection == VerifyDeviceDriversCheckBox) return VerificationArea.Drivers;
        if (selection == OptimizeDrivesCheckBox) return VerificationArea.Optimization;
        throw new ArgumentException("Unknown verification selection.", nameof(selection));
    }

    private void ResetVerificationFeedback()
    {
        finishedChecks.Clear();
        activeArea = null;
        summaryFocus = null;
        requestedChecks = ToolRows.Count(row => row.Selection.IsChecked == true);
        GuideDetailsButton.Visibility = Visibility.Collapsed;
        SessionProgress.Value = 0;
        SessionCount.Text = UiText.Format("SessionCountFormat", 0, requestedChecks);
        GuideEyebrow.Text = UiText.Get("GuideReadyBadge");
        GuideHeadline.Text = UiText.Get("GuideReadyTitle");
        GuideDetail.Text = UiText.Get("GuideReadyDetail");
        AnnounceGuide(announce: false);
    }

    private void SetPresentationRunning(bool running)
    {
        StartTuneUpButton.Content = UiText.Get(running ? "VerifyingAction" : "RunSelected");
        if (running)
        {
            GuideEyebrow.Text = UiText.Get("GuideRunningBadge");
            GuideHeadline.Text = UiText.Get("GuideStartingTitle");
            GuideDetail.Text = UiText.Get("GuideStartingDetail");
            AnnounceGuide();
            PlayVerificationEffect(SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast
                && WindowState != WindowState.Minimized);
        }
        else verificationEffects?.Stop();
    }

    internal void PlayVerificationEffect(bool animationsAllowed) => verificationEffects?.Play(animationsAllowed);
    internal void SeekVerificationEffect(TimeSpan position) => verificationEffects?.Seek(position);
    internal void StopVerificationEffects() => verificationEffects?.Stop();

    internal void OnCheckStarted(CheckBox selection)
    {
        activeArea = AreaFor(selection);
        GuideEyebrow.Text = UiText.Format("GuideStepFormat", finishedChecks.Count + 1, requestedChecks);
        GuideHeadline.Text = UiText.Get("Stage" + activeArea + "Title");
        GuideDetail.Text = UiText.Get("Stage" + activeArea + "Detail");
        summaryFocus = activeArea;
        GuideDetailsButton.Visibility = Visibility.Visible;
        OnCheckProgress(selection, 0);
        AnnounceGuide();
    }

    internal void OnCheckRepairing(CheckBox selection)
    {
        if (activeArea != AreaFor(selection) || cancellation?.IsCancellationRequested == true) return;
        GuideHeadline.Text = UiText.Get("GuideRepairTitle");
        GuideDetail.Text = UiText.Get("GuideRepairDetail");
        AnnounceGuide();
    }
    internal void OnCheckProgress(CheckBox selection, double percent)
    {
        if (activeArea != AreaFor(selection) || requestedChecks == 0) return;
        SessionProgress.Value = Math.Clamp(100.0 * (finishedChecks.Count + Math.Clamp(percent, 0, 100) / 100) / requestedChecks, 0, 100);
    }

    internal void OnCheckFinished(CheckBox selection, VerifierBase.VerifierResult result)
    {
        var area = AreaFor(selection);
        finishedChecks[area] = new(area, result);
        activeArea = null;
        SessionCount.Text = UiText.Format("SessionCountFormat", finishedChecks.Count, requestedChecks);
        SessionProgress.Value = requestedChecks == 0 ? 0 : Math.Clamp(100.0 * finishedChecks.Count / requestedChecks, 0, 100);
    }

    internal void FinishVerificationFeedback(bool cancelled, bool sessionFailed)
    {
        activeArea = null;
        var summary = VerificationSummary.Create(finishedChecks.Values.ToArray(), requestedChecks, cancelled, sessionFailed);
        GuideEyebrow.Text = UiText.Get(summary.HasIssues ? "GuideAttentionBadge" : "GuideResultsBadge");
        GuideHeadline.Text = summary.Headline;
        GuideDetail.Text = summary.Detail;
        summaryFocus = summary.FocusArea;
        GuideDetailsButton.Visibility = summaryFocus.HasValue ? Visibility.Visible : Visibility.Collapsed;
        AnnounceGuide();
    }

    private void ShowCancellationFeedback()
    {
        verificationEffects?.Stop();
        GuideEyebrow.Text = UiText.Get("GuideStoppingBadge");
        GuideHeadline.Text = UiText.Get("GuideStoppingTitle");
        GuideDetail.Text = UiText.Get("GuideStoppingDetail");
        AnnounceGuide();
    }

    private void AnnounceGuide(bool announce = true)
    {
        // One polite live region carries the headline and the useful next step.
        // Progress changes do not call this method or generate spoken updates.
        string text = string.Join(" ", GuideHeadline.Text, GuideDetail.Text);
        bool changed = AutomationProperties.GetName(GuideHeadline) != text;
        AutomationProperties.SetName(GuideHeadline, text);
        if (!announce || !changed || !IsLoaded ||
            !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;
        var peer = UIElementAutomationPeer.FromElement(GuideHeadline)
            ?? UIElementAutomationPeer.CreatePeerForElement(GuideHeadline);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void GuideDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (summaryFocus is not { } area) return;
        var row = ToolRows.First(item => AreaFor(item.Selection) == area);
        // Preserve the selected scan/repair page and focus the log that it displays.
        var log = row.Details.Content is TabControl { SelectedItem: TabItem { Content: ListView selectedLog } }
            ? selectedLog : row.Log;
        DashboardTabs.SelectedItem = row.Details;
        DashboardTabs.UpdateLayout();
        log.Focus();
    }
}
