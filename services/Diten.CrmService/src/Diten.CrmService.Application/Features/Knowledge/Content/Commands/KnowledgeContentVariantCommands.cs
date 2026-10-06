using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Content.Commands;

/// <summary>
/// SCMM-13 (docx §13) — create a TARGET language variant bound to an existing source's logical component
/// (<c>ContentSetId</c>). The new record is a distinct versioned row (its own ContentId / ContentCode / ContentVersion)
/// in a DIFFERENT language, never the source. Classification (subject / topic / audience / concept / brand / product /
/// campaign / segment) and content type are INHERITED from the source — a variant of one logical component cannot drift
/// to a different subject. <c>TenantId</c> is server-resolved and never in the payload. At most one active (non-archived)
/// variant may exist per language in a set, and a set keeps exactly one source.
/// </summary>
public sealed record CreateContentVariantCommand(
    Guid SourceContentId,
    string LanguageCode,
    string ContentCode,
    string ContentTitle,
    string ContentVersion,
    DateTimeOffset EffectiveFrom,
    string? ContentStatus = null,
    string? Summary = null,
    string? ContentBodyRef = null,
    string? ContentAssetRef = null,
    string? FileRef = null,
    string? Url = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    IReadOnlyList<string>? Tags = null) : IRequest<Response<Guid>>;

/// <summary>
/// SCMM-13 — clear the <c>needs_assessment</c> flag on a TARGET variant (needs_assessment → current) once a human has
/// reviewed the translation against the changed source. This is a DISTINCT command from update (the SoD seam: opening
/// assessment is automatic on a source edit; closing it is an explicit reviewer action, gated by its own permission at
/// the HTTP surface). There is no auto-translation and no silent state change.
/// </summary>
public sealed record MarkTranslationAssessedCommand(Guid ContentId) : IRequest<Response<bool>>;
