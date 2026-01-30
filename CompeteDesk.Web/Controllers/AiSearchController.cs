using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CompeteDesk.Services.Ai;
using CompeteDesk.Services.Gemini;

namespace CompeteDesk.Controllers;

[Authorize]
[ApiController]
public sealed class AiSearchController : ControllerBase
{
    private readonly GeminiClient _gemini;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly DecisionTraceService _trace;

    public AiSearchController(
        GeminiClient gemini,
        UserManager<IdentityUser> userManager,
        DecisionTraceService trace)
    {
        _gemini = gemini;
        _userManager = userManager;
        _trace = trace;
    }

    // GET /api/ai-search?q=...
    [HttpGet("/api/ai-search")]
    public async Task<IActionResult> Get([FromQuery] string? q, CancellationToken ct)
    {
        var query = (q ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "Query is required." });

        if (query.Length > 400)
            return BadRequest(new { error = "Query is too long (max 400 characters)." });

        var sw = Stopwatch.StartNew();
        string raw;
        string? topic = null;
        string? overview = null;
        string[] keyAspects = Array.Empty<string>();
        string[] examples = Array.Empty<string>();

        try
        {
            // Produce a Google-like "AI Overview" (definition + key aspects + examples).
            raw = await _gemini.GenerateAiOverviewJsonAsync(query, ct);

            // Best-effort parse (Gemini should return strict JSON; if it doesn't, we fall back to plain text).
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("topic", out var t) && t.ValueKind == JsonValueKind.String)
                    topic = t.GetString();
                if (root.TryGetProperty("overview", out var o) && o.ValueKind == JsonValueKind.String)
                    overview = o.GetString();

                if (root.TryGetProperty("keyAspects", out var ka) && ka.ValueKind == JsonValueKind.Array)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var item in ka.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString() ?? string.Empty);
                    keyAspects = list.FindAll(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                }

                if (root.TryGetProperty("examples", out var ex) && ex.ValueKind == JsonValueKind.Array)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var item in ex.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString() ?? string.Empty);
                    examples = list.FindAll(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                }
            }
            catch
            {
                // ignore parse error, fall back to raw
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            sw.Stop();
        }

        // Optional: log to AI History (DecisionTraces)
        try
        {
            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var outputJson = JsonSerializer.Serialize(new { topic, overview, keyAspects, examples, raw }, new JsonSerializerOptions { WriteIndented = false });
                await _trace.LogAsync(
                    ownerId: userId,
                    workspaceId: null,
                    feature: "TopbarAiSearch",
                    input: new { query },
                    outputJson: outputJson,
				aiProvider: "Gemini",
                    model: null,
                    temperature: null,
                    ct: ct);
            }
        }
        catch
        {
            // Don't block UX if trace logging fails.
        }

        // If parsing failed, return plain answer for backward compatibility.
        if (string.IsNullOrWhiteSpace(topic) && string.IsNullOrWhiteSpace(overview) && keyAspects.Length == 0 && examples.Length == 0)
        {
            return Ok(new
            {
                answer = raw,
                elapsedMs = sw.ElapsedMilliseconds
            });
        }

        return Ok(new
        {
            topic = topic ?? query,
            overview,
            keyAspects,
            examples,
            elapsedMs = sw.ElapsedMilliseconds
        });
    }
}
