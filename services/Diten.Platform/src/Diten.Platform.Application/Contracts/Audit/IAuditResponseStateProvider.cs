namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// MOD-0262-FU08 (K-6 path b; form: CT decision; protected-path permission K-7, Beste 2026-11-13) — an audited command that
/// can name, FROM ITS OWN SUCCESSFUL RESPONSE, the object its record is about. <c>AuditBehavior</c> takes the command's
/// metadata before the handler runs, so a store-assigned id (a <c>ContentId</c> born in the provider) cannot be in it; this
/// optional interface lets the command hand the id and the after-state over once the response exists.
/// <list type="bullet">
/// <item>Asked ONLY on a successful outcome. Never on a failed response, a runtime exception, a cancellation, an excluded or
/// query request, or a transaction-owned command.</item>
/// <item>A pure mapping of the response: no repository read, no service call, no side effect.</item>
/// <item>The command's own metadata wins: <c>EntityId = metadata.EntityId ?? state.EntityId</c>, the same for
/// <c>AfterState</c>.</item>
/// <item>A provider that throws does not change the business answer: the record is written without state.</item>
/// </list>
/// Optional: a command that does not implement it is recorded exactly as before (no reflection over the response).
/// </summary>
public interface IAuditResponseStateProvider<in TResponse>
{
    AuditResponseState? GetAuditResponseState(TResponse response);
}

/// <summary>MOD-0262-FU08 — what a successful response adds to its audit record: the entity id and the after-state.</summary>
public sealed record AuditResponseState(Guid? EntityId, IReadOnlyDictionary<string, object?>? AfterState);
