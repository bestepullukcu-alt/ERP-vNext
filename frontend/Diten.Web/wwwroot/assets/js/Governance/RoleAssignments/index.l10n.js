'use strict';

(function () {
    const payload = document.getElementById('roleassignments-l10n');
    const requiredKeys = [
        'Assign', 'Remove', 'BadgeBaseline', 'BadgeModule', 'BadgeManual', 'LockedHint',
        'LockedHintSystem', 'LockedHintModule', 'ModuleUngrouped', 'PermissionSummary',
        'SelectedRoleTitle', 'CoverageLabel', 'AssignedRemaining', 'StatTotal', 'StatAssigned',
        'StatBaselineLocked', 'StatUnassigned', 'ActionDistributionTitle',
        'BaselineRequiredNote', 'ShowingCount', 'CountBadge',
        'SelectRolePrompt', 'NoPermissions', 'AreYouSure', 'RecordCreated', 'RecordDeleted',
        'ErrorOccurred', 'Cancel', 'Delete', 'Reset',
        // FEAT-ROLEPERMS-LABEL-DERIVE — nested { action: verb } map for key-derived row labels.
        'ActionVerbs',
        // FIX-ROLEPERMS-ROLENAME-L10N-STICKY — nested { roleCode: localizedName } map for system roles.
        'RoleNames',
        // FIX-ROLEPERMS-MODULE-LABEL — nested { NORMALIZEDMODULECODE: localizedName } map for module headers/filter.
        'ModuleNames'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
    };

    if (!payload) {
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
        return;
    }

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) normalized[toPascalCase(key)] = raw[key];
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[RoleAssignments] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
