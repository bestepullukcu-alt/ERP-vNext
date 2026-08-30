'use strict';
// Shared PpmCrud owns: new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
const endpoint = '/PPM/Programs/api';
const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
document.addEventListener('DOMContentLoaded', () => window.PpmCrud.mount({
    resource: 'programs', endpoint, headers: getAuthHeaders(), defaultLifecycle: 'Draft', hasPortfolio: true,
    transitions: {
        Draft: ['Active', 'Cancelled'],
        Active: ['OnHold', 'Completed', 'Cancelled'],
        OnHold: ['Active', 'Completed', 'Cancelled'],
        Completed: [], Cancelled: []
    }
}));
