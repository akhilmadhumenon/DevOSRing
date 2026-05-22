using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevOSRing.Core.Process;

/// <summary>
/// Single chokepoint for launching child processes. Honours cancellation
/// (kills the process tree) and captures stdout / stderr without dead-locking.
/// </summary>
public sealed class ProcessRunner
{
    public Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        // Resolve "dotnet" / "npm" / etc. to a full path because LogiPluginService
        // inherits a stripped PATH when launched by macOS LaunchServices.
        var resolved = BinaryResolver.Resolve(fileName);

        var psi = new ProcessStartInfo
        {
            FileName = resolved,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Augment PATH so any sub-tools the child spawns (e.g. `dotnet test` calling
        // out to `git`, or `npm test` calling node) can also be found.
        AugmentPath(psi);

        if (environment != null)
        {
            foreach (var kv in environment)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        return RunCoreAsync(psi, timeout ?? TimeSpan.FromMinutes(5), cancellationToken);
    }

    private static void AugmentPath(ProcessStartInfo psi)
    {
        var current = (psi.Environment.TryGetValue("PATH", out var existing) ? existing : null)
                      ?? Environment.GetEnvironmentVariable("PATH")
                      ?? string.Empty;

        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        var have = new HashSet<string>(current.Split(sep, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        var additions = new List<string>();
        foreach (var dir in BinaryResolver.ExtraPathDirs())
        {
            if (have.Add(dir)) additions.Add(dir);
        }
        if (additions.Count == 0) return;

        psi.Environment["PATH"] = string.IsNullOrEmpty(current)
            ? string.Join(sep, additions)
            : current + sep + string.Join(sep, additions);
    }

    private static async Task<ProcessResult> RunCoreAsync(
        ProcessStartInfo psi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource<bool>();
        var stderrClosed = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) stdoutClosed.TrySetResult(true);
            else lock (stdout) { stdout.AppendLine(e.Data); }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) stderrClosed.TrySetResult(true);
            else lock (stderr) { stderr.AppendLine(e.Data); }
        };

        var sw = Stopwatch.StartNew();
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {psi.FileName} {psi.Arguments}");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillTree(process);
            throw;
        }
        sw.Stop();

        string outText, errText;
        lock (stdout) outText = stdout.ToString();
        lock (stderr) errText = stderr.ToString();
        return new ProcessResult(process.ExitCode, outText, errText, sw.Elapsed);
    }

    private static void TryKillTree(System.Diagnostics.Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch { /* best-effort */ }
    }
}

public readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr, TimeSpan Elapsed)
{
    public bool Succeeded => ExitCode == 0;
}
