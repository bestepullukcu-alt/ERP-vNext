namespace Diten.InventoryService.Application.Features.Expiry;

/// <summary>
/// FEFO-BUNDLE v1.0.0 (<c>docs/analysis/contracts/fefo.openapi.yaml</c>, owner MOD-0176) — the constants the F1 slice serves:
/// base path, idempotency-key bounds, paging, the audit actions of pack §8.10 and the signable-action module (F3 registers it).
/// </summary>
public static class ExpiryContract
{
    public const string ModuleId = "MOD-0176";

    public const string BasePath = "/api/expiry";

    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public const int MaxDescriptionLength = 1000;
    public const int MaxNoteLength = 1000;

    /// <summary>The MOD-0022 module of the four signable actions (pack §8.6; registration and subject provider in F3).</summary>
    public const string ESignModule = "expiry";

    public const string ExceptionSubjectType = "expiry.exception";

    /// <summary>CT-F8: the default meaning of all four actions.</summary>
    public const string DefaultMeaningCode = "APPROVE";

    public const string DefaultVisualMode = "savedAccepted";

    // AUD-001 (pack §8.10) — the F1 events.
    public const string AuditPolicyUpdated = "expiry.policy.updated";
    public const string AuditExceptionOpened = "expiry.exception.opened";
    public const string AuditExceptionTaken = "expiry.exception.taken";
    public const string AuditExceptionCommented = "expiry.exception.commented";
    public const string AuditExceptionResolved = "expiry.exception.resolved";
    public const string AuditExceptionClosed = "expiry.exception.closed";
    public const string AuditExceptionCancelled = "expiry.exception.cancelled";

    /// <summary>prepareResolution changes the case head (the pending decision), so it is audited too (audit-trail standard §2).</summary>
    public const string AuditResolutionPrepared = "expiry.exception.resolution-prepared";

    /// <summary>The sweeper (system, no user): an EXPIRED_ON_HAND case opened / an SLA breach recorded.</summary>
    public const string AuditSlaBreached = "expiry.exception.sla-breached";

    // F2 (pack §8.10)
    public const string AuditFefoRefused = "expiry.fefo.refused";
    public const string AuditReceiptRefused = "expiry.receipt.refused";
    public const string AuditDateOverridePrepared = "expiry.date-override.prepared";
    public const string AuditDateOverrideConsumed = "expiry.date-override.consumed";

    /// <summary>KF-D: a prepared override is usable for 24 hours.</summary>
    public static readonly TimeSpan DateOverrideLifetime = TimeSpan.FromHours(24);

    public const string SubjectPolicy = "expiry.policy";
    public const string SubjectException = "expiry.exception";

    /// <summary>The number prefix of a case — <c>FX-{yyyy}-{nnnn}</c> (mockup id format).</summary>
    public const string NumberPrefix = "FX";
}
