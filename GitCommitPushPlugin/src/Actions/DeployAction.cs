using System;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using DevOSRing.Core.Git;
using DevOSRing.Core.Llm;
using DevOSRing.Core.Llm.Canned;
using DevOSRing.Core.Hosting;

namespace Loupedeck.GitCommitPushPlugin.Actions;

/// <summary>
/// Press: stage everything in the active workspace's git repo, generate a
/// Conventional Commits-flavoured commit message (real LLM when configured, else
/// a deterministic message built from the diff), commit, and push.
/// Auto-handles "no upstream branch" by adding <c>--set-upstream origin &lt;branch&gt;</c>.
/// </summary>
public class DeployAction : PluginActionBase
{
    private static readonly TimeSpan PushTimeout = TimeSpan.FromMinutes(5);
    protected override TimeSpan RunTimeout => PushTimeout;

    public DeployAction()
        : base("Deploy", "Stage, AI commit message, and push to origin", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        var plugin = (GitCommitPushPlugin)this.Plugin;
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

        var git = new GitOps();
        if (!await git.IsRepoAsync(gitRoot, ct))
        {
            await companion.NotifyAsync("warning", $"DevOS: {gitRoot} is not a git repository.", ct);
            return ActionOutcome.Fail("Not a repo");
        }

        if (!await git.HasChangesAsync(gitRoot, ct))
        {
            await companion.NotifyAsync("info", "DevOS: nothing to commit.", ct);
            return ActionOutcome.Ok("Clean");
        }

        SetState(ActionState.Busy, "Staging");
        var stageResult = await git.StageAllAsync(gitRoot, ct);
        if (!stageResult.Succeeded)
        {
            await companion.NotifyAsync("error", $"DevOS: git add failed. {stageResult.StdErr}", ct);
            return ActionOutcome.Fail("Stage fail");
        }

        var diff = await git.DiffStagedAsync(gitRoot, ct);
        if (string.IsNullOrWhiteSpace(diff))
        {
            await companion.NotifyAsync("info", "DevOS: nothing staged after `git add`.", ct);
            return ActionOutcome.Ok("Nothing");
        }

        SetState(ActionState.Busy, "Commit msg");
        string message;
        if (settings.IsConfigured)
        {
            using var llm = new LlmClient(settings);
            try
            {
                message = (await llm.ChatAsync(
                    LlmPrompts.CommitSystem,
                    LlmPrompts.CommitUser(TruncateDiff(diff)),
                    temperature: 0.1,
                    maxTokens: 200,
                    ct: ct)).Trim();
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = CannedCommitMessage.From(diff);
                }
            }
            catch (LlmRequestException ex)
            {
                PluginLog.Warning(ex, "[Deploy] LLM failed, falling back to canned commit message");
                message = CannedCommitMessage.From(diff);
            }
        }
        else
        {
            message = CannedCommitMessage.From(diff);
        }

        SetState(ActionState.Busy, "Commit");
        var commitResult = await git.CommitAsync(gitRoot, message, ct);
        if (!commitResult.Succeeded)
        {
            var combined = commitResult.StdOut + commitResult.StdErr;
            if (combined.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
            {
                await companion.NotifyAsync("info", "DevOS: nothing to commit.", ct);
                return ActionOutcome.Ok("Nothing");
            }
            await companion.NotifyAsync("error", $"DevOS: git commit failed. {Tail(combined, 6)}", ct);
            return ActionOutcome.Fail("Commit fail");
        }

        SetState(ActionState.Busy, "Pushing");
        var pushResult = await git.PushAsync(gitRoot, ct);
        if (!pushResult.Succeeded)
        {
            await companion.NotifyAsync("error", $"DevOS: git push failed. {Tail(pushResult.StdOut + pushResult.StdErr, 6)}", ct);
            return ActionOutcome.Fail("Push fail");
        }

        var branch = await git.CurrentBranchAsync(gitRoot, ct) ?? "?";
        var remote = await git.RemoteUrlAsync(gitRoot, "origin", ct) ?? "origin";
        await companion.NotifyAsync("info", $"DevOS: pushed `{branch}` to {remote}.", ct);
        return ActionOutcome.Ok($"Pushed {branch}");
    }

    private static string Tail(string s, int lines)
    {
        if (string.IsNullOrEmpty(s)) return "(no output)";
        var split = s.Replace("\r\n", "\n").Split('\n');
        return split.Length <= lines
            ? s
            : string.Join("\n", split, split.Length - lines, lines);
    }

    private static string TruncateDiff(string diff)
    {
        const int maxChars = 30_000;
        return diff.Length <= maxChars ? diff : diff.Substring(0, maxChars) + "\n... [diff truncated]";
    }
}
