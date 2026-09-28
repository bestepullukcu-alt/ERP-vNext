using Diten.Platform.Domain.Entities.EvidenceLinking;

namespace Diten.Platform.Application.Features.EvidenceLinking;

/// <summary>MOD-0031 slice 1 permission keys. Registered by the Platform permission auto-registration worker from the
/// controller's <c>[HasPermission]</c> attributes; no grant is made here (Faz 4).</summary>
public static class EvidenceLinkPermissions
{
    public const string Read = "platform.evidence.links.read";
    public const string Manage = "platform.evidence.links.manage";
}

/// <summary>Stable reason codes (<c>Response.ReasonCode</c>) of the evidence-linking surface.</summary>
public static class EvidenceLinkReasonCodes
{
    public const string ValidationFailed = "evidence_validation_failed";
    public const string QuoteRequired = "quote_required";
    public const string DocumentNotFound = "document_not_found";
    public const string DocumentNotReadable = "document_not_readable";
    public const string VersionRequired = "version_required";
    public const string VersionNotAllowed = "version_not_allowed";
    public const string VersionNotFound = "version_not_found";
    public const string VersionDocumentMismatch = "version_document_mismatch";
    public const string VersionDeleted = "version_deleted";
    public const string ReferenceSetMissing = "reference_set_missing";
    public const string InvalidEvidenceType = "invalid_evidence_type";
    public const string DuplicateLink = "duplicate_link";
    public const string LinkNotFound = "link_not_found";
    public const string AlreadyRemoved = "already_removed";
    public const string RemovalReasonRequired = "removal_reason_required";
    public const string TransactionUnavailable = "transaction_unavailable";
}

/// <summary>Field limits of slice 1 (MOD-0031 WP-CL-BE-2).</summary>
public static class EvidenceLinkLimits
{
    public const int Module = 32;
    public const int ObjectType = 64;
    public const int ObjectId = 64;
    public const int ObjectVersion = 32;
    public const int Section = 120;
    public const int Page = 20;
    public const int Table = 60;
    public const int Quote = 1000;
    public const int SpanText = 500;
    public const int SpanLanguage = 16;
    public const int MaxSpans = 10;
    public const int RemovalReason = 500;
    public const int DefaultOptionTake = 20;
    public const int MaxOptionTake = 50;
}

/// <summary>The Global BRD set of evidence types (values come from MOD-0048, never compiled in).</summary>
public static class EvidenceReferenceSets
{
    public const string EvidenceType = "evidence-type";
}

public sealed record EvidenceObjectRefInput(string Module, string ObjectType, string ObjectId, string? ObjectVersion = null);

public sealed record EvidenceLocatorInput(string? Quote, string? Section = null, string? Page = null, string? Table = null);

public sealed record EvidenceSupportedSpanInput(string LanguageCode, string Text, int? Start = null, int? End = null);

public sealed record EvidenceObjectRefDto(string Module, string ObjectType, string ObjectId, string? ObjectVersion);

public sealed record EvidenceLocatorDto(string? Section, string? Page, string? Table, string Quote);

public sealed record EvidenceSupportedSpanDto(string LanguageCode, string Text, int? Start, int? End);

public sealed record EvidenceLinkDto(
    Guid LinkId,
    EvidenceObjectRefDto ObjectRef,
    string DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? DocumentVersionLabel,
    string DocumentTitle,
    string EvidenceTypeCode,
    EvidenceLocatorDto Locator,
    IReadOnlyList<EvidenceSupportedSpanDto> SupportedSpans,
    string Status,
    string? LinkedBy,
    DateTimeOffset LinkedAt,
    string? RemovedBy,
    DateTimeOffset? RemovedAt,
    string? RemovalReason);

/// <summary>A picker row. Controlled rows carry the current version; external rows the source provenance.</summary>
public sealed record EvidenceDocumentOptionDto(
    string Kind,
    Guid DocumentId,
    string Title,
    string? Code,
    string? DocumentType,
    Guid? CurrentVersionId,
    string? CurrentVersionLabel,
    string Status,
    DateTimeOffset? EffectiveDate,
    string? CountryCode,
    string? SourceVersion,
    string? SourceStatus);

public static class EvidenceLinkMapper
{
    public static EvidenceLinkDto ToDto(EvidenceLink x) => new(
        x.Id,
        new EvidenceObjectRefDto(x.ObjectRef.Module, x.ObjectRef.ObjectType, x.ObjectRef.ObjectId, x.ObjectRef.ObjectVersion),
        x.DocumentKind,
        x.DocumentId,
        x.DocumentVersionId,
        x.DocumentVersionLabel,
        x.DocumentTitle,
        x.EvidenceTypeCode,
        new EvidenceLocatorDto(x.Locator.Section, x.Locator.Page, x.Locator.Table, x.Locator.Quote),
        x.SupportedSpans.Select(s => new EvidenceSupportedSpanDto(s.LanguageCode, s.Text, s.Start, s.End)).ToList(),
        x.Status,
        x.LinkedBy,
        x.LinkedAt,
        x.RemovedBy,
        x.RemovedAt,
        x.RemovalReason);
}
