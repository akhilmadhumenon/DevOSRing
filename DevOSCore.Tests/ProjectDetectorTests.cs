using System;
using System.IO;
using DevOSRing.Core.Tests;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

public class ProjectDetectorTests : IDisposable
{
    private readonly string _root;

    public ProjectDetectorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "devosring-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* */ }
    }

    [Fact]
    public void Detects_dotnet_when_sln_present()
    {
        File.WriteAllText(Path.Combine(_root, "App.sln"), "");
        var p = ProjectDetector.Detect(_root);
        Assert.Equal(ProjectKind.Dotnet, p.Kind);
        Assert.StartsWith("test", p.Arguments);
    }

    [Fact]
    public void Detects_cargo_when_Cargo_toml_present()
    {
        File.WriteAllText(Path.Combine(_root, "Cargo.toml"), "[package]");
        Assert.Equal(ProjectKind.Cargo, ProjectDetector.Detect(_root).Kind);
    }

    [Fact]
    public void Detects_pnpm_over_npm_when_lockfile_present()
    {
        File.WriteAllText(Path.Combine(_root, "package.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "pnpm-lock.yaml"), "");
        Assert.Equal(ProjectKind.Pnpm, ProjectDetector.Detect(_root).Kind);
    }

    [Fact]
    public void Detects_pytest_via_pyproject_toml()
    {
        File.WriteAllText(Path.Combine(_root, "pyproject.toml"), "[tool.pytest.ini_options]");
        Assert.Equal(ProjectKind.Pytest, ProjectDetector.Detect(_root).Kind);
    }

    [Fact]
    public void Falls_back_to_Unknown_when_nothing_matches()
    {
        Assert.Equal(ProjectKind.Unknown, ProjectDetector.Detect(_root).Kind);
    }
}
