'use strict';
(function () {
    const payload = document.getElementById('shipment-l10n');
    if (!payload) return;
    try { window.L10n = Object.assign({}, window.L10n || {}, JSON.parse(payload.textContent || '{}')); }
    catch (error) { console.error('[Shipments] Localization payload could not be parsed.', error); }
})();
