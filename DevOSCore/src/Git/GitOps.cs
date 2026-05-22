using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevOSRing.Core.Process;

namespace DevOSRing.Core.Git;

/// <summary>
/// Thin async wrapper over the <c>git</c> CLI. We deliberately shell out instead of
/// embedding LibGit2Sharp to honour the user's existing git config (credentials,
/// SSH agent, GPG signing, hooks, etc.) without re-implementing any of it.
/// </summary>
public sealed class GitOps
{
    private readonly ProcessRunner _runner;

    public GitOps(ProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner();
    }

    public async Task<bool> IsRepoAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        var r = await Git("rev-parse --is-inside-work-tree", path, ct);
        return r.Succeeded && r.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetRootAsync(string path, CancellationToken ct = default)
    {
        var r = await Git("rev-parse --show-toplevel", path, ct);
        return r.Succeeded ? r.StdOut.Trim() : null;
    }

    public async Task<string?> CurrentBranchAsync(string root, CancellationToken ct = default)
    {
        var r = await Git("rev-parse --abbrev-ref HEAD", root, ct);
        return r.Succeeded ? r.StdOut.Trim() : null;
    }

    public async Task<string?> RemoteUrlAsync(string root, string remote = "origin", CancellationToken ct = default)
    {
        var r = await Git($"remote get-url {remote}", root, ct);
        return r.Succeeded ? r.StdOut.Trim() : null;
    }

    public async Task<string> DiffAsync(string root, CancellationToken ct = default)
    {
        var r = await Git("diff", root, ct);
        return r.StdOut;
    }

    public async Task<string> DiffStagedAsync(string root, CancellationToken ct = default)
    {
        var r = await Git("diff --cached", root, ct);
        return r.StdOut;
    }

    public async Task<bool> HasChangesAsync(string root, CancellationToken ct = default)
    {
        var r = await Git("status --porcelain", root, ct);
        return r.Succeeded && !string.IsNullOrWhiteSpace(r.StdOut);
    }

    public async Task<ProcessResult> StageAllAsync(string root, CancellationToken ct = default)
        => await Git("add -A", root, ct);

    public async Task<ProcessResult> CommitAsync(string root, string message, CancellationToken ct = default)
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, message, ct);
            return await Git($"commit -F \"{tmp}\"", root, ct);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Pushes; if no upstream is configured, sets one via <c>--set-upstream</c>.
    /// </summary>
    public async Task<ProcessResult> PushAsync(string root, CancellationToken ct = default)
    {
        var first = await Git("push", root, ct);
        if (first.Succeeded) return first;

        var combined = (first.StdOut + first.StdErr);
        if (combined.Contains("no upstream branch", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("has no upstream", StringComparison.OrdinalIgnoreCase))
        {
            var branch = await CurrentBranchAsync(root, ct);
            if (!string.IsNullOrEmpty(branch))
            {
                return await Git($"push --set-upstream origin {branch}", root, ct);
            }
        }
        return first;
    }

    private Task<ProcessResult> Git(string args, string cwd, CancellationToken ct) =>
        _runner.RunAsync("git", args, cwd, TimeSpan.FromMinutes(2), cancellationToken: ct);
}
