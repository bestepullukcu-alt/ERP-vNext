'use strict';

(function () {
    const element = document.getElementById('verified-participants-l10n');
    if (!element) return;

    try {
        const content = JSON.parse(element.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, content);
    } catch (error) {
        console.error('[VerifiedParticipants] Localization content could not be parsed.', error);
    }
})();
