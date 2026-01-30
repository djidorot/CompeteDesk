using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace CompeteDesk.Services.Gemini;

/// <summary>
/// Minimal Gemini Developer API client (no external SDK).
/// Uses: POST https://generativelanguage.googleapis.com/{ApiVersion}/models/{model}:generateContent?key=...
/// </summary>
public sealed class GeminiClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _opt;

    public GeminiClient(HttpClient http, IOptions<GeminiOptions> opt)
    {
        _http = http;
        _opt = opt.Value ?? new GeminiOptions();
    }

    /// <summary>
    /// True if an API key is present.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opt.ApiKey);

    /// <summary>
    /// Backward-compatible method used by AiSearchController from the initial patch.
    /// </summary>
    public Task<string> GenerateForSearchAsync(string query, CancellationToken ct = default)
        => GenerateAsync(query, ct);

    /// <summary>
    /// Generates an "AI Overview"-style response (definition + key aspects + examples)
    /// as a strict JSON object so the UI can render it predictably.
    /// </summary>
    public async Task<string> GenerateAiOverviewJsonAsync(string query, CancellationToken ct = default)
    {
        // We intentionally do NOT ask a follow-up question for ambiguous single-word queries.
        // Default to the most common meaning and present an overview like Google "AI Overview".
        var prompt =
            "Create an AI Overview for the query below. " +
            "Return ONLY valid JSON with this exact schema (no markdown, no code fences):\n" +
            "{\n" +
            "  \"topic\": string,\n" +
            "  \"overview\": string,\n" +
            "  \"keyAspects\": string[],\n" +
            "  \"examples\": string[]\n" +
            "}\n\n" +
            "Rules:\n" +
            "- overview: 1-2 sentences.\n" +
            "- keyAspects: 3-6 bullets (short phrases).\n" +
            "- examples: 2-5 bullets.\n" +
            "- If the query is broad, assume the most common general meaning (do NOT ask clarifying questions).\n\n" +
            $"Query: {query.Trim()}";

        return await GenerateAsync(prompt, ct);
    }

    public async Task<string> GenerateAsync(string userQuery, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            throw new ArgumentException("Query is required.", nameof(userQuery));

        if (string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new InvalidOperationException("Gemini API key is missing. Set Gemini:ApiKey in configuration.");

        var model = NormalizeModel(_opt.Model);
        var apiVersion = string.IsNullOrWhiteSpace(_opt.ApiVersion) ? "v1" : _opt.ApiVersion.Trim();

        // v1 endpoint fixes the common 404: "model not found for API version v1beta"
        var url = $"https://generativelanguage.googleapis.com/{apiVersion}/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(_opt.ApiKey)}";

        // NOTE:
        // Gemini REST schemas have changed across versions/models and some combinations reject
        // both "systemInstruction" and "system_instruction" with:
        //   "Invalid JSON payload received. Unknown name ...: Cannot find field."
        // To be maximally compatible, we do NOT send a dedicated system instruction field.
        // Instead, we prepend our instruction text to the user prompt.
        var sys = (_opt.SystemInstruction ?? string.Empty).Trim();
        var combinedPrompt = string.IsNullOrEmpty(sys)
            ? userQuery.Trim()
            : (sys + "\n\n" + userQuery.Trim());

        var payload = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = combinedPrompt } }
                }
            },
            ["generationConfig"] = new
            {
                temperature = _opt.Temperature,
                maxOutputTokens = _opt.MaxOutputTokens
            }
        };

        var json = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            // Bubble up the body - your UI shows it in the panel (good for debugging).
            throw new InvalidOperationException($"Gemini call failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }

        return ExtractText(body);
    }

    private static string NormalizeModel(string? model)
    {
        var m = (model ?? string.Empty).Trim();
        if (m.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            m = m.Substring("models/".Length);

        if (string.IsNullOrWhiteSpace(m))
            m = "gemini-2.5-flash";

        return m;
    }

    private static string ExtractText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
                return string.Empty;

            var cand0 = candidates[0];

            if (!cand0.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
                return string.Empty;

            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var p in parts.EnumerateArray())
            {
                if (p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(t.GetString());
                }
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
