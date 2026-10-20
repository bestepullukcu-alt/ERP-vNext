using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Queries;

/// <summary>The preview (§4.2 ④): the counts + the rows, filtered by <paramref name="RowStatus"/> (comma separated:
/// matched, unmatched, retired, duplicate, invalid-price, removed) and / or <paramref name="DiffKind"/> (added, changed,
/// unchanged).</summary>
public sealed record GetPriceListImportQuery(Guid Id, string? RowStatus, string? DiffKind, int? Page, int? PageSize)
    : IRequest<Response<PriceListImportPreviewDto>>;
