using System;
using System.Text.RegularExpressions;

namespace DevOSRing.Core.Tests;

public readonly record struct TestSummary(int Passed, int Failed, int Skipped, int Total)
{
    public bool HasAnyResult => Total > 0 || Passed > 0 || Failed > 0 || Skipped > 0;
    public string Short => HasAnyResult
        ? (Failed == 0 ? $"{Passed} pass" : $"{Passed} pass, {Failed} fail")
        : "no results";
}

/// <summary>
/// Parses test runner output (vstest / xunit / npm / pytest) heuristically into a
/// <see cref="TestSummary"/>. We sniff several known patterns and pick the most
/// specific one that matches; falls back to <see cref="TestSummary"/> with all zeros.
/// </summary>
public static class TestResultParser
{
    // dotnet test (vstest) "Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3"
    private static readonly Regex Vstest = new(
        @"Failed:\s*(?<failed>\d+),\s*Passed:\s*(?<passed>\d+),\s*Skipped:\s*(?<skipped>\d+),\s*Total:\s*(?<total>\d+)",
        RegexOptions.Compiled);

    // pytest summary "===== 12 passed, 1 failed, 2 skipped in 0.42s ====="
    private static readonly Regex PytestPass = new(@"(?<n>\d+)\s+passed",  RegexOptions.Compiled);
    private static readonly Regex PytestFail = new(@"(?<n>\d+)\s+failed",  RegexOptions.Compiled);
    private static readonly Regex PytestSkip = new(@"(?<n>\d+)\s+skipped", RegexOptions.Compiled);
    private static readonly Regex PytestErr  = new(@"(?<n>\d+)\s+error",   RegexOptions.Compiled);

    // jest "Tests:       1 failed, 4 passed, 5 total"
    private static readonly Regex Jest = new(
        @"Tests:\s*(?:(?<failed>\d+)\s+failed,\s*)?(?:(?<passed>\d+)\s+passed,?\s*)?(?:(?<skipped>\d+)\s+skipped,?\s*)?(?<total>\d+)\s+total",
        RegexOptions.Compiled);

    // mocha "  5 passing\n  2 failing\n  1 pending"
    private static readonly Regex MochaPass = new(@"(?<n>\d+)\s+passing", RegexOptions.Compiled);
    private static readonly Regex MochaFail = new(@"(?<n>\d+)\s+failing", RegexOptions.Compiled);
    private static readonly Regex MochaSkip = new(@"(?<n>\d+)\s+pending", RegexOptions.Compiled);

    public static TestSummary Parse(string stdout, string stderr = "", int exitCode = 0)
    {
        var combined = (stdout ?? "") + "\n" + (stderr ?? "");

        var m = Vstest.Match(combined);
        if (m.Success)
        {
            return new TestSummary(
                Passed: int.Parse(m.Groups["passed"].Value),
                Failed: int.Parse(m.Groups["failed"].Value),
                Skipped: int.Parse(m.Groups["skipped"].Value),
                Total: int.Parse(m.Groups["total"].Value));
        }

        m = Jest.Match(combined);
        if (m.Success)
        {
            return new TestSummary(
                Passed: SafeInt(m.Groups["passed"].Value),
                Failed: SafeInt(m.Groups["failed"].Value),
                Skipped: SafeInt(m.Groups["skipped"].Value),
                Total: SafeInt(m.Groups["total"].Value));
        }

        var p = MochaPass.Match(combined);
        var f = MochaFail.Match(combined);
        var s = MochaSkip.Match(combined);
        if (p.Success || f.Success)
        {
            var passed = SafeInt(p.Groups["n"].Value);
            var failed = SafeInt(f.Groups["n"].Value);
            var skipped = SafeInt(s.Groups["n"].Value);
            return new TestSummary(passed, failed, skipped, passed + failed + skipped);
        }

        var pp = PytestPass.Match(combined);
        var pf = PytestFail.Match(combined);
        var ps = PytestSkip.Match(combined);
        var pe = PytestErr.Match(combined);
        if (pp.Success || pf.Success || ps.Success || pe.Success)
        {
            var passed  = SafeInt(pp.Groups["n"].Value);
            var failed  = SafeInt(pf.Groups["n"].Value) + SafeInt(pe.Groups["n"].Value);
            var skipped = SafeInt(ps.Groups["n"].Value);
            return new TestSummary(passed, failed, skipped, passed + failed + skipped);
        }

        return exitCode == 0
            ? new TestSummary(0, 0, 0, 0)
            : new TestSummary(0, 1, 0, 1);
    }

    private static int SafeInt(string s) => int.TryParse(s, out var n) ? n : 0;
}
