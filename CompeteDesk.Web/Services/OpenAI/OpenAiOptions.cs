namespace CompeteDesk.Services.OpenAI;

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Example: "gpt-4o-mini" or "gpt-4.1-mini".
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Optional system instruction for general-purpose, non-JSON responses
    /// (e.g., Topbar AI Search).
    /// </summary>
    public string SystemInstruction { get; set; } =
        "You are an assistant integrated into an app search bar. Answer the user's query with concise, practical information. If the query is ambiguous, ask one brief clarifying question.";

    /// <summary>
    /// Max tokens for non-JSON responses.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 512;
}
