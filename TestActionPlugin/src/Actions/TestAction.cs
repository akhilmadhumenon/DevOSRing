using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using DevOSRing.Core.Hosting;
using DevOSRing.Core.Process;
using DevOSRing.Core.Tests;

namespace Loupedeck.TestActionPlugin.Actions;

/// <summary>
/// Press: detect the project type in the current workspace, run the right test
/// command via <see cref="ProcessRunner"/>, parse the summary, surface pass/fail
/// on the device button, and push the full log into a VS Code webview.
///
/// UX timeline:
/// <list type="number">
///   <item><b>Started</b> — IDE notification "Run Tests started — {kind}: `{cmd}` in `{workspace}`"</item>
///   <item><b>Running</b> — button ticks "Running 5s", "Running 10s"… every 2s while the runner streams</item>
///   <item><b>Completed</b> — IDE notification with PASSED/FAILED, counts, duration; plus the full
///         output rendered as a webview (existing behavior)</item>
/// </list>
/// </summary>
public class TestAction : PluginActionBase
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TickerInterval = TimeSpan.FromSeconds(2);
    protected override TimeSpan RunTimeout => TestTimeout;

    public TestAction()
        : base("Run Tests", "Run the workspace test suite", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        using var companion = new CompanionClient();
        var wakeProgress = new Progress<string>(stage => SetState(ActionState.Busy, stage));
        if (!await companion.EnsureConnectedAsync(ct, autoLaunch: true, wakeProgress))
        {
            PluginLog.Warning("[Tests] companion still offline after auto-wake — open Cursor and reload the window");
            return ActionOutcome.Fail("Open Cursor");
        }

        SetState(ActionState.Busy, "Detecting");
        var ctx = await companion.GetContextAsync(ct);
        var root = ctx?.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            await companion.NotifyAsync("warning", "DevOS: no workspace folder open.", ct);
            return ActionOutcome.Fail("No workspace");
        }

        var plan = ProjectDetector.Detect(root);
        if (!plan.IsRunnable)
        {
            await companion.NotifyAsync("warning",
                $"DevOS: could not detect a test runner in {root}. Supported: dotnet, npm/pnpm/yarn, pytest, cargo, go, maven, gradle.", ct);
            return ActionOutcome.Fail("Unknown project");
        }

        // --- STARTED ---
        var workspaceName = Path.GetFileName(root.TrimEnd('/', '\\'));
        var commandLine   = string.IsNullOrWhiteSpace(plan.Arguments) ? plan.FileName : $"{plan.FileName} {plan.Arguments}";
        var startMsg = $"DevOS: Run Tests started — {plan.Kind}: `{commandLine}` in `{workspaceName}` (timeout {TestTimeout.TotalMinutes:F0}m)";
        PluginLog.Info($"[Tests] {startMsg}");
        await companion.NotifyAsync("info", startMsg, ct);

        // --- RUNNING ---
        // Button shows the kind first, then a per-2s elapsed ticker so the user can see the
        // run is actually progressing. The ticker stops as soon as the runner returns.
        SetState(ActionState.Busy, plan.Kind.ToString());
        var runSw = Stopwatch.StartNew();
        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ticker = StartTicker(tickerCts.Token, runSw);

        var runner = new ProcessRunner();
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(plan.FileName, plan.Arguments, plan.WorkingDirectory,
                timeout: TestTimeout, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            tickerCts.Cancel();
            try { await ticker; } catch { /* expected */ }
            var msg = $"DevOS: failed to launch test command ({plan.Kind}: {plan.FileName}) — {ex.Message}";
            PluginLog.Error(ex, $"[Tests] {msg}");
            await companion.NotifyAsync("error", msg, ct);
            return ActionOutcome.Fail("Launch fail");
        }
        finally
        {
            tickerCts.Cancel();
            try { await ticker; } catch { /* expected */ }
        }
        runSw.Stop();

        // --- COMPLETED ---
        var summary  = TestResultParser.Parse(result.StdOut, result.StdErr, result.ExitCode);
        var markdown = BuildMarkdown(plan, result, summary);
        await companion.ShowReviewAsync(new ReviewRequest
        {
            Title    = "DevOS Test Results",
            Markdown = markdown,
        }, ct);

        var durSecs = result.Elapsed.TotalSeconds;
        if (result.Succeeded && summary.Failed == 0)
        {
            var msg = summary.HasAnyResult
                ? $"DevOS: tests PASSED — {summary.Passed}/{summary.Total}" +
                  (summary.Skipped > 0 ? $" ({summary.Skipped} skipped)" : "") +
                  $" in {durSecs:F1}s — {plan.Kind}: `{commandLine}`"
                : $"DevOS: tests PASSED in {durSecs:F1}s — {plan.Kind}: `{commandLine}` (no result count parsed; exit=0)";
            PluginLog.Info($"[Tests] {msg}");
            await companion.NotifyAsync("info", msg, ct);
            return ActionOutcome.Ok(summary.Short);
        }
        else
        {
            var msg = summary.HasAnyResult
                ? $"DevOS: tests FAILED — {summary.Failed} failed / {summary.Passed} passed / {summary.Total} total" +
                  $" in {durSecs:F1}s — {plan.Kind}: `{commandLine}` (exit={result.ExitCode})"
                : $"DevOS: tests FAILED — exit={result.ExitCode} in {durSecs:F1}s — {plan.Kind}: `{commandLine}`";
            PluginLog.Warning($"[Tests] {msg}");
            await companion.NotifyAsync("error", msg, ct);
            return ActionOutcome.Fail(summary.Short);
        }
    }

    /// <summary>
    /// Refreshes the device button label every <see cref="TickerInterval"/> with the elapsed
    /// runtime so the user can see the suite is still progressing. Stops silently when the
    /// linked token cancels (runner returned, errored, or user pressed cancel).
    /// </summary>
    private Task StartTicker(CancellationToken ct, Stopwatch sw) => Task.Run(async () =>
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TickerInterval, ct);
                if (ct.IsCancellationRequested) break;
                var secs = (int)sw.Elapsed.TotalSeconds;
                SetState(ActionState.Busy, $"Running {secs}s");
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
    }, ct);

    private static string BuildMarkdown(TestPlan plan, ProcessResult result, TestSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Test run: {plan.Kind}");
        sb.AppendLine();
        sb.AppendLine($"- Command: `{plan.FileName} {plan.Arguments}`");
        sb.AppendLine($"- Working dir: `{plan.WorkingDirectory}`");
        sb.AppendLine($"- Duration: `{result.Elapsed.TotalSeconds:F1}s`");
        sb.AppendLine($"- Exit code: `{result.ExitCode}`");
        if (summary.HasAnyResult)
        {
            sb.AppendLine($"- Results: **{summary.Passed} passed**, **{summary.Failed} failed**, {summary.Skipped} skipped, {summary.Total} total");
        }
        sb.AppendLine();
        sb.AppendLine("### Output");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(Tail(result.StdOut, 200));
        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            sb.AppendLine("--- stderr ---");
            sb.AppendLine(Tail(result.StdErr, 80));
        }
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string Tail(string s, int maxLines)
    {
        if (string.IsNullOrEmpty(s)) return "(no output)";
        var lines = s.Replace("\r\n", "\n").Split('\n');
        if (lines.Length <= maxLines) return s;
        return string.Join("\n", lines, lines.Length - maxLines, maxLines);
    }
}
