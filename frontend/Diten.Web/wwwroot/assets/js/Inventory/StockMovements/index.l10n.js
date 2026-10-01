'use strict';

(function () {
    const payload = document.getElementById('stockmovements-l10n');
    const movementTypes = [
        'OPENING_BALANCE', 'GOODS_RECEIPT_PO', 'GOODS_RECEIPT_PROD', 'GOODS_ISSUE_SALES', 'GOODS_ISSUE_PROD',
        'WAREHOUSE_TRANSFER', 'LOCATION_TRANSFER', 'STATUS_TRANSFER', 'CUSTOMER_RETURN', 'SUPPLIER_RETURN',
        'COUNT_GAIN', 'COUNT_LOSS', 'SCRAP', 'WRITE_OFF', 'REVERSAL'
    ];
    const statuses = ['AVAILABLE', 'QUALITY_INSPECTION', 'QUARANTINE', 'BLOCKED', 'IN_TRANSIT', 'DAMAGED', 'EXPIRED'];
    const requiredKeys = [
        'Actions', 'Reversal', 'ReversalConfirmTitle', 'ReversalConfirmText', 'ReversalConfirmButton', 'ReversedBadge',
        'ReversedInSource', 'Automatic', 'Unknown', 'Cancel', 'Filter', 'Apply', 'Reset', 'ShowAll', 'SaveView',
        'ColumnVisibility', 'RecordSaved', 'ErrorOccurred', 'Searching', 'NoResults', 'PickerUnavailable',
        'ErrServiceUnavailable', 'SkuLevelGsku', 'SkuLevelLsku', 'SkuLevelFinishedGood'
    ].concat(movementTypes.map((t) => 'Movement' + t), statuses.map((s) => 'Status' + s));

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

    // ASP.NET Json.Serialize writes camelCase; the page reads PascalCase (L().MovementREVERSAL).
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
        console.error('[StockMovements] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
