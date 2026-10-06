(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('workspace-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[ContentStudio] Localization payload could not be parsed.', error); }
    window.SetWorkspaceL10n = Object.freeze(values);
})(window, document);
