'use strict';

(function () {
    const payload = document.getElementById('audit-log-l10n');
    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, raw);
    } catch (error) {
        console.error('[AuditLog] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
