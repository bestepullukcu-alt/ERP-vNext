'use strict';
// Shared PpmCrud owns: new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
const endpoint = '/PPM/InvestmentCases/api';
const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
document.addEventListener('DOMContentLoaded', () => {
document.getElementById('inlineFilterHost')?.classList.add('px-3');
return window.PpmCrud.mount({
    resource: 'investment-cases', endpoint, headers: getAuthHeaders(), titleProperty: 'title',
    defaultLifecycle: 'Draft', hasPortfolio: true, hasPlanningDates: true,
    hideRawParentId: true, immutableParent: true,
    transitions: {
        Draft: ['UnderAnalysis', 'Withdrawn'],
        UnderAnalysis: ['Closed', 'Withdrawn'],
        Closed: [], Withdrawn: []
    }
});
});
