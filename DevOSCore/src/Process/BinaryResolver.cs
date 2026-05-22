using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevOSRing.Core.Process;

/// <summary>
/// Resolves a short tool name (e.g. <c>dotnet</c>, <c>npm</c>, <c>pytest</c>) to a
/// fully-qualified executable path. Needed because macOS apps launched from
/// <c>/Applications</c> get a stripped PATH (no Homebrew, no /usr/local/share/dotnet),
/// so <c>Process.Start("dotnet", ...)</c> fails with "No such file or directory" inside
/// LogiPluginService.
///
/// Resolution order:
/// <list type="number">
///   <item>If the name already contains a directory separator, return it as-is.</item>
///   <item>Probe a list of well-known install locations per tool.</item>
///   <item>Ask the user's login shell where it is: <c>zsh -lc 'command -v &lt;name&gt;'</c>.</item>
///   <item>Fall back to the original name (Process.Start will surface the failure).</item>
/// </list>
/// Successful lookups are memoised for the process lifetime.
/// </summary>
public static class BinaryResolver
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Well-known absolute paths for common dev-tools, in priority order.
    /// Hits here are the fastest (no shell spawn).
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownPaths = new(StringComparer.Ordinal)
    {
        ["dotnet"] = new[]
        {
            "/usr/local/share/dotnet/dotnet",
            "/opt/homebrew/bin/dotnet",
            "/opt/homebrew/share/dotnet/dotnet",
            "/usr/local/bin/dotnet",
        },
        ["node"]   = new[] { "/usr/local/bin/node",   "/opt/homebrew/bin/node",   "/usr/local/opt/node/bin/node" },
        ["npm"]    = new[] { "/usr/local/bin/npm",    "/opt/homebrew/bin/npm" },
        ["pnpm"]   = new[] { "/usr/local/bin/pnpm",   "/opt/homebrew/bin/pnpm" },
        ["yarn"]   = new[] { "/usr/local/bin/yarn",   "/opt/homebrew/bin/yarn" },
        ["python"] = new[] { "/usr/local/bin/python", "/opt/homebrew/bin/python", "/usr/bin/python3" },
        ["python3"]= new[] { "/usr/local/bin/python3","/opt/homebrew/bin/python3","/usr/bin/python3" },
        ["pytest"] = new[] { "/usr/local/bin/pytest", "/opt/homebrew/bin/pytest" },
        ["cargo"]  = new[] { Path.Combine(HomeDir, ".cargo", "bin", "cargo"), "/usr/local/bin/cargo", "/opt/homebrew/bin/cargo" },
        ["go"]     = new[] { "/usr/local/go/bin/go",  "/opt/homebrew/bin/go",   "/usr/local/bin/go" },
        ["mvn"]    = new[] { "/usr/local/bin/mvn",    "/opt/homebrew/bin/mvn" },
        ["gradle"] = new[] { "/usr/local/bin/gradle", "/opt/homebrew/bin/gradle" },
        ["git"]    = new[] { "/usr/local/bin/git",    "/opt/homebrew/bin/git",   "/usr/bin/git" },
    };

    private static string HomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Returns the absolute path to <paramref name="name"/> or <paramref name="name"/>
    /// itself if no resolution was possible. Never throws.
    /// </summary>
    public static string Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        if (name.Contains(Path.DirectorySeparatorChar) || name.Contains('/')) return name;

        if (Cache.TryGetValue(name, out var cached)) return cached;

        var resolved = ProbeKnown(name) ?? AskLoginShell(name) ?? name;
        Cache[name] = resolved;
        return resolved;
    }

    /// <summary>
    /// Returns the directories that should be on PATH for tools like <c>dotnet</c>
    /// to find their helpers (e.g. <c>dotnet</c> needs <c>node</c> on PATH for some
    /// SDKs). Used by <see cref="ProcessRunner"/> to augment the child env block.
    /// </summary>
    public static IReadOnlyList<string> ExtraPathDirs()
    {
        var dirs = new List<string>
        {
            "/usr/local/share/dotnet",
            "/usr/local/bin",
            "/opt/homebrew/bin",
            "/opt/homebrew/sbin",
            Path.Combine(HomeDir, ".dotnet"),
            Path.Combine(HomeDir, ".cargo", "bin"),
            Path.Combine(HomeDir, "go", "bin"),
            "/usr/local/go/bin",
            "/usr/bin",
            "/bin",
            "/usr/sbin",
            "/sbin",
        };
        return dirs.Where(Directory.Exists).Distinct(StringComparer.Ordinal).ToList();
    }

    private static string? ProbeKnown(string name)
    {
        if (!KnownPaths.TryGetValue(name, out var candidates)) return null;
        foreach (var p in candidates)
        {
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string? AskLoginShell(string name)
    {
        // We use the user's login shell (typically zsh on modern macOS) so that ~/.zshrc /
        // ~/.zprofile augmentations to PATH (e.g. nvm, asdf, mise, conda) are honoured.
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (string.IsNullOrWhiteSpace(shell)) shell = "/bin/zsh";

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = shell,
                // -l: login shell so profile is loaded. -c: run the given command.
                Arguments = $"-lc \"command -v {EscapeForShell(name)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return null;

            // Cap the wait to 2s — if a user's profile is slow to source we don't want
            // to deadlock the action button.
            if (!p.WaitForExit(2_000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return null;
            }

            var stdout = p.StandardOutput.ReadToEnd().Trim();
            if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout)) return null;

            // command -v can emit shell builtins / aliases too; only accept real files.
            return File.Exists(stdout) ? stdout : null;
        }
        catch
        {
            return null;
        }
    }

    private static string EscapeForShell(string s) => s.Replace("\"", "\\\"");
}
