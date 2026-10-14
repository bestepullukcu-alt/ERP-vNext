using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>One stock bucket a recall asks MOD-0175 to quarantine (pack §8.4: one bucket per call).</summary>
public sealed record RecallQuarantineBucket(Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string WarehouseId, string LocationId, decimal Quantity, string LotId);

/// <summary>
/// The call (CT-R2): the bucket, the reason <c>"Geri çağırma RC-… · &lt;case reason&gt;"</c>, the idempotency key
/// <c>recall:{caseId}:r{revision}:{nodeKey}[:a{n}]</c> and the origin (case id + number) the hold records (QUARANTINE 1.3.0).
/// The actor is the caller's resolved scope user (the approver / the QA user who asked).
/// </summary>
public sealed record RecallQuarantineCall(RecallQuarantineBucket Bucket, string Reason, string IdempotencyKey, Guid CaseId, string CaseNumber);

/// <summary>A hold of MOD-0175 as the recall shows it (read at display time, never copied).</summary>
public sealed record RecallHoldView(string HoldId, string HoldNumber, string Status, string ReviewState, decimal Quantity, string? LotId,
    string WarehouseId, string LocationId);

/// <summary>
/// MOD-0175's answer: <see cref="HoldIds"/> when it held (201 new / 200 replay), else its refusal — <c>QUARANTINE_STOCK_RESERVED</c> (with the
/// reservation lines in <see cref="Details"/>), <c>QUARANTINE_NOTHING_AVAILABLE</c>, or any other code (period closed, 503 …).
/// </summary>
public sealed record RecallQuarantineAnswer(int Status, IReadOnlyList<RecallHoldView> Holds, string? ErrorCode, string? Message, IReadOnlyDictionary<string, object?>? Details)
{
    public bool Held => Status is 200 or 201 && Holds.Count > 0;
}

/// <summary>
/// MOD-0177 R3 (pack §8.4 KR-B, CT-R2) — the in-process port into MOD-0175's <c>quarantineStock</c> (mode Manual), implemented by MOD-0175's
/// application layer (no HTTP). 0175 runs every stock / lot / reservation / e-sign-policy check unchanged; the recall never decides for 0175 and
/// never cancels a reservation. The approver's <c>recall.cases.approve</c> (or the QA user's <c>recall.quarantine-requests.create</c>) authorises
/// the call — 0175 does not re-check <c>quarantine.holds.create</c> for this in-process caller (CT-R2).
/// </summary>
public interface IRecallQuarantineRequester
{
    Task<RecallQuarantineAnswer> QuarantineAsync(RecallQuarantineCall call, CancellationToken cancellationToken = default);

    /// <summary>The holds (any status) of these ids in the caller's company — the live status of a request.</summary>
    Task<IReadOnlyList<RecallHoldView>> HoldsAsync(IReadOnlyCollection<string> holdIds, CancellationToken cancellationToken = default);

    /// <summary>The open (not decided) holds of a lot in the caller's company — "already quarantined" shows the existing hold.</summary>
    Task<IReadOnlyList<RecallHoldView>> OpenHoldsOfLotAsync(string lotId, CancellationToken cancellationToken = default);
}
