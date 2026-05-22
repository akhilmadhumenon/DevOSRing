using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DevOSRing.Core.Companion;

/// <summary>
/// Reads the discovery file written by the <c>devos-companion</c> VS Code extension
/// on activation. The file lives at <c>~/.devos/companion.json</c> with mode 0600 and contains
/// the loopback port + bearer token + extension version + IDE name + pid.
/// </summary>
public static class CompanionDiscovery
{
    private const string DiscoveryFileName = "companion.json";
    private const string DotDevOsFolder = ".devos";

    public static string DiscoveryFilePath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, DotDevOsFolder, DiscoveryFileName);
        }
    }

    public static async Task<CompanionInfo?> ReadAsync()
    {
        var path = DiscoveryFilePath;
        if (!File.Exists(path)) return null;

        try
        {
            await using var fs = File.OpenRead(path);
            var info = await JsonSerializer.DeserializeAsync<CompanionInfo>(fs, SerializerOptions)
                .ConfigureAwait(false);
            return info is { Port: > 0 } && !string.IsNullOrWhiteSpace(info.Token) ? info : null;
        }
        catch
        {
            return null;
        }
    }

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Mirror of the JSON written by <c>devos-companion/src/discovery.ts</c>.</summary>
public sealed record CompanionInfo
{
    [JsonPropertyName("port")] public int Port { get; init; }
    [JsonPropertyName("token")] public string Token { get; init; } = string.Empty;
    [JsonPropertyName("pid")] public int Pid { get; init; }
    [JsonPropertyName("version")] public string Version { get; init; } = "0.0.0";
    [JsonPropertyName("ide")] public string Ide { get; init; } = "unknown";

    /// <summary>"starting" | "ready" | "stopped". Older companions may omit this; treat empty as "ready".</summary>
    [JsonPropertyName("state")] public string State { get; init; } = "ready";

    /// <summary>Epoch milliseconds when the server first bound.</summary>
    [JsonPropertyName("startedAt")] public long StartedAt { get; init; }

    /// <summary>Epoch milliseconds, updated by the extension's heartbeat timer every few seconds.</summary>
    [JsonPropertyName("heartbeatAt")] public long HeartbeatAt { get; init; }

    /// <summary>Epoch milliseconds when deactivate() ran. Only set when State == "stopped".</summary>
    [JsonPropertyName("stoppedAt")] public long? StoppedAt { get; init; }

    /// <summary>True only when the doc represents a currently-serving companion (port + token + ready + recent heartbeat).</summary>
    [JsonIgnore]
    public bool IsLive
    {
        get
        {
            if (Port <= 0 || string.IsNullOrWhiteSpace(Token)) return false;
            if (!string.Equals(State, "ready", StringComparison.OrdinalIgnoreCase)) return false;
            // No heartbeat field (older companion) → trust it, fall back to ping.
            if (HeartbeatAt <= 0) return true;
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // 60s window covers the 5s heartbeat plus generous slack for an extension host that
            // was briefly paused (debugger, GC, sleep).
            return (nowMs - HeartbeatAt) < 60_000;
        }
    }
}
