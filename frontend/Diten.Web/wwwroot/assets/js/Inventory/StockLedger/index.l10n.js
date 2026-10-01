'use strict';

(function () {
    const payload = document.getElementById('stockledger-l10n');
    const requiredKeys = [
        'Actions', 'History', 'Unknown', 'Filter', 'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility',
        'RecordSaved', 'ErrorOccurred', 'Searching', 'NoResults', 'PickerUnavailable', 'ErrServiceUnavailable',
        'StatusAVAILABLE', 'StatusQUALITY_INSPECTION', 'StatusQUARANTINE', 'StatusBLOCKED', 'StatusIN_TRANSIT',
        'StatusDAMAGED', 'StatusEXPIRED', 'SkuLevelGsku', 'SkuLevelLsku', 'SkuLevelFinishedGood', 'KpiSelectProduct'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) {
                console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            }
        });
    };

    if (!payload) {
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
        return;
    }

    // ASP.NET Json.Serialize writes camelCase; the page reads PascalCase (L().StatusAVAILABLE).
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[StockLedger] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
