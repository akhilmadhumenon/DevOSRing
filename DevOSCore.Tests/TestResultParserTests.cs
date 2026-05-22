using DevOSRing.Core.Tests;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

public class TestResultParserTests
{
    [Fact]
    public void Parses_dotnet_vstest_summary()
    {
        var stdout = """
        Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 12 ms
        """;
        var s = TestResultParser.Parse(stdout, exitCode: 0);
        Assert.Equal(3, s.Passed);
        Assert.Equal(0, s.Failed);
        Assert.Equal(0, s.Skipped);
        Assert.Equal(3, s.Total);
    }

    [Fact]
    public void Parses_pytest_summary()
    {
        var stdout = "============ 12 passed, 1 failed, 2 skipped in 0.42s ============";
        var s = TestResultParser.Parse(stdout, exitCode: 1);
        Assert.Equal(12, s.Passed);
        Assert.Equal(1, s.Failed);
        Assert.Equal(2, s.Skipped);
    }

    [Fact]
    public void Parses_jest_summary()
    {
        var stdout = "Tests:       1 failed, 4 passed, 5 total";
        var s = TestResultParser.Parse(stdout);
        Assert.Equal(4, s.Passed);
        Assert.Equal(1, s.Failed);
        Assert.Equal(5, s.Total);
    }

    [Fact]
    public void Parses_mocha_summary()
    {
        var stdout = """
          7 passing (123ms)
          2 failing
          1 pending
        """;
        var s = TestResultParser.Parse(stdout);
        Assert.Equal(7, s.Passed);
        Assert.Equal(2, s.Failed);
        Assert.Equal(1, s.Skipped);
        Assert.Equal(10, s.Total);
    }

    [Fact]
    public void Empty_output_with_zero_exit_returns_no_results()
    {
        var s = TestResultParser.Parse("", exitCode: 0);
        Assert.False(s.HasAnyResult);
    }

    [Fact]
    public void Empty_output_with_nonzero_exit_records_one_failure()
    {
        var s = TestResultParser.Parse("", exitCode: 137);
        Assert.Equal(1, s.Failed);
    }
}
