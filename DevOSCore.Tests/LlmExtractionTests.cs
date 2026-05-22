using DevOSRing.Core.Llm;
using Xunit;

namespace DevOSRing.Core.Tests.UnitTests;

public class LlmExtractionTests
{
    [Fact]
    public void Extracts_first_fenced_block()
    {
        var raw = """
            Sure! Here's the refactored code:

            ```csharp
            public class A {}
            ```

            Hope that helps.
            """;
        var code = LlmExtraction.ExtractCode(raw);
        Assert.Equal("public class A {}\n", code);
    }

    [Fact]
    public void Strips_preamble_when_no_fence()
    {
        var raw = """
            Here is the refactored code:
            public class A {}
            """;
        var code = LlmExtraction.ExtractCode(raw);
        Assert.StartsWith("public class A", code);
    }

    [Fact]
    public void Empty_returns_empty()
    {
        Assert.Equal("", LlmExtraction.ExtractCode(""));
        Assert.Equal("", LlmExtraction.ExtractCode("   \n"));
    }
}
