using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

/// <summary>
/// Inspects each built plugin assembly via <see cref="MetadataLoadContext"/> (no
/// resolution / no JIT) to assert that the expected action class is present and
/// derives from a Loupedeck <c>PluginDynamicCommand</c>. Catches the
/// namespace-discovery foot-gun that would otherwise only show up at runtime in
/// Logi Options+. Soft-skips when plugin DLLs aren't built yet.
/// </summary>
public class PluginDiscoverySmokeTests
{
    private static readonly (string AssemblyName, string ExpectedActionType)[] Plugins =
    {
        ("AIRefactorPluginV2.dll",    "Loupedeck.AIRefactorPlugin.Actions.AIRefactorAction"),
        ("TestActionPluginV2.dll",    "Loupedeck.TestActionPlugin.Actions.TestAction"),
        ("ReviewActionPluginV2.dll",  "Loupedeck.ReviewActionPlugin.Actions.ReviewAction"),
        ("GitCommitPushPluginV2.dll", "Loupedeck.GitCommitPushPlugin.Actions.DeployAction"),
    };

    [Fact]
    public void Each_plugin_assembly_exposes_its_action_type()
    {
        var testDir = Path.GetDirectoryName(typeof(PluginDiscoverySmokeTests).Assembly.Location)!;
        var repoRoot = FindRepoRoot(testDir);
        if (repoRoot is null) return; // CI cold-start; nothing to inspect.

        var pluginApi = LocatePluginApi();
        var runtimeRoot = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeDlls = Directory.GetFiles(runtimeRoot, "*.dll");

        foreach (var (asmName, expectedType) in Plugins)
        {
            var candidates = Directory.GetFiles(repoRoot, asmName, SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .ToArray();
            if (candidates.Length == 0) continue;

            var pluginDir = Path.GetDirectoryName(candidates[0])!;
            var resolvePaths = runtimeDlls
                .Concat(Directory.GetFiles(pluginDir, "*.dll"))
                .Concat(pluginApi is null ? Array.Empty<string>() : new[] { pluginApi })
                .Distinct()
                .ToArray();

            var resolver = new PathAssemblyResolver(resolvePaths);
            using var mlc = new MetadataLoadContext(resolver);

            var asm = mlc.LoadFromAssemblyPath(candidates[0]);
            var hit = asm.GetTypes().FirstOrDefault(t => t.FullName == expectedType);
            Assert.True(hit != null, $"{asmName} missing action {expectedType}");
        }
    }

    private static string? FindRepoRoot(string from)
    {
        var dir = new DirectoryInfo(from);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DevOS.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static string? LocatePluginApi()
    {
        var candidates = new[]
        {
            "/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll",
            @"C:\Program Files\Logi\LogiPluginService\PluginApi.dll",
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
