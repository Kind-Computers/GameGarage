namespace GameGarage.Core;

/// <summary>Results describe completed evidence, not the absence of parsed error text.</summary>
public enum DiagnosticStatus
{
    Passed, IssuesFound, Error, Cancelled, Inconclusive, Unsupported, Repaired, RepairScheduled
}

public sealed record DiagnosticResult(DiagnosticStatus Status, string? Message = null, int? ExitCode = null);
public sealed record DiagnosticProgress(string CheckId, double Percentage);

/// <summary>Platform-independent contract; hosts run blocking checks on a background task.</summary>
public interface IDiagnosticCheck
{
    string Id { get; }
    event EventHandler<DiagnosticProgress>? ProgressChanged;
    DiagnosticResult Run();
}

/// <summary>The host presents messages and obtains explicit repair consent.</summary>
public interface IUserInteraction
{
    bool Confirm(string title, string message);
    void Notify(string title, string message, DiagnosticStatus status);
}
