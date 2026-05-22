using System;
using DevOSRing.Core.Settings;
using Loupedeck;

namespace DevOSRing.Core.Llm;

/// <summary>
/// LLM configuration loaded from the plugin's settings store. Endpoint is
/// OpenAI-compatible (works with OpenAI, Azure OpenAI, OpenRouter, Ollama's <c>/v1</c>, etc.).
/// </summary>
public sealed class LlmSettings
{
    public const string KeyEndpoint = "devos.llm.endpoint";
    public const string KeyModel    = "devos.llm.model";
    public const string KeyApiKey   = "devos.llm.apiKey";
    public const string KeySystem   = "devos.llm.systemPrompt";

    public string Endpoint { get; init; } = "https://api.openai.com/v1";
    public string Model    { get; init; } = "gpt-4o-mini";
    public string ApiKey   { get; init; } = string.Empty;
    public string? SystemPromptOverride { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Model) &&
        !string.IsNullOrWhiteSpace(ApiKey);

    public static LlmSettings Load(Plugin plugin)
    {
        if (plugin is null) throw new ArgumentNullException(nameof(plugin));
        return new LlmSettings
        {
            Endpoint = plugin.GetString(KeyEndpoint, "https://api.openai.com/v1")!,
            Model    = plugin.GetString(KeyModel,    "gpt-4o-mini")!,
            ApiKey   = plugin.GetSecret(KeyApiKey)   ?? string.Empty,
            SystemPromptOverride = plugin.GetString(KeySystem),
        };
    }
}
