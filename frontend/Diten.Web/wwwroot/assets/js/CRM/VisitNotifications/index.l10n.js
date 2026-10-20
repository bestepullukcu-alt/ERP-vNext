'use strict';

/* WP-VW-W5 · the sent visit e-mails (W5-WEB) — the L10n bridge of the standard: _IndexL10n.cshtml writes a JSON
   payload (the shared toolbar / filter words + every page word), this merges it into window.L10n (PascalCase keys) and warns
   about any key the page needs that did not arrive. */
(function () {
    const payload = document.getElementById('visitnotifications-l10n');
    const requiredKeys = [
        'Actions', 'Cancel', 'Export', 'Filter', 'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Status', 'ErrorOccurred',
        'PeriodLast30', 'PeriodLast7', 'PeriodCustom', 'EventAll', 'StatusAll', 'RepAll', 'EmptyTitle', 'EmptyText', 'QuickView',
        'MailTo', 'MailCc', 'MailDate', 'FooterKvkk', 'ErrRangeOrder', 'ErrRangeMissing', 'Err_conflict', 'Err_generic'
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
        console.error('[VisitNotifications] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
