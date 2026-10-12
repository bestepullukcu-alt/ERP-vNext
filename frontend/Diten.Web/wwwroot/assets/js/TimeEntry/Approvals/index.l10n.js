'use strict';

/*
 * MOD-0280-FU01 T2b — the approvals pages' words, read from #timeapprovals-l10n into window.L10n (Golden Reference l10n bridge: the
 * serializer camelCases the keys, they are PascalCased back here). The Time Entry error sentences ride in
 * #time-entry-errors-l10n — one bridge for every Time Entry page.
 */
(function () {
    const payload = document.getElementById('timeapprovals-l10n');
    const errorsNode = document.getElementById('time-entry-errors-l10n');
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    let errors = {};
    try { errors = errorsNode ? JSON.parse(errorsNode.textContent || '{}') : {}; } catch (error) { errors = {}; }

    if (!payload) {
        window.L10n = Object.assign({}, window.L10n || {}, errors);
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, errors, normalized);
    } catch (error) {
        console.error('[TimeEntry] Localization payload could not be parsed.', error);
        window.L10n = Object.assign({}, window.L10n || {}, errors);
    }
})();
