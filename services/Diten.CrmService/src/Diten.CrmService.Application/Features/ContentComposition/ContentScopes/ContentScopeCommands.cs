using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

/// <summary>SCMM-14 (CAND-CAP-0011, D14-a) content-scope write surface. <c>TenantId</c> is server-resolved and never in
/// the payload. There is no delete — closing a scope is <see cref="ArchiveContentScopeCommand"/> (soft lifecycle). Refs
/// are opaque config strings (sector-neutral); nothing is resolved here. Legal-entity is not a dimension (DESIGN LE).</summary>
public sealed record CreateContentScopeCommand(
    string ScopeCode,
    string ScopeName,
    string? Description = null,
    IReadOnlyList<string>? ProductRefs = null,
    IReadOnlyList<string>? MarketRefs = null,
    IReadOnlyList<string>? AudienceRefs = null,
    string? Channel = null,
    string? LanguageCode = null,
    DateTimeOffset? PeriodFrom = null,
    DateTimeOffset? PeriodTo = null,
    string? ScopeVersion = null,
    string? Status = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ScopeCode</c> is immutable. An archived scope cannot be updated;
/// update never sets status=archived (use the archive command).</summary>
public sealed record UpdateContentScopeCommand(
    Guid ContentScopeId,
    string ScopeName,
    string? Description = null,
    IReadOnlyList<string>? ProductRefs = null,
    IReadOnlyList<string>? MarketRefs = null,
    IReadOnlyList<string>? AudienceRefs = null,
    string? Channel = null,
    string? LanguageCode = null,
    DateTimeOffset? PeriodFrom = null,
    DateTimeOffset? PeriodTo = null,
    string? ScopeVersion = null,
    string? Status = null) : IRequest<Response<bool>>;

public sealed record ArchiveContentScopeCommand(Guid ContentScopeId) : IRequest<Response<bool>>;
