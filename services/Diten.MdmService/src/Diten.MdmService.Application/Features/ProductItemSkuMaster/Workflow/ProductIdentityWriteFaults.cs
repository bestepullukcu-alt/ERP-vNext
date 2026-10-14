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
    // BL-552: the Global Product repository's name for the same capacity.
    public const string AuditCapacityExceeded = "AUDIT_INTENT_CAPACITY_EXCEEDED";

    /// <summary>
    /// The write may go through later by itself: the audit intent capacity (the outbox drains it), the store, or a
    /// lease held by another worker for the moment. Waits at the ceiling — never quarantined.
    /// </summary>
    public static bool MayPassByItself(string? code) =>
        code is AuditCapacityReached or AuditCapacityExceeded or ProductIdentityStore.Unavailable
        || code?.EndsWith("_BUSY", StringComparison.Ordinal) == true;

    /// <summary>
    /// The write met a concurrent one: read again and tried again — counted, so it cannot go on for ever.
    /// FIX6 item 4 (c): GSKU_LIFECYCLE_RELEASE_CONFLICT is the lock release's conditional write missing although the GSKU
    /// exists and still holds a lifecycle lock — its version or binding moved between the read and the write. Only a
    /// re-read can tell a race (gone on the next attempt) from a lasting mismatch (handed to a person at the limit), so
    /// it is retried and counted like any concurrent write; the rule of what may release the lock is unchanged.
    /// BL-552: GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT is the Global Product's same miss.
    /// </summary>
    public static bool MetAConcurrentWrite(string? code) =>
        code?.EndsWith("_CONCURRENCY_CONFLICT", StringComparison.Ordinal) == true
        || code is "GSKU_LIFECYCLE_RELEASE_CONFLICT" or GlobalProductReleaseConflict;

    public const string GlobalProductReleaseConflict = "GLOBAL_PRODUCT_LIFECYCLE_RELEASE_CONFLICT";
}
