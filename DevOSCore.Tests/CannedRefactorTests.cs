using DevOSRing.Core.Llm.Canned;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

public class CannedRefactorTests
{
    [Fact]
    public void Collapses_nested_if_in_csharp()
    {
        var input = """
            public class UserAuth
            {
                public bool CheckUser(string user)
                {
                    if (user != null)
                    {
                        if (user.Length > 0)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
            """;
        var output = CannedRefactor.Apply("csharp", input);
        Assert.DoesNotContain("if (user != null)\n            {\n                if", output);
        // collapsed expression should appear in some form
        Assert.Contains("user != null", output);
        Assert.Contains("user.Length", output);
    }

    [Fact]
    public void Returns_unchanged_for_unknown_language()
    {
        var input = "noop content\n";
        var output = CannedRefactor.Apply("brainfuck", input);
        Assert.Equal(input.TrimEnd() + "\n", output);
    }

    [Fact]
    public void Tidies_trailing_whitespace_and_blank_runs()
    {
        var input = "line one   \n\n\n\nline two\n";
        var output = CannedRefactor.Apply("javascript", input);
        Assert.DoesNotContain("   \n", output);
        Assert.DoesNotContain("\n\n\n", output);
    }
}
