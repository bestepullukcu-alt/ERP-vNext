'use strict';
document.addEventListener('DOMContentLoaded', () => window.PpmCrud.mount({
    resource: 'benefit-commitments', endpoint: '/ppm/benefit-commitments/api', titleProperty: 'title',
    defaultLifecycle: 'Draft', hasInvestmentCaseParent: true, hasBenefitTarget: true,
    showReferenceability: false, immutableParent: true,
    transitions: {
        Draft: ['Planned', 'Cancelled'],
        Planned: ['Active', 'Cancelled'],
        Active: ['Closed', 'Cancelled'],
        Closed: [], Cancelled: []
    }
}));
