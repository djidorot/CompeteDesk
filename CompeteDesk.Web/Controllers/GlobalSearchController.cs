using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CompeteDesk.Data;
using CompeteDesk.ViewModels.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Controllers;

[Authorize]
[ApiController]
public sealed class GlobalSearchController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public GlobalSearchController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("/api/search/global")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string entity = "all",
        [FromQuery] string? category = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var query = (q ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(new GlobalSearchResponseViewModel());
        }

        if (query.Length > 160)
        {
            return BadRequest(new { error = "Search query is too long." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var normalizedEntity = (entity ?? "all").Trim().ToLowerInvariant();
        if (normalizedEntity is not ("all" or "strategies" or "workspaces" or "users"))
        {
            normalizedEntity = "all";
        }

        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var normalizedPriority = NormalizePriority(priority);
        var normalizedStatus = NormalizeStatus(status);
        var canSearchUsers = User.IsInRole("Admin");

        var strategyBase = _db.Strategies
            .AsNoTracking()
            .Where(x => x.OwnerId == user.Id);

        var categories = await strategyBase
            .Where(x => !string.IsNullOrWhiteSpace(x.Category))
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .Take(30)
            .ToListAsync(ct);

        var response = new GlobalSearchResponseViewModel
        {
            Query = query,
            Entity = normalizedEntity,
            Category = normalizedCategory,
            Priority = normalizedPriority,
            Status = normalizedStatus,
            Categories = categories,
            CanSearchUsers = canSearchUsers
        };

        if (normalizedEntity is "all" or "strategies")
        {
            var strategiesQuery = strategyBase.Where(x =>
                x.Name.Contains(query) ||
                (x.CorePrinciple != null && x.CorePrinciple.Contains(query)) ||
                (x.Summary != null && x.Summary.Contains(query)) ||
                (x.Category != null && x.Category.Contains(query)));

            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                strategiesQuery = strategiesQuery.Where(x => x.Category == normalizedCategory);
            }

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                strategiesQuery = strategiesQuery.Where(x => x.Status == normalizedStatus);
            }

            if (normalizedPriority is not null)
            {
                strategiesQuery = normalizedPriority switch
                {
                    "High" => strategiesQuery.Where(x => x.Priority >= 8),
                    "Medium" => strategiesQuery.Where(x => x.Priority >= 4 && x.Priority <= 7),
                    "Low" => strategiesQuery.Where(x => x.Priority <= 3),
                    _ => strategiesQuery
                };
            }

            response.StrategyCount = await strategiesQuery.CountAsync(ct);
            response.Strategies = await strategiesQuery
                .OrderByDescending(x => x.Priority)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .Select(x => new GlobalSearchItemViewModel
                {
                    EntityType = "strategy",
                    Title = x.Name,
                    Subtitle = string.IsNullOrWhiteSpace(x.CorePrinciple) ? (x.Summary ?? "No summary available.") : x.CorePrinciple!,
                    Meta = $"{(string.IsNullOrWhiteSpace(x.Category) ? "Uncategorized" : x.Category)} • Priority {x.Priority} • {x.Status}",
                    Url = $"/Strategies/Details/{x.Id}"
                })
                .Take(8)
                .ToListAsync(ct);
        }

        if (normalizedEntity is "all" or "workspaces")
        {
            var workspacesQuery = _db.Workspaces
                .AsNoTracking()
                .Where(x => x.OwnerId == user.Id)
                .Where(x => x.Name.Contains(query) ||
                            (x.Description != null && x.Description.Contains(query)) ||
                            (x.BusinessType != null && x.BusinessType.Contains(query)) ||
                            (x.Country != null && x.Country.Contains(query)));

            response.WorkspaceCount = await workspacesQuery.CountAsync(ct);
            response.Workspaces = await workspacesQuery
                .OrderBy(x => x.Name)
                .Select(x => new GlobalSearchItemViewModel
                {
                    EntityType = "workspace",
                    Title = x.Name,
                    Subtitle = string.IsNullOrWhiteSpace(x.Description) ? "Workspace" : x.Description!,
                    Meta = string.Join(" • ", new[]
                    {
                        string.IsNullOrWhiteSpace(x.BusinessType) ? null : x.BusinessType,
                        string.IsNullOrWhiteSpace(x.Country) ? null : x.Country
                    }.Where(v => !string.IsNullOrWhiteSpace(v))),
                    Url = $"/Workspaces/Details/{x.Id}"
                })
                .Take(8)
                .ToListAsync(ct);
        }

        if (canSearchUsers && (normalizedEntity is "all" or "users"))
        {
            var usersQuery = _userManager.Users
                .AsNoTracking()
                .Where(x =>
                    (x.Email != null && x.Email.Contains(query)) ||
                    (x.UserName != null && x.UserName.Contains(query)));

            response.UserCount = await usersQuery.CountAsync(ct);
            response.Users = await usersQuery
                .OrderBy(x => x.Email)
                .Select(x => new GlobalSearchItemViewModel
                {
                    EntityType = "user",
                    Title = x.Email ?? x.UserName ?? "User",
                    Subtitle = x.UserName ?? "User account",
                    Meta = "User account",
                    Url = "/Admin/Users"
                })
                .Take(8)
                .ToListAsync(ct);
        }

        response.TotalCount = response.StrategyCount + response.WorkspaceCount + response.UserCount;
        return Ok(response);
    }

    private static string? NormalizePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "high" => "High",
            "medium" => "Medium",
            "low" => "Low",
            _ => null
        };
    }

    private static string? NormalizeStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "active" => "Active",
            "archived" => "Archived",
            _ => null
        };
    }
}
