using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CompeteDesk.Services.BusinessAnalysis;

public sealed class BusinessAnalysisResult
{
    [JsonPropertyName("swot")]
    public SwotResult Swot { get; set; } = new();

    [JsonPropertyName("fiveForces")]
    public FiveForcesResult FiveForces { get; set; } = new();

    [JsonPropertyName("competitors")]
    public List<CompetitorResult> Competitors { get; set; } = new();
}

public sealed class SwotResult
{
    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("weaknesses")]
    public List<string> Weaknesses { get; set; } = new();

    [JsonPropertyName("opportunities")]
    public List<string> Opportunities { get; set; } = new();

    [JsonPropertyName("threats")]
    public List<string> Threats { get; set; } = new();
}

public sealed class FiveForcesResult
{
    [JsonPropertyName("rivalry")]
    public ForceRating Rivalry { get; set; } = new();

    [JsonPropertyName("newEntrants")]
    public ForceRating NewEntrants { get; set; } = new();

    [JsonPropertyName("substitutes")]
    public ForceRating Substitutes { get; set; } = new();

    [JsonPropertyName("supplierPower")]
    public ForceRating SupplierPower { get; set; } = new();

    [JsonPropertyName("buyerPower")]
    public ForceRating BuyerPower { get; set; } = new();
}

public sealed class ForceRating
{
    /// <summary>
    /// 1 (low) - 5 (high)
    /// </summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class CompetitorResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;


    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    [JsonPropertyName("whyRelevant")]
    public string? WhyRelevant { get; set; }

    [JsonPropertyName("fiveForces")]
    public FiveForcesResult FiveForces { get; set; } = new();
}
