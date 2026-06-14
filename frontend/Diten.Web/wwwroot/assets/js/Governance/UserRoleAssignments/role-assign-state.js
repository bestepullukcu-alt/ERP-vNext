// FE-C 3/3 (MOD-0018-FU9) — pure row-state for the user-role assignment screen. UX ONLY: assigned
// roles are removable (with permission), unassigned are assignable. Backend [HasPermission] is
// authoritative. (User-role grants have no source distinction — unlike role-permission grants.)
(function (global) {
    'use strict';

    function evaluate(role, isAssigned, canAssign) {
        var assigned = !!isAssigned;
        return {
            assigned: assigned,
            removable: assigned && !!canAssign,
            assignable: !assigned && !!canAssign
        };
    }

    global.UserRoleAssignState = { evaluate: evaluate };
})(typeof window !== 'undefined' ? window : this);
