'use strict';

(function () {
    const node = document.getElementById('rehire-recommendations-l10n');
    if (!node) return;
    try {
        window.L10n = JSON.parse(node.textContent || '{}');
    } catch (error) {
        console.error('[RehireRecommendations] Localization parse failed.', error);
        window.L10n = {};
    }
})();
