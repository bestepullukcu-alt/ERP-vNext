namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>RECORD-CODE-ALLOCATE 1.0.0 §4 — the <c>reason_code</c> values of the record-code endpoints.</summary>
public static class RecordCodeReasonCodes
{
    public const string RequestTooLarge = "RECORD_CODE_REQUEST_TOO_LARGE";
    public const string Unauthenticated = "RECORD_CODE_UNAUTHENTICATED";
    public const string Forbidden = "RECORD_CODE_FORBIDDEN";
    public const string RateLimited = "RECORD_CODE_RATE_LIMITED";
    public const string TenantNotActive = "RECORD_CODE_TENANT_NOT_ACTIVE";
    public const string RequestInvalid = "RECORD_CODE_REQUEST_INVALID";
    public const string RecordTypeUnknown = "RECORD_TYPE_UNKNOWN";
    public const string PermissionMissing = "RECORD_CODE_PERMISSION_MISSING";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string ScopeInvalid = "RECORD_CODE_SCOPE_INVALID";
    public const string GroupPrefixMissing = "GROUP_PREFIX_MISSING";
    public const string GroupPrefixMismatch = "GROUP_PREFIX_MISMATCH";
    public const string LegalEntityNotFound = "LEGAL_ENTITY_NOT_FOUND";
    public const string LegalEntityReadPermissionMissing = "LEGAL_ENTITY_READ_PERMISSION_MISSING";
    public const string LegalEntityNotEligible = "LEGAL_ENTITY_NOT_ELIGIBLE";
    public const string LegalEntityShortCodeMissing = "LEGAL_ENTITY_SHORT_CODE_MISSING";
    public const string LegalEntityShortCodeChanged = "LEGAL_ENTITY_SHORT_CODE_CHANGED";
    public const string LegalEntitySourceUnavailable = "LEGAL_ENTITY_SOURCE_UNAVAILABLE";
    public const string Unavailable = "RECORD_CODE_UNAVAILABLE";

    // The group-prefix alignment endpoint (pack §14).
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string GroupPrefixSourceInvalid = "GROUP_PREFIX_SOURCE_INVALID";
    public const string GroupPrefixLocked = "GROUP_PREFIX_LOCKED";
}
