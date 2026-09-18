using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Commands;

/// <summary>
/// submitBid (contract POST /events/{rfxId}/bids). Yalnız Published + closesAt öncesi kabul (aksi 409 INVALID_STATE).
/// supplierId MOD-0140'ta doğrulanır + RFx invited listesinde olmalı; line.itemId MOD-0290'da (fail-closed → 404).
/// Idempotency-Key ile idempotent.
/// </summary>
public sealed record SubmitBidCommand(
    string RfxId,
    string SupplierId,
    List<BidLineInput>? Lines,
    string? IdempotencyKey) : IRequest<Response<BidDto>>;
