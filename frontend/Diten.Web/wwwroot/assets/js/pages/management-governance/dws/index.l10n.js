(function () {
    'use strict';
    const source = document.getElementById('dws-l10n');
    window.DwsL10n = source ? JSON.parse(source.textContent || '{}') : {};
})();
