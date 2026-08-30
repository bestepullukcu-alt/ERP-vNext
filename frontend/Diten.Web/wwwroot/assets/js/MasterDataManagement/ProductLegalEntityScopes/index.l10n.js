(() => {
    'use strict';
    const payload = document.getElementById('product-legal-entity-scopes-l10n');
    if (!payload) return;
    try { window.L10n = Object.assign(window.L10n || {}, JSON.parse(payload.textContent || '{}')); }
    catch { window.L10n = window.L10n || {}; }
})();
