// FE-E (MOD-0018-FU9) — pure view-model builder for the FU14 self-explain DTO. Produces TWO SEPARATE
// observations (permission gate + data scope) plus meta. It intentionally does NOT synthesise a
// combined "Allowed" verdict — the DTO has none (data-scope is opt-in; empty scope ≠ universal deny;
// no permission↔module binding catalog), and inventing one would be dishonest. Bounded: only the
// DTO's redacted fields are surfaced (no raw JWT/claims/alias/scope ids).
(function (global) {
    'use strict';

    function asList(v) { return Array.isArray(v) ? v : []; }

    function build(dto) {
        if (!dto || typeof dto !== 'object') return null;
        return {
            meta: {
                mode: dto.mode || 'self',
                actorType: dto.actorType || null,
                tenantId: dto.tenantId || null,
                tokenExpiresAtUtc: dto.tokenExpiresAtUtc || null,
                freshnessNotes: asList(dto.freshnessNotes),
                diagnosticFailure: dto.diagnosticFailure === true
            },
            // Observation 1 — permission gate only (never a combined verdict).
            permission: {
                requiredPermission: dto.requiredPermission || '',
                satisfied: dto.permissionSatisfied === true,
                match: dto.permissionMatch || 'missing',
                matchedViaLegacyAlias: dto.matchedViaLegacyAlias === true
            },
            // Observation 2 — descriptive data scope. When resolution failed (diagnosticFailure) the
            // scope observation is unavailable but the permission observation above is still valid.
            scope: {
                kinds: asList(dto.scopeKinds),
                counts: (dto.scopeCounts && typeof dto.scopeCounts === 'object') ? dto.scopeCounts : {},
                notes: asList(dto.scopeNotes),
                unavailable: dto.diagnosticFailure === true
            }
            // NOTE: deliberately no `allowed` / combined-verdict field anywhere in this view model.
        };
    }

    global.SelfAccessView = { build: build };
})(typeof window !== 'undefined' ? window : this);
