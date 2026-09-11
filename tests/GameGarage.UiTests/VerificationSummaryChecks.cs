using System.Globalization;
using GameGarage.Interface;
using GameGarage.Resources;
using static GameGarage.Tools.VerifierBase;

internal static class VerificationSummaryChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: verification summary: " + name);
            checks++;
        }

        var allPassed = VerificationSummary.Create(
            Enum.GetValues<VerificationArea>().Select(area => new VerificationCheckResult(area, VerifierResult.Scanned)).ToArray(),
            6, false, false);
        Check(allPassed.Headline == "Selected checks passed" && !allPassed.HasIssues && allPassed.FocusArea is null,
            "all selected outcomes pass without a whole-system diagnosis");
        Check(allPassed.Detail.Contains("Selected drive maintenance also completed"), "mixed selection acknowledges maintenance separately");

        var onePassed = VerificationSummary.Create([new(VerificationArea.Memory, VerifierResult.Scanned)], 1, false, false);
        Check(onePassed.Headline == "Selected checks passed" && onePassed.Detail.Contains("during this run"),
            "one selected check is explicitly limited to this run");
        var optimization = VerificationSummary.Create([new(VerificationArea.Optimization, VerifierResult.Scanned)], 1, false, false);
        Check(optimization.Headline == "Selected maintenance completed" &&
            optimization.FocusArea == VerificationArea.Optimization && !optimization.HasIssues,
            "optimization alone never becomes a system-health pass");

        var stoppedFinding = VerificationSummary.Create([new(VerificationArea.Memory, VerifierResult.IssuesFound)], 2, true, false);
        Check(stoppedFinding.HasIssues && stoppedFinding.FocusArea == VerificationArea.Memory &&
            stoppedFinding.Headline == "Problems found", "completed finding survives cancellation");
        Check(stoppedFinding.Detail.Contains("did not complete"), "finding summary also acknowledges unfinished checks");

        var completedThenClosed = VerificationSummary.Create([new(VerificationArea.Memory, VerifierResult.IssuesFound)], 1, true, false);
        Check(completedThenClosed.HasIssues && completedThenClosed.Detail.Contains("Verification was stopped") &&
            !completedThenClosed.Detail.Contains("did not complete"),
            "closing after the only completed finding does not invent unfinished checks");

        var unrepaired = VerificationSummary.Create([
            new(VerificationArea.Drives, VerifierResult.Error),
            new(VerificationArea.WindowsFiles, VerifierResult.Not_Repaired)], 2, false, true);
        Check(unrepaired.HasIssues && unrepaired.FocusArea == VerificationArea.WindowsFiles,
            "unrepaired evidence has priority over an execution failure");

        foreach (var area in Enum.GetValues<VerificationArea>())
        {
            var finding = VerificationSummary.Create([new(area, VerifierResult.IssuesFound)], 1, false, false);
            Check(finding.HasIssues && finding.FocusArea == area && !finding.Detail.Contains('['),
                "every finding area has a localized next step and matching detail target: " + area);
        }
        var multiple = VerificationSummary.Create([
            new(VerificationArea.WindowsFiles, VerifierResult.IssuesFound),
            new(VerificationArea.Memory, VerifierResult.IssuesFound)], 2, false, false);
        Check(multiple.FocusArea == VerificationArea.Memory && multiple.Detail.Contains("Other checks also reported problems"),
            "multiple findings have stable focus and do not hide remaining findings");

        var scheduled = VerificationSummary.Create([
            new(VerificationArea.Drives, VerifierResult.Repair_Scheduled_On_Reboot),
            new(VerificationArea.WindowsFiles, VerifierResult.Error)], 2, true, true);
        Check(scheduled.Headline == "Restart required to finish repairs" &&
            scheduled.FocusArea == VerificationArea.Drives && scheduled.Detail.Contains("then run the affected check again"),
            "scheduled restart has priority over incomplete work and requests re-verification");
        Check(scheduled.Detail.Contains("did not complete"), "scheduled result retains incomplete selection context");
        var findingAndSchedule = VerificationSummary.Create([
            new(VerificationArea.Memory, VerifierResult.IssuesFound),
            new(VerificationArea.Drives, VerifierResult.Repair_Scheduled_On_Reboot)], 2, false, false);
        Check(findingAndSchedule.HasIssues && findingAndSchedule.FocusArea == VerificationArea.Memory &&
            findingAndSchedule.Detail.Contains("next restart"), "finding priority does not hide a scheduled restart");

        var repaired = VerificationSummary.Create([
            new(VerificationArea.WindowsFiles, VerifierResult.Repaired),
            new(VerificationArea.Memory, VerifierResult.Scanned)], 2, false, false);
        Check(repaired.Headline == "Repairs completed — verify again" &&
            repaired.FocusArea == VerificationArea.WindowsFiles && !repaired.HasIssues,
            "repaired results request a fresh verification");
        var repairThenStopped = VerificationSummary.Create([
            new(VerificationArea.WindowsFiles, VerifierResult.Repaired)], 2, true, false);
        Check(repairThenStopped.Headline == "Verification incomplete" && repairThenStopped.Detail.Contains("Some repairs completed"),
            "repair cannot mask stopped or unstarted checks");
        var repairThenMissing = VerificationSummary.Create([
            new(VerificationArea.WindowsFiles, VerifierResult.Repaired)], 2, false, false);
        Check(repairThenMissing.Headline == "Verification incomplete", "repair cannot mask missing results without cancellation");

        foreach (var result in new[] { VerifierResult.Error, VerifierResult.Inconclusive,
            VerifierResult.Unsupported, VerifierResult.Cancelled, (VerifierResult)999 })
        {
            var summary = VerificationSummary.Create([new(VerificationArea.Memory, result)], 1, false, false);
            Check(summary.Headline == "Verification incomplete" && !summary.HasIssues &&
                summary.FocusArea == VerificationArea.Memory && !summary.Detail.Contains('['),
                "nonterminal or unknown outcome cannot pass: " + result);
        }
        var errorFocus = VerificationSummary.Create([
            new(VerificationArea.Memory, VerifierResult.Cancelled),
            new(VerificationArea.WindowsImage, VerifierResult.Error)], 2, true, false);
        Check(errorFocus.FocusArea == VerificationArea.WindowsImage, "execution failure is the useful detail target among incomplete outcomes");

        Check(VerificationSummary.Create([new(VerificationArea.Memory, VerifierResult.Scanned)], 1, false, true)
            .Headline == "Verification incomplete", "session failure cannot become a pass");
        Check(VerificationSummary.Create([new(VerificationArea.Memory, VerifierResult.Scanned)], 1, true, false)
            .Headline == "Verification incomplete", "cancelled session cannot become a pass");
        Check(VerificationSummary.Create([], 0, false, false).Headline == "No checks run", "empty selection is explicit");
        Check(VerificationSummary.Create([], 2, false, false).Headline == "Verification incomplete", "unstarted selection is incomplete");
        Check(VerificationSummary.Create([], -1, false, false).Headline == "Verification incomplete", "invalid requested count cannot pass");
        Check(VerificationSummary.Create([
            new(VerificationArea.Memory, VerifierResult.Scanned),
            new(VerificationArea.Memory, VerifierResult.Scanned)], 2, false, false)
            .Headline == "Verification incomplete", "duplicate outcomes cannot substitute for another selected area");
        Check(VerificationSummary.Create([new((VerificationArea)999, VerifierResult.Scanned)], 1, false, false)
            .Headline == "Verification incomplete", "unknown area cannot establish selected coverage");
        var unknownFinding = VerificationSummary.Create([new((VerificationArea)999, VerifierResult.IssuesFound)], 1, false, false);
        Check(unknownFinding.HasIssues && unknownFinding.FocusArea is null && !unknownFinding.Detail.Contains('['),
            "unknown-area finding retains evidence without an invalid navigation target");

        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(VerificationSummary.Create([new(VerificationArea.Memory, VerifierResult.Scanned)], 1, false, false)
                .Headline == "Selected checks passed", "summary resources fall back to English");
        }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
        return checks;
    }
}
