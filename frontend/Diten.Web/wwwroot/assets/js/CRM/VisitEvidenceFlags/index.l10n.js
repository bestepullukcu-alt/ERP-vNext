'use strict';

/* WP-VW-W3E · the visit evidence flags (W3E-FLAGS-STD) — the L10n bridge of the standard: _IndexL10n.cshtml writes a JSON
   payload (the shared toolbar / filter words + every page word), this merges it into window.L10n (PascalCase keys) and warns
   about any key the page needs that did not arrive. */
(function () {
    const payload = document.getElementById('visitevidenceflags-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Unknown', 'Actions', 'Edit', 'ViewDetails', 'QuickView', 'AreYouSure', 'Cancel', 'Search', 'Export',
        'Filter', 'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Status', 'RecordSaved', 'ErrorOccurred',
        'PeriodLast7', 'PeriodLast30', 'PeriodCustom', 'RepAllTeam', 'RepAllTenant', 'AccountAll', 'EmptyTitle', 'EmptyAllTitle',
        'EmptyText', 'OpenVisit', 'ErrRangeLong', 'ErrRangeOrder', 'ErrRangeMissing'
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
        console.error('[VisitEvidenceFlags] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
