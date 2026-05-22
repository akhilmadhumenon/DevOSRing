using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using DevOSRing.Core.Hosting;
using DevOSRing.Core.Llm;

namespace Loupedeck.AIRefactorPlugin.Actions;

/// <summary>
/// Diagnostic action: pings the configured LLM endpoint and surfaces the resolved
/// settings + round-trip status. Use this to verify the plugin can actually reach
/// Groq / OpenAI / etc. from inside the LogiPluginService process (which has its
/// own PATH, env, and network reachability — different from your shell's).
///
/// On the button: <c>Idle → Busy ("Pinging") → Success ("200 / 312ms") | Error ("&lt;status&gt;")</c>.
/// In Cursor: a notification with the full diagnostic.
/// In the log: the full settings (endpoint + model + key length, not the key) and result.
/// </summary>
public class TestLlmAction : PluginActionBase
{
    public TestLlmAction()
        : base("Test LLM", "Verify the configured LLM endpoint is reachable", "DevOS")
    {
    }

    protected override async Task<ActionOutcome> ExecuteAsync(CancellationToken ct)
    {
        var plugin = (AIRefactorPlugin)this.Plugin;
        var settings = LlmSettings.Load(plugin);

        var summary = $"endpoint={settings.Endpoint}, model={settings.Model}, " +
                      $"apiKey={Mask(settings.ApiKey)} (len={settings.ApiKey.Length}), " +
                      $"isConfigured={settings.IsConfigured}";
        PluginLog.Info($"[TestLLM] Settings: {summary}");

        using var companion = new CompanionClient();
        // Diagnostic action — don't auto-launch the IDE just to send a notification.
        var companionOnline = await companion.EnsureConnectedAsync(ct, autoLaunch: false);

        if (!settings.IsConfigured)
        {
            var msg = $"DevOS: LLM is not configured. {summary}";
            PluginLog.Warning($"[TestLLM] {msg}");
            if (companionOnline) await companion.NotifyAsync("warning", msg, ct);
            return ActionOutcome.Fail("Not configured");
        }

        SetState(ActionState.Busy, "Pinging");
        using var llm = new LlmClient(settings);
        var sw = Stopwatch.StartNew();
        try
        {
            // Tiny round-trip prompt: deterministic, ~1 token reply.
            var reply = await llm.ChatAsync(
                systemPrompt: "You are a health-check responder. Reply with only the single word PONG.",
                userPrompt:   "ping",
                temperature:  0.0,
                maxTokens:    5,
                ct:           ct);
            sw.Stop();
            var trimmed = reply.Trim();
            var ok = trimmed.IndexOf("PONG", StringComparison.OrdinalIgnoreCase) >= 0;
            var line = $"DevOS LLM ok: {settings.Model} via {settings.Endpoint} replied \"{Truncate(trimmed, 32)}\" in {sw.ElapsedMilliseconds}ms";
            PluginLog.Info($"[TestLLM] {line}");
            if (companionOnline) await companion.NotifyAsync(ok ? "info" : "warning", line, ct);
            return ok
                ? ActionOutcome.Ok($"{sw.ElapsedMilliseconds}ms")
                : ActionOutcome.Fail("Bad reply");
        }
        catch (LlmRequestException ex)
        {
            sw.Stop();
            var line = $"DevOS LLM FAIL ({ex.StatusCode}): {Truncate(ex.Message, 200)}";
            PluginLog.Error(ex, $"[TestLLM] {line}");
            if (companionOnline) await companion.NotifyAsync("error", line, ct);
            return ActionOutcome.Fail($"HTTP {ex.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            var line = $"DevOS LLM timeout after {sw.ElapsedMilliseconds}ms (endpoint may be unreachable from LogiPluginService)";
            PluginLog.Warning($"[TestLLM] {line}");
            if (companionOnline) await companion.NotifyAsync("error", line, ct);
            return ActionOutcome.Fail("Timeout");
        }
        catch (Exception ex)
        {
            sw.Stop();
            var line = $"DevOS LLM error: {Truncate(ex.Message, 200)}";
            PluginLog.Error(ex, $"[TestLLM] {line}");
            if (companionOnline) await companion.NotifyAsync("error", line, ct);
            return ActionOutcome.Fail(Truncate(ex.GetType().Name, 32));
        }
    }

    private static string Mask(string key) =>
        string.IsNullOrEmpty(key)
            ? "(empty)"
            : key.Length <= 8 ? "****" : key.Substring(0, 4) + "…" + key.Substring(key.Length - 2);

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");
}
