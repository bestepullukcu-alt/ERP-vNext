'use strict';

(function () {
    const payload = document.getElementById('administrators-l10n');
    const requiredKeys = [
        'Accepted', 'Actions', 'Active', 'AddNew', 'Apply', 'AreYouSure',
        'BulkDelete', 'BulkDeleteConfirm', 'BulkDeleteSuccess', 'Cancel',
        'ColumnVisibility', 'ComingSoon', 'Delete', 'Details', 'Disabled',
        'DisplayName', 'Edit', 'Email', 'UserName', 'ErrorOccurred', 'Export', 'Expired',
        'Filter', 'FormTitleCreate', 'FormTitleEdit', 'FormValidationError',
        'Import', 'Invited', 'NotAvailable', 'PartnerAdmin', 'Passive',
        'PartnerScopeRequired', 'PendingInvitation', 'PlatformAdmin',
        'QuickView', 'Reactivate', 'ReasonPrompt', 'RecordCreated',
        'RecordDeleted', 'RecordSaved', 'RecordUpdated', 'ResendInvite',
        'Reset', 'RoleBillingAdmin', 'RoleReadOnly', 'RoleSuperAdmin',
        'RoleSupportAdmin', 'Save', 'SaveView', 'Search', 'ShowAll',
        'Status', 'Suspend', 'Suspended', 'Unknown', 'Update', 'ViewDetails',
        'SuspendModalTitle', 'SuspendModalWarning', 'SuspendModalReasonLabel',
        'SuspendModalReasonPlaceholder', 'SuspendModalReasonRequired', 'SuspendModalConfirmButton',
        'EmailUnlockTooltip', 'ResendInviteSuccess',
        'ProtectedAccount', 'AdminSelfActionDenied', 'AdminSelfRoleDowngradeDenied',
        'AdminLastSuperAdminDenied', 'AdminSeedDeleteDenied', 'AdminSeedSuspendDenied'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) {
                console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            }
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
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[Administrators] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
