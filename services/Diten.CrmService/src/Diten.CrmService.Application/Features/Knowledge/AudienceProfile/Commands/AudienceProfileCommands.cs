using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Commands;

/// <summary>MOD-0162 FU02 audience-profile write surface. <c>TenantId</c> is server-resolved. No delete command —
/// closing a profile is <see cref="ArchiveAudienceProfileCommand"/>.</summary>
public sealed record CreateAudienceProfileCommand(
    string ProfileCode,
    string ProfileName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? ProfileType = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ProfileCode</c> is immutable. An archived profile cannot be updated.</summary>
public sealed record UpdateAudienceProfileCommand(
    Guid AudienceProfileId,
    string ProfileName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? ProfileType = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

public sealed record ArchiveAudienceProfileCommand(Guid AudienceProfileId) : IRequest<Response<bool>>;

/// <summary>Reverses <see cref="ArchiveAudienceProfileCommand"/>. The profile comes back as <c>inactive</c>, never
/// straight to <c>active</c>. An archived <c>ProfileCode</c> is reusable, so this fails with 409 when another
/// non-archived profile has taken the code in the meantime.</summary>
public sealed record UnarchiveAudienceProfileCommand(Guid AudienceProfileId) : IRequest<Response<bool>>;
