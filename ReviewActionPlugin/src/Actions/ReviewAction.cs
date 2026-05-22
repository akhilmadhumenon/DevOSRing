#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using DevOSRing.Core.Git;
using DevOSRing.Core.Hosting;
using DevOSRing.Core.Llm;
using DevOSRing.Core.Llm.Canned;

namespace Loupedeck.ReviewActionPlugin.Actions;

/// <summary>
/// Press: review what the user is actively looking at. Resolution order:
/// <list type="number">
///   <item>If there is a non-empty <b>selection</b> in the active editor, review just that
///         selection (with the file path and language sent as context).</item>
///   <item>Else if there is an <b>active file</b>, review the whole file.</item>
///   <item>Else if the workspace has a <b>git diff</b> (staged → unstaged), review the diff.</item>
///   <item>Else surface "nothing to review".</item>
/// </list>
/// The output is rendered as a Markdown webview inside Cursor / VS Code via the
/// companion extension. Uses the configured LLM, falling back to a diff-aware
/// canned summary when the LLM is unavailable (diff path only — there is no
/// canned reviewer for whole files).
/// </summary>
public class ReviewAction : PluginActionBase
{
    private const int MaxFileChars      = 24_000;   // ~6 K tokens — comfortable for Llama-3.3-70B
    private const int MaxSelectionChars = 12_000;
    private const int MaxDiffChars      = 60_000;

    private static readonly TimeSpan ReviewTimeout = TimeSpan.FromMinutes(3);
    protected override TimeSpan RunTimeout => ReviewTimeout;

    public ReviewAction()
        : base("AI Review", "Review the active file, selection, or git diff with AI", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        var plugin = (ReviewActionPlugin)this.Plugin;
        var settings = LlmSettings.Load(plugin);
        PluginLog.Info($"[Review] press start — LLM configured={settings.IsConfigured}, endpoint={settings.Endpoint}, model={settings.Model}");

        using var companion = new CompanionClient();
        var wakeProgress = new Progress<string>(stage => SetState(ActionState.Busy, stage));
        if (!await companion.EnsureConnectedAsync(ct, autoLaunch: true, wakeProgress))
        {
            PluginLog.Warning("[Review] companion still offline after auto-wake — open Cursor and reload the window (Cmd+Shift+P → Developer: Reload Window)");
            return ActionOutcome.Fail("Open Cursor");
        }

        var ctx = await companion.GetContextAsync(ct);
        if (ctx is null)
        {
            PluginLog.Warning("[Review] companion returned no context");
            await companion.NotifyAsync("warning", "DevOS: companion returned no workspace context.", ct);
            return ActionOutcome.Fail("No context");
        }

        var target = await ResolveTargetAsync(ctx, ct);
        PluginLog.Info($"[Review] resolved target source={target.Source}, " +
                       $"path={target.FilePath ?? "(none)"}, language={target.Language}, " +
                       $"chars={target.Content.Length}");

        if (target.Source == ReviewSource.None)
        {
            await companion.NotifyAsync("info", target.Message ?? "DevOS: nothing to review.", ct);
            return ActionOutcome.Ok(target.Message ?? "Nothing");
        }

        SetState(ActionState.Busy, target.Source switch
        {
            ReviewSource.Selection => "Selection",
            ReviewSource.File      => "File",
            ReviewSource.GitDiff   => "Diff",
            _                      => "Reviewing",
        });

        string markdown;
        bool usedLlm;
        if (settings.IsConfigured)
        {
            SetState(ActionState.Busy, "Calling AI");
            using var llm = new LlmClient(settings);
            var sw = Stopwatch.StartNew();
            try
            {
                var (system, user) = target.Source == ReviewSource.GitDiff
                    ? (LlmPrompts.ReviewDiffSystem, LlmPrompts.ReviewDiffUser(target.Content))
                    : (LlmPrompts.ReviewFileSystem,
                       LlmPrompts.ReviewFileUser(
                           target.Language,
                           target.FilePath ?? "(unknown)",
                           target.Content,
                           target.Source == ReviewSource.Selection));
                markdown = await llm.ChatAsync(system, user, temperature: 0.2, ct: ct);
                sw.Stop();
                PluginLog.Info($"[Review] LLM ok in {sw.ElapsedMilliseconds}ms, {markdown.Length} chars");
                usedLlm = true;
            }
            catch (LlmRequestException ex)
            {
                sw.Stop();
                PluginLog.Warning(ex, $"[Review] LLM failed in {sw.ElapsedMilliseconds}ms (HTTP {ex.StatusCode}), falling back");
                await companion.NotifyAsync("warning", $"DevOS: LLM call failed ({ex.StatusCode}). Using canned summary.", ct);
                markdown = FallbackMarkdown(target);
                usedLlm = false;
            }
        }
        else
        {
            PluginLog.Warning($"[Review] LLM not configured (apiKeyLen={settings.ApiKey.Length}); using canned");
            SetState(ActionState.Busy, "Canned");
            markdown = FallbackMarkdown(target);
            usedLlm = false;
        }

        var title = target.Source switch
        {
            ReviewSource.Selection => $"DevOS Review — {Path.GetFileName(target.FilePath ?? "")} (selection)",
            ReviewSource.File      => $"DevOS Review — {Path.GetFileName(target.FilePath ?? "")}",
            ReviewSource.GitDiff   => "DevOS Review — git diff",
            _                      => "DevOS Review",
        };
        if (!usedLlm) title += " (canned)";

        await companion.ShowReviewAsync(new ReviewRequest { Title = title, Markdown = markdown }, ct);
        PluginLog.Info($"[Review] webview shown (source={target.Source}, llm={usedLlm})");

        return target.Source switch
        {
            ReviewSource.Selection => ActionOutcome.Ok($"Sel {target.Content.Length}c"),
            ReviewSource.File      => ActionOutcome.Ok($"File {target.Content.Length}c"),
            ReviewSource.GitDiff   => ActionOutcome.Ok(GitDiffShort(target.Content)),
            _                      => ActionOutcome.Ok("Reviewed"),
        };
    }

    private async Task<ReviewTarget> ResolveTargetAsync(WorkspaceContext ctx, CancellationToken ct)
    {
        // 1) Selection in the active editor wins.
        if (ctx.Selection is { IsEmpty: false, Text: { Length: > 0 } sel })
        {
            return new ReviewTarget(
                ReviewSource.Selection,
                ctx.ActiveFilePath,
                ctx.Language ?? Languages.FromPath(ctx.ActiveFilePath ?? ""),
                Truncate(sel, MaxSelectionChars));
        }

        // 2) Otherwise the active file.
        if (!string.IsNullOrWhiteSpace(ctx.ActiveFilePath) && File.Exists(ctx.ActiveFilePath))
        {
            string content;
            try
            {
                content = await File.ReadAllTextAsync(ctx.ActiveFilePath, ct);
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[Review] could not read active file {ctx.ActiveFilePath}");
                content = string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(content))
            {
                return new ReviewTarget(
                    ReviewSource.File,
                    ctx.ActiveFilePath,
                    ctx.Language ?? Languages.FromPath(ctx.ActiveFilePath),
                    Truncate(content, MaxFileChars));
            }
        }

        // 3) Fall back to git diff at gitRoot (or workspaceRoot).
        var gitRoot = ctx.GitRoot ?? ctx.WorkspaceRoot;
        if (!string.IsNullOrWhiteSpace(gitRoot))
        {
            var git = new GitOps();
            if (await git.IsRepoAsync(gitRoot, ct))
            {
                var diff = await git.DiffStagedAsync(gitRoot, ct);
                if (string.IsNullOrWhiteSpace(diff))
                {
                    diff = await git.DiffAsync(gitRoot, ct);
                }
                if (!string.IsNullOrWhiteSpace(diff))
                {
                    return new ReviewTarget(
                        ReviewSource.GitDiff,
                        gitRoot,
                        "diff",
                        Truncate(diff, MaxDiffChars));
                }
            }
        }

        return new ReviewTarget(
            ReviewSource.None,
            null,
            "",
            "",
            "Open a file or stage some changes — nothing to review.");
    }

    private static string FallbackMarkdown(ReviewTarget t) => t.Source == ReviewSource.GitDiff
        ? CannedReview.Summarise(t.Content)
        : $"## Summary\n\nFile `{t.FilePath}` ({t.Content.Length} chars, language {t.Language}). LLM unavailable — configure `LLM_API_KEY` for a real review.\n\n## Risks\nNot analysed.\n\n## Suggestions\nNot analysed.\n";

    private static string GitDiffShort(string diff)
    {
        int added = 0, removed = 0;
        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith("+") && !line.StartsWith("+++")) added++;
            else if (line.StartsWith("-") && !line.StartsWith("---")) removed++;
        }
        return $"+{added} -{removed}";
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "\n... [truncated]";
}

internal enum ReviewSource { None, Selection, File, GitDiff }

internal sealed record ReviewTarget(
    ReviewSource Source,
    string? FilePath,
    string Language,
    string Content,
    string? Message = null);
