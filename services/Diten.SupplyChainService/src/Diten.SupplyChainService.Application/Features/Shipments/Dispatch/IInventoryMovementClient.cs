using System.Text.Json.Nodes;
namespace Diten.SupplyChainService.Application.Features.Shipments.Dispatch;

/// <summary>What INVENTORY 1.5.0 postMovement answered, told apart the way MOD-0183 §23.2–§23.4 acts on it.</summary>
public enum InventoryOutcome
{
    /// <summary>201, or 200 with idempotentReplay — the movement exists.</summary>
    Posted,
    /// <summary>422 — a business refusal; nothing was stored (or ALREADY_REVERSED on a REVERSAL).</summary>
    Refused,
    /// <summary>400 — our request is malformed (or IDEMPOTENCY_KEY_REUSED): an integrity fault, never a refusal.</summary>
    Invalid,
    /// <summary>403 — an authorization outcome (missing permission or CROSS_LE_FORBIDDEN).</summary>
    Forbidden,
    /// <summary>5xx, 401, timeout, unreachable, or an answer that does not parse.</summary>
    Unavailable
}

public sealed record InventoryPostRequest(string IdempotencyKey, string BodyJson, Guid TenantId, Guid LegalEntityId, Guid CorrelationId, string Authorization);

/// <param name="Ambiguous">True when INVENTORY may still have processed the request (timeout, connection lost after send).</param>
public sealed record InventoryPostResult(InventoryOutcome Outcome, int HttpStatus, string? TransactionId = null, bool Replay = false,
    string? LotId = null, string? InventoryCode = null, JsonObject? Details = null, bool Ambiguous = false);

/// <summary>
/// The only INVENTORY write of SupplyChain (MOD-0183 §23). Only the dispatch coordinator and the late-writer policy may
/// use it — no hosted service posts or reverses (S23-12, architecture test).
/// </summary>
public interface IInventoryMovementClient
{
    Task<InventoryPostResult> PostAsync(InventoryPostRequest request, CancellationToken ct);
}

/// <summary>The INVENTORY request timeout; the dispatch lease is that timeout + 30 s (§23.4).</summary>
public sealed record DispatchOptions(TimeSpan RequestTimeout)
{
    public TimeSpan Lease => RequestTimeout + TimeSpan.FromSeconds(30);
}
