using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Commands;

/// <summary>
/// createRfxEvent (contract POST /events). Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile idempotent.
/// invitedSupplierIds MOD-0140'ta, line.itemId MOD-0290'da doğrulanır (fail-closed → 404 UNKNOWN_REFERENCE).
/// </summary>
public sealed record CreateRfxEventCommand(
    Domain.Entities.RfxType Type,
    string Title,
    DateTimeOffset? ClosesAt,
    List<string>? InvitedSupplierIds,
    List<RfxLineInput>? Lines,
    string? IdempotencyKey) : IRequest<Response<RfxEventDto>>;
