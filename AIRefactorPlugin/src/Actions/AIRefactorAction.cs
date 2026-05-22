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
        if (!await companion.EnsureConnectedAsync(ct))
        {
            return ActionOutcome.Fail("Companion offline");
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
        if (settings.IsConfigured)
        {
            SetState(ActionState.Busy, "Calling AI");
            using var llm = new LlmClient(settings);
            try
            {
                var raw = await llm.ChatAsync(
                    LlmPrompts.RefactorSystem,
                    LlmPrompts.RefactorUser(language, input, isSelection),
                    temperature: 0.1,
                    ct: ct);
                refactored = LlmExtraction.ExtractCode(raw);
            }
            catch (LlmRequestException ex)
            {
                PluginLog.Warning(ex, "[AIRefactor] LLM failed, falling back to canned");
                await companion.NotifyAsync("warning", $"DevOS: LLM call failed ({ex.StatusCode}). Using canned refactor.", ct);
                refactored = CannedRefactor.Apply(language, input);
            }
        }
        else
        {
            SetState(ActionState.Busy, "Canned");
            refactored = CannedRefactor.Apply(language, input);
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
            Title = settings.IsConfigured ? "DevOS AI Refactor" : "DevOS Canned Refactor",
        }, ct);

        if (diff?.Accepted == true)
        {
            return ActionOutcome.Ok("Applied");
        }
        return ActionOutcome.Ok("Reviewed");
    }
}
