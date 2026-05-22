using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DevOSRing.Core.Llm;

/// <summary>
/// OpenAI-compatible chat-completions client. Used for AI Refactor, AI Review,
/// and commit-message generation. Caller is expected to check
/// <see cref="LlmSettings.IsConfigured"/> before calling.
/// </summary>
public sealed class LlmClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly LlmSettings _settings;

    public LlmClient(LlmSettings settings, HttpClient? http = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public bool IsConfigured => _settings.IsConfigured;

    /// <summary>Send a chat completion. Throws on non-2xx response.</summary>
    public async Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.2,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("LLM is not configured.");

        var url = $"{_settings.Endpoint.TrimEnd('/')}/chat/completions";
        var request = new ChatRequest
        {
            Model = _settings.Model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user",   Content = userPrompt },
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        req.Headers.UserAgent.ParseAdd("DevOSRing/1.0");

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new LlmRequestException((int)resp.StatusCode, Truncate(body, 600));
        }

        var parsed = System.Text.Json.JsonSerializer.Deserialize<ChatResponse>(body);
        var content = parsed?.Choices is { Count: > 0 } ? parsed.Choices[0].Message?.Content : null;
        if (string.IsNullOrWhiteSpace(content))
            throw new LlmRequestException(0, "LLM returned empty content.");
        return content!;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    public void Dispose() => _http.Dispose();

    // ---- DTOs ----
    internal sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = new();
        [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.2;
        [JsonPropertyName("max_tokens"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxTokens { get; set; }
    }

    internal sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    internal sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    internal sealed class Choice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
    }
}

public class LlmRequestException : Exception
{
    public int StatusCode { get; }
    public LlmRequestException(int statusCode, string body)
        : base($"LLM request failed: HTTP {statusCode}. {body}")
    {
        StatusCode = statusCode;
    }
}
