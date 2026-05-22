using System;
using System.IO;
using System.Text.Json;
using DevOSRing.Core.Settings;
using Loupedeck;

namespace DevOSRing.Core.Llm;

/// <summary>
/// LLM configuration loaded from one of three places, in order of precedence:
/// <list type="number">
///   <item>Per-plugin settings (set via the "Open LLM Settings" command in Logi Options+).</item>
///   <item><c>~/.devos/llm.json</c> (set by <c>scripts/setup-llm.sh</c> or by hand).</item>
///   <item>Environment variables: <c>LLM_API_KEY</c>, <c>LLM_ENDPOINT</c>, <c>LLM_MODEL</c>.</item>
/// </list>
/// The endpoint is OpenAI-compatible (works with OpenAI, Azure OpenAI, Groq,
/// OpenRouter, Ollama's <c>/v1</c>, LM Studio, etc.).
/// </summary>
public sealed class LlmSettings
{
    public const string KeyEndpoint = "devos.llm.endpoint";
    public const string KeyModel    = "devos.llm.model";
    public const string KeyApiKey   = "devos.llm.apiKey";
    public const string KeySystem   = "devos.llm.systemPrompt";

    public const string DefaultEndpoint = "https://api.openai.com/v1";
    public const string DefaultModel    = "gpt-4o-mini";

    public string Endpoint { get; init; } = DefaultEndpoint;
    public string Model    { get; init; } = DefaultModel;
    public string ApiKey   { get; init; } = string.Empty;
    public string? SystemPromptOverride { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Model) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public static LlmSettings Load(Plugin plugin)
    {
        if (plugin is null) throw new ArgumentNullException(nameof(plugin));

        var perPlugin = new LlmSettings
        {
            Endpoint = plugin.GetString(KeyEndpoint, "")!,
            Model    = plugin.GetString(KeyModel,    "")!,
            ApiKey   = plugin.GetSecret(KeyApiKey)   ?? string.Empty,
            SystemPromptOverride = plugin.GetString(KeySystem),
        };

        var file = TryReadFile();
        var env  = ReadEnv();

        return new LlmSettings
        {
            Endpoint = FirstNonEmpty(perPlugin.Endpoint, file?.Endpoint, env.Endpoint, DefaultEndpoint),
            Model    = FirstNonEmpty(perPlugin.Model,    file?.Model,    env.Model,    DefaultModel),
            ApiKey   = FirstNonEmpty(perPlugin.ApiKey,   file?.ApiKey,   env.ApiKey,   ""),
            SystemPromptOverride = FirstNonEmptyNullable(perPlugin.SystemPromptOverride, file?.SystemPromptOverride),
        };
    }

    internal static LlmSettings? TryReadFile()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(home, ".devos", "llm.json");
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            using var doc = JsonDocument.Parse(fs);
            var root = doc.RootElement;
            return new LlmSettings
            {
                Endpoint = ReadStringProp(root, "endpoint", ""),
                Model    = ReadStringProp(root, "model", ""),
                ApiKey   = ReadStringProp(root, "apiKey", ""),
                SystemPromptOverride = ReadOptionalProp(root, "systemPrompt"),
            };
        }
        catch
        {
            return null;
        }
    }

    private static LlmSettings ReadEnv() => new()
    {
        Endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT") ?? "",
        Model    = Environment.GetEnvironmentVariable("LLM_MODEL")    ?? "",
        ApiKey   = Environment.GetEnvironmentVariable("LLM_API_KEY")  ?? "",
    };

    private static string ReadStringProp(JsonElement obj, string name, string fallback)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback
            : fallback;

    private static string? ReadOptionalProp(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c!;
        return string.Empty;
    }

    private static string? FirstNonEmptyNullable(params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c;
        return null;
    }
}
