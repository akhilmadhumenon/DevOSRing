using System;
using System.IO;

namespace DevOSRing.Core.Hosting;

/// <summary>
/// Maps file extensions to language hints used in LLM prompts and the canned refactorer.
/// </summary>
public static class Languages
{
    public static string FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "text";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".cs"   => "csharp",
            ".ts"   => "typescript",
            ".tsx"  => "typescript",
            ".js"   => "javascript",
            ".jsx"  => "javascript",
            ".mjs"  => "javascript",
            ".py"   => "python",
            ".rb"   => "ruby",
            ".rs"   => "rust",
            ".go"   => "go",
            ".java" => "java",
            ".kt"   => "kotlin",
            ".swift"=> "swift",
            ".php"  => "php",
            ".cpp" or ".cc" or ".cxx" or ".hpp" or ".h" => "cpp",
            ".c"    => "c",
            ".sh"   => "bash",
            ".ps1"  => "powershell",
            ".sql"  => "sql",
            ".html" => "html",
            ".css"  => "css",
            ".scss" => "scss",
            ".json" => "json",
            ".yml" or ".yaml" => "yaml",
            ".md"   => "markdown",
            ".xml"  => "xml",
            _       => "text",
        };
    }
}
