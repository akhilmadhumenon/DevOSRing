using System;
using Loupedeck;

namespace DevOSRing.Core.Settings;

/// <summary>
/// Typed wrappers over <c>Plugin.SetPluginSetting</c> / <c>Plugin.TryGetPluginSetting</c>.
/// Secrets (API keys, tokens) are stored with <c>isSecure: true</c> so the host encrypts at rest.
/// </summary>
public static class PluginSettingsExtensions
{
    public static string? GetString(this Plugin plugin, string key, string? fallback = null)
    {
        if (plugin is null) throw new ArgumentNullException(nameof(plugin));
        return plugin.TryGetPluginSetting(key, out var value) ? value : fallback;
    }

    public static void SetString(this Plugin plugin, string key, string value)
    {
        if (plugin is null) throw new ArgumentNullException(nameof(plugin));
        plugin.SetPluginSetting(key, value ?? string.Empty, false);
    }

    public static string? GetSecret(this Plugin plugin, string key)
    {
        if (plugin is null) throw new ArgumentNullException(nameof(plugin));
        return plugin.TryGetPluginSetting(key, out var value) ? value : null;
    }

    public static void SetSecret(this Plugin plugin, string key, string value)
    {
        if (plugin is null) throw new ArgumentNullException(nameof(plugin));
        plugin.SetPluginSetting(key, value ?? string.Empty, true);
    }

    public static bool GetBool(this Plugin plugin, string key, bool fallback = false)
    {
        var raw = plugin.GetString(key);
        return bool.TryParse(raw, out var b) ? b : fallback;
    }

    public static void SetBool(this Plugin plugin, string key, bool value)
        => plugin.SetString(key, value ? "true" : "false");
}
