'use strict';
document.addEventListener('DOMContentLoaded', () => window.PpmCrud.mount({
    resource: 'investment-cases', endpoint: '/ppm/investment-cases/api', titleProperty: 'title',
    defaultLifecycle: 'Draft', hasPortfolio: true, hasPlanningDates: true,
    hideRawParentId: true, immutableParent: true,
    transitions: {
        Draft: ['UnderAnalysis', 'Withdrawn'],
        UnderAnalysis: ['Closed', 'Withdrawn'],
        Closed: [], Withdrawn: []
    }
}));
