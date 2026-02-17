using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Models.Common;

/// <summary>
/// Lightweight, server-side pagination helper.
/// Keep this in Web (not Data) so controllers can paginate both entities and projections.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; private set; } = Array.Empty<T>();
    public int Page { get; private set; }
    public int PageSize { get; private set; }
    public int TotalCount { get; private set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    private PagedResult() { }

    public static async Task<PagedResult<T>> CreateAsync(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Defensive caps to prevent accidental DoS.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var total = await query.CountAsync(ct);
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
