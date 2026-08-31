'use strict';
// Shared PpmCrud owns: new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
const endpoint = '/PPM/Initiatives/api';
const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
document.addEventListener('DOMContentLoaded', () => window.PpmCrud.mount({
    resource: 'initiatives', endpoint, headers: getAuthHeaders(), defaultLifecycle: 'Proposed', hasPortfolio: true,
    transitions: {
        Proposed: ['Active', 'Cancelled'],
        Active: ['OnHold', 'Completed', 'Cancelled'],
        OnHold: ['Active', 'Completed', 'Cancelled'],
        Completed: [], Cancelled: []
    }
}));
