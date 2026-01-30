using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.Services.BusinessAnalysis;
using CompeteDesk.ViewModels.BusinessAnalysis;
using CompeteDesk.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers
{
    [Authorize]
    public sealed class BusinessAnalysisController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public BusinessAnalysisController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /BusinessAnalysis
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var userId = await GetUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();

            var ws = await _db.Workspaces
                .AsNoTracking()
                .Where(w => w.OwnerId == userId)
                .OrderByDescending(w => w.UpdatedAtUtc ?? w.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            var vm = new BusinessAnalysisPageViewModel
            {
                NeedsWorkspace = ws is null,
                WorkspaceId = ws?.Id ?? 0,
                WorkspaceName = ws?.Name ?? "No workspace yet",
                BusinessType = ws?.BusinessType,
                Country = ws?.Country,
                NeedsBusinessProfile = ws is not null && (string.IsNullOrWhiteSpace(ws.BusinessType) || string.IsNullOrWhiteSpace(ws.Country))
            };

            if (ws is not null)
            {
                var latest = await _db.BusinessAnalysisReports
                    .AsNoTracking()
                    .Where(r => r.OwnerId == userId && r.WorkspaceId == ws.Id)
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefaultAsync(ct);

                if (latest is not null)
                {
                    vm.Latest = MapBusinessAnalysis(latest);
                    vm.Latest.WorkspaceId = ws.Id;
                    vm.Latest.WorkspaceName = ws.Name;
                    vm.Latest.BusinessType = ws.BusinessType ?? "";
                    vm.Latest.Country = ws.Country ?? "";
                }
            }

            ViewData["Title"] = "Business Analysis";
            ViewData["LayoutFluid"] = true;
            ViewData["UseSidebar"] = true;

            return View(vm);
        }

        private async Task<string?> GetUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.Id;
        }

        private static BusinessAnalysisViewModel MapBusinessAnalysis(BusinessAnalysisReport report)
        {
            var vm = new BusinessAnalysisViewModel
            {
                CreatedAtUtc = report.CreatedAtUtc
            };

            try
            {
                var parsed = JsonSerializer.Deserialize<BusinessAnalysisResult>(report.AiInsightsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null) return vm;

                vm.Strengths = parsed.Swot?.Strengths ?? new();
                vm.Weaknesses = parsed.Swot?.Weaknesses ?? new();
                vm.Opportunities = parsed.Swot?.Opportunities ?? new();
                vm.Threats = parsed.Swot?.Threats ?? new();

                vm.Rivalry = new ForceVm { Score = parsed.FiveForces?.Rivalry?.Score ?? 0, Notes = parsed.FiveForces?.Rivalry?.Notes };
                vm.NewEntrants = new ForceVm { Score = parsed.FiveForces?.NewEntrants?.Score ?? 0, Notes = parsed.FiveForces?.NewEntrants?.Notes };
                vm.Substitutes = new ForceVm { Score = parsed.FiveForces?.Substitutes?.Score ?? 0, Notes = parsed.FiveForces?.Substitutes?.Notes };
                vm.SupplierPower = new ForceVm { Score = parsed.FiveForces?.SupplierPower?.Score ?? 0, Notes = parsed.FiveForces?.SupplierPower?.Notes };
                vm.BuyerPower = new ForceVm { Score = parsed.FiveForces?.BuyerPower?.Score ?? 0, Notes = parsed.FiveForces?.BuyerPower?.Notes };

                if (parsed.Competitors != null)
                {
                    foreach (var c in parsed.Competitors)
                    {
                        vm.Competitors.Add(new CompetitorVm
                        {
                            Name = c.Name,
                            Website = c.Website,
                            Summary = c.Summary,
                            WhyRelevant = c.WhyRelevant,
                            Rivalry = new ForceVm { Score = c.FiveForces?.Rivalry?.Score ?? 0, Notes = c.FiveForces?.Rivalry?.Notes },
                            NewEntrants = new ForceVm { Score = c.FiveForces?.NewEntrants?.Score ?? 0, Notes = c.FiveForces?.NewEntrants?.Notes },
                            Substitutes = new ForceVm { Score = c.FiveForces?.Substitutes?.Score ?? 0, Notes = c.FiveForces?.Substitutes?.Notes },
                            SupplierPower = new ForceVm { Score = c.FiveForces?.SupplierPower?.Score ?? 0, Notes = c.FiveForces?.SupplierPower?.Notes },
                            BuyerPower = new ForceVm { Score = c.FiveForces?.BuyerPower?.Score ?? 0, Notes = c.FiveForces?.BuyerPower?.Notes },
                        });
                    }
                }
            }
            catch
            {
                // Ignore parse errors; show empty sections.
            }

            return vm;
        }
    }
}
