using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DevOSRing.Core.Llm;
using DevOSRing.Core.Settings;
using Loupedeck;

namespace DevOSRing.Core.Hosting;

/// <summary>
/// Opens a small JSON config file in the user's default editor so they can paste
/// their LLM endpoint / model / API key once, and have all 4 plugins pick it up.
///
/// Loupedeck only auto-discovers <see cref="PluginDynamicCommand"/> subclasses
/// inside the plugin's own assembly. Each plugin therefore declares a trivial
/// subclass of this base in its own namespace; all the behaviour lives here.
/// </summary>
public abstract class OpenLlmSettingsCommandBase : PluginDynamicCommand
{
    protected OpenLlmSettingsCommandBase()
        : base("Open LLM Settings", "Edit DevOS LLM endpoint / model / API key", "DevOS Settings")
    {
    }

    protected override void RunCommand(string actionParameter)
    {
        try
        {
            var path = ConfigFilePath();
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, DefaultConfigJson);
            }
            ApplyToPlugin(path);
            OpenInEditor(path);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "[DevOS] Failed to open LLM settings");
        }
    }

    private void ApplyToPlugin(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("endpoint", out var ep))
                this.Plugin.SetString(LlmSettings.KeyEndpoint, ep.GetString() ?? "");
            if (root.TryGetProperty("model", out var m))
                this.Plugin.SetString(LlmSettings.KeyModel, m.GetString() ?? "");
            if (root.TryGetProperty("apiKey", out var k))
                this.Plugin.SetSecret(LlmSettings.KeyApiKey, k.GetString() ?? "");
            if (root.TryGetProperty("systemPrompt", out var sp))
                this.Plugin.SetString(LlmSettings.KeySystem, sp.GetString() ?? "");
        }
        catch (Exception ex)
        {
            PluginLog.Warning(ex, "[DevOS] Could not parse LLM settings JSON; leaving stored values untouched.");
        }
    }

    private static string ConfigFilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".devos", "llm.json");
    }

    private static void OpenInEditor(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                System.Diagnostics.Process.Start("/usr/bin/open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                System.Diagnostics.Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                System.Diagnostics.Process.Start("/usr/bin/xdg-open", path);
        }
        catch (Exception ex)
        {
            PluginLog.Warning(ex, $"[DevOS] Could not open {path} in editor.");
        }
    }

    private const string DefaultConfigJson = """
        {
          "endpoint": "https://api.openai.com/v1",
          "model": "gpt-4o-mini",
          "apiKey": "",
          "systemPrompt": ""
        }
        """;
}
