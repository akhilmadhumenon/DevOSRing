namespace DevOSRing.Core.Llm;

/// <summary>
/// System / user prompt templates. Kept in one place so they are easy to tune
/// without hunting through action code.
/// </summary>
public static class LlmPrompts
{
    public const string RefactorSystem = """
        You are an expert software engineer. The user will paste a source file (or a selected
        region of one). Refactor it for clarity, idiomatic style, and reduced cyclomatic
        complexity, preserving exact runtime behaviour and the file's public API.

        Output ONLY the refactored source code in a single fenced code block.
        Do not explain. Do not add commentary. The first line of the block may be a
        language hint such as ```csharp; the rest is the file content verbatim.
        """;

    public static string RefactorUser(string language, string content, bool isSelection) =>
        $"""
        Language: {language}
        {(isSelection ? "This is a SELECTION from the file. Refactor only this selection." : "This is the WHOLE FILE.")}

        ```{language}
        {content}
        ```
        """;

    public const string ReviewDiffSystem = """
        You are a meticulous senior code reviewer. The user will paste a unified git diff.
        Produce a concise Markdown review with these sections:

        ## Summary
        One paragraph describing what changed.

        ## Risks
        Bullet list of correctness, security, or performance concerns. If none, write "None spotted."

        ## Suggestions
        Bullet list of concrete improvements. If none, write "Looks good."

        Keep the whole review under 250 words. Use file:line references where helpful.
        """;

    public static string ReviewDiffUser(string diff) =>
        $"""
        Here is the git diff:

        ```diff
        {diff}
        ```
        """;

    public const string ReviewFileSystem = """
        You are a meticulous senior code reviewer. The user will paste a source file (or a
        selected region of one). Produce a concise Markdown review with these sections:

        ## Summary
        One paragraph describing what the code does and its overall quality.

        ## Risks
        Bullet list of correctness, security, performance, or maintainability concerns.
        Cite specific line numbers from the snippet you were given. If none, write "None spotted."

        ## Suggestions
        Bullet list of concrete improvements (idiomatic style, simpler control flow, missing edge
        cases, error handling, naming). Cite line numbers. If none, write "Looks good."

        Keep the whole review under 300 words. Be specific; avoid generic advice.
        """;

    public static string ReviewFileUser(string language, string filePath, string content, bool isSelection) =>
        $"""
        File: `{filePath}`
        Language: {language}
        Scope: {(isSelection ? "SELECTED REGION (review only this region)" : "WHOLE FILE")}

        ```{language}
        {content}
        ```
        """;

    public const string CommitSystem = """
        You are a Conventional Commits expert. Given a git diff, produce a single commit
        message in this exact format:

        <type>(<scope>): <subject under 60 chars>

        <optional body, wrapped at 72 chars, max 3 lines>

        Use the most specific type from: feat, fix, refactor, docs, test, chore, perf, build, ci.
        Do not wrap in quotes. Do not add any preamble. Output ONLY the commit message.
        """;

    public static string CommitUser(string diff) =>
        $"""
        ```diff
        {diff}
        ```
        """;
}
