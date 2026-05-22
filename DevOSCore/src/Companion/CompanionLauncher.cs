using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SysProcess = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace DevOSRing.Core.Companion;

/// <summary>
/// Brings the <c>devos-companion</c> extension up on demand. The contract is:
/// <list type="number">
///   <item>If the existing discovery file is live and responds to <c>/v1/ping</c>, return it.</item>
///   <item>Otherwise, best-effort launch the user's IDE (Cursor first, then VS Code,
///         then Antigravity) so its <c>onStartupFinished</c> event fires and the extension
///         activates.</item>
///   <item>Poll the discovery file + ping endpoint until both succeed or the timeout elapses.</item>
/// </list>
/// The launcher never crashes the action — it returns <c>null</c> on failure and lets the caller
/// surface an actionable error.
/// </summary>
public static class CompanionLauncher
{
    private static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultWakeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Returns a working <see cref="CompanionInfo"/> if the companion is (or becomes) reachable,
    /// otherwise <c>null</c>. Always pings before returning, so callers never get a stale info.
    /// </summary>
    public static async Task<CompanionInfo?> AwakeAsync(
        HttpClient http,
        TimeSpan? timeout = null,
        bool autoLaunchIde = true,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (http is null) throw new ArgumentNullException(nameof(http));
        var deadline = DateTime.UtcNow + (timeout ?? DefaultWakeTimeout);

        var existing = await CompanionDiscovery.ReadAsync().ConfigureAwait(false);
        if (existing is not null && existing.IsLive && await PingAsync(http, existing, ct).ConfigureAwait(false))
        {
            return existing;
        }

        progress?.Report("Waking IDE");
        var launched = false;
        if (autoLaunchIde)
        {
            launched = TryLaunchIde(existing);
        }

        var attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            attempt++;
            progress?.Report(attempt <= 4 ? "Waking IDE" : "Still waking");

            var info = await CompanionDiscovery.ReadAsync().ConfigureAwait(false);
            if (info is null) continue;

            // Refresh enabled state on each iteration: state may flip starting → ready as the
            // server binds, and heartbeat may catch up. We ping in either case because IsLive
            // is a hint, not proof.
            if (info.Port <= 0 || string.IsNullOrWhiteSpace(info.Token)) continue;
            if (!string.Equals(info.State, "ready", StringComparison.OrdinalIgnoreCase)) continue;

            if (await PingAsync(http, info, ct).ConfigureAwait(false))
            {
                return info;
            }
        }

        if (!launched)
        {
            progress?.Report("Companion offline");
        }
        return null;
    }

    /// <summary>One-shot authenticated GET against /v1/ping with a tight timeout.</summary>
    public static async Task<bool> PingAsync(HttpClient http, CompanionInfo info, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DefaultPingTimeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{info.Port}/v1/ping");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", info.Token);
            req.Headers.UserAgent.ParseAdd("DevOSRing-Launcher/1.0");
            using var resp = await http.SendAsync(req, cts.Token).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLaunchIde(CompanionInfo? lastInfo)
    {
        var preferred = NormalizeIdeName(lastInfo?.Ide);
        var ordered = OrderedIdeCandidates(preferred);
        foreach (var ide in ordered)
        {
            if (TryLaunchSpecific(ide))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> OrderedIdeCandidates(string? preferred)
    {
        var defaults = new[] { "cursor", "vscode", "antigravity" };
        if (string.IsNullOrWhiteSpace(preferred)) return defaults;
        return new[] { preferred! }.Concat(defaults.Where(d => !d.Equals(preferred, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? NormalizeIdeName(string? ide)
    {
        if (string.IsNullOrWhiteSpace(ide)) return null;
        var lower = ide.ToLowerInvariant();
        if (lower.Contains("cursor")) return "cursor";
        if (lower.Contains("antigravity")) return "antigravity";
        if (lower.Contains("code")) return "vscode";
        return null;
    }

    private static bool TryLaunchSpecific(string ide)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return LaunchOnMac(ide);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return LaunchOnWindows(ide);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return LaunchOnLinux(ide);
            }
        }
        catch
        {
            /* swallow — best-effort */
        }
        return false;
    }

    private static bool LaunchOnMac(string ide)
    {
        // `open -g -a "<App>"` focuses an existing window or launches a new one. -g prevents
        // stealing focus when the user is mid-action on the device.
        var appName = ide switch
        {
            "cursor"      => "Cursor",
            "vscode"      => "Visual Studio Code",
            "antigravity" => "Antigravity",
            _             => null,
        };
        if (appName is null) return false;

        var appDir = Path.Combine("/Applications", appName + ".app");
        if (!Directory.Exists(appDir)) return false;

        var psi = new ProcessStartInfo
        {
            FileName = "/usr/bin/open",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-g");
        psi.ArgumentList.Add("-a");
        psi.ArgumentList.Add(appName);
        using var p = SysProcess.Start(psi);
        return p is not null;
    }

    private static bool LaunchOnWindows(string ide)
    {
        var exeName = ide switch
        {
            "cursor"      => "Cursor.exe",
            "vscode"      => "Code.exe",
            "antigravity" => "Antigravity.exe",
            _             => null,
        };
        if (exeName is null) return false;

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(local, "Programs", "cursor", exeName),
            Path.Combine(local, "Programs", "Microsoft VS Code", exeName),
            Path.Combine(pf, "Microsoft VS Code", exeName),
            Path.Combine(local, "Programs", "antigravity", exeName),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                SysProcess.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                return true;
            }
        }
        return false;
    }

    private static bool LaunchOnLinux(string ide)
    {
        var binary = ide switch
        {
            "cursor"      => "cursor",
            "vscode"      => "code",
            "antigravity" => "antigravity",
            _             => null,
        };
        if (binary is null) return false;
        try
        {
            SysProcess.Start(new ProcessStartInfo
            {
                FileName = binary,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
