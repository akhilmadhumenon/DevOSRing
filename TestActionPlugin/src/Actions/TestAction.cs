using System;
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
/// </summary>
public class TestAction : PluginActionBase
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(10);
    protected override TimeSpan RunTimeout => TestTimeout;

    public TestAction()
        : base("Run Tests", "Run the workspace test suite", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        using var companion = new CompanionClient();
        if (!await companion.EnsureConnectedAsync(ct))
        {
            return ActionOutcome.Fail("Companion offline");
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

        SetState(ActionState.Busy, plan.Kind.ToString());
        PluginLog.Info($"[Tests] Running {plan.FileName} {plan.Arguments} in {plan.WorkingDirectory}");

        var runner = new ProcessRunner();
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(plan.FileName, plan.Arguments, plan.WorkingDirectory,
                timeout: TestTimeout, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await companion.NotifyAsync("error", $"DevOS: failed to launch test command: {ex.Message}", ct);
            return ActionOutcome.Fail("Launch fail");
        }

        var summary = TestResultParser.Parse(result.StdOut, result.StdErr, result.ExitCode);
        var markdown = BuildMarkdown(plan, result, summary);
        await companion.ShowReviewAsync(new ReviewRequest
        {
            Title = "DevOS Test Results",
            Markdown = markdown,
        }, ct);

        if (result.Succeeded && summary.Failed == 0)
        {
            await companion.NotifyAsync("info", $"DevOS: tests passed ({summary.Passed}/{summary.Total}) in {result.Elapsed.TotalSeconds:F1}s.", ct);
            return ActionOutcome.Ok(summary.Short);
        }
        else
        {
            await companion.NotifyAsync("error", $"DevOS: tests failed ({summary.Failed}/{summary.Total}).", ct);
            return ActionOutcome.Fail(summary.Short);
        }
    }

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
