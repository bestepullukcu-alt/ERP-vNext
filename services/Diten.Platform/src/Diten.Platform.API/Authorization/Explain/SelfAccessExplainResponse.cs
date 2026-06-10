namespace Diten.Platform.API.Authorization.Explain;

/// <summary>
/// AG-STEP-011 / MOD-0018-FU14 Group B/C — bounded, redacted self-explain result for the authenticated caller's own
/// access. It returns TWO SEPARATE, honest observations and NO combined verdict:
/// (1) a permission-gate observation (<see cref="PermissionSatisfied"/> + <see cref="PermissionMatch"/>), and
/// (2) a descriptive data-scope observation (<see cref="ScopeKinds"/> + <see cref="ScopeCounts"/> + <see cref="ScopeNotes"/>).
///
/// It intentionally does NOT produce an <c>Allowed</c> verdict: data-scope is opt-in per resource and platform/partner
/// admins are not row-scoped (BME-001), so an empty scope does not imply a universal deny; and there is no
/// permission-key↔module binding catalog, so the two inputs cannot be soundly combined. It carries no raw JWT/claims,
/// raw alias values, permission inventory, role ids/names, raw scope ids (ScopeId/ScopeCode/Value), org-chain ids,
/// secrets, or cross-user subject selector.
/// </summary>
/// <param name="Mode">Always "self" — this endpoint only ever explains the caller's own access.</param>
/// <param name="PermissionSatisfied">The permission-gate observation only (bypass or claim match). Not a combined verdict; data-scope and resolver failures never override it.</param>
/// <param name="RequiredPermission">The canonical permission key explained (echoed from the request).</param>
/// <param name="PermissionMatch">canonical | legacy-alias | missing | bypass-platform-admin | bypass-partner-admin.</param>
/// <param name="MatchedViaLegacyAlias">True only for a genuine legacy alias (never a case-variant of the canonical key).</param>
/// <param name="ActorType">The caller's actor type, from the authenticated context.</param>
/// <param name="TenantId">The caller's tenant id, from the authenticated context (never from the request).</param>
/// <param name="ScopeKinds">Distinct data-scope KINDS the resolver returned for the given module — never raw scope ids.</param>
/// <param name="ScopeCounts">Count of scopes per kind — counts only, never raw ids.</param>
/// <param name="ScopeNotes">Bounded notes about the scope observation (e.g. data-scope is opt-in per resource; empty scope ≠ universal deny).</param>
/// <param name="TokenExpiresAtUtc">JWT expiry if the exp claim is present and parseable; otherwise null.</param>
/// <param name="FreshnessNotes">Bounded static freshness notes (no token-version / revocation / cache state claimed).</param>
/// <param name="DiagnosticFailure">True when data-scope resolution failed; the permission observation is preserved, no raw error returned.</param>
public sealed record SelfAccessExplainResponse(
    string Mode,
    bool PermissionSatisfied,
    string RequiredPermission,
    string PermissionMatch,
    bool MatchedViaLegacyAlias,
    string? ActorType,
    Guid TenantId,
    IReadOnlyList<string> ScopeKinds,
    IReadOnlyDictionary<string, int> ScopeCounts,
    IReadOnlyList<string> ScopeNotes,
    DateTimeOffset? TokenExpiresAtUtc,
    IReadOnlyList<string> FreshnessNotes,
    bool DiagnosticFailure);
