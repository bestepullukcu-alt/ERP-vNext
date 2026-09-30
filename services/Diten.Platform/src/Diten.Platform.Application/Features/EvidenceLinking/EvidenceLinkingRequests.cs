using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.EvidenceLinking;

/// <summary>Creates an immutable evidence link. TenantId is server-resolved, never from the body.</summary>
public sealed record CreateEvidenceLinkCommand(
    EvidenceObjectRefInput? ObjectRef,
    string? DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string? EvidenceTypeCode,
    EvidenceLocatorInput? Locator,
    IReadOnlyList<EvidenceSupportedSpanInput>? SupportedSpans,
    string? CorrelationId = null) : IRequest<Response<EvidenceLinkDto>>;

/// <summary>Removes a link (status stamp; the record stays). A reason is mandatory.</summary>
public sealed record RemoveEvidenceLinkCommand(Guid LinkId, string? Reason, string? CorrelationId = null)
    : IRequest<Response<EvidenceLinkDto>>;

public sealed record GetEvidenceLinksByObjectQuery(
    string? Module,
    string? ObjectType,
    string? ObjectId,
    string? ObjectVersion,
    bool IncludeRemoved,
    string? CorrelationId = null) : IRequest<Response<IReadOnlyList<EvidenceLinkDto>>>;

/// <summary>WP-CL-BE-5 — bulk read: the links of up to <see cref="EvidenceLinkLimits.MaxQueryObjects"/> objects in one
/// call, each with the computed document state. Same permission and tenant scope as the single-object read.</summary>
public sealed record QueryEvidenceLinksByObjectsQuery(
    IReadOnlyList<EvidenceObjectRefInput?>? Objects,
    bool IncludeRemoved,
    string? CorrelationId = null) : IRequest<Response<IReadOnlyList<EvidenceObjectLinksDto>>>;

public sealed record GetEvidenceLinkByIdQuery(Guid LinkId, string? CorrelationId = null)
    : IRequest<Response<EvidenceLinkDto>>;

/// <summary>Reverse lookup — the ACTIVE links that use a document (optionally one version).</summary>
public sealed record GetEvidenceLinksByDocumentQuery(Guid DocumentId, Guid? VersionId, string? CorrelationId = null)
    : IRequest<Response<IReadOnlyList<EvidenceLinkDto>>>;

/// <summary>Picker source — only documents the caller can read.</summary>
public sealed record GetEvidenceDocumentOptionsQuery(string? Search, string? Kind, int? Take, string? CorrelationId = null)
    : IRequest<Response<IReadOnlyList<EvidenceDocumentOptionDto>>>;
