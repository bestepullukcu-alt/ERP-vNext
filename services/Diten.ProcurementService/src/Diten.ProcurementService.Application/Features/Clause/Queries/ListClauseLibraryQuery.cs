using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Clause.Queries;

/// <summary>listClauseLibrary (contract GET /api/contracts/clauses). Tenant+LE filtreli; opsiyonel category + cursor.</summary>
public sealed record ListClauseLibraryQuery(
    string? Category = null,
    string? Cursor = null) : IRequest<Response<ClauseListResultDto>>;
