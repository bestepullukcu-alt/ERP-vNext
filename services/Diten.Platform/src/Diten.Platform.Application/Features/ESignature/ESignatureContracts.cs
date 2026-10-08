using Diten.Platform.Domain.Entities.ESignature;

namespace Diten.Platform.Application.Features.ESignature;

/// <summary>MOD-0022 — the <c>esign.*</c> permission namespace (pack §14, ESIGN-BUNDLE §3). Every key is Scope = Tenant.</summary>
public static class ESignPermissions
{
    public const string SignaturesRead = "esign.signatures.read";
    public const string SignaturesSign = "esign.signatures.sign";
    public const string SignaturesVerify = "esign.signatures.verify";
    public const string SignaturesInvalidate = "esign.signatures.invalidate";
    public const string SignaturesExport = "esign.signatures.export";
    public const string RequestsRead = "esign.requests.read";
    public const string RequestsCreate = "esign.requests.create";
    public const string RequestsReject = "esign.requests.reject";
    public const string MySamplesManage = "esign.my-samples.manage";
    public const string SamplesApprove = "esign.samples.approve";
    public const string SamplesReject = "esign.samples.reject";
    public const string PolicyRead = "esign.policy.read";
    public const string PolicyManage = "esign.policy.manage";
    public const string MeaningsRead = "esign.meanings.read";
    public const string MeaningsManage = "esign.meanings.manage";

    public static readonly IReadOnlyList<string> All =
    [
        SignaturesRead, SignaturesSign, SignaturesVerify, SignaturesInvalidate, SignaturesExport,
        RequestsRead, RequestsCreate, RequestsReject, MySamplesManage, SamplesApprove, SamplesReject,
        PolicyRead, PolicyManage, MeaningsRead, MeaningsManage
    ];
}

/// <summary>MOD-0022 — error codes (ESIGN-BUNDLE §4.27). Carried in the Platform envelope's <c>reason_code</c>.</summary>
public static class ESignCodes
{
    public const string TicketRequired = "ESIGN_TICKET_REQUIRED";
    public const string TicketInvalid = "ESIGN_TICKET_INVALID";
    public const string ReauthRequired = "ESIGN_REAUTH_REQUIRED";
    public const string ReauthUnavailable = "ESIGN_REAUTH_UNAVAILABLE";
    public const string SubjectNotFound = "ESIGN_SUBJECT_NOT_FOUND";
    public const string MeaningNotAllowed = "ESIGN_MEANING_NOT_ALLOWED";
    public const string AppearanceNotAllowed = "ESIGN_APPEARANCE_NOT_ALLOWED";
    public const string SampleNotActive = "ESIGN_SAMPLE_NOT_ACTIVE";
    public const string DrawingEmpty = "ESIGN_DRAWING_EMPTY";
    public const string ImageNotPng = "ESIGN_IMAGE_NOT_PNG";
    public const string ImageTooLarge = "ESIGN_IMAGE_TOO_LARGE";
    public const string RequestNotOpen = "ESIGN_REQUEST_NOT_OPEN";
    public const string SignerNotNominated = "ESIGN_SIGNER_NOT_NOMINATED";
    public const string SignerNotFound = "ESIGN_SIGNER_NOT_FOUND";
    public const string ReasonTooShort = "ESIGN_REASON_TOO_SHORT";
    public const string AlreadyInvalidated = "ESIGN_ALREADY_INVALIDATED";
    public const string PolicyVersionConflict = "ESIGN_POLICY_VERSION_CONFLICT";
    public const string SecondFactorNotAvailable = "ESIGN_SECOND_FACTOR_NOT_AVAILABLE";
    public const string MeaningLanguageMissing = "ESIGN_MEANING_LANGUAGE_MISSING";
    public const string MeaningCodeExists = "ESIGN_MEANING_CODE_EXISTS";
    public const string MeaningCodeLocked = "ESIGN_MEANING_CODE_LOCKED";
    public const string MeaningInUse = "ESIGN_MEANING_IN_USE";
    public const string ActionPrefixMismatch = "ESIGN_ACTION_PREFIX_MISMATCH";
    public const string ExportTooLarge = "ESIGN_EXPORT_TOO_LARGE";
    public const string SignatureMismatch = "ESIGN_SIGNATURE_MISMATCH";
    public const string SelfApprovalForbidden = "ESIGN_SELF_APPROVAL_FORBIDDEN";
    public const string SampleNotPending = "ESIGN_SAMPLE_NOT_PENDING";
    public const string SampleInUse = "ESIGN_SAMPLE_IN_USE";

    /// <summary>Validation failures that have no ESIGN code of their own (V11, V15, V23, V24, shapes).</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    public const string NotFound = "ESIGN_NOT_FOUND";
}

/// <summary>Audit event names (ESIGN-BUNDLE §4) and the source module.</summary>
public static class ESignAuditEvents
{
    public const string SourceModule = "MOD-0022";
    public const string Signed = "esign.signature.signed";
    public const string Verified = "esign.signature.verified";
    public const string Invalidated = "esign.signature.invalidated";
    public const string RequestCreated = "esign.request.created";
    public const string RequestRejected = "esign.request.rejected";
    public const string RequestCancelled = "esign.request.cancelled";
    public const string RequestExpired = "esign.request.expired";
    public const string PolicyChanged = "esign.policy.changed";
    public const string PolicyOverrideReverted = "esign.policy.override-reverted";
    public const string MeaningCreated = "esign.meaning.created";
    public const string MeaningUpdated = "esign.meaning.updated";
    public const string MeaningActivated = "esign.meaning.activated";
    public const string MeaningDeactivated = "esign.meaning.deactivated";
    public const string ActionsRegistered = "esign.actions.registered";
    public const string ExportCreated = "esign.export.created";
    public const string SampleSubmitted = "esign.sample.submitted";
    public const string SampleApproved = "esign.sample.approved";
    public const string SampleRejected = "esign.sample.rejected";
    public const string SampleDeactivated = "esign.sample.deactivated";
    public const string SampleWithdrawn = "esign.sample.withdrawn";
}

// ── wire DTOs (ESIGN-BUNDLE §2; camelCase by the default System.Text.Json policy) ─────────────────────────────────────

public sealed record SubjectRefDto(string Module, string SubjectType, string SubjectId);

public sealed record SummaryLineDto(string Label, string Value);

public sealed record SignableSubjectDto(string Display, IReadOnlyList<SummaryLineDto> Summary, Guid? LegalEntityId, IReadOnlyDictionary<string, string?> Fields);

public sealed record SignerDto(string UserId, string Login, string Name);

public sealed record ReauthDto(string Kind, DateTime PasswordVerifiedAtUtc);

public sealed record AppearanceDto(string Type, Guid? SampleId, string? ImageHash, bool HasVector);

public sealed record PolicySnapshotDto(string Scope, Guid? LegalEntityId, int Version, string VisualMode, bool ReauthEvery, int ReauthWindowMinutes);

public sealed record InvalidationDto(string Reason, string By, string ByName, DateTime AtUtc);

public sealed record SignatureRecordDto(
    Guid Id,
    string SignatureNumber,
    SubjectRefDto Subject,
    string SubjectDisplay,
    IReadOnlyList<SummaryLineDto> SubjectSummary,
    Guid? LegalEntityId,
    string? ActionCode,
    IReadOnlyDictionary<string, string?> Context,
    string MeaningCode,
    string MeaningText,
    string MeaningLanguage,
    SignerDto Signer,
    DateTime SignedAtUtc,
    string? Note,
    ReauthDto Reauth,
    string SecondFactor,
    AppearanceDto Appearance,
    string SubjectHashAlgorithm,
    string SubjectHash,
    string SignatureDigest,
    Guid? RequestId,
    PolicySnapshotDto Policy,
    InvalidationDto? Invalidation);

public sealed record VerificationResultDto(
    Guid SignatureId,
    string Status,
    string SubjectHashAtSign,
    string? CurrentSubjectHash,
    bool ImageIntact,
    bool DigestIntact,
    DateTime VerifiedAtUtc);

public sealed record RequesterDto(string UserId, string Name);

public sealed record SignatureRequestDto(
    Guid Id,
    string RequestNumber,
    SubjectRefDto Subject,
    string SubjectDisplay,
    Guid? LegalEntityId,
    string? ActionCode,
    string MeaningCode,
    RequesterDto RequestedBy,
    DateTime RequestedAtUtc,
    string SignerUserId,
    DateTime DueAtUtc,
    string Status,
    Guid? SignatureId,
    string? RejectReason,
    DateTime? DecidedAtUtc);

public sealed record SignatureRequestPageDto(IReadOnlyList<SignatureRequestDto> Items, long Total, int Page, int PageSize, long WaitingCount);

public sealed record PolicyRowDto(
    string ActionCode,
    string Module,
    string ActionName,
    bool Required,
    string? MeaningCode,
    string VisualMode,
    string SecondFactor,
    bool DiffersFromTenant);

public sealed record PolicyDto(
    string Scope,
    Guid? LegalEntityId,
    int Version,
    IReadOnlyList<PolicyRowDto> Rows,
    bool ReauthEvery,
    int ReauthWindowMinutes,
    bool SampleApprovalRequired);

public sealed record PolicyLegalEntityDto(Guid Id, string Name, bool HasOverride);

public sealed record PolicyViewDto(PolicyDto Tenant, PolicyDto? Override, PolicyDto Effective, IReadOnlyList<PolicyLegalEntityDto> LegalEntities);

public sealed record PolicyHistoryEntryDto(DateTime AtUtc, string By, string Scope, Guid? LegalEntityId, IReadOnlyList<string> Changes, string Reason);

public sealed record MeaningDto(string Code, IReadOnlyDictionary<string, string> Names, string? Description, bool IsActive, bool IsLocked, long UsageCount);

public sealed record SignerIdentityDto(string Login, string Name);

public sealed record EffectivePolicyDto(
    bool Required,
    string? MeaningCode,
    string VisualMode,
    bool ReauthEvery,
    int ReauthWindowMinutes,
    IReadOnlyList<MeaningDto> AllowedMeanings,
    SignableSubjectDto Subject,
    object? MySample,
    object? MyPendingSample,
    SignerIdentityDto Signer);

public sealed record SignableActionDto(
    string ActionCode,
    string Module,
    IReadOnlyDictionary<string, string> Names,
    bool DefaultRequired,
    string? DefaultMeaningCode,
    string DefaultVisualMode);

public sealed record RegisterActionsResultDto(int Registered, int Unchanged);

public sealed record SignatureSearchRowDto(SignatureRecordDto Signature, string VerificationStatus);

public sealed record PageDto<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);

public sealed record ExportFilterDto(DateTime FromUtc, DateTime ToUtc, string? Module, string? SignerUserId, string? MeaningCode, Guid? LegalEntityId);

public sealed record ExportByDto(string UserId, string Name);

public sealed record ExportLogEntryDto(
    Guid Id,
    string ExportNumber,
    string Format,
    int RecordCount,
    ExportFilterDto Filter,
    string FilterText,
    ExportByDto By,
    DateTime AtUtc,
    string FileHash);

// ── E3 — samples (ESIGN-BUNDLE §2.10, §4.17–4.19) ───────────────────────────────────────────────────────────────────────

public sealed record SampleOwnerDto(string UserId, string Login, string Name);

public sealed record SampleDecisionInfoDto(string By, string ByName, DateTime AtUtc, string? Reason);

public sealed record SampleDto(
    Guid Id,
    string SampleNumber,
    SampleOwnerDto Owner,
    string Kind,
    string Format,
    string Status,
    string ImageHash,
    DateTime CreatedAtUtc,
    long UsedCount,
    SampleDecisionInfoDto? Decision);

public sealed record SampleDecisionLogDto(DateTime AtUtc, string By, string Owner, string SampleNumber, string Decision, string? Reason);

/// <summary>A sample's PNG for the image endpoint.</summary>
public sealed record SampleImageFile(byte[] Content, string ImageHash);

/// <summary>The rendered export: the bytes, the media type, the file name and the log entry written with it.</summary>
public sealed record ESignatureExportFile(byte[] Content, string MediaType, string FileName, ExportLogEntryDto Entry);

/// <summary>Maps entities to the wire shapes.</summary>
public static class ESignatureMapping
{
    public static SignatureRecordDto ToDto(ESignatureRecord r, ESignatureInvalidation? invalidation) => new(
        r.Id,
        r.SignatureNumber,
        new SubjectRefDto(r.Module, r.SubjectType, r.SubjectId),
        r.SubjectDisplay,
        r.SubjectSummary.Select(s => new SummaryLineDto(s.Label, s.Value)).ToList(),
        r.LegalEntityId,
        r.ActionCode,
        r.Context,
        r.MeaningCode,
        r.MeaningTextAtSign,
        r.MeaningLanguage,
        new SignerDto(r.SignerUserId, r.SignerLogin, r.SignerNameAtSign),
        r.SignedAtUtc,
        r.Note,
        new ReauthDto(ESignatureWire.Name(r.ReauthKind), r.PasswordVerifiedAtUtc),
        ESignatureWire.Name(r.SecondFactor),
        new AppearanceDto(ESignatureWire.Name(r.AppearanceType), r.AppearanceSampleId, r.AppearanceImageHash, r.AppearanceVectorJson is not null),
        r.SubjectHashAlgorithm,
        r.SubjectHash,
        r.SignatureDigest,
        r.RequestId,
        new PolicySnapshotDto(ESignatureWire.Name(r.PolicySnapshot.Scope), r.PolicySnapshot.LegalEntityId, r.PolicySnapshot.PolicyVersion,
            ESignatureWire.Name(r.PolicySnapshot.VisualMode), r.PolicySnapshot.ReauthEvery, r.PolicySnapshot.ReauthWindowMinutes),
        invalidation is null ? null : new InvalidationDto(invalidation.Reason, invalidation.InvalidatedBy, invalidation.InvalidatedByName, invalidation.InvalidatedAtUtc));

    public static SignatureRequestDto ToDto(ESignatureRequest r) => new(
        r.Id, r.RequestNumber, new SubjectRefDto(r.Module, r.SubjectType, r.SubjectId), r.SubjectDisplay, r.LegalEntityId, r.ActionCode,
        r.MeaningCode, new RequesterDto(r.RequestedBy, r.RequestedByName), r.RequestedAtUtc, r.SignerUserId, r.DueAtUtc,
        ESignatureWire.Name(r.Status), r.SignatureId, r.RejectReason, r.DecidedAtUtc);

    public static MeaningDto ToDto(ESignatureMeaning m) =>
        new(m.Code, m.Names, m.Description, m.IsActive, m.IsLocked, m.UsageCount);

    public static SignableActionDto ToDto(ESignableAction a) =>
        new(a.ActionCode, a.Module, a.Names, a.DefaultRequired, a.DefaultMeaningCode, ESignatureWire.Name(a.DefaultVisualMode));
}
