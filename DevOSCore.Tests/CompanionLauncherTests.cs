using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Companion;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

/// <summary>
/// Exercises the self-healing path:
/// <list type="bullet">
///   <item>If the discovery file is missing, <c>AwakeAsync</c> returns null within the timeout
///         (we pass autoLaunchIde=false so it doesn't try to spawn Cursor on the build agent).</item>
///   <item>If a stub HTTP server is bound on a chosen port and discovery points to it, ping
///         succeeds and the launcher returns the info on the first poll iteration.</item>
///   <item><c>CompanionInfo.IsLive</c> rejects stopped state and stale heartbeats.</item>
/// </list>
/// We don't test the actual IDE-launching paths — that's a side-effect we can't run cleanly
/// in CI. The logic above is what we actually depend on at runtime.
/// </summary>
public class CompanionLauncherTests
{
    [Fact]
    public async Task AwakeAsync_NoFile_ReturnsNullQuickly()
    {
        using var http = new HttpClient();
        using var scope = new TempDiscovery(null);  // no file
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var info = await CompanionLauncher.AwakeAsync(
            http,
            timeout: TimeSpan.FromSeconds(2),
            autoLaunchIde: false,
            progress: null,
            ct: CancellationToken.None);
        sw.Stop();
        Assert.Null(info);
        Assert.True(sw.ElapsedMilliseconds < 3_500, $"should give up near the timeout, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task AwakeAsync_HappyPath_PingsAndReturns()
    {
        var token = Guid.NewGuid().ToString("N");
        using var stub = new StubCompanionServer(token);
        var info = new CompanionInfo
        {
            Port        = stub.Port,
            Token       = token,
            Pid         = Environment.ProcessId,
            Version     = "1.0.0",
            Ide         = "cursor",
            State       = "ready",
            StartedAt   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            HeartbeatAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        using var scope = new TempDiscovery(info);
        using var http  = new HttpClient();

        var awoken = await CompanionLauncher.AwakeAsync(
            http,
            timeout: TimeSpan.FromSeconds(5),
            autoLaunchIde: false,
            progress: null,
            ct: CancellationToken.None);

        Assert.NotNull(awoken);
        Assert.Equal(stub.Port, awoken!.Port);
        Assert.Equal(token, awoken.Token);
    }

    [Fact]
    public void IsLive_RejectsStoppedAndStaleHeartbeat()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var ready = new CompanionInfo { Port = 1234, Token = "t", State = "ready", HeartbeatAt = now };
        Assert.True(ready.IsLive);

        var stopped = ready with { State = "stopped" };
        Assert.False(stopped.IsLive);

        var stale = ready with { HeartbeatAt = now - 5 * 60 * 1000 };  // 5 min stale
        Assert.False(stale.IsLive);

        var noPort = ready with { Port = 0 };
        Assert.False(noPort.IsLive);
    }

    /// <summary>RAII helper that writes a discovery file and restores the previous one on dispose.</summary>
    private sealed class TempDiscovery : IDisposable
    {
        private readonly string _path = CompanionDiscovery.DiscoveryFilePath;
        private readonly string? _backup;
        private readonly bool _restoredOriginal;

        public TempDiscovery(CompanionInfo? info)
        {
            if (File.Exists(_path))
            {
                _backup = _path + ".testbak";
                File.Copy(_path, _backup, overwrite: true);
                _restoredOriginal = true;
            }

            if (info is null)
            {
                if (File.Exists(_path)) File.Delete(_path);
                return;
            }

            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(info, opts);
            File.WriteAllText(_path, json);
        }

        public void Dispose()
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
            if (_restoredOriginal && _backup is not null && File.Exists(_backup))
            {
                try { File.Move(_backup, _path, overwrite: true); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>Minimal HTTP listener that responds 200 to /v1/ping when the bearer matches.</summary>
    private sealed class StubCompanionServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        public int Port { get; }

        public StubCompanionServer(string token)
        {
            Port = FindFreePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(() => Loop(token));
        }

        private async Task Loop(string token)
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch { return; }

                var auth = ctx.Request.Headers["Authorization"];
                var ok = string.Equals(auth, "Bearer " + token, StringComparison.Ordinal);
                ctx.Response.StatusCode = ok ? 200 : 401;
                if (ok)
                {
                    var bytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                }
                ctx.Response.Close();
            }
        }

        private static int FindFreePort()
        {
            // Bind to 0 to let the OS pick, then immediately release.
            var tmp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tmp.Start();
            var port = ((IPEndPoint)tmp.LocalEndpoint).Port;
            tmp.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* ignore */ }
            try { _listener.Close(); } catch { /* ignore */ }
        }
    }
}
