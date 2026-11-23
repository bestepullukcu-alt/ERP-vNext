namespace Diten.MdmService.Application.Contracts.RecordCodes;

/// <summary>
/// RCS-001 (RECORD-CODE-ALLOCATE 1.0.0, KARAR D59/1, D95/1) — the ONE port through which MdmService takes a record's
/// display code from the shared code service. MdmService has exactly one port and one adapter (MOD-0289 and MOD-0217 share
/// it): no counter, fallback generator or <c>CodeReservation</c> of its own produces a code for these records (RCS-001 §6).
/// A caller that has no port bound (the adapter's configuration is missing) fails closed — no record without a code.
/// </summary>
public interface IRecordCodeAllocator
{
    /// <summary>
    /// One allocation attempt. <paramref name="legalEntityId"/> is null for <see cref="RecordCodePrefixScope.Group"/> (a bank:
    /// <c>BNK</c>, group prefix) and the owning company for <see cref="RecordCodePrefixScope.LegalEntity"/>.
    /// <paramref name="idempotencyKey"/> is made by the server for each Create attempt (1–200 printable ASCII).
    /// </summary>
    Task<RecordCodeAllocationResult> AllocateAsync(
        RecordCodePrefixScope prefixScope,
        Guid? legalEntityId,
        string recordType,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>The contract's <c>prefixScope</c> (written as the member's name on the wire).</summary>
public enum RecordCodePrefixScope
{
    LegalEntity,
    Group,
    Platform
}

/// <summary>How an allocation ended, in the terms the Create answers (KARAR D59/1, D133).</summary>
public enum RecordCodeAllocationOutcome
{
    /// <summary>200 with <c>isSuccessful</c> true and a <c>data.code</c>.</summary>
    Allocated,

    /// <summary>403 RECORD_CODE_PERMISSION_MISSING or LEGAL_ENTITY_READ_PERMISSION_MISSING: Create answers 403, not retried.</summary>
    PermissionMissing,

    /// <summary>404 LEGAL_ENTITY_NOT_FOUND: Create answers 422 MDM_BANK_LEGAL_ENTITY_UNKNOWN, not retried.</summary>
    LegalEntityUnknown,

    /// <summary>
    /// Anything else — a non-200 answer (422 / 409 / 5xx, ProblemDetails included), a 200 that does not fit the envelope, a
    /// timeout, or no bound port: Create answers 503 MDM_BANK_RECORD_CODE_UNAVAILABLE and writes nothing.
    /// </summary>
    Unavailable
}

public sealed record RecordCodeAllocationResult(
    RecordCodeAllocationOutcome Outcome,
    string? Code = null,
    long Sequence = 0,
    string? ReasonCode = null)
{
    public static RecordCodeAllocationResult Allocated(string code, long sequence) =>
        new(RecordCodeAllocationOutcome.Allocated, code, sequence);

    public static RecordCodeAllocationResult PermissionMissing(string? reasonCode = null) =>
        new(RecordCodeAllocationOutcome.PermissionMissing, ReasonCode: reasonCode);

    public static RecordCodeAllocationResult LegalEntityUnknown() =>
        new(RecordCodeAllocationOutcome.LegalEntityUnknown, ReasonCode: "LEGAL_ENTITY_NOT_FOUND");

    public static RecordCodeAllocationResult Unavailable(string? reasonCode = null) =>
        new(RecordCodeAllocationOutcome.Unavailable, ReasonCode: reasonCode);
}
