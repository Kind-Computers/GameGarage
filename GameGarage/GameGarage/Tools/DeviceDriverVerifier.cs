namespace GameGarage.Tools;

public sealed class DeviceDriverVerifier : VerifierBase
{
    public DeviceDriverVerifier(AppendToLogAction log, EventHandler<double> progress, CancellationToken cancel,
        IWindowsUserInteraction? userInteraction = null)
        : base("DriverQuery.exe", true, log, progress, cancel, userInteraction: userInteraction) { }

    public override VerifierResult RunVerifier()
    {
        var (result, drivers) = WindowsOutputParsers.Drivers(RunCapturedProcess(["/SI", "/FO", "LIST"], "", false));
        if (drivers.Count > 0 && !ToolWrapper.CancelToken.IsCancellationRequested)
            UserInteraction.ShowDrivers(drivers);
        return Finish(result);
    }
}

public sealed class DeviceDriverInfo
{
    public string DeviceName { get; set; } = "";
    public string InfName { get; set; } = "";
    public bool? IsSigned { get; set; }
    public string Manufacturer { get; set; } = "";
    // Some real records omit display names/manufacturers; INF and a parsed signature flag are required.
    public bool IsValid() => !string.IsNullOrWhiteSpace(InfName) && IsSigned.HasValue;
    public override string ToString() => DiagnosticsText.Format("DriverDetails", DeviceName, InfName, IsSigned, Manufacturer);
}
