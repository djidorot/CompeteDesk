using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.Ai;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.WebsiteAnalysis;

public sealed class WebsiteAnalysisService
{
    private static readonly Regex TitleRx =
        new(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MetaDescRx =
        new(@"<meta\s+[^>]*name\s*=\s*['""]description['""][^>]*content\s*=\s*['""](.*?)['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ViewportRx =
        new(@"<meta\s+[^>]*name\s*=\s*['""]viewport['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex RobotsRx =
        new(@"<meta\s+[^>]*name\s*=\s*['""]robots['""][^>]*content\s*=\s*['""](.*?)['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CanonicalRx =
        new(@"<link\s+[^>]*rel\s*=\s*['""]canonical['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OgRx =
        new(@"<meta\s+[^>]*property\s*=\s*['""]og:",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex H1Rx = new(@"<h1\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex H2Rx = new(@"<h2\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LinkRx =
        new(@"<a\s+[^>]*href\s*=\s*['""](.*?)['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ImgRx =
        new(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ImgAltRx =
        new(@"<img\b[^>]*\balt\s*=\s*['""]([^'""]*)['""][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagStripRx =
        new(@"<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpFactory;
    private readonly OpenAiChatClient _openAi;
    private readonly ApplicationDbContext _db;
    private readonly DecisionTraceService _trace;
    private readonly AiContextPackBuilder _contextPack;

    public WebsiteAnalysisService(
        IHttpClientFactory httpFactory,
        OpenAiChatClient openAi,
        ApplicationDbContext db,
        DecisionTraceService trace,
        AiContextPackBuilder contextPack)
    {
        _httpFactory = httpFactory;
        _openAi = openAi;
        _db = db;
        _trace = trace;
        _contextPack = contextPack;
    }

    public async Task<WebsiteAnalysisReport> AnalyzeAndSaveAsync(string urlInput, string ownerId, int? workspaceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new InvalidOperationException("OwnerId is required.");

        var normalized = NormalizeUrl(urlInput);
        var client = _httpFactory.CreateClient("site-analyzer");
        var sw = Stopwatch.StartNew();

        HttpResponseMessage? res = null;
        string? html = null;
        Uri? finalUri = null;

        try
        {
            res = await client.GetAsync(normalized, ct);
            sw.Stop();
            finalUri = res.RequestMessage?.RequestUri;

            html = await ReadAtMostAsync(res, maxBytes: 1024 * 1024, ct);
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
        }
        catch
        {
            sw.Stop();
        }

        var report = new WebsiteAnalysisReport
        {
            OwnerId = ownerId,
            WorkspaceId = workspaceId,
            Url = normalized,
            FinalUrl = finalUri?.ToString(),
            ResponseTimeMs = sw.ElapsedMilliseconds,
            HttpStatusCode = (int?)res?.StatusCode ?? 0,
            IsHttps = finalUri?.Scheme?.Equals("https", StringComparison.OrdinalIgnoreCase)
                      ?? normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(html))
        {
            report.Title = ExtractFirst(TitleRx, html)?.Trim();
            report.MetaDescription = ExtractFirst(MetaDescRx, html)?.Trim();
            report.HasViewportMeta = ViewportRx.IsMatch(html);

            var robots = ExtractFirst(RobotsRx, html);
            report.HasRobotsNoindex = !string.IsNullOrWhiteSpace(robots)
                                     && robots.Contains("noindex", StringComparison.OrdinalIgnoreCase);

            report.HasCanonicalLink = CanonicalRx.IsMatch(html);
            report.HasOpenGraph = OgRx.IsMatch(html);

            report.H1Count = H1Rx.Matches(html).Count;
            report.H2Count = H2Rx.Matches(html).Count;

            var (internalLinks, externalLinks) = CountLinks(html, finalUri ?? new Uri(normalized));
            report.InternalLinkCount = internalLinks;
            report.ExternalLinkCount = externalLinks;

            report.ImageCount = ImgRx.Matches(html).Count;
            report.ImagesMissingAltCount = CountImagesMissingAlt(html);

            report.WordCount = EstimateWordCount(html);
        }

        if (res != null)
        {
            report.HasCspHeader = res.Headers.Contains("Content-Security-Policy") || res.Content.Headers.Contains("Content-Security-Policy");
            report.HasHstsHeader = res.Headers.Contains("Strict-Transport-Security") || res.Content.Headers.Contains("Strict-Transport-Security");
        }

        await AddAiInsightsAsync(report, ct);

        _db.WebsiteAnalysisReports.Add(report);
        await _db.SaveChangesAsync(ct);

        // Traceability (use your existing LogAsync overload)
        try
        {
            var ctxJson = await _contextPack.BuildAsync(ownerId, workspaceId, ct);

            await _trace.LogAsync(
                ownerId: ownerId,
                workspaceId: workspaceId,
                feature: "WebsiteAnalysis.Analyze",
                input: new
                {
                    urlInput,
                    normalizedUrl = normalized,
                    contextPack = JsonDocument.Parse(ctxJson).RootElement,
                    system = "WebsiteAnalysisService.AddAiInsightsAsync"
                },
                outputJson: report.AiInsightsJson ?? "{}",
                entityType: "WebsiteAnalysisReport",
                entityId: report.Id,
                entityTitle: report.Url,
                ct: ct
            );
        }
        catch
        {
            // Never block user flows.
        }

        return report;
    }

    private async Task AddAiInsightsAsync(WebsiteAnalysisReport report, CancellationToken ct)
    {
        var payload = new
        {
            url = report.Url,
            finalUrl = report.FinalUrl,
            httpStatus = report.HttpStatusCode,
            responseTimeMs = report.ResponseTimeMs,
            title = report.Title,
            metaDescription = report.MetaDescription,
            wordCount = report.WordCount,
            h1Count = report.H1Count,
            h2Count = report.H2Count,
            internalLinks = report.InternalLinkCount,
            externalLinks = report.ExternalLinkCount,
            imageCount = report.ImageCount,
            imagesMissingAlt = report.ImagesMissingAltCount,
            isHttps = report.IsHttps,
            hasViewportMeta = report.HasViewportMeta,
            hasRobotsNoindex = report.HasRobotsNoindex,
            hasCanonical = report.HasCanonicalLink,
            hasOpenGraph = report.HasOpenGraph,
            hasCspHeader = report.HasCspHeader,
            hasHstsHeader = report.HasHstsHeader
        };

        var systemPrompt = @"You are an expert product strategist + SEO/UX auditor.
Given website crawl metrics (JSON), produce a concise, actionable assessment.

Return STRICT JSON with this schema:
{
  ""overallStatus"": ""On Track"" | ""At Risk"" | ""Off Track"",
  ""score"": 0-100,
  ""strengths"": [""...""],
  ""weaknesses"": [""...""],
  ""quickWins"": [""...""],
  ""risks"": [""...""],
  ""recommendedNextActions"": [{""title"":""..."",""impact"":""High|Medium|Low"",""effort"":""High|Medium|Low"",""why"":""...""}],
  ""keyMetrics"": { ""responseTimeMs"": number, ""wordCount"": number, ""internalLinks"": number, ""externalLinks"": number, ""imageCount"": number, ""imagesMissingAlt"": number }
}

Rules:
- Base conclusions only on provided metrics (do not invent facts).
- If the HTML could not be fetched, explain that in weaknesses and set status Off Track.
- Keep each bullet short (max ~14 words).";

        var userJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        if (!_openAi.IsConfigured)
        {
            report.AiInsightsJson = JsonSerializer.Serialize(new
            {
                overallStatus = report.HttpStatusCode >= 200 && report.HttpStatusCode < 400 ? "At Risk" : "Off Track",
                score = report.HttpStatusCode >= 200 && report.HttpStatusCode < 400 ? 60 : 20,
                strengths = new[] { "Basic metrics captured (configure OpenAI for deeper insights)." },
                weaknesses = new[] { "OpenAI not configured (set OpenAI:ApiKey)." },
                quickWins = Array.Empty<string>(),
                risks = Array.Empty<string>(),
                recommendedNextActions = new[]
                {
                    new { title = "Add OpenAI API key", impact = "High", effort = "Low", why = "Unlock AI insights for the report." }
                },
                keyMetrics = new
                {
                    report.ResponseTimeMs,
                    report.WordCount,
                    report.InternalLinkCount,
                    report.ExternalLinkCount,
                    report.ImageCount,
                    report.ImagesMissingAltCount
                }
            });

            report.AiSummary = "OpenAI not configured. Add OpenAI:ApiKey to enable AI insights.";
            return;
        }

        try
        {
            var aiJson = await _openAi.CreateJsonInsightsAsync(systemPrompt, userJson, ct);
            report.AiInsightsJson = aiJson;
        }
        catch (Exception ex)
        {
            report.AiSummary = $"AI insights failed: {ex.Message}";
        }
    }

    private static string NormalizeUrl(string input)
    {
        var s = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("URL is required.");

        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            s = "https://" + s;
        }

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Only http/https URLs are supported.");

        return uri.ToString();
    }

    private static async Task<string?> ReadAtMostAsync(HttpResponseMessage res, int maxBytes, CancellationToken ct)
    {
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[16 * 1024];
        int read;
        int total = 0;

        using var ms = new System.IO.MemoryStream();
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
                break;

            ms.Write(buffer, 0, read);
        }

        var bytes = ms.ToArray();
        try { return System.Text.Encoding.UTF8.GetString(bytes); }
        catch { return System.Text.Encoding.Latin1.GetString(bytes); }
    }

    private static string? ExtractFirst(Regex rx, string html)
    {
        var m = rx.Match(html);
        if (!m.Success) return null;
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    private static int EstimateWordCount(string html)
    {
        var text = TagStripRx.Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static (int internalLinks, int externalLinks) CountLinks(string html, Uri baseUri)
    {
        int internalCount = 0;
        int externalCount = 0;

        foreach (Match m in LinkRx.Matches(html))
        {
            var href = (m.Groups[1].Value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(href)) continue;
            if (href.StartsWith("#")) continue;
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) continue;
            if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;

            if (Uri.TryCreate(baseUri, href, out var link))
            {
                if (string.Equals(link.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                    internalCount++;
                else
                    externalCount++;
            }
        }

        return (internalCount, externalCount);
    }

    private static int CountImagesMissingAlt(string html)
    {
        var total = ImgRx.Matches(html).Count;
        if (total == 0) return 0;

        int withAlt = 0;
        foreach (Match m in ImgAltRx.Matches(html))
        {
            var alt = (m.Groups[1].Value ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(alt))
                withAlt++;
        }

        return Math.Max(0, total - withAlt);
    }
}
