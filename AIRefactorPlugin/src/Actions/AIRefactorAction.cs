using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using DevOSRing.Core.Llm;
using DevOSRing.Core.Llm.Canned;
using DevOSRing.Core.Hosting;

namespace Loupedeck.AIRefactorPlugin.Actions;

/// <summary>
/// Press: ask the devos-companion extension for the active file, send it (or the
/// selection) to the configured LLM (or run the Roslyn-based canned refactor if no
/// LLM is configured), then open the proposed change in the IDE's diff viewer.
/// </summary>
public class AIRefactorAction : PluginActionBase
{
    public AIRefactorAction()
        : base("AI Refactor", "Refactor the active file (or selection) with AI", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        var plugin = (AIRefactorPlugin)this.Plugin;
        var settings = LlmSettings.Load(plugin);

        using var companion = new CompanionClient();
        var wakeProgress = new Progress<string>(stage => SetState(ActionState.Busy, stage));
        if (!await companion.EnsureConnectedAsync(ct, autoLaunch: true, wakeProgress))
        {
            PluginLog.Warning("[AIRefactor] companion still offline after auto-wake — open Cursor and reload the window");
            return ActionOutcome.Fail("Open Cursor");
        }

        SetState(ActionState.Busy, "Reading file");
        var ctx = await companion.GetContextAsync(ct);
        if (ctx?.ActiveFilePath is null || !File.Exists(ctx.ActiveFilePath))
        {
            await companion.NotifyAsync("warning", "DevOS: open a file in the editor before pressing AI Refactor.", ct);
            return ActionOutcome.Fail("No active file");
        }

        var source = await File.ReadAllTextAsync(ctx.ActiveFilePath, ct);
        var isSelection = ctx.Selection is { IsEmpty: false, Text: { Length: > 0 } };
        var input = isSelection ? ctx.Selection!.Text : source;
        var language = ctx.Language ?? Languages.FromPath(ctx.ActiveFilePath);

        string refactored;
        bool usedLlm;
        if (settings.IsConfigured)
        {
            PluginLog.Info($"[AIRefactor] Calling LLM: endpoint={settings.Endpoint}, model={settings.Model}, " +
                           $"language={language}, inputChars={input.Length}, selection={isSelection}");
            SetState(ActionState.Busy, "Calling AI");
            using var llm = new LlmClient(settings);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var raw = await llm.ChatAsync(
                    LlmPrompts.RefactorSystem,
                    LlmPrompts.RefactorUser(language, input, isSelection),
                    temperature: 0.1,
                    ct: ct);
                sw.Stop();
                PluginLog.Info($"[AIRefactor] LLM ok in {sw.ElapsedMilliseconds}ms, {raw.Length} chars");
                refactored = LlmExtraction.ExtractCode(raw);
                usedLlm = true;
            }
            catch (LlmRequestException ex)
            {
                sw.Stop();
                PluginLog.Warning(ex, $"[AIRefactor] LLM failed in {sw.ElapsedMilliseconds}ms (HTTP {ex.StatusCode}), falling back to canned");
                await companion.NotifyAsync("warning", $"DevOS: LLM call failed ({ex.StatusCode}). Using canned refactor.", ct);
                refactored = CannedRefactor.Apply(language, input);
                usedLlm = false;
            }
        }
        else
        {
            PluginLog.Warning($"[AIRefactor] LLM not configured (endpoint={settings.Endpoint}, model={settings.Model}, " +
                              $"apiKeyLen={settings.ApiKey.Length}); using canned Roslyn refactor");
            SetState(ActionState.Busy, "Canned");
            refactored = CannedRefactor.Apply(language, input);
            usedLlm = false;
        }

        if (string.IsNullOrWhiteSpace(refactored) || refactored.Equals(input, StringComparison.Ordinal))
        {
            await companion.NotifyAsync("info", "DevOS: no changes suggested.", ct);
            return ActionOutcome.Ok("No change");
        }

        var newFileContent = isSelection
            ? source.Replace(ctx.Selection!.Text, refactored)
            : refactored;

        SetState(ActionState.Busy, "Open diff");
        var diff = await companion.OpenDiffAsync(new DiffRequest
        {
            Path = ctx.ActiveFilePath,
            RefactoredText = newFileContent,
            Title = usedLlm ? "DevOS AI Refactor" : "DevOS Canned Refactor",
        }, ct);

        PluginLog.Info($"[AIRefactor] Diff opened (source={(usedLlm ? "llm" : "canned")}, " +
                       $"outputChars={refactored.Length}); user will accept/discard via Cursor Command Palette");
        // We deliberately don't block the button on the user's accept/discard click —
        // the companion's diff route returns immediately and the user takes their time
        // in Cursor (`DevOS: Accept Refactor` / `DevOS: Discard Refactor`).
        return ActionOutcome.Ok(usedLlm ? "AI diff" : "Canned diff");
    }
}
