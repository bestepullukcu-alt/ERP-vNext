'use strict';
// Shared PpmCrud owns: new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
const endpoint = '/PPM/Projects/api';
const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
document.addEventListener('DOMContentLoaded', () => window.PpmCrud.mount({
    resource: 'projects', endpoint, headers: getAuthHeaders(), defaultLifecycle: 'Draft', hasProjectParent: true,
    workspaceUrl: (id) => `/ppm/projects/${encodeURIComponent(id)}`,
    transitions: {
        Draft: ['Planned', 'Cancelled'],
        Planned: ['Active', 'OnHold', 'Cancelled'],
        Active: ['OnHold', 'Completed', 'Cancelled'],
        OnHold: ['Active', 'Completed', 'Cancelled'],
        Completed: [], Cancelled: []
    }
}));
