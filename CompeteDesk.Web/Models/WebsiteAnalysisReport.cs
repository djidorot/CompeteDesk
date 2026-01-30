using System;
using System.ComponentModel.DataAnnotations;

namespace CompeteDesk.Models;

/// <summary>
/// Persisted website analysis report for a given URL.
/// Includes raw metrics + AI structured insights.
/// </summary>
public sealed class WebsiteAnalysisReport
{
    public int Id { get; set; }

    [Required]
    [StringLength(2048)]
    public string Url { get; set; } = string.Empty;

    [StringLength(512)]
    public string? FinalUrl { get; set; }

    public int HttpStatusCode { get; set; }
    public long ResponseTimeMs { get; set; }

    [StringLength(512)]
    public string? Title { get; set; }

    [StringLength(1024)]
    public string? MetaDescription { get; set; }

    public int WordCount { get; set; }
    public int H1Count { get; set; }
    public int H2Count { get; set; }

    public int InternalLinkCount { get; set; }
    public int ExternalLinkCount { get; set; }

    public int ImageCount { get; set; }
    public int ImagesMissingAltCount { get; set; }

    public bool IsHttps { get; set; }
    public bool HasViewportMeta { get; set; }
    public bool HasRobotsNoindex { get; set; }
    public bool HasCanonicalLink { get; set; }
    public bool HasOpenGraph { get; set; }
    public bool HasCspHeader { get; set; }
    public bool HasHstsHeader { get; set; }

    /// <summary>
    /// JSON string produced by OpenAI, containing structured insights
    /// (strengths, weaknesses, status, recommendations, etc.).
    /// </summary>
    public string? AiInsightsJson { get; set; }

    /// <summary>
    /// A short readable summary (optional). Kept separate in case the AI returns non-JSON.
    /// </summary>
    public string? AiSummary { get; set; }

    [Required]
    public string OwnerId { get; set; } = string.Empty;

    public int? WorkspaceId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
