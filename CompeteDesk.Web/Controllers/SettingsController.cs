using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Data;
using CompeteDesk.Models;
using CompeteDesk.ViewModels.Settings;

namespace CompeteDesk.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _db;

        public SettingsController(UserManager<IdentityUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? string.Empty;

            var prefs = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _db.UserAiPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

            var data = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _db.UserDataControls.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);

            var vm = new SettingsIndexViewModel
            {
                Email = user?.Email ?? "",
                DisplayName = user?.UserName ?? (user?.Email ?? ""),

                Verbosity = prefs?.Verbosity ?? "Balanced",
                Tone = prefs?.Tone ?? "Analytical",
                AutoDraftPlans = prefs?.AutoDraftPlans ?? true,
                AutoSummaries = prefs?.AutoSummaries ?? true,
                AutoRecommendations = prefs?.AutoRecommendations ?? true,
                StoreDecisionTraces = prefs?.StoreDecisionTraces ?? true,

                RetentionDays = data?.RetentionDays ?? 90,
                ExportFormat = data?.ExportFormat ?? "json",
            };

            ViewData["SavedMessage"] = TempData["SettingsSaved"] as string;
            ViewData["ResetMessage"] = TempData["ResetDone"] as string;
            ViewData["ExportError"] = TempData["ExportError"] as string;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SettingsIndexViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            // --- AI Preferences ---
            vm.Verbosity = NormalizeEnum(vm.Verbosity, "Balanced", "Short", "Balanced", "Detailed");
            vm.Tone = NormalizeEnum(vm.Tone, "Analytical", "Executive", "Analytical", "Tactical");

            var prefs = await _db.UserAiPreferences.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (prefs == null)
            {
                prefs = new UserAiPreferences
                {
                    UserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.UserAiPreferences.Add(prefs);
            }

            prefs.Verbosity = vm.Verbosity;
            prefs.Tone = vm.Tone;
            prefs.AutoDraftPlans = vm.AutoDraftPlans;
            prefs.AutoSummaries = vm.AutoSummaries;
            prefs.AutoRecommendations = vm.AutoRecommendations;
            prefs.StoreDecisionTraces = vm.StoreDecisionTraces;
            prefs.UpdatedAtUtc = DateTime.UtcNow;

            // --- Data & Analytics Controls ---
            vm.RetentionDays = NormalizeRetention(vm.RetentionDays);
            vm.ExportFormat = NormalizeEnum(vm.ExportFormat, "json", "csv", "json").ToLowerInvariant();

            var data = await _db.UserDataControls.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (data == null)
            {
                data = new UserDataControls
                {
                    UserId = user.Id,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.UserDataControls.Add(data);
            }

            data.RetentionDays = vm.RetentionDays;
            data.ExportFormat = vm.ExportFormat;
            data.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Apply retention immediately to log-like tables (safe, non-core business data).
            await ApplyRetentionAsync(user.Id, vm.RetentionDays);

            TempData["SettingsSaved"] = "Settings updated.";
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------
        // Data & Analytics Controls: Export
        // ------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Export(string entity, string? format = null, int? days = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            entity = (entity ?? "").Trim().ToLowerInvariant();
            var fmt = (format ?? "").Trim();
            if (string.IsNullOrWhiteSpace(fmt))
            {
                var prefs = await _db.UserDataControls.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
                fmt = prefs?.ExportFormat ?? "json";
            }
            fmt = NormalizeEnum(fmt, "json", "csv", "json").ToLowerInvariant();

            var windowDays = days.HasValue ? Math.Max(1, days.Value) : 30;
            var fromUtc = DateTime.UtcNow.AddDays(-windowDays);

            try
            {
                if (entity == "actions")
                {
                    var items = await _db.Actions.AsNoTracking()
                        .Where(x => x.OwnerId == user.Id)
                        .OrderByDescending(x => x.CreatedAtUtc)
                        .ToListAsync();

                    return ExportList(items.Select(a => new
                    {
                        a.Id,
                        a.Title,
                        a.Status,
                        a.Category,
                        a.Priority,
                        a.DueAtUtc,
                        a.CreatedAtUtc,
                        a.StrategyId,
                        a.WorkspaceId
                    }), "actions", fmt);
                }

                if (entity == "strategies")
                {
                    var items = await _db.Strategies.AsNoTracking()
                        .Where(x => x.OwnerId == user.Id)
                        .OrderByDescending(x => x.CreatedAtUtc)
                        .ToListAsync();

                    return ExportList(items.Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Category,
                        s.SourceBook,
                        s.Status,
                        s.CreatedAtUtc,
                        s.WorkspaceId
                    }), "strategies", fmt);
                }

                if (entity == "metrics")
                {
                    // Lightweight metrics export: counts over time window
                    var workspaces = await _db.Workspaces.AsNoTracking()
                        .Where(x => x.OwnerId == user.Id)
                        .ToListAsync();

                    var strategies = await _db.Strategies.AsNoTracking()
                        .Where(x => x.OwnerId == user.Id)
                        .ToListAsync();

                    var actions = await _db.Actions.AsNoTracking()
                        .Where(x => x.OwnerId == user.Id)
                        .ToListAsync();

                    var traces = await _db.DecisionTraces.AsNoTracking()
                        .Where(x => x.OwnerId == user.Id)
                        .ToListAsync();

                    var metrics = new
                    {
                        WindowDays = windowDays,
                        FromUtc = fromUtc,
                        ToUtc = DateTime.UtcNow,
                        Totals = new
                        {
                            Workspaces = workspaces.Count,
                            Strategies = strategies.Count,
                            Actions = actions.Count,
                            AiRuns = traces.Count
                        },
                        NewInWindow = new
                        {
                            Workspaces = workspaces.Count(x => x.CreatedAtUtc >= fromUtc),
                            Strategies = strategies.Count(x => x.CreatedAtUtc >= fromUtc),
                            Actions = actions.Count(x => x.CreatedAtUtc >= fromUtc),
                            AiRuns = traces.Count(x => x.CreatedAtUtc >= fromUtc)
                        }
                    };

                    return ExportObject(metrics, "metrics", fmt);
                }

                TempData["ExportError"] = "Unknown export entity. Use: actions, strategies, metrics.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ExportError"] = "Export failed. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ------------------------------------------------------------
        // Data & Analytics Controls: Reset demo data (per-user)
        // ------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetDemoData(SettingsIndexViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            if (!string.Equals((vm.ResetConfirm ?? "").Trim(), "RESET", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ResetDone"] = "Reset not performed. Type RESET to confirm.";
                return RedirectToAction(nameof(Index));
            }

            // Delete user-owned application data. Identity tables are untouched.
            await using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                var uid = user.Id;

                // Child tables first
                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HabitCheckins WHERE OwnerId = {uid};");
                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Habits WHERE OwnerId = {uid};");

                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WarPlans WHERE OwnerId = {uid};");
                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WarIntel WHERE OwnerId = {uid};");

                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WebsiteAnalysisReports WHERE OwnerId = {uid};");
                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM BusinessAnalysisReports WHERE OwnerId = {uid};");

                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DecisionTraces WHERE OwnerId = {uid};");

                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Actions WHERE OwnerId = {uid};");
                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Strategies WHERE OwnerId = {uid};");
                await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Workspaces WHERE OwnerUserId = {uid};");

                await tx.CommitAsync();

                TempData["ResetDone"] = "Demo data reset complete.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["ResetDone"] = "Reset failed. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private async Task ApplyRetentionAsync(string userId, int retentionDays)
        {
            // Safety: only apply to log-like tables; do NOT delete core business entities.
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));

            // Decision traces
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM DecisionTraces WHERE OwnerId = {userId} AND CreatedAtUtc < {cutoff};");

            // Analysis reports
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM WebsiteAnalysisReports WHERE OwnerId = {userId} AND CreatedAtUtc < {cutoff};");

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM BusinessAnalysisReports WHERE OwnerId = {userId} AND CreatedAtUtc < {cutoff};");
        }

        private IActionResult ExportObject<T>(T obj, string baseName, string format)
        {
            if (format == "csv")
            {
                // CSV is best for lists; for a single object fall back to JSON-like CSV
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
                var csv = "json\n" + EscapeCsv(json) + "\n";
                return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{baseName}.csv");
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(obj, new JsonSerializerOptions { WriteIndented = true });
            return File(bytes, "application/json", $"{baseName}.json");
        }

        private IActionResult ExportList<T>(IEnumerable<T> rows, string baseName, string format)
        {
            if (format == "csv")
            {
                var list = rows.ToList();
                var csv = ToCsv(list);
                return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{baseName}.csv");
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions { WriteIndented = true });
            return File(bytes, "application/json", $"{baseName}.json");
        }

        private static string ToCsv<T>(IReadOnlyList<T> items)
        {
            if (items.Count == 0) return string.Empty;

            var props = typeof(T).GetProperties();
            var sb = new StringBuilder();

            sb.AppendLine(string.Join(",", props.Select(p => EscapeCsv(p.Name))));

            foreach (var item in items)
            {
                var vals = props.Select(p =>
                {
                    var v = p.GetValue(item, null);
                    return EscapeCsv(v?.ToString() ?? "");
                });
                sb.AppendLine(string.Join(",", vals));
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return "";
            var needs = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            if (!needs) return s;

            return "\""+ s.Replace("\"", "\"\"") + "\"";
        }

        private static int NormalizeRetention(int value)
        {
            return value switch
            {
                30 => 30,
                90 => 90,
                365 => 365,
                _ => 90
            };
        }

        private static string NormalizeEnum(string? value, string fallback, params string[] allowed)
        {
            var v = (value ?? "").Trim();
            foreach (var a in allowed)
            {
                if (string.Equals(v, a, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return fallback;
        }
    }
}