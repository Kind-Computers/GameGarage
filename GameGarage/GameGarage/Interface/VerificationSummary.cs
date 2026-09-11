using GameGarage.Resources;
using GameGarage.Tools;
using static GameGarage.Tools.VerifierBase;

namespace GameGarage.Interface;

public enum VerificationArea { Memory, Drives, WindowsImage, WindowsFiles, Drivers, Optimization }

public sealed record VerificationCheckResult(VerificationArea Area, VerifierResult Result);

/// <summary>A frontend interpretation of recorded outcomes, limited to the selected checks.</summary>
public sealed record VerificationSummary(string Headline, string Detail, VerificationArea? FocusArea, bool HasIssues)
{
    public static VerificationSummary Create(IReadOnlyList<VerificationCheckResult> results,
        int requestedCount, bool cancelled, bool sessionFailed)
    {
        ArgumentNullException.ThrowIfNull(results);
        bool completeSelection = requestedCount > 0 && results.Count == requestedCount &&
            results.All(result => Enum.IsDefined(result.Area)) &&
            results.Select(result => result.Area).Distinct().Count() == requestedCount;
        var ordered = results.OrderBy(result => result.Area).ToList();
        var incompleteResult = ordered
            .Where(result => result.Result is not (VerifierResult.Scanned or VerifierResult.Repaired or
                VerifierResult.IssuesFound or VerifierResult.Not_Repaired or VerifierResult.Repair_Scheduled_On_Reboot))
            .OrderBy(result => IncompletePriority(result.Result)).FirstOrDefault();
        bool incomplete = cancelled || sessionFailed || !completeSelection || incompleteResult is not null;
        string incompleteContextKey = !completeSelection || incompleteResult is not null ? "UnfinishedDetail" :
            sessionFailed ? "SessionFailedDetail" : "CancelledSessionDetail";

        var findings = ordered.Where(result => result.Result is VerifierResult.IssuesFound or VerifierResult.Not_Repaired).ToList();
        if (findings.Count > 0)
        {
            var finding = findings[0];
            string detail = VerificationText.Get(IssueDetailKey(finding.Area));
            if (findings.Count > 1) detail = Append(detail, "AdditionalFindings");
            if (ordered.Any(result => result.Result == VerifierResult.Repair_Scheduled_On_Reboot))
                detail = Append(detail, "ScheduledDetail");
            if (incomplete) detail = Append(detail, incompleteContextKey);
            return new(VerificationText.Get("IssuesHeadline"), detail, ValidArea(finding), true);
        }

        var scheduled = ordered.FirstOrDefault(result => result.Result == VerifierResult.Repair_Scheduled_On_Reboot);
        if (scheduled is not null)
        {
            string detail = VerificationText.Get("ScheduledDetail");
            if (incomplete) detail = Append(detail, incompleteContextKey);
            return new(VerificationText.Get("ScheduledHeadline"), detail, ValidArea(scheduled), false);
        }

        if (incomplete)
        {
            if (requestedCount == 0 && results.Count == 0 && !cancelled && !sessionFailed)
                return new(VerificationText.Get("NoChecksHeadline"), VerificationText.Get("NoChecksDetail"), null, false);

            string detail;
            if (incompleteResult is not null)
            {
                string key = incompleteResult.Result switch
                {
                    VerifierResult.Error => "ErrorDetail",
                    VerifierResult.Unsupported => "UnsupportedDetail",
                    VerifierResult.Cancelled => "CancelledDetail",
                    _ => "InconclusiveDetail"
                };
                detail = VerificationText.Format(key, VerificationText.Get(AreaKey(incompleteResult.Area)));
            }
            else detail = VerificationText.Get(sessionFailed ? "SessionFailedDetail" :
                cancelled ? "CancelledSessionDetail" : "MissingResultsDetail");

            if (ordered.Any(result => result.Result == VerifierResult.Repaired))
                detail = Append(detail, "SomeRepairsDetail");
            return new(VerificationText.Get("IncompleteHeadline"), detail,
                incompleteResult is null ? null : ValidArea(incompleteResult), false);
        }

        var repaired = ordered.FirstOrDefault(result => result.Result == VerifierResult.Repaired);
        if (repaired is not null)
            return new(VerificationText.Get("RepairedHeadline"), VerificationText.Get("RepairedDetail"), ValidArea(repaired), false);

        // An optimization result describes completed maintenance, not a health finding.
        if (ordered.All(result => result.Area == VerificationArea.Optimization))
            return new(VerificationText.Get("MaintenanceHeadline"), VerificationText.Get("MaintenanceDetail"),
                VerificationArea.Optimization, false);

        return new(VerificationText.Get("PassedHeadline"),
            VerificationText.Get(ordered.Any(result => result.Area == VerificationArea.Optimization)
                ? "PassedWithMaintenanceDetail" : "PassedDetail"), null, false);
    }

    private static VerificationArea? ValidArea(VerificationCheckResult result) =>
        Enum.IsDefined(result.Area) ? result.Area : null;

    private static int IncompletePriority(VerifierResult result) => result switch
    {
        VerifierResult.Error => 0,
        VerifierResult.Inconclusive => 1,
        VerifierResult.Unsupported => 2,
        VerifierResult.Cancelled => 3,
        _ => 0
    };

    private static string Append(string detail, string resourceKey) =>
        VerificationText.Format("CombinedDetail", detail, VerificationText.Get(resourceKey));

    private static string AreaKey(VerificationArea area) => area switch
    {
        VerificationArea.Memory => "MemoryArea",
        VerificationArea.Drives => "DrivesArea",
        VerificationArea.WindowsImage => "WindowsImageArea",
        VerificationArea.WindowsFiles => "WindowsFilesArea",
        VerificationArea.Drivers => "DriversArea",
        VerificationArea.Optimization => "OptimizationArea",
        _ => "UnknownArea"
    };

    private static string IssueDetailKey(VerificationArea area) => area switch
    {
        VerificationArea.Memory => "MemoryIssuesDetail",
        VerificationArea.Drives => "DrivesIssuesDetail",
        VerificationArea.WindowsImage => "WindowsImageIssuesDetail",
        VerificationArea.WindowsFiles => "WindowsFilesIssuesDetail",
        VerificationArea.Drivers => "DriversIssuesDetail",
        VerificationArea.Optimization => "OptimizationIssuesDetail",
        _ => "UnknownIssuesDetail"
    };
}
