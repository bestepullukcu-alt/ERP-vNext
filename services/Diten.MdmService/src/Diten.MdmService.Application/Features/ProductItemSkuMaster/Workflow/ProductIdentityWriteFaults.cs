using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX5 item 3: what a refused GSKU write (apply a decision, give a lock back, record why) means
/// for the request that made it. FIX4 retried every such refusal as "transient" — for ever, so a refusal that repeating
/// can never change (an idempotency conflict, a contract the stored request does not meet, a lock held by another
/// operation, a GSKU that is gone) kept the GSKU locked with nobody told.
/// </summary>
public static class ProductIdentityWriteFaults
{
    public const string AuditCapacityReached = "AUDIT_INTENT_CAPACITY_REACHED";

    /// <summary>
    /// The write may go through later by itself: the audit intent capacity (the outbox drains it), the store, or a
    /// lease held by another worker for the moment. Waits at the ceiling — never quarantined.
    /// </summary>
    public static bool MayPassByItself(string? code) =>
        code is AuditCapacityReached or ProductIdentityStore.Unavailable
        || code?.EndsWith("_BUSY", StringComparison.Ordinal) == true;

    /// <summary>The write met a concurrent one: read again and tried again — counted, so it cannot go on for ever.</summary>
    public static bool MetAConcurrentWrite(string? code) =>
        code?.EndsWith("_CONCURRENCY_CONFLICT", StringComparison.Ordinal) == true;
}
