using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;

/// <summary>
/// The Golden Compact list — the SERVER-MODE reference (WP-UI-LIST-SERVER-01, BL-440 package 3). The factory
/// (diten-datatable.js `createList({ dataMode: 'server' })`) sends exactly these names: `start`, `length`, `search`,
/// `orderBy` (a column's data name, whitelisted in <see cref="GoldenReferenceCompactListSort"/>), `orderDir`
/// (asc|desc), and the applied filters by key — repeated for multi (`status=Active&status=Passive`), once for single
/// (`priority=70`). Every parameter is optional; with none of them the query is the whole tenant list (no paging,
/// `length` absent = no limit), which is what the report/lookup callers read.
/// <para><see cref="ExportRowCap"/> set = the export of this same list (BL-452): from the first row, at most that many rows,
/// whatever `start`/`length` say. The export is this query — not a second one that could disagree with the screen. It is
/// never bound from the wire: only the export endpoint sets it.</para>
/// </summary>
public sealed record GetGoldenReferenceCompactListQuery(
    int? Start = null,
    int? Length = null,
    string? Search = null,
    string? OrderBy = null,
    string? OrderDir = null,
    IReadOnlyList<string>? Status = null,
    IReadOnlyList<string>? ReferenceType = null,
    IReadOnlyList<string>? Category = null,
    IReadOnlyList<string>? Owner = null,
    int? Priority = null,
    int? ExportRowCap = null) : IRequest<Response<GoldenReferenceCompactListPageDto>>
{
    /// <summary>A page is never larger than this; the DataTables length menu stops at 100.</summary>
    public const int MaxLength = 500;
}
