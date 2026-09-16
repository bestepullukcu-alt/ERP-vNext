'use strict';

(function () {
    const source = document.getElementById('candidate-profiles-l10n');
    if (!source) return;
    try {
        window.L10n = Object.assign({}, window.L10n || {}, JSON.parse(source.textContent || '{}'));
    } catch (error) {
        console.error('[CandidateProfiles] Failed to parse l10n data.', error);
    }
})();
