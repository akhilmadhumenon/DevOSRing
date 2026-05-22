using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DevOSRing.Core.Companion;

/// <summary>
/// Typed HTTP client for the <c>devos-companion</c> VS Code / Cursor / Antigravity extension.
/// All endpoints are bearer-token authenticated; the token + port are discovered via
/// <see cref="CompanionDiscovery"/>.
/// </summary>
public sealed class CompanionClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Func<HttpClient, TimeSpan?, bool, IProgress<string>?, CancellationToken, Task<CompanionInfo?>> _awake;
    private CompanionInfo? _info;
    private readonly SemaphoreSlim _discoverLock = new(1, 1);

    public CompanionClient(
        HttpClient? http = null,
        Func<HttpClient, TimeSpan?, bool, IProgress<string>?, CancellationToken, Task<CompanionInfo?>>? awake = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _awake = awake ?? CompanionLauncher.AwakeAsync;
    }

    public bool IsAvailable => _info is { Port: > 0 };

    /// <summary>
    /// Ensures the companion is up and responsive. Strategy:
    /// <list type="number">
    ///   <item>If we already have a verified info from this session, ping it. If the ping
    ///         succeeds, reuse it.</item>
    ///   <item>Otherwise, ask <see cref="CompanionLauncher"/> to wake the IDE and poll for the
    ///         companion to come online (default 15s budget).</item>
    /// </list>
    /// Pass <paramref name="autoLaunch"/>=false to disable opening the IDE (e.g. for a quick
    /// diagnostic that should fail fast).
    /// </summary>
    public async Task<bool> EnsureConnectedAsync(
        CancellationToken ct = default,
        bool autoLaunch = true,
        IProgress<string>? progress = null,
        TimeSpan? wakeTimeout = null)
    {
        await _discoverLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Fast path: known info that still pings.
            if (_info is { Port: > 0 } &&
                await CompanionLauncher.PingAsync(_http, _info, ct).ConfigureAwait(false))
            {
                return true;
            }

            // Slow path: clear cached info, run the wake-up sequence.
            _info = null;
            var fresh = await _awake(_http, wakeTimeout, autoLaunch, progress, ct).ConfigureAwait(false);
            _info = fresh;
            return _info is { Port: > 0 };
        }
        finally
        {
            _discoverLock.Release();
        }
    }

    public Task<WorkspaceContext?> GetContextAsync(CancellationToken ct = default)
        => GetJsonAsync<WorkspaceContext>("/v1/context", ct);

    public Task<DiffResponse?> OpenDiffAsync(DiffRequest req, CancellationToken ct = default)
        => PostJsonAsync<DiffRequest, DiffResponse>("/v1/diff", req, ct);

    public Task<bool> ApplyAsync(ApplyRequest req, CancellationToken ct = default)
        => PostNoContentAsync("/v1/apply", req, ct);

    public Task<bool> ShowReviewAsync(ReviewRequest req, CancellationToken ct = default)
        => PostNoContentAsync("/v1/review", req, ct);

    public Task<bool> NotifyAsync(string level, string message, CancellationToken ct = default)
        => PostNoContentAsync("/v1/notify", new NotifyRequest { Level = level, Message = message }, ct);

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            throw new CompanionUnavailableException();

        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri(path));
        AddAuth(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            HandleNonSuccess(resp);
        }
        return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
    }

    private async Task<TResp?> PostJsonAsync<TReq, TResp>(string path, TReq body, CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            throw new CompanionUnavailableException();

        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(path))
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        AddAuth(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            HandleNonSuccess(resp);
        }
        return await resp.Content.ReadFromJsonAsync<TResp>(JsonOptions, ct).ConfigureAwait(false);
    }

    private async Task<bool> PostNoContentAsync<TReq>(string path, TReq body, CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            throw new CompanionUnavailableException();

        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(path))
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        AddAuth(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            HandleNonSuccess(resp);
            return false;
        }
        return true;
    }

    private Uri BuildUri(string path) =>
        new($"http://127.0.0.1:{_info!.Port}{path}");

    private void AddAuth(HttpRequestMessage req)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _info!.Token);
        req.Headers.UserAgent.ParseAdd("DevOSRing/1.0");
    }

    private static void HandleNonSuccess(HttpResponseMessage resp)
    {
        if ((int)resp.StatusCode == 401) throw new CompanionAuthException();
        throw new CompanionRequestException((int)resp.StatusCode, resp.ReasonPhrase ?? "Unknown error");
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Dispose() => _http.Dispose();
}

public class CompanionUnavailableException : Exception
{
    public CompanionUnavailableException()
        : base("DevOS Companion is not running. Install / open the devos-companion extension in your IDE.") { }
}

public class CompanionAuthException : Exception
{
    public CompanionAuthException()
        : base("DevOS Companion rejected the auth token. Restart the IDE so the companion regenerates its token.") { }
}

public class CompanionRequestException : Exception
{
    public int StatusCode { get; }
    public CompanionRequestException(int statusCode, string reason)
        : base($"Companion request failed: HTTP {statusCode} {reason}")
    {
        StatusCode = statusCode;
    }
}
