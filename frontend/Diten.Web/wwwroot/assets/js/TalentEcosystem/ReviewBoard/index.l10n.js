'use strict';

(function () {
    const element = document.getElementById('review-board-l10n');
    if (!element) return;

    try {
        const content = JSON.parse(element.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, content);
    } catch (error) {
        console.error('[ReviewBoard] Localization content could not be parsed.', error);
    }
})();
