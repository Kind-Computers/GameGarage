using System.IO;
using GameGarage.Tools;
using System.Diagnostics;
using System.Text;

namespace GameGarage.Utilities;

/// <summary>Streams both pipes and requests cooperative cancellation without terminating repair tools.</summary>
public sealed class ProcessWrapper(string Filename, bool UseSystemDirectory, CancellationToken CancelToken,
    Encoding? StandardOutputEncoding = null, Action<Process>? CancellationAction = null)
{
    private static readonly SemaphoreSlim ConsoleProcessGate = new(1, 1);
    public readonly string Filename = Filename;
    public readonly CancellationToken CancelToken = CancelToken;
    public readonly Encoding? StandardOutputEncoding = StandardOutputEncoding;

    public string ResolvedFilename => Path.IsPathRooted(Filename) ? Filename
        : Path.Combine(UseSystemDirectory ? Environment.SystemDirectory : AppContext.BaseDirectory, Filename);

    public List<(string Line, bool IsNewLine)> RunProcessAndCollectOutput(List<string> Args, string Input, out int ExitCode)
    {
        List<(string Line, bool IsNewLine)> lines = [];
        ExitCode = RunProcess(Args, Input, (line, newline) => lines.Add((line, newline)),
            (line, newline) => lines.Add((DiagnosticsText.Format("StandardError", line), newline)));
        return lines;
    }

    public int RunProcess(List<string> Args, string Input, ParseOutputAction ParseOutputDelegate,
        ParseOutputAction? ParseErrorDelegate = null)
    {
        // CTRL-BREAK targets the shared private console. Serialize complete child lifetimes,
        // including cancellation registration disposal, so a stale request cannot reach a new tool.
        ConsoleProcessGate.Wait(CancelToken);
        try { return RunProcessExclusive(Args, Input, ParseOutputDelegate, ParseErrorDelegate); }
        finally { ConsoleProcessGate.Release(); }
    }

    private int RunProcessExclusive(List<string> Args, string Input, ParseOutputAction ParseOutputDelegate,
        ParseOutputAction? ParseErrorDelegate)
    {
        CancelToken.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo
        {
            FileName = ResolvedFilename,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = StandardOutputEncoding,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string arg in Args) info.ArgumentList.Add(arg);

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException(DiagnosticsText.Format("UnableToLaunch", ResolvedFilename));
        object callbackLock = new();
        // Register independently of stdout so a quiet child receives cancellation promptly.
        // The GUI owns a hidden console shared only by its diagnostic children.
        using CancellationTokenRegistration registration = CancelToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    if (CancellationAction is not null) CancellationAction(process);
                    else ConsoleUtilities.BreakControl.SendBreakSignal();
                }
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Debug.WriteLine(error);
            }
        });

        Task outputTask = ReadPipeAsync(process.StandardOutput, (line, newline) =>
        {
            lock (callbackLock) ParseOutputDelegate(line, newline);
        });
        Task errorTask = ReadPipeAsync(process.StandardError, (line, newline) =>
        {
            lock (callbackLock)
            {
                if (ParseErrorDelegate is not null) ParseErrorDelegate(line, newline);
                else ParseOutputDelegate(DiagnosticsText.Format("StandardError", line), newline);
            }
        });
        try
        {
            if (Input.Length > 0) process.StandardInput.WriteLine(Input);
        }
        catch (IOException) { /* A fast-exiting tool can close stdin before this write. */ }
        finally
        {
            try { process.StandardInput.Close(); }
            catch (IOException) { }
        }

        Task.WhenAll(outputTask, errorTask).GetAwaiter().GetResult();
        process.WaitForExit();
        // Preserve the real exit code. Callers separately classify requested cancellation.
        return process.ExitCode;
    }

    private static async Task ReadPipeAsync(StreamReader reader, ParseOutputAction callback)
    {
        char[] buffer = new char[1024];
        StringBuilder line = new();
        bool pendingCarriageReturn = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < count; i++)
            {
                char c = buffer[i];
                if (pendingCarriageReturn)
                {
                    callback(line.ToString().TrimEnd(), c == '\n');
                    line.Clear();
                    pendingCarriageReturn = false;
                    if (c == '\n') continue;
                }
                if (c == '\r') pendingCarriageReturn = true;
                else if (c == '\n')
                {
                    callback(line.ToString().TrimEnd(), true);
                    line.Clear();
                }
                else line.Append(c);
            }
        }
        if (line.Length > 0 || pendingCarriageReturn)
            callback(line.ToString().TrimEnd(), true);
    }
}
