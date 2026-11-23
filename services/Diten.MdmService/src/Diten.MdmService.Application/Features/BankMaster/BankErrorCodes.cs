namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>MOD-0289 DL-1 error dictionary (the pack's "Hata sözlüğü" subset). The user-facing text is a resx key, never this code.</summary>
public static class BankErrorCodes
{
    public const string NotFound = "MDM_BANK_NOT_FOUND";
    public const string BranchNotFound = "MDM_BANK_BRANCH_NOT_FOUND";
    public const string ValidationFailed = "MDM_BANK_VALIDATION_FAILED";
    public const string CodeForbidden = "MDM_BANK_CODE_FORBIDDEN";
    public const string BicConflict = "MDM_BANK_BIC_CONFLICT";
    public const string NationalCodeConflict = "MDM_BANK_NATIONAL_CODE_CONFLICT";
    public const string BranchCodeConflict = "MDM_BANK_BRANCH_CODE_CONFLICT";
    public const string BranchBankMismatch = "MDM_BANK_BRANCH_BANK_MISMATCH";
    public const string VersionConflict = "MDM_BANK_VERSION_CONFLICT";
    public const string StateInvalid = "MDM_BANK_STATE_INVALID";
    public const string Inactive = "MDM_BANK_INACTIVE";
    public const string RecordCodeUnavailable = "MDM_BANK_RECORD_CODE_UNAVAILABLE";
    public const string LegalEntityUnknown = "MDM_BANK_LEGAL_ENTITY_UNKNOWN";
    public const string AuditUnavailable = "MDM_BANK_AUDIT_UNAVAILABLE";

    /// <summary>The store's collection indexes are not in place (shared with the other MDM stores).</summary>
    public const string StoreNotReady = "MDM_STORE_INDEXES_NOT_READY";

    /// <summary>The token's <c>sub</c> is not a Guid (the actor of a write must be one, D23/2).</summary>
    public const string ActorInvalid = "MDM_BANK_ACTOR_INVALID";

    /// <summary>A PUT / POST that needs <c>If-Match</c> (the version) and has none, or an unreadable one.</summary>
    public const string IfMatchRequired = "MDM_BANK_VALIDATION_FAILED";
}
