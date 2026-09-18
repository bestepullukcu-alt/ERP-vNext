using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Clause.Commands;

/// <summary>
/// createClause (contract POST /api/contracts/clauses). Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile
/// idempotent (replay → aynı clause, yeni kayıt YOK). category/title/body zorunlu (trim). (Category+Title) tenant+LE
/// bazında unique → 409 DUPLICATE_CLAUSE. Clause IMMUTABLE (ASSUMPTION-0144-03): update/delete YOK.
/// </summary>
public sealed record CreateClauseCommand(
    string Category,
    string Title,
    string Body,
    string? IdempotencyKey) : IRequest<Response<ClauseDto>>;
