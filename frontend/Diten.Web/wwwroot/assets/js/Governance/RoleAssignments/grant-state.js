// FE-C (MOD-0018-FU9) — pure GrantSource-aware row-state for the role-permission assignment screen.
// UX ONLY: System (baseline) and Module (entitlement) grants are locked here; only Manual grants are
// operator-removable, and new assignments become Manual. Backend [HasPermission] is authoritative.
(function (global) {
    'use strict';

    // permission : catalog item (has .id, .key, ...)
    // grant      : the role's grant for this permission, or null/undefined when not assigned
    //              ({ grantSource: 'System'|'Module'|'Manual', sourceModuleCode })
    // canAssign  : FE-B permission gate (auth.roles.assign-permission)
    function evaluate(permission, grant, canAssign) {
        var assigned = !!grant;
        var source = assigned ? String(grant.grantSource || '').toLowerCase() : null;
        var isSystem = source === 'system';
        var isModule = source === 'module';
        var isManual = source === 'manual';

        var badge = null;
        if (assigned) {
            if (isSystem) badge = { key: 'Baseline' };
            else if (isModule) badge = { key: 'Module', moduleCode: grant.sourceModuleCode || '' };
            else badge = { key: 'Manual' };
        }

        return {
            assigned: assigned,
            source: assigned ? (isSystem ? 'System' : isModule ? 'Module' : 'Manual') : null,
            // System/Module grants cannot be changed from this screen.
            locked: assigned && (isSystem || isModule),
            // Only Manual grants are removable, and only with the assign-permission right.
            removable: assigned && isManual && !!canAssign,
            // Unassigned permissions can be added (becomes a Manual grant) with the right.
            assignable: !assigned && !!canAssign,
            badge: badge
        };
    }

    global.RoleGrantState = { evaluate: evaluate };
})(typeof window !== 'undefined' ? window : this);
