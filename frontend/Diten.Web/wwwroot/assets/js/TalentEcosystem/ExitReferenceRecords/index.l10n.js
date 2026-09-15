'use strict';

(function () {
    const source = document.getElementById('exit-reference-records-l10n');
    if (!source) return;
    try {
        window.L10n = Object.assign({}, window.L10n || {}, JSON.parse(source.textContent || '{}'));
    } catch (error) {
        console.error('[ExitReferenceRecords] Failed to parse l10n data.', error);
    }
})();
