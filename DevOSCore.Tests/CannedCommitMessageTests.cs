using DevOSRing.Core.Llm.Canned;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

public class CannedCommitMessageTests
{
    [Fact]
    public void Empty_diff_produces_chore_empty_commit()
    {
        var msg = CannedCommitMessage.From("");
        Assert.StartsWith("chore", msg);
    }

    [Fact]
    public void Single_test_file_yields_test_type()
    {
        var diff = """
            +++ b/tests/UserTests.cs
            +new test
            """;
        var msg = CannedCommitMessage.From(diff);
        Assert.StartsWith("test", msg);
        Assert.Contains("UserTests", msg);
    }

    [Fact]
    public void Multiple_files_use_common_directory_as_scope()
    {
        var diff = """
            +++ b/src/api/auth.ts
            +diff
            +++ b/src/api/users.ts
            +diff
            """;
        var msg = CannedCommitMessage.From(diff);
        Assert.Contains("(api)", msg);
    }

    [Fact]
    public void Header_is_capped_at_72_chars()
    {
        var paths = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"+++ b/somewhere/file_with_a_long_name_{i}.ts"));
        var msg = CannedCommitMessage.From(paths);
        var firstLine = msg.Split('\n')[0];
        Assert.True(firstLine.Length <= 72);
    }
}
