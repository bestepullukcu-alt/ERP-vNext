using Diten.InventoryService.Application.Features.Inventory.Commands;

namespace Diten.InventoryService.Application.Features.Inventory.Ports;

/// <summary>What the ledger holds under an idempotency key, compared with the request that would be posted under it.</summary>
public enum PostingKeyState
{
    /// <summary>No movement carries the key in this Tenant + LE.</summary>
    NotCommitted,

    /// <summary>A movement carries the key and its request fingerprint equals the given request's — the same movement.</summary>
    Committed,

    /// <summary>A movement carries the key but was posted from a DIFFERENT request (postMovement would answer 400 IDEMPOTENCY_KEY_REUSED).</summary>
    KeyReusedDifferentPayload
}

/// <summary>
/// The movement that holds a key (MOD-0178 pack §5, §4.2 <c>HolderMovement</c>): its identity, its source envelope, what it moved and — when
/// MOD-0173's owner reversed it — the id of the REVERSAL (a key bound to a movement is never freed: the ledger is append-only).
/// </summary>
public sealed record PostingKeyHolder(
    Guid TransactionId,
    string TransactionNumber,
    string SourceModule,
    string SourceType,
    string? SourceDocumentId,
    string? SourceLineId,
    decimal Quantity,
    string UomId,
    decimal BaseQuantity,
    string? FromWarehouseId,
    string? FromLocationId,
    string? ToWarehouseId,
    string? ToLocationId,
    string? LotId,
    IReadOnlyList<string>? SerialIds,
    string? FromStockStatus,
    string? ToStockStatus,
    DateTimeOffset PostingDate,
    Guid? ReversedByTransactionId);

public sealed record PostingKeyLookupResult(PostingKeyState State, PostingKeyHolder? Holder)
{
    public static readonly PostingKeyLookupResult NotCommitted = new(PostingKeyState.NotCommitted, null);
}

/// <summary>
/// MOD-0173 additive port (MOD-0178 pack §5, inspection 02 F-1): "has the ledger already posted this request's key?" — the same answer
/// <c>postMovement</c> gives before it validates anything (<see cref="Services.InternalMovementPostingService"/>: key lookup, then the
/// fingerprint), WITHOUT posting and WITHOUT any upstream call (repository + <see cref="Services.RequestFingerprint"/> only). Bound to the
/// caller's resolved Tenant + LE like the ledger repository. A caller that must not depend on the ledger store (MOD-0178) asks this port.
/// </summary>
public interface IPostingKeyLookup
{
    /// <summary><paramref name="storedRequest"/> is the exact request the caller posts (or would post) under the key — it must parse.</summary>
    Task<PostingKeyLookupResult> LookupAsync(PostMovementCommand storedRequest, CancellationToken cancellationToken = default);
}
