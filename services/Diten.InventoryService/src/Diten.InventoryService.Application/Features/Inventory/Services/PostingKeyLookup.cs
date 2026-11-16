using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// <see cref="IPostingKeyLookup"/> over the ledger repository (MOD-0178 pack §5): the same parser and fingerprint as the posting path, the
/// committed movement under the key, and — for a committed one — its reversal (<see cref="IInventoryLedgerRepository.FindReversalOfAsync"/>).
/// Reads only; no upstream call.
/// </summary>
public sealed class PostingKeyLookup : IPostingKeyLookup
{
    private readonly IInventoryLedgerRepository _ledger;

    public PostingKeyLookup(IInventoryLedgerRepository ledger) => _ledger = ledger;

    public async Task<PostingKeyLookupResult> LookupAsync(PostMovementCommand storedRequest, CancellationToken cancellationToken = default)
    {
        var (movement, errors) = MovementRequestParser.Parse(storedRequest);
        if (movement is null)
        {
            throw new InvalidOperationException($"The stored movement request does not parse: {errors[0].Message}");
        }

        var committed = await _ledger.FindByIdempotencyKeyAsync(movement.IdempotencyKey, cancellationToken);
        if (committed is null) return PostingKeyLookupResult.NotCommitted;

        var reversal = await _ledger.FindReversalOfAsync(committed.Id, cancellationToken);
        var holder = new PostingKeyHolder(
            committed.Id, committed.TransactionNumber, committed.SourceModule, committed.SourceType, committed.SourceDocumentId, committed.SourceLineId,
            committed.Quantity, committed.UomId, committed.BaseQuantity, committed.FromWarehouseId, committed.FromLocationId, committed.ToWarehouseId,
            committed.ToLocationId, committed.LotId, committed.SerialIds,
            committed.FromStockStatus is { } from ? WireNames.ToWire(from) : null, committed.ToStockStatus is { } to ? WireNames.ToWire(to) : null,
            committed.PostingDate, reversal?.Id);
        var same = string.Equals(committed.RequestFingerprint, RequestFingerprint.Compute(movement), StringComparison.Ordinal);
        return new PostingKeyLookupResult(same ? PostingKeyState.Committed : PostingKeyState.KeyReusedDifferentPayload, holder);
    }
}
