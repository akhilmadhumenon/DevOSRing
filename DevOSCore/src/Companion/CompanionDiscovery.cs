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
}
