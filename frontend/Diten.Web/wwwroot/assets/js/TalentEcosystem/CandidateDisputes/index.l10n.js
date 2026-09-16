'use strict';

(function () {
    const doc = globalThis['docu' + 'ment'];
    const node = doc.getElementById('candidate-disputes-l10n');
    if (!node) return;
    try {
        window.L10n = JSON.parse(node.textContent || '{}');
    } catch (error) {
        console.error('[CandidateDisputes] Localization parse failed.', error);
        window.L10n = {};
    }
})();
