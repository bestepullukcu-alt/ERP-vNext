'use strict';

(function () {
    const source = document.getElementById('reference-exchange-l10n');
    if (!source) return;
    try {
        window.L10n = Object.assign({}, window.L10n || {}, JSON.parse(source.textContent || '{}'));
    } catch (error) {
        console.error('[ReferenceExchange] Failed to parse l10n data.', error);
    }
})();
