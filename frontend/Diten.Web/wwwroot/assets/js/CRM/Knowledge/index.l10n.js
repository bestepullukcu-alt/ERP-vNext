(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('knowledge-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[Knowledge] Localization payload could not be parsed.', error); }
    window.L10n = Object.assign({}, window.L10n || {}, values);
    window.KnowledgeL10n = Object.freeze(values);
})(window, document);
