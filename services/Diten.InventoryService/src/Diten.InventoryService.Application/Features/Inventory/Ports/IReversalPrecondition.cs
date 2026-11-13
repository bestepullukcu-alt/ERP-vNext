using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Application.Features.Inventory.Ports;

/// <summary>
/// MOD-0175-FU01 DL-3 (pack §8.8, Q-06 = A; XMC-001 MOD-0173, Beste 2026-11-03) — the ONE seam a REVERSAL passes before the ledger builds it:
/// <c>InternalMovementPostingService.BuildReversalAsync</c> asks it right after the "already reversed" check, out of any transaction.
/// <list type="bullet">
/// <item><see cref="CheckAsync"/> — <see cref="ReversalPreconditionResult.Allow"/> or <see cref="ReversalPreconditionResult.Refuse"/>
/// (a 422 with a closed-catalogue code and its details); nothing is written by the ledger on a refusal. An exception means the
/// precondition could not read what it needs: the posting answers 503 <c>INVENTORY_UNAVAILABLE</c> (fail-closed, never Allow).</item>
/// <item><see cref="FlushRefusalAuditAsync"/> — after a refusal: the precondition writes its own <c>inventory_audit</c> refusal row
/// (<c>IExpiryPostingPolicy.FlushRefusalAuditAsync</c> pattern); a no-op otherwise.</item>
/// </list>
/// The precondition reads in the posting's own tenant + company only (the scoped repositories).
/// </summary>
public interface IReversalPrecondition
{
    Task<ReversalPreconditionResult> CheckAsync(InventoryTransaction original, CancellationToken cancellationToken);

    Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default);
}

/// <summary>Allow, or Refuse(code, message, details) — a refusal is always a 422.</summary>
public sealed record ReversalPreconditionResult(bool Allowed, string? Code, string? Message, IReadOnlyDictionary<string, object?>? Details)
{
    public static ReversalPreconditionResult Allow { get; } = new(true, null, null, null);

    public static ReversalPreconditionResult Refuse(string code, string message, IReadOnlyDictionary<string, object?> details)
        => new(false, code, message, details);
}

/// <summary>A hand-built posting service (tests, tools) has no precondition: every REVERSAL is allowed. DI always registers the real one.</summary>
public sealed class AllowAllReversalPrecondition : IReversalPrecondition
{
    public Task<ReversalPreconditionResult> CheckAsync(InventoryTransaction original, CancellationToken cancellationToken)
        => Task.FromResult(ReversalPreconditionResult.Allow);

    public Task FlushRefusalAuditAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
