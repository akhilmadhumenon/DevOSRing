using System;
using System.IO;
using System.Linq;

namespace DevOSRing.Core.Tests;

public enum ProjectKind { Unknown, Dotnet, Npm, Pnpm, Yarn, Pytest, Maven, Gradle, Cargo, Go }

public sealed record TestPlan(ProjectKind Kind, string FileName, string Arguments, string WorkingDirectory)
{
    public bool IsRunnable => Kind != ProjectKind.Unknown;
}

/// <summary>
/// Walks a workspace root and decides which test command to invoke. Detection is
/// deliberately conservative: prefer the most specific signal (lockfile / project file)
/// over the generic one (raw <c>package.json</c>).
/// </summary>
public static class ProjectDetector
{
    public static TestPlan Detect(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return Unknown(workspaceRoot ?? Environment.CurrentDirectory);

        if (HasAny(workspaceRoot, "*.sln") || HasAny(workspaceRoot, "*.csproj", recursive: true))
            return new(ProjectKind.Dotnet, "dotnet", "test --nologo --verbosity quiet", workspaceRoot);

        if (HasAny(workspaceRoot, "Cargo.toml"))
            return new(ProjectKind.Cargo, "cargo", "test --quiet", workspaceRoot);

        if (HasAny(workspaceRoot, "go.mod"))
            return new(ProjectKind.Go, "go", "test ./...", workspaceRoot);

        if (HasAny(workspaceRoot, "pom.xml"))
            return new(ProjectKind.Maven, "mvn", "-q test", workspaceRoot);

        if (HasAny(workspaceRoot, "build.gradle") || HasAny(workspaceRoot, "build.gradle.kts"))
            return new(ProjectKind.Gradle, OsExe("gradle"), "test --console=plain", workspaceRoot);

        if (HasAny(workspaceRoot, "pytest.ini") || HasAny(workspaceRoot, "pyproject.toml") ||
            HasAny(workspaceRoot, "conftest.py") || HasAny(workspaceRoot, "setup.cfg"))
            return new(ProjectKind.Pytest, OsExe("pytest"), "-q", workspaceRoot);

        if (File.Exists(Path.Combine(workspaceRoot, "pnpm-lock.yaml")))
            return new(ProjectKind.Pnpm, OsExe("pnpm"), "test --silent", workspaceRoot);
        if (File.Exists(Path.Combine(workspaceRoot, "yarn.lock")))
            return new(ProjectKind.Yarn, OsExe("yarn"), "test --silent", workspaceRoot);
        if (File.Exists(Path.Combine(workspaceRoot, "package.json")))
            return new(ProjectKind.Npm,  OsExe("npm"),  "test --silent", workspaceRoot);

        return Unknown(workspaceRoot);
    }

    private static TestPlan Unknown(string cwd) => new(ProjectKind.Unknown, "", "", cwd);

    private static bool HasAny(string root, string pattern, bool recursive = false)
    {
        try
        {
            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(root, pattern, opt).Take(1).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string OsExe(string name) =>
        OperatingSystem.IsWindows() ? $"{name}.cmd" : name;
}
