namespace Diten.Platform.API.Authorization.Explain;

/// <summary>
/// AG-STEP-011 / MOD-0018-FU14 Group B — bounded, redacted self-explain result. It describes ONLY the authenticated
/// caller's own effective-access observation for a single (permission key, module[, feature]) pair. It carries no raw
/// JWT/claims/claim values, no raw alias values, no permission inventory, no role ids/names, no raw scope ids
/// (ScopeId/ScopeCode/Value), no org-chain ids, no secrets, and no cross-user subject selector.
/// </summary>
/// <param name="Mode">Always "self" — this endpoint only ever explains the caller's own access.</param>
/// <param name="Allowed">Observed combined outcome: permission satisfied AND non-empty scope AND no diagnostic failure.</param>
/// <param name="RequiredPermission">The canonical permission key explained (echoed from the request).</param>
/// <param name="PermissionMatch">canonical | legacy-alias | missing | bypass-platform-admin | bypass-partner-admin.</param>
/// <param name="MatchedViaLegacyAlias">True only for a genuine legacy alias (never a case-variant of the canonical key).</param>
/// <param name="ActorType">The caller's actor type, from the authenticated context.</param>
/// <param name="TenantId">The caller's tenant id, from the authenticated context (never from the request).</param>
/// <param name="ScopeKinds">Distinct data-scope KINDS the resolver returned — never raw scope ids.</param>
/// <param name="ScopeCounts">Count of scopes per kind — counts only, never raw ids.</param>
/// <param name="TokenExpiresAtUtc">JWT expiry if the exp claim is present and parseable; otherwise null.</param>
/// <param name="FreshnessNotes">Bounded static freshness notes (no token-version / revocation / cache state claimed).</param>
/// <param name="DiagnosticFailure">True when data-scope resolution failed; access is never opened, no raw error returned.</param>
public sealed record SelfAccessExplainResponse(
    string Mode,
    bool Allowed,
    string RequiredPermission,
    string PermissionMatch,
    bool MatchedViaLegacyAlias,
    string? ActorType,
    Guid TenantId,
    IReadOnlyList<string> ScopeKinds,
    IReadOnlyDictionary<string, int> ScopeCounts,
    DateTimeOffset? TokenExpiresAtUtc,
    IReadOnlyList<string> FreshnessNotes,
    bool DiagnosticFailure);
