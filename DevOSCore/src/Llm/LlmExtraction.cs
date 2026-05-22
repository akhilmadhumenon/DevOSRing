using System;
using System.Text.RegularExpressions;

namespace DevOSRing.Core.Llm;

/// <summary>
/// Defensive parsing of LLM output. The Refactor prompt instructs the model to return
/// exactly one fenced block, but real-world output often includes preamble / postscript
/// or omits the fence; this helper recovers gracefully.
/// </summary>
public static class LlmExtraction
{
    private static readonly Regex FenceRegex = new(
        @"```(?<lang>[a-zA-Z0-9_+\-]*)\r?\n(?<body>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns the first fenced code block body if present, otherwise the raw input
    /// stripped of obvious "Here is the refactored code:" preambles.
    /// </summary>
    public static string ExtractCode(string llmOutput)
    {
        if (string.IsNullOrWhiteSpace(llmOutput)) return string.Empty;
        var m = FenceRegex.Match(llmOutput);
        if (m.Success) return m.Groups["body"].Value.TrimEnd() + "\n";

        var lines = llmOutput.Replace("\r\n", "\n").Split('\n');
        var startIdx = 0;
        for (int i = 0; i < Math.Min(3, lines.Length); i++)
        {
            var l = lines[i].TrimStart();
            if (l.StartsWith("Here") || l.StartsWith("Sure") || l.StartsWith("Below") || l.EndsWith(":"))
                startIdx = i + 1;
        }
        return string.Join("\n", lines, startIdx, lines.Length - startIdx).TrimEnd() + "\n";
    }
}
