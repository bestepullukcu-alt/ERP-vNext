(function (window, document) {
    'use strict';
    let values = {};
    const payload = document.getElementById('vfp-l10n');
    try { values = JSON.parse(payload.textContent || '{}'); }
    catch (error) { console.error('[VisitFrequencyPolicies] Localization payload could not be parsed.', error); }
    // Razor payload'ı anahtarları camelCase serialize eder (MVC JSON policy); konsol scriptleri ise
    // üst-düzey skaler anahtarları PascalCase (L.NewPolicy…), nested map'leri camelCase (L.statusLabels) okur.
    // Her üst-düzey anahtarın iki yazımını da sun → her iki okuma da çözülür (bkz memory: l10n-bridge-pascalcase-loader).
    const bridged = {};
    Object.keys(values).forEach(function (k) {
        bridged[k] = values[k];
        const pascal = k.charAt(0).toUpperCase() + k.slice(1);
        if (!(pascal in bridged)) bridged[pascal] = values[k];
    });
    window.L10n = Object.assign({}, window.L10n || {}, bridged);
    window.VfpL10n = Object.freeze(bridged);
})(window, document);
