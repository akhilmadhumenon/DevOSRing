using System;
using System.IO;
using System.Threading.Tasks;
using DevOSRing.Core.Git;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

/// <summary>
/// Smoke tests against a real temp git repo. Skipped when the `git` binary is
/// not on PATH so the test suite can still run in minimal CI containers.
/// </summary>
public class GitOpsTests : IDisposable
{
    private readonly string _root;
    private readonly bool _gitAvailable;

    public GitOpsTests()
    {
        _gitAvailable = ProcessExists("git");
        _root = Path.Combine(Path.GetTempPath(), "devosring-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* */ }
    }

    [Fact]
    public async Task IsRepo_false_for_plain_dir()
    {
        if (!_gitAvailable) return; // soft-skip when git not installed
        var git = new GitOps();
        Assert.False(await git.IsRepoAsync(_root));
    }

    [Fact]
    public async Task After_init_IsRepo_is_true_and_root_resolves()
    {
        if (!_gitAvailable) return;
        await Run("git", "init -q", _root);
        var git = new GitOps();
        Assert.True(await git.IsRepoAsync(_root));
        var root = await git.GetRootAsync(_root);
        Assert.NotNull(root);
    }

    [Fact]
    public async Task Detects_changes_after_creating_a_file()
    {
        if (!_gitAvailable) return;
        await Run("git", "init -q", _root);
        await Run("git", "config user.email devos@example.com", _root);
        await Run("git", "config user.name DevOS", _root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello\n");
        var git = new GitOps();
        Assert.True(await git.HasChangesAsync(_root));
    }

    private static bool ProcessExists(string name)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(name, "--version")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p!.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task Run(string file, string args, string cwd)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(file, args)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        await p.WaitForExitAsync();
    }
}

