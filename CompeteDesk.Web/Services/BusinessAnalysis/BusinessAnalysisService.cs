using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.BusinessAnalysis;

public sealed class BusinessAnalysisService
{
    private readonly OpenAiChatClient _openAi;

    public BusinessAnalysisService(OpenAiChatClient openAi)
    {
        _openAi = openAi;
    }

    public sealed record GenerateInput(
        string WorkspaceName,
        string BusinessType,
        string Country);

    public sealed record GenerateOutput(
        string Json,
        BusinessAnalysisResult Parsed);

    public async Task<GenerateOutput> GenerateAsync(GenerateInput input, CancellationToken ct)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        if (string.IsNullOrWhiteSpace(input.BusinessType))
            throw new ArgumentException("BusinessType is required.", nameof(input));

        if (string.IsNullOrWhiteSpace(input.Country))
            throw new ArgumentException("Country is required.", nameof(input));

        // IMPORTANT:
        // Use C# raw string literal to avoid escaping issues (\) that caused the build errors.
        // Requires C# 11+ (NET 7/8 default). If your project is older, tell me and I'll convert to safe concatenation.
        var systemPrompt = """
You are a business strategy analyst.
Return ONLY a valid JSON object with this exact schema (no markdown):

{
  "swot": {
    "strengths": ["..."],
    "weaknesses": ["..."],
    "opportunities": ["..."],
    "threats": ["..."]
  },
  "fiveForces": {
    "rivalry": { "score": 1-5, "notes": "..." },
    "newEntrants": { "score": 1-5, "notes": "..." },
    "substitutes": { "score": 1-5, "notes": "..." },
    "supplierPower": { "score": 1-5, "notes": "..." },
    "buyerPower": { "score": 1-5, "notes": "..." }
  },
  "competitors": [
    {
      "name": "Competitor name",
      "whyRelevant": "Why they are a competitor in the selected country",
      "fiveForces": {
        "rivalry": { "score": 1-5, "notes": "..." },
        "newEntrants": { "score": 1-5, "notes": "..." },
        "substitutes": { "score": 1-5, "notes": "..." },
        "supplierPower": { "score": 1-5, "notes": "..." },
        "buyerPower": { "score": 1-5, "notes": "..." }
      }
    }
  ]
}

Rules:
- SWOT lists: 5-8 bullets each, concise and specific.
- Competitors: 5-10 realistic competitors for the selected country.
- Five Forces score: integer 1..5 (1=low force, 5=high force).
""";

        var userPayload = JsonSerializer.Serialize(new
        {
            workspace = input.WorkspaceName ?? "",
            businessType = input.BusinessType,
            country = input.Country,
            task = "Generate SWOT and Porter's Five Forces for the business and for key competitors in the specified country."
        });

        // This should return JSON (your OpenAiChatClient likely sets response_format=json_object)
        var json = await _openAi.CreateJsonInsightsAsync(systemPrompt, userPayload, ct);

        BusinessAnalysisResult parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<BusinessAnalysisResult>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new BusinessAnalysisResult();
        }
        catch
        {
            // If AI returned malformed JSON despite JSON-mode, keep raw JSON and return empty Parsed.
            parsed = new BusinessAnalysisResult();
        }

        return new GenerateOutput(json, parsed);
    }
}
