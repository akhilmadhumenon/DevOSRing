using DevOSRing.Core.Llm.Canned;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

public class CannedReviewTests
{
    [Fact]
    public void Empty_diff_says_clean()
    {
        var md = CannedReview.Summarise("");
        Assert.Contains("Working tree clean", md);
    }

    [Fact]
    public void Counts_added_and_removed_lines()
    {
        var diff = """
            diff --git a/a.txt b/a.txt
            --- a/a.txt
            +++ b/a.txt
            @@
            -old
            +new
            +second new line
            """;
        var md = CannedReview.Summarise(diff);
        Assert.Contains("+2", md);
        Assert.Contains("-1", md);
        Assert.Contains("a.txt", md);
    }

    [Fact]
    public void Detects_TODO_and_console_log_risks()
    {
        var diff = """
            +++ b/foo.js
            +// TODO: revisit
            +console.log("debug")
            """;
        var md = CannedReview.Summarise(diff);
        Assert.Contains("TODO/FIXME/HACK", md);
        Assert.Contains("console.log", md);
    }

    [Fact]
    public void Detects_possible_hardcoded_secret()
    {
        var diff = "+++ b/cfg.ts\n+const apiKey = \"sk-veryrealkey123456\"\n";
        var md = CannedReview.Summarise(diff);
        Assert.Contains("hardcoded secret", md);
    }
}
