'use strict';
// Shared PpmCrud owns: new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
const endpoint = '/PPM/Portfolios/api';
const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
document.addEventListener('DOMContentLoaded', () => window.PpmCrud.mount({
    resource: 'portfolios', endpoint, headers: getAuthHeaders(), defaultLifecycle: 'Draft',
    transitions: { Draft: ['Active', 'Archived'], Active: ['Archived'], Archived: [] }
}));
