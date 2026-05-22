using System;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using DevOSRing.Core.Git;
using DevOSRing.Core.Llm;
using DevOSRing.Core.Llm.Canned;
using DevOSRing.Core.Hosting;

namespace Loupedeck.ReviewActionPlugin.Actions;

/// <summary>
/// Press: ask git for the diff of staged (or, if none, unstaged) changes in the
/// active workspace, send to the configured LLM (or run the diff-aware canned
/// summary if no LLM is configured), and render the markdown in a VS Code webview
/// via the companion extension.
/// </summary>
public class ReviewAction : PluginActionBase
{
    private static readonly TimeSpan ReviewTimeout = TimeSpan.FromMinutes(3);
    protected override TimeSpan RunTimeout => ReviewTimeout;

    public ReviewAction()
        : base("AI Review", "Review the current git diff with AI", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        var plugin = (ReviewActionPlugin)this.Plugin;
        var settings = LlmSettings.Load(plugin);

        using var companion = new CompanionClient();
        if (!await companion.EnsureConnectedAsync(ct))
        {
            return ActionOutcome.Fail("Companion offline");
        }

        var ctx = await companion.GetContextAsync(ct);
        var gitRoot = ctx?.GitRoot ?? ctx?.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(gitRoot))
        {
            await companion.NotifyAsync("warning", "DevOS: no git repo in the active workspace.", ct);
            return ActionOutcome.Fail("No repo");
        }

        SetState(ActionState.Busy, "Diffing");
        var git = new GitOps();
        if (!await git.IsRepoAsync(gitRoot, ct))
        {
            await companion.NotifyAsync("warning", $"DevOS: {gitRoot} is not a git repository.", ct);
            return ActionOutcome.Fail("Not a repo");
        }

        var diff = await git.DiffStagedAsync(gitRoot, ct);
        if (string.IsNullOrWhiteSpace(diff))
        {
            diff = await git.DiffAsync(gitRoot, ct);
        }
        if (string.IsNullOrWhiteSpace(diff))
        {
            await companion.NotifyAsync("info", "DevOS: working tree clean. Nothing to review.", ct);
            return ActionOutcome.Ok("Clean");
        }

        string markdown;
        if (settings.IsConfigured)
        {
            SetState(ActionState.Busy, "Calling AI");
            using var llm = new LlmClient(settings);
            try
            {
                markdown = await llm.ChatAsync(
                    LlmPrompts.ReviewSystem,
                    LlmPrompts.ReviewUser(TruncateDiff(diff)),
                    temperature: 0.2,
                    ct: ct);
            }
            catch (LlmRequestException ex)
            {
                PluginLog.Warning(ex, "[Review] LLM failed, falling back to canned");
                await companion.NotifyAsync("warning", $"DevOS: LLM call failed ({ex.StatusCode}). Using canned review.", ct);
                markdown = CannedReview.Summarise(diff);
            }
        }
        else
        {
            SetState(ActionState.Busy, "Canned");
            markdown = CannedReview.Summarise(diff);
        }

        await companion.ShowReviewAsync(new ReviewRequest
        {
            Title = settings.IsConfigured ? "DevOS AI Review" : "DevOS Review (canned)",
            Markdown = markdown,
        }, ct);

        var stats = CountStats(diff);
        return ActionOutcome.Ok($"+{stats.added} -{stats.removed}");
    }

    private static (int added, int removed) CountStats(string diff)
    {
        int a = 0, r = 0;
        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith("+") && !line.StartsWith("+++")) a++;
            else if (line.StartsWith("-") && !line.StartsWith("---")) r++;
        }
        return (a, r);
    }

    private static string TruncateDiff(string diff)
    {
        const int maxChars = 60_000;
        return diff.Length <= maxChars ? diff : diff.Substring(0, maxChars) + "\n... [diff truncated]";
    }
}
