using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace CompeteDesk.Services.OpenAI;

/// <summary>
/// Minimal OpenAI Chat Completions client (no external SDK).
/// Uses: POST https://api.openai.com/v1/chat/completions
/// </summary>
public sealed class OpenAiChatClient
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _opt;

    public OpenAiChatClient(HttpClient http, IOptions<OpenAiOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opt.ApiKey);

    /// <summary>
    /// Simple text generation intended for short, user-facing answers (e.g., Topbar AI Search).
    /// </summary>
    public async Task<string> GenerateForSearchAsync(string userQuery, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            throw new ArgumentException("Query is required.", nameof(userQuery));

        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI is not configured. Set OpenAI:ApiKey in appsettings or user-secrets.");

        var system = string.IsNullOrWhiteSpace(_opt.SystemInstruction)
            ? "You are an assistant integrated into an app search bar. Answer concisely."
            : _opt.SystemInstruction.Trim();

        var req = new
        {
            model = _opt.Model,
            temperature = _opt.Temperature,
            max_tokens = _opt.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = userQuery.Trim() }
            }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(msg, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI call failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {body}");

        return ExtractAssistantText(body);
    }

    public async Task<string> CreateJsonInsightsAsync(string systemPrompt, string userJsonPayload, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI is not configured. Set OpenAI:ApiKey in appsettings or user-secrets.");

        var req = new
        {
            model = _opt.Model,
            temperature = _opt.Temperature,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userJsonPayload }
            }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(msg, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI call failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? "{}";
    }

    private static string ExtractAssistantText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return string.Empty;

            var msg = choices[0].GetProperty("message");
            if (msg.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                return (content.GetString() ?? string.Empty).Trim();

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
