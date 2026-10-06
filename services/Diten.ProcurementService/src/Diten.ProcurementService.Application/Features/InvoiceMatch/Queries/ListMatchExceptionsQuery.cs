using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;

/// <summary>listMatchExceptions (contract GET /api/invoice-match/exceptions). Tenant+LE filtreli; opsiyonel reasonCode
/// + cursor sayfalama.</summary>
public sealed record ListMatchExceptionsQuery(
    MatchExceptionReason? ReasonCode = null,
    string? Cursor = null) : IRequest<Response<MatchExceptionListResultDto>>;
