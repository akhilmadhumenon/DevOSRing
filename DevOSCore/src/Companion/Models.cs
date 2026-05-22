using System.Text.Json.Serialization;

namespace DevOSRing.Core.Companion;

/// <summary>Editor / workspace snapshot returned by <c>GET /v1/context</c>.</summary>
public sealed record WorkspaceContext
{
    [JsonPropertyName("activeFilePath")] public string? ActiveFilePath { get; init; }
    [JsonPropertyName("workspaceRoot")]  public string? WorkspaceRoot { get; init; }
    [JsonPropertyName("language")]       public string? Language { get; init; }
    [JsonPropertyName("selection")]      public Selection? Selection { get; init; }
    [JsonPropertyName("gitRoot")]        public string? GitRoot { get; init; }
    [JsonPropertyName("gitBranch")]      public string? GitBranch { get; init; }
    [JsonPropertyName("isDirty")]        public bool IsDirty { get; init; }
    [JsonPropertyName("ide")]            public string Ide { get; init; } = "unknown";
}

public sealed record Selection
{
    [JsonPropertyName("text")]      public string Text { get; init; } = string.Empty;
    [JsonPropertyName("startLine")] public int StartLine { get; init; }
    [JsonPropertyName("endLine")]   public int EndLine { get; init; }
    [JsonPropertyName("isEmpty")]   public bool IsEmpty { get; init; }
}

public sealed record DiffRequest
{
    [JsonPropertyName("path")]           public string Path { get; init; } = string.Empty;
    [JsonPropertyName("refactoredText")] public string RefactoredText { get; init; } = string.Empty;
    [JsonPropertyName("title")]          public string Title { get; init; } = "DevOS Refactor";
}

public sealed record DiffResponse
{
    [JsonPropertyName("accepted")] public bool Accepted { get; init; }
}

public sealed record ApplyRequest
{
    [JsonPropertyName("path")] public string Path { get; init; } = string.Empty;
    [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
}

public sealed record ReviewRequest
{
    [JsonPropertyName("markdown")] public string Markdown { get; init; } = string.Empty;
    [JsonPropertyName("title")]    public string Title { get; init; } = "DevOS AI Review";
}

public sealed record NotifyRequest
{
    [JsonPropertyName("level")]   public string Level { get; init; } = "info"; // info | warning | error
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
