namespace Diten.Platform.API.Models.EvidenceLinking;

// MOD-0031 slice 1 request bodies. TenantId is never part of a body — it is resolved server-side.

public sealed record EvidenceObjectRefRequest(string Module, string ObjectType, string ObjectId, string? ObjectVersion = null);

public sealed record EvidenceLocatorRequest(string? Quote, string? Section = null, string? Page = null, string? Table = null);

public sealed record EvidenceSupportedSpanRequest(string LanguageCode, string Text, int? Start = null, int? End = null);

public sealed record CreateEvidenceLinkRequest(
    EvidenceObjectRefRequest? ObjectRef,
    string? DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? EvidenceTypeCode,
    EvidenceLocatorRequest? Locator,
    IReadOnlyList<EvidenceSupportedSpanRequest>? SupportedSpans = null);

public sealed record RemoveEvidenceLinkRequest(string? Reason);
