using System;

namespace CompeteDesk.Services.Gemini;

/// <summary>
/// Configuration for Gemini Developer API (Google Generative Language API).
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>API key for Gemini Developer API.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API version segment. Use "v1" for current stable endpoints.
    /// </summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>
    /// Model id, e.g. "gemini-2.5-flash" or "gemini-1.5-flash".
    /// Do NOT include the "models/" prefix; we normalize if you do.
    /// </summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// System instruction to keep answers concise and helpful for "search".
    /// </summary>
    public string SystemInstruction { get; set; } =
        "You are an assistant integrated into an app search bar. " +
        "When the user enters a query, produce a Google-like 'AI Overview' style response. " +
        "Default to the most common meaning if the query is broad (do not ask clarifying questions). " +
        "Keep answers concise and structured.";

    public int MaxOutputTokens { get; set; } = 512;
    public double Temperature { get; set; } = 0.3;
}
