using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Commands;

/// <summary>
/// awardRfxEvent (contract POST /events/{rfxId}/award). awardedBidId RFx'e ait mevcut bid olmalı (aksi 404); RFx
/// status ∈ {Published, Evaluating} + ≥1 bid (aksi 409 INVALID_STATE). awardedSupplierId sunucu tarafından bid'den
/// çözülür. Idempotency-Key ile idempotent.
/// </summary>
public sealed record AwardRfxEventCommand(
    string RfxId,
    string AwardedBidId,
    string? Rationale,
    string? IdempotencyKey) : IRequest<Response<AwardDecisionDto>>;
