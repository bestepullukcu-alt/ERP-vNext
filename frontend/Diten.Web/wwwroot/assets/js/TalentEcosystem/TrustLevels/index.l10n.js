'use strict';

(function () {
    const element = document.getElementById('trust-levels-l10n');
    if (!element) return;

    try {
        const content = JSON.parse(element.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, content);
    } catch (error) {
        console.error('[TrustLevels] Localization content could not be parsed.', error);
    }
})();
