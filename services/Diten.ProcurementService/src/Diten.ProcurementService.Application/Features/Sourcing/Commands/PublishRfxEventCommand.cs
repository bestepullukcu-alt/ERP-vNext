using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Commands;

/// <summary>
/// publishRfxEvent (contract POST /events/{rfxId}/publish). Yalnız Draft→Published; Draft dışı → 409 INVALID_STATE.
/// Idempotency-Key ile idempotent (aynı key ile replay yan-etkisiz).
/// </summary>
public sealed record PublishRfxEventCommand(
    string RfxId,
    string? IdempotencyKey) : IRequest<Response<RfxEventDto>>;
