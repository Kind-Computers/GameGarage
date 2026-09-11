using GameGarage.Core;
using GameGarage.Tools;
using GameGarage.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using static GameGarage.Tools.VerifierBase;

if (args.FirstOrDefault() == "--child") return RunChild(args.Skip(1).ToArray());

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException("FAILED: " + description);
}
ToolRunResult Output(int exitCode, params string[] lines) => new(exitCode, lines, []);
void Expect(VerifierResult expected, VerifierResult actual, string description) => Check(actual == expected, description + ": " + actual);

// Genuine terminal messages, ambiguous output, and exit failures are intentionally separate fixtures.
Expect(VerifierResult.Scanned, WindowsOutputParsers.Sfc(Output(0, "Windows Resource Protection did not find any integrity violations."), false), "SFC clean");
Expect(VerifierResult.IssuesFound, WindowsOutputParsers.Sfc(Output(0, "Windows Resource Protection found integrity violations."), false), "SFC findings despite exit zero");
Expect(VerifierResult.Repaired, WindowsOutputParsers.Sfc(Output(0, "Windows Resource Protection found corrupt files and successfully repaired them."), true), "SFC repair");
Expect(VerifierResult.Not_Repaired, WindowsOutputParsers.Sfc(Output(0, "Windows Resource Protection found corrupt files but was unable to fix some of them."), true), "SFC unresolved repair");
Expect(VerifierResult.Error, WindowsOutputParsers.Sfc(Output(0, "Windows Resource Protection could not perform the requested operation."), false), "SFC failed operation");
Expect(VerifierResult.Error, WindowsOutputParsers.Sfc(Output(5, "Windows Resource Protection did not find any integrity violations."), false), "SFC exit failure overrides text");
Expect(VerifierResult.Scanned, WindowsOutputParsers.Dism(Output(0, "No component store corruption detected.", "The operation completed successfully."), false), "DISM clean");
Expect(VerifierResult.IssuesFound, WindowsOutputParsers.Dism(Output(0, "The component store is repairable."), false), "DISM repairable");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Dism(Output(0, "The operation completed successfully."), false), "DISM generic completion is not clean evidence");
Expect(VerifierResult.Repair_Scheduled_On_Reboot, WindowsOutputParsers.Dism(Output(3010, "The restore operation completed successfully."), true), "DISM reboot");
Expect(VerifierResult.Error, WindowsOutputParsers.Dism(Output(unchecked((int)0x800F081F), "The source files could not be found."), true), "DISM missing source");
Expect(VerifierResult.Scanned, WindowsOutputParsers.Chkdsk(Output(0, "Windows has scanned the file system and found no problems."), false), "CHKDSK clean");
Expect(VerifierResult.Unsupported, WindowsOutputParsers.Chkdsk(Output(3, "CHKDSK is not available for RAW drives."), false), "Unsupported filesystem is not a pass");
Expect(VerifierResult.IssuesFound, WindowsOutputParsers.Chkdsk(Output(3, "Errors found. CHKDSK cannot continue in read-only mode."), false), "CHKDSK findings");
Expect(VerifierResult.Error, WindowsOutputParsers.Chkdsk(Output(3, "Cannot open volume for direct access."), false), "CHKDSK tool failure");
Expect(VerifierResult.Repair_Scheduled_On_Reboot, WindowsOutputParsers.Chkdsk(Output(3, "This volume will be checked the next time the system restarts."), true), "CHKDSK scheduled repair");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Chkdsk(Output(2, "File system is NTFS."), false), "CHKDSK cleanup ambiguous");
Expect(VerifierResult.Scanned, WindowsOutputParsers.Defrag(Output(0, "The operation completed successfully.")), "Defrag clean");
Expect(VerifierResult.Error, WindowsOutputParsers.Defrag(Output(5, "The operation completed successfully.")), "Defrag failure");
Expect(VerifierResult.Scanned, WindowsOutputParsers.Stability(Output(0, "Result: Passed")), "Worker completed");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Stability(Output(0, "Result: Passed", "Result: Error")), "Worker nonterminal success ignored");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Stability(Output(0, "All tests passed!")), "Human wording is not worker protocol");
Expect(VerifierResult.IssuesFound, WindowsOutputParsers.Stability(Output(1)), "Worker mismatch");
Expect(VerifierResult.Error, WindowsOutputParsers.Stability(Output(2)), "Worker failure");
Expect(VerifierResult.Cancelled, WindowsOutputParsers.Stability(Output(3)), "Worker cancellation");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Stability(Output(4)), "Worker no coverage");

Func<ToolRunResult, VerifierResult>[] parsers =
[
    x => WindowsOutputParsers.Sfc(x, false), x => WindowsOutputParsers.Dism(x, false),
    x => WindowsOutputParsers.Chkdsk(x, false), WindowsOutputParsers.Defrag,
    WindowsOutputParsers.Stability, x => WindowsOutputParsers.Drivers(x).Result
];
foreach (var parser in parsers)
{
    Expect(VerifierResult.Inconclusive, parser(Output(0)), "Empty output");
    Expect(VerifierResult.Inconclusive, parser(Output(0, "Unrecognized output")), "Unknown output");
    Expect(VerifierResult.Cancelled, parser(new(0, [], [], Cancelled: true)), "Cancellation");
    Expect(VerifierResult.Error, parser(new(-1, [], [], LaunchFailed: true)), "Launch error");
}
string[] signed = ["DeviceName: Test device", "InfName: test.inf", "IsSigned: TRUE", "Manufacturer: Test"];
Expect(VerifierResult.Scanned, WindowsOutputParsers.Drivers(Output(0, signed)).Result, "Signed driver record");
Expect(VerifierResult.IssuesFound, WindowsOutputParsers.Drivers(Output(0, "DeviceName: Test", "InfName: test.inf", "IsSigned: FALSE", "Manufacturer: Test")).Result, "Unsigned driver");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Drivers(Output(0, "DeviceName: Test", "InfName: test.inf", "IsSigned: INVALID", "Manufacturer: Test")).Result, "Malformed boolean does not pass");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Drivers(Output(0, signed.Concat(["", "DeviceName: Truncated"]).ToArray())).Result, "Partial driver record");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Drivers(new(0, signed, ["Access denied"])).Result, "stderr prevents clean driver result");
Expect(VerifierResult.Unsupported, WindowsOutputParsers.Aggregate([]), "No eligible drives");
Expect(VerifierResult.Inconclusive, WindowsOutputParsers.Aggregate([VerifierResult.Scanned, VerifierResult.Unsupported]), "Partial coverage");
Expect(VerifierResult.Error, WindowsOutputParsers.Aggregate([VerifierResult.Error, VerifierResult.Scanned]), "Later pass cannot mask failure");
Expect(VerifierResult.Cancelled, WindowsOutputParsers.Aggregate([VerifierResult.Error, VerifierResult.Cancelled]), "Cancellation precedence");
Check(DiagnosticsText.Get("Result_Error").Length > 0, "Resource lookup");

// A completed mismatch survives the Close action cancelling the rest of the session.
// These adapters use fake child transcripts and never launch the RAM worker.
using (var session = new CancellationTokenSource())
{
    var interaction = new ClosingInteraction(session);
    var stability = new StabilityVerifier((_, _, _, _, _) => { }, (_, _) => { }, session.Token, interaction)
    {
        ProcessExecutor = (_, _, output, _) => { output("Result: Issues", true); return 1; }
    };
    DiagnosticResult result = stability.Run();
    Check(interaction.ExitRequested && session.IsCancellationRequested, "RAM mismatch Close cancels the remaining session");
    Check(result.Status == DiagnosticStatus.IssuesFound && stability.LastVerifierResult == VerifierResult.IssuesFound &&
        result.ExitCode == 1, "Completed RAM mismatch evidence survives Finish and Run cancellation handling");
}
using (var session = new CancellationTokenSource())
{
    var interaction = new ClosingInteraction(session);
    var stability = new StabilityVerifier((_, _, _, _, _) => { }, (_, _) => { }, session.Token, interaction)
    {
        ProcessExecutor = (_, _, output, _) =>
        {
            output("Result: Issues", true);
            session.Cancel();
            return 1;
        }
    };
    DiagnosticResult result = stability.Run();
    Check(result.Status == DiagnosticStatus.Cancelled && stability.LastVerifierResult == VerifierResult.Cancelled,
        "Cancellation during a child does not preserve an incomplete mismatch result");
    Check(interaction.ConfirmCount == 0 && !interaction.ExitRequested, "Interrupted RAM child does not show completed findings prompt");
}

// Process tests launch only this test harness, never Windows servicing or RAM utilities.
(string executable, List<string> prefix) = ChildCommand();
var stdout = new List<(string Line, bool Newline)>();
var stderr = new List<string>();
int code = new ProcessWrapper(executable, false, CancellationToken.None).RunProcess(
    [.. prefix, "--child", "streams"], "", (line, newline) => stdout.Add((line, newline)), (line, _) => stderr.Add(line));
Check(code == 7, "Real child exit code preserved");
Check(stdout.Contains(("first", false)), "CR progress line preserved");
Check(stdout.Contains(("progress", true)), "CRLF line preserved");
Check(stdout.Contains(("final-no-newline", true)), "EOF partial line preserved");
Check(stderr.Count == 2000, "stderr drained concurrently beyond pipe capacity");
int consoleHostExit = new ProcessWrapper(executable, false, CancellationToken.None).RunProcess(
    [.. prefix, "--child", "console-host"], "", (_, _) => { }, (_, _) => { });
Check(consoleHostExit == 0, "Native CTRL-BREAK reaches only the private-console child, even after GC");
string unicode = "";
new ProcessWrapper(executable, false, CancellationToken.None, System.Text.Encoding.Unicode).RunProcess(
    [.. prefix, "--child", "unicode"], "", (line, _) => unicode += line);
Check(unicode == "Windows Resource Protection did not find any integrity violations.", "UTF-16 output decoded without a trailing newline");
var missingCheck = new FixtureCheck(Path.Combine(AppContext.BaseDirectory, "missing-test-tool.exe"), [], CancellationToken.None);
Check(missingCheck.Run().Status == DiagnosticStatus.Error, "Real launch failure becomes an error result");
string inputEcho = "";
new ProcessWrapper(executable, false, CancellationToken.None).RunProcess(
    [.. prefix, "--child", "stdin"], "answer", (line, _) => inputEcho += line);
Check(inputEcho == "answer", "stdin delivered and closed");
Check(new ProcessWrapper("StabilityTest.exe", false, CancellationToken.None).ResolvedFilename ==
    Path.Combine(AppContext.BaseDirectory, "StabilityTest.exe"), "Worker resolution ignores current directory");

string marker = Path.Combine(Path.GetTempPath(), "GameGarage-process-test-" + Guid.NewGuid().ToString("N"));
try
{
    using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    var watch = Stopwatch.StartNew();
    code = new ProcessWrapper(executable, false, cancel.Token, CancellationAction: _ => File.WriteAllText(marker, "cancel"))
        .RunProcess([.. prefix, "--child", "quiet", marker], "", (_, _) => { });
    Check(code == 13 && watch.Elapsed < TimeSpan.FromSeconds(4), "Quiet child cancellation independent of stdout");
}
finally { if (File.Exists(marker)) File.Delete(marker); }

using (var firstStarted = new ManualResetEventSlim())
using (var queuedCancellation = new CancellationTokenSource())
{
    var first = Task.Run(() => new ProcessWrapper(executable, false, CancellationToken.None).RunProcess(
        [.. prefix, "--child", "hold"], "", (_, _) => firstStarted.Set()));
    Check(firstStarted.Wait(TimeSpan.FromSeconds(2)), "First gated child started");
    queuedCancellation.CancelAfter(100);
    bool queuedWasCancelled = false;
    try
    {
        new ProcessWrapper("queued-must-not-launch.exe", false, queuedCancellation.Token)
            .RunProcess([], "", (_, _) => { });
    }
    catch (OperationCanceledException) { queuedWasCancelled = true; }
    Check(queuedWasCancelled, "Queued child can cancel before launching or receiving another child's break");
    Check(first.GetAwaiter().GetResult() == 0, "Queued cancellation leaves active child untouched");
}

using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    bool prevented = false;
    try { new ProcessWrapper("must-not-be-launched.exe", false, cancelled.Token).RunProcess([], "", (_, _) => { }); }
    catch (OperationCanceledException) { prevented = true; }
    Check(prevented, "Pre-cancelled process never launches");
    var fixture = new FixtureCheck(executable, [.. prefix, "--child", "success"], cancelled.Token);
    Check(((IDiagnosticCheck)fixture).Run().Status == DiagnosticStatus.Cancelled &&
        fixture.LastVerifierResult == VerifierResult.Cancelled, "Core cancellation contract");
}
var savedCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    var fixture = new FixtureCheck(executable, [.. prefix, "--child", "success"], CancellationToken.None);
    var seenProgress = new ManualResetEventSlim();
    fixture.ProgressChanged += (_, value) => { if (value.Percentage == 12.5) seenProgress.Set(); };
    Check(((IDiagnosticCheck)fixture).Run().Status == DiagnosticStatus.Passed, "Core success contract");
    Check(seenProgress.Wait(TimeSpan.FromSeconds(2)), "Invariant fractional progress under comma culture");
}
finally { CultureInfo.CurrentCulture = savedCulture; }
Console.WriteLine($"Passed {checks} diagnostic fixture and controlled-process checks.");
return 0;

static (string Executable, List<string> Prefix) ChildCommand()
{
    string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path");
    return Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        ? (executable, [Assembly.GetExecutingAssembly().Location]) : (executable, []);
}

static int RunChild(string[] childArgs)
{
    switch (childArgs[0])
    {
        case "console-host":
            using (var console = new ConsoleUtilities.AttachToConsole(false))
            using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var (path, prefix) = ChildCommand();
                int result = new ProcessWrapper(path, false, cancel.Token).RunProcess(
                    [.. prefix, "--child", "native-quiet"], "", (_, _) => { });
                return result == 13 ? 0 : 91;
            }
        case "native-quiet":
            using (var signal = new ManualResetEventSlim())
            {
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; signal.Set(); };
                return signal.Wait(TimeSpan.FromSeconds(5)) ? 13 : 92;
            }
        case "unicode":
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            Console.Write("Windows Resource Protection did not find any integrity violations.");
            return 0;
        case "hold":
            Console.WriteLine("started");
            Thread.Sleep(700);
            return 0;
        case "streams":
            Console.Write("first\rprogress\r\n");
            for (int i = 0; i < 2000; i++) Console.Error.WriteLine(new string('x', 128));
            Console.Write("final-no-newline");
            return 7;
        case "stdin":
            Console.Write(Console.In.ReadToEnd().Trim());
            return 0;
        case "quiet":
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (File.Exists(childArgs[1])) return 13;
                Thread.Sleep(20);
            }
            return 14;
        case "success":
            Console.WriteLine("Progress: 12.5%");
            Console.Write("The operation completed successfully.");
            return 0;
        default: return 99;
    }
}

sealed class FixtureCheck(string executable, List<string> arguments, CancellationToken cancel)
    : VerifierBase(executable, false, (_, _, _, _, _) => { }, (_, _) => { }, cancel)
{
    public override VerifierResult RunVerifier() => WindowsOutputParsers.Defrag(RunCapturedProcess(arguments, "", false));
}

sealed class ClosingInteraction(CancellationTokenSource session) : IWindowsUserInteraction
{
    public int ConfirmCount { get; private set; }
    public bool ExitRequested { get; private set; }
    public bool Confirm(string title, string message) { ConfirmCount++; return true; }
    public void Notify(string title, string message, DiagnosticStatus status) { }
    public (string Filename, int Index)? SelectWindowsImage() => null;
    public void ShowDrivers(IReadOnlyList<DeviceDriverInfo> drivers) { }
    public void RequestExit() { ExitRequested = true; session.Cancel(); }
}
