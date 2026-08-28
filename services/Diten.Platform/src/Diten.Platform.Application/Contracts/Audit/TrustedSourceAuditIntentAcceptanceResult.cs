namespace Diten.Platform.Application.Contracts.Audit;

public sealed class TrustedSourceAuditIntentAcceptanceResult
{
    private TrustedSourceAuditIntentAcceptanceResult(
        AcceptanceStatus status,
        TrustedSourceAuditIntentAcceptanceReceipt? receipt,
        string? errorCode)
    {
        Status = status;
        Receipt = receipt;
        ErrorCode = errorCode;
    }

    public enum AcceptanceStatus
    {
        Accepted = 1,
        Duplicate = 2,
        Invalid = 3,
        ContractUnsupported = 4,
        MappingUnsupported = 5,
        IdempotencyConflict = 6,
        Unavailable = 7
    }

    public AcceptanceStatus Status { get; }
    public TrustedSourceAuditIntentAcceptanceReceipt? Receipt { get; }
    public string? ErrorCode { get; }
    public bool IsAccepted => Status is AcceptanceStatus.Accepted or AcceptanceStatus.Duplicate;

    public static TrustedSourceAuditIntentAcceptanceResult Accepted(
        TrustedSourceAuditIntentAcceptanceReceipt receipt) =>
        new(AcceptanceStatus.Accepted, receipt with { Duplicate = false }, null);

    public static TrustedSourceAuditIntentAcceptanceResult Duplicate(
        TrustedSourceAuditIntentAcceptanceReceipt receipt) =>
        new(AcceptanceStatus.Duplicate, receipt with { Duplicate = true }, null);

    public static TrustedSourceAuditIntentAcceptanceResult Invalid() =>
        new(AcceptanceStatus.Invalid, null, "AUDIT_SOURCE_INTENT_INVALID");

    public static TrustedSourceAuditIntentAcceptanceResult ContractUnsupported() =>
        new(AcceptanceStatus.ContractUnsupported, null, "AUDIT_SOURCE_INTENT_CONTRACT_UNSUPPORTED");

    public static TrustedSourceAuditIntentAcceptanceResult MappingUnsupported() =>
        new(AcceptanceStatus.MappingUnsupported, null, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED");

    public static TrustedSourceAuditIntentAcceptanceResult IdempotencyConflict() =>
        new(AcceptanceStatus.IdempotencyConflict, null, "AUDIT_SOURCE_INTENT_IDEMPOTENCY_CONFLICT");

    public static TrustedSourceAuditIntentAcceptanceResult Unavailable() =>
        new(AcceptanceStatus.Unavailable, null, "AUDIT_SOURCE_INTENT_UNAVAILABLE");
}
