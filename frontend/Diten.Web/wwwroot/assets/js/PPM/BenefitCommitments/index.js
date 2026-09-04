'use strict';
// Shared PpmCrud owns: new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
const endpoint = '/PPM/BenefitCommitments/api';
const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
document.addEventListener('DOMContentLoaded', () => {
document.getElementById('inlineFilterHost')?.classList.add('px-3');
return window.PpmCrud.mount({
    resource: 'benefit-commitments', endpoint, headers: getAuthHeaders(), titleProperty: 'title',
    defaultLifecycle: 'Draft', hasInvestmentCaseParent: true, hasBenefitTarget: true,
    showReferenceability: false, immutableParent: true,
    transitions: {
        Draft: ['Planned', 'Cancelled'],
        Planned: ['Active', 'Cancelled'],
        Active: ['Closed', 'Cancelled'],
        Closed: [], Cancelled: []
    }
});
});
