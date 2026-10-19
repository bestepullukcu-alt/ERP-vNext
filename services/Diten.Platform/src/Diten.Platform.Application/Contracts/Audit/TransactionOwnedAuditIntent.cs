using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — what a handler SAYS about the change it is writing in its own transaction. It is not a
/// payload: the canonical payload (tenant, correlation, actor, masking, redaction, source service) is built in one
/// place, at the in-transaction writer's entry (<c>CanonicalTransactionalAuditOutboxWriter</c>). A request without an
/// intent is refused there — the in-transaction door takes no hand-written payload.
/// </summary>
public sealed record TransactionOwnedAuditIntent
{
    public required AuditCategory Category { get; init; }

    /// <summary>The tenant the change is ABOUT (a platform administrator acting on a tenant); null when platform-global.</summary>
    public Guid? TargetTenantId { get; init; }

    /// <summary>State before the change — statuses and dates, never secrets or personal data.</summary>
    public IReadOnlyDictionary<string, object?>? BeforeState { get; init; }

    public IReadOnlyDictionary<string, object?>? AfterState { get; init; }

    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    public string? SourceModule { get; init; }

    /// <summary>
    /// The NAME of the unattended job that may make this change without a signed-in person (a startup seed, a
    /// self-registration worker, a service-to-service call). Null means a person must be named: a call with no
    /// authenticated principal is then refused and — being the same transaction — the business data is not written.
    /// </summary>
    public string? SystemActor { get; init; }
}
