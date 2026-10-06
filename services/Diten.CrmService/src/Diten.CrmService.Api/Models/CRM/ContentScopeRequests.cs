namespace Diten.CrmService.Api.Models.CRM;

// SCMM-14 (CAND-CAP-0011) content-scope request models. TenantId is NEVER part of any request body (server-resolved from
// the JWT claim). Route ids come from the path. These map 1:1 onto the ContentScope create / update commands (archive
// carries only the route id).

public sealed record CreateContentScopeRequest(
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
    string? Status = null);

public sealed record UpdateContentScopeRequest(
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
    string? Status = null);
