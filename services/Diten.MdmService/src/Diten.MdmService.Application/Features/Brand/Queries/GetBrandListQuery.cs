using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Queries;

// Every filter is server-applied. The UI must not invent client-side filters for anything absent here.
public sealed record GetBrandListQuery : IRequest<Response<BrandListResultDto>>
{
    /// <summary>Free-text over BrandCode + BrandName.</summary>
    public string? Search { get; init; }

    public string? BrandStatus { get; init; }
    public Guid? BusinessUnitId { get; init; }
    public Guid? TherapeuticAreaId { get; init; }

    /// <summary>Defaults to false — archived brands stay out of the list unless explicitly requested.</summary>
    public bool IncludeArchived { get; init; }

    // ── WP-MDM-BRAND-ACCEPT-01 FIX1 (item 6) — server-side paging for the register (DataTables start / length) ──
    // ABSENT `Length` = every matching row, sorted by name, exactly as before: the CRM pickers read this list with a
    // `pageSize=200` this query never knew, and they keep receiving all brands. `pageSize` stays unknown on purpose.

    public const int MaxLength = 100;

    /// <summary>Zero-based first row of the page. Only read with <see cref="Length"/>.</summary>
    public int? Start { get; init; }

    /// <summary>Rows per page (1–<see cref="MaxLength"/>). Its presence turns paging on.</summary>
    public int? Length { get; init; }

    /// <summary>One of <see cref="SortableFields"/>; an unknown key is a 400, never a silent default.</summary>
    public string? OrderBy { get; init; }

    /// <summary><c>asc</c> or <c>desc</c>.</summary>
    public string? OrderDir { get; init; }

    public static readonly IReadOnlyList<string> SortableFields =
        ["brandCode", "brandName", "brandStatus", "effectiveFrom", "effectiveTo", "isArchived", "updatedAt"];
}
