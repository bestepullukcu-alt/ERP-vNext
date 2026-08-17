'use strict';

(function () {
    const payload = document.getElementById('interface-registry-l10n');
    if (!payload) {
        window.L10n = window.L10n || {};
        return;
    }

    try {
        window.L10n = Object.assign({}, window.L10n || {}, JSON.parse(payload.textContent || '{}'));
    } catch (error) {
        console.error('InterfaceRegistry localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
})();
