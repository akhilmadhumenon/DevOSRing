using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevOSRing.Core.Llm.Canned;

/// <summary>
/// LLM-less commit-message generator. Inspects a diff (file paths + counts) and produces
/// a Conventional Commits-flavoured message that is deterministic and never empty.
/// </summary>
public static class CannedCommitMessage
{
    private static readonly Regex FilePlusPattern = new(@"^\+\+\+\s+b/(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);

    public static string From(string diff)
    {
        if (string.IsNullOrWhiteSpace(diff))
            return "chore: empty commit (no diff)";

        var files = FilePlusPattern.Matches(diff)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(p => !p.Equals("/dev/null", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        var added = diff.Split('\n').Count(l => l.StartsWith("+") && !l.StartsWith("+++"));
        var removed = diff.Split('\n').Count(l => l.StartsWith("-") && !l.StartsWith("---"));

        var type = GuessType(files, added, removed);
        var scope = GuessScope(files);
        var subject = GuessSubject(files, added, removed);

        var header = string.IsNullOrEmpty(scope)
            ? $"{type}: {subject}"
            : $"{type}({scope}): {subject}";

        if (header.Length > 72) header = header.Substring(0, 71) + "…";

        var body = $"Touched {files.Count} file(s), +{added}/-{removed} lines.";
        return $"{header}\n\n{body}\n";
    }

    private static string GuessType(IReadOnlyCollection<string> files, int added, int removed)
    {
        if (files.All(IsTestFile))   return "test";
        if (files.All(IsDocFile))    return "docs";
        if (files.All(IsBuildFile))  return "build";
        if (added > 0 && removed == 0) return "feat";
        if (added == 0 && removed > 0) return "chore";
        if (added > 0 && removed > 0 && added <= removed) return "refactor";
        return "chore";
    }

    private static string? GuessScope(IReadOnlyCollection<string> files)
    {
        if (files.Count == 0) return null;
        if (files.Count == 1)
        {
            var dir = Path.GetDirectoryName(files.First())?.Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? null : LastSegment(dir);
        }
        var common = LongestCommonDirectory(files);
        return string.IsNullOrEmpty(common) ? null : LastSegment(common);
    }

    private static string GuessSubject(IReadOnlyCollection<string> files, int added, int removed)
    {
        if (files.Count == 1)
        {
            var name = Path.GetFileNameWithoutExtension(files.First());
            return $"update {name}";
        }
        return $"update {files.Count} files (+{added}/-{removed})";
    }

    private static bool IsTestFile(string p) =>
        p.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("/test/",  StringComparison.OrdinalIgnoreCase) ||
        p.Contains(".test.",  StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".spec.js", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsDocFile(string p) =>
        p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".rst", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildFile(string p) =>
        p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
        p.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

    private static string LongestCommonDirectory(IEnumerable<string> files)
    {
        var dirs = files.Select(f => Path.GetDirectoryName(f)?.Replace('\\', '/') ?? "").ToList();
        if (dirs.Count == 0) return "";
        var split = dirs.Select(d => d.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToList();
        var minLen = split.Min(s => s.Length);
        var common = new List<string>();
        for (int i = 0; i < minLen; i++)
        {
            var first = split[0][i];
            if (split.All(s => s[i] == first)) common.Add(first);
            else break;
        }
        return string.Join('/', common);
    }

    private static string LastSegment(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? path : parts[^1];
    }
}
