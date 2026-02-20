using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Services.OpenAI;

namespace CompeteDesk.Services.BusinessAnalysis;

public sealed class BusinessAnalysisService
{
    private readonly OpenAiChatClient _openAi;

    // Keep competitor lists consistently useful in the UI.
    private const int MinCompetitors = 10;
    private const int MaxCompetitors = 14;

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
- Competitors: 10-14 competitors for the selected country.
- Competitor names must sound like real businesses in that country.
  - DO NOT use placeholders like "Competitor 1", "Company A", "Dental Clinic A", "Clinic B", etc.
  - Use distinct, brand-like names (2-6 words) and avoid single-letter suffixes.
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

        // Post-process for consistent UX:
        // - ensure we have enough competitors
        // - replace placeholder-y names (e.g., "Dental Clinic A")
        // - ensure Five Forces fields have valid scores
        SanitizeCompetitors(parsed, input.BusinessType, input.Country);

        return new GenerateOutput(json, parsed);
    }

    private static void SanitizeCompetitors(BusinessAnalysisResult parsed, string businessType, string country)
    {
        if (parsed == null) return;

        parsed.Competitors ??= new List<CompetitorResult>();

        // Normalize and drop empty shells
        parsed.Competitors = parsed.Competitors
            .Where(c => c != null)
            .Select(c =>
            {
                c.Name = (c.Name ?? string.Empty).Trim();
                c.WhyRelevant = string.IsNullOrWhiteSpace(c.WhyRelevant) ? null : c.WhyRelevant.Trim();
                c.Website = string.IsNullOrWhiteSpace(c.Website) ? null : c.Website.Trim();
                c.Summary = string.IsNullOrWhiteSpace(c.Summary) ? null : c.Summary.Trim();
                c.FiveForces ??= new FiveForcesResult();
                ClampFiveForces(c.FiveForces);
                return c;
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .ToList();

        // Replace placeholder names with realistic ones.
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in parsed.Competitors)
        {
            if (IsPlaceholderName(c.Name))
            {
                c.Name = GenerateCompetitorName(businessType, country, used);
            }
            used.Add(c.Name);
        }

        // Ensure minimum competitors
        while (parsed.Competitors.Count < MinCompetitors)
        {
            parsed.Competitors.Add(new CompetitorResult
            {
                Name = GenerateCompetitorName(businessType, country, used),
                WhyRelevant = "Competes for the same customers and services in the local market.",
                FiveForces = DefaultMidForces()
            });
        }

        // Cap maximum to avoid overly long tables.
        if (parsed.Competitors.Count > MaxCompetitors)
            parsed.Competitors = parsed.Competitors.Take(MaxCompetitors).ToList();
    }

    private static void ClampFiveForces(FiveForcesResult ff)
    {
        ff.Rivalry ??= new ForceRating();
        ff.NewEntrants ??= new ForceRating();
        ff.Substitutes ??= new ForceRating();
        ff.SupplierPower ??= new ForceRating();
        ff.BuyerPower ??= new ForceRating();

        ff.Rivalry.Score = ClampScore(ff.Rivalry.Score);
        ff.NewEntrants.Score = ClampScore(ff.NewEntrants.Score);
        ff.Substitutes.Score = ClampScore(ff.Substitutes.Score);
        ff.SupplierPower.Score = ClampScore(ff.SupplierPower.Score);
        ff.BuyerPower.Score = ClampScore(ff.BuyerPower.Score);

        ff.Rivalry.Notes ??= string.Empty;
        ff.NewEntrants.Notes ??= string.Empty;
        ff.Substitutes.Notes ??= string.Empty;
        ff.SupplierPower.Notes ??= string.Empty;
        ff.BuyerPower.Notes ??= string.Empty;
    }

    private static int ClampScore(int score)
    {
        // Sometimes the model returns 0 or null-like defaults.
        if (score < 1) return 3;
        if (score > 5) return 5;
        return score;
    }

    private static FiveForcesResult DefaultMidForces()
    {
        return new FiveForcesResult
        {
            Rivalry = new ForceRating { Score = 4, Notes = "Crowded local market with similar offerings." },
            NewEntrants = new ForceRating { Score = 3, Notes = "Moderate barriers; new players can enter with capital and licenses." },
            Substitutes = new ForceRating { Score = 3, Notes = "Some substitutes exist, but core service remains necessary." },
            SupplierPower = new ForceRating { Score = 3, Notes = "Suppliers have alternatives; pricing can fluctuate." },
            BuyerPower = new ForceRating { Score = 4, Notes = "Customers compare options and switch based on price/reviews." }
        };
    }

    private static bool IsPlaceholderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;

        var n = name.Trim();

        // Examples: "Competitor 1", "Company A", "Dental Clinic A", "Clinic B"
        if (Regex.IsMatch(n, @"^(Competitor|Company|Business)\s*#?\s*\d+$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(n, @"\b(Clinic|Dental\s+Clinic|Store|Shop|Center|Centre)\s+[A-Z]$", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(n, @"\b[A-Z]$"))
            return true;

        // Overly-generic, low-signal names.
        var generic = new[] { "local competitor", "nearby clinic", "regional competitor" };
        if (generic.Any(g => string.Equals(n, g, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static string GenerateCompetitorName(string businessType, string country, HashSet<string> used)
    {
        // Light-weight generator: no web calls required.
        // Produces brand-like names (2–6 words) and tries to reflect country naming patterns.
        businessType ??= string.Empty;
        country ??= string.Empty;

        var core = ExtractCoreNoun(businessType);

        var prefixes = new[]
        {
            "Smile", "Bright", "Prime", "Metro", "City", "Family", "Elite", "Care", "Ever", "Harmony",
            "Zen", "Nova", "Pearl", "Sunrise", "Silver", "Golden", "Beacon", "River", "Summit", "Apex"
        };

        var suffixes = new[]
        {
            "Center", "Clinic", "Care", "Group", "Hub", "Studio", "Solutions", "Partners", "Associates", "Practice"
        };

        var connectors = new[] { "&", "of", "for" };

        var locations = GetCountryLocations(country);

        // Try multiple combinations until unique.
        for (var i = 0; i < 200; i++)
        {
            var p = prefixes[i % prefixes.Length];
            var s = suffixes[(i / prefixes.Length) % suffixes.Length];
            var loc = locations.Length == 0 ? null : locations[i % locations.Length];

            string candidate;
            if (!string.IsNullOrWhiteSpace(loc) && i % 3 == 0)
            {
                candidate = $"{loc} {core} {s}";
            }
            else if (i % 5 == 0)
            {
                var c = connectors[(i / 5) % connectors.Length];
                candidate = $"{p} {core} {c} {s}";
            }
            else
            {
                candidate = $"{p} {core} {s}";
            }

            candidate = Regex.Replace(candidate, @"\s+", " ").Trim();
            candidate = Regex.Replace(candidate, @"\s+[A-Z]$", "", RegexOptions.None);

            if (!used.Contains(candidate))
            {
                used.Add(candidate);
                return candidate;
            }
        }

        // Fallback
        var fallback = $"{core} Market Competitor";
        if (!used.Contains(fallback)) used.Add(fallback);
        return fallback;
    }

    private static string ExtractCoreNoun(string businessType)
    {
        var bt = (businessType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(bt)) return "Business";

        // Normalize common patterns
        if (Regex.IsMatch(bt, "dental", RegexOptions.IgnoreCase)) return "Dental";
        if (Regex.IsMatch(bt, "restaurant|cafe|coffee", RegexOptions.IgnoreCase)) return "Cafe";
        if (Regex.IsMatch(bt, "gym|fitness", RegexOptions.IgnoreCase)) return "Fitness";
        if (Regex.IsMatch(bt, "school|academy|learning", RegexOptions.IgnoreCase)) return "Academy";
        if (Regex.IsMatch(bt, "clinic|health", RegexOptions.IgnoreCase)) return "Clinic";
        if (Regex.IsMatch(bt, "shop|store|retail", RegexOptions.IgnoreCase)) return "Store";

        // Use a cleaned first token as a reasonable core.
        var token = Regex.Split(bt, @"\s+")
            .Select(t => Regex.Replace(t ?? string.Empty, "[^A-Za-z]", ""))
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t) && t.Length > 1);

        return string.IsNullOrWhiteSpace(token) ? "Business" : token;
    }

    private static string[] GetCountryLocations(string country)
    {
        if (string.IsNullOrWhiteSpace(country)) return Array.Empty<string>();
        var c = country.Trim().ToLowerInvariant();

        if (c.Contains("philippines"))
        {
            return new[] { "Manila", "Quezon City", "Makati", "Pasig", "Cebu", "Davao", "Baguio", "Iloilo" };
        }
        if (c.Contains("singapore"))
        {
            return new[] { "Central", "Orchard", "Tampines", "Jurong", "Woodlands" };
        }
        if (c.Contains("united states") || c.Equals("usa"))
        {
            return new[] { "Downtown", "Midtown", "Bay", "Lakeside", "Valley" };
        }

        return Array.Empty<string>();
    }
}
