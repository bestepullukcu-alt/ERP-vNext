using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>The fixed part of the movements of one 0175 document (pack §8 "Every movement carries").</summary>
public sealed record MovementEnvelope(
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    string? LotId,
    string BaseUomId,
    string SourceType,
    string SourceDocumentId,
    string? DocumentGroupId,
    string? MovementReason);

/// <summary>Outcome of one posting: the ledger transaction id, or the ledger's refusal (code, status, message, details).</summary>
public sealed record PostedMovement(
    bool Posted,
    string? TransactionId,
    bool WasAlreadyPosted,
    string? ErrorCode,
    int Status,
    string? Message,
    IReadOnlyDictionary<string, object?>? Details);

/// <summary>
/// The ONLY way MOD-0175 changes stock (pack §10): every planned movement goes through the MOD-0173
/// <see cref="InternalMovementPostingService"/> — same parser, validators, lot rules and atomic repository posting as
/// <c>postMovement</c> — with <c>sourceModule = MOD-0175</c>, the 0175 source type / document / line, and an idempotency key
/// derived from the 0175 document. A key the ledger already committed is answered from the committed transaction BEFORE
/// posting, so a retry (reapply, a repeated outcome, a repeated request) never posts twice and never trips the request
/// fingerprint (the posting date of a retry differs). 0175 never touches a ledger collection itself.
/// </summary>
public sealed class QuarantineMovementPoster
{
    private readonly InternalMovementPostingService _posting;
    private readonly IInventoryLedgerRepository _ledger;
    private readonly QuarantineSupport _support;

    public QuarantineMovementPoster(InternalMovementPostingService posting, IInventoryLedgerRepository ledger, QuarantineSupport support)
    {
        _posting = posting;
        _ledger = ledger;
        _support = support;
    }

    /// <summary>The ledger idempotency key of a decision line (pack §8: <c>quarantine:{decisionId}:{lineNo}</c>).</summary>
    public static string DecisionKey(Guid decisionId, int lineNo) => $"quarantine:{decisionId:D}:{lineNo}";

    /// <summary>The ledger key of bucket <paramref name="index"/> of a no-workflow request (≤ 128, user-scoped by hash).</summary>
    public static string RequestKey(string kind, Guid userId, string requestKey, int index)
        => $"quarantine:{kind}:{QuarantineSupport.Fingerprint(("user", userId.ToString("D")), ("key", requestKey))[..32].ToLowerInvariant()}:{index}";

    /// <summary>The 0175 document number a committed ledger key already carries (a retry re-uses it), or null.</summary>
    public async Task<string?> CommittedDocumentAsync(string idempotencyKey, CancellationToken ct)
        => (await _ledger.FindByIdempotencyKeyAsync(idempotencyKey, ct))?.SourceDocumentId;

    public async Task<PostedMovement> PostAsync(
        PlannedMovement movement, MovementEnvelope envelope, string idempotencyKey, Guid actorUserId, CancellationToken ct)
    {
        var committed = await _ledger.FindByIdempotencyKeyAsync(idempotencyKey, ct);
        if (committed is not null)
        {
            return new PostedMovement(true, committed.Id.ToString("D"), true, null, 200, null, null);
        }

        var now = QuarantineSupport.Wire(QuarantineSupport.Now());
        var command = new PostMovementCommand
        {
            HeaderIdempotencyKey = idempotencyKey,
            IdempotencyKey = idempotencyKey,
            DocumentGroupId = envelope.DocumentGroupId,
            MovementType = WireNames.ToWire(movement.MovementType),
            MovementReason = envelope.MovementReason,
            ItemId = envelope.ItemId.ToString("D"),
            SkuId = envelope.SkuId.ToString("D"),
            SkuLevel = WireNames.ToWire(envelope.SkuLevel),
            FromWarehouseId = movement.FromWarehouseId,
            FromLocationId = movement.FromLocationId,
            ToWarehouseId = movement.ToWarehouseId,
            ToLocationId = movement.ToLocationId,
            Quantity = InventoryQuantity.ToWire(movement.Quantity),
            UomId = envelope.BaseUomId,
            LotId = envelope.LotId,
            SerialIds = movement.SerialIds is { Count: > 0 } serials ? serials : null, // FU-0175-Q1-SER
            FromStockStatus = movement.FromStockStatus is { } from ? WireNames.ToWire(from) : null,
            ToStockStatus = movement.ToStockStatus is { } to ? WireNames.ToWire(to) : null,
            SourceModule = QuarantineContract.ModuleId,
            SourceType = envelope.SourceType,
            SourceDocumentId = envelope.SourceDocumentId,
            SourceLineId = movement.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TransactionDate = now,
            PostingDate = now
        };

        var response = await _posting.PostAsync(command, _support.LegalEntityId, actorUserId, ct);
        return response.IsSuccessful
            ? new PostedMovement(true, response.Data!.TransactionId, response.Data.IdempotentReplay, null, response.StatusCode, null, null)
            : new PostedMovement(false, null, false, response.ErrorCode ?? InventoryErrorCodes.InvalidRequest, response.StatusCode,
                string.Join(" ", response.Errors), response.ErrorDetails);
    }
}
