namespace Diten.Platform.Domain.Features.RecordCodes;

// MOD-0040-FU02 §3 / §8.6 — none of these is a MongoException or an InvalidOperationException: the transaction executor
// aborts and rethrows them as they are, and the Platform's exception handler does not turn them into a 400 with the
// inner sentence. The first three are caught by the handlers; the fourth is a programming error (500).

/// <summary>The ledger insert met another allocation's (tenant, caller, idempotency key) — the inner exception is the E11000.</summary>
public sealed class RecordCodeIdempotencyConflictException(Exception innerException)
    : Exception("Another allocation holds this idempotency key.", innerException);

/// <summary>The group-prefix lock filter matched nothing: the setting was re-aligned to another prefix after step 11.</summary>
public sealed class RecordCodeGroupPrefixChangedException(string usedPrefix)
    : Exception("The tenant's group prefix changed while the code was being allocated.")
{
    public string UsedPrefix { get; } = usedPrefix;
}

/// <summary>The in-transaction outbox insert answered <c>false</c> — an invariant violation; nothing may commit.</summary>
public sealed class RecordCodeAuditOutboxInvariantException()
    : Exception("The record-code audit outbox row was not inserted.");

/// <summary>A record-code repository was called outside its tenant (empty tenant, platform context, another tenant).</summary>
public sealed class RecordCodeTenantPredicateViolationException(string message) : Exception(message);
