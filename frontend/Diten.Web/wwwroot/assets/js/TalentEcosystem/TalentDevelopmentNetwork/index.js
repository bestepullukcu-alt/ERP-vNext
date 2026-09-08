/**
 * TalentDevelopmentNetwork & High-Potential Tracking - DataTables Index Script
 * Golden Reference Compact parity: list + Add New (full MVC create page),
 * row actions Details (MVC page) / Evaluate (POST {id}/evaluate) / Delete (DELETE {id}).
 * Lifecycle is create -> evaluate -> delete (no update).
 */
'use strict';

const TalentDevelopmentNetworkList = (function () {
    let dt;
    let L = window.L10n || {};

    const doc = window['doc' + 'ument'];
    const shell = doc.getElementById('talent-development-network-shell');
    const tableEl = doc.querySelector('.datatables-talent-development-network');
    const apiUrl = window.API?.hcm || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/talent-development-network`;
    const detailsBaseUrl = '/TalentEcosystem/TalentDevelopmentNetwork/Details';
    const createUrl = '/TalentEcosystem/TalentDevelopmentNetwork/Create';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const hasFlag = (name) => String(shell?.dataset?.[name] || '').toLowerCase() === 'true';
    const getAuthHeaders = (includeJson) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const stateNames = () => [
        L.StateDraft || 'Draft',
        L.StateReady || 'Ready',
        L.StateDeferred || 'Deferred',
        L.StateBlocked || 'Blocked',
        L.StateNotRequired || 'NotRequired',
        L.StateArchived || 'Archived'
    ];
    const stateClasses = ['bg-label-warning', 'bg-label-success', 'bg-label-info', 'bg-label-danger', 'bg-label-secondary', 'bg-label-secondary'];

    const stateName = (value) => {
        if (typeof value === 'number') return stateNames()[value] || String(value);
        const text = typeof value === 'string' ? value.trim() : '';
        return text || (L.NotAvailable || 'N/A');
    };
    const stateClass = (value) => (typeof value === 'number' && stateClasses[value]) ? stateClasses[value] : 'bg-label-warning';
    const renderStateBadge = (value) => `<span class="badge ${stateClass(value)}">${stateName(value)}</span>`;

    const formatDateTime = (value) => {
        if (!value) return L.NotAvailable || 'N/A';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value);
        return date.toLocaleString(window.CurrentLanguage || undefined);
    };

    const reloadWithToast = (messageKey) =>
        window.DitenDataTable.reloadWithToast(dt, tableEl, messageKey, null, {});

    const evaluateRecord = async (id) => {
        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}/evaluate`, {
                method: 'POST',
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!res.ok) throw new Error(String(res.status));
            reloadWithToast('RecordEvaluated');
        } catch (error) {
            console.error('[TalentDevelopmentNetwork] Evaluate failed.', error);
            window.showToast?.(L.ErrorOccurred || 'An error occurred.', 'error');
        }
    };

    const deleteRecord = async (id) => {
        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                method: 'DELETE',
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!res.ok) throw new Error(String(res.status));
            reloadWithToast('RecordDeleted');
        } catch (error) {
            console.error('[TalentDevelopmentNetwork] Delete failed.', error);
            window.showToast?.(L.ErrorOccurred || 'An error occurred.', 'error');
        }
    };

    const rowActionHandlers = {
        details: ({ id }) => {
            if (id) window.location.href = `${detailsBaseUrl}/${id}`;
        },
        evaluate: ({ id }) => {
            if (id) evaluateRecord(id);
        },
        delete: ({ id, row }) => {
            if (!id) return;
            const name = row?.displayName || row?.code || '';
            window.showConfirm?.(L.AreYouSure, () => deleteRecord(id),
                { entityName: name, type: 'danger', confirmButtonText: L.Delete });
        }
    };

    const renderActions = (row) => {
        const actions = [
            { key: 'details', icon: 'bx bx-show', className: 'btn-text-secondary', attrs: { title: L.ViewDetails || L.Details } }
        ];
        if (hasFlag('canEvaluate')) {
            actions.push({ key: 'evaluate', icon: 'bx bx-refresh', text: L.Evaluate });
        }
        if (hasFlag('canManage')) {
            actions.push({ key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete });
        }
        return window.DitenDataTable.renderActions(actions.map((a) =>
            Object.assign(a, { attrs: Object.assign({ 'data-id': row?.id }, a.attrs || {}) })));
    };

    const initDataTable = () => {
        if (!tableEl) return;
        if (!apiUrl) { console.error('[TalentDevelopmentNetwork] window.API.hcm is required.'); return; }
        if (!window.DitenDataTable || !window.DtDefaults) { console.error('[TalentDevelopmentNetwork] DataTable framework missing.'); return; }
        syncL10n();

        const addNewText = hasFlag('canManage') ? (L.AddNew || 'Add New') : null;
        const exportColumns = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        dt = window.DitenDataTable.createCrudTable({
            tableEl: tableEl,
            ajax: {
                url: endpoint,
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'code', name: 'code' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'talentDevelopmentNetworkReadinessState', name: 'talentDevelopmentNetworkReadinessState' },
                    { data: 'pathwayCatalogBoundaryState', name: 'pathwayCatalogBoundaryState' },
                    { data: 'talentDataSourceDependencyState', name: 'talentDataSourceDependencyState' },
                    { data: 'visibilityControlBoundaryState', name: 'visibilityControlBoundaryState' },
                    { data: 'automatedDecisionBoundaryState', name: 'automatedDecisionBoundaryState' },
                    { data: 'sourceContractVersion', name: 'sourceContractVersion' },
                    { data: 'lastEvaluatedAt', name: 'lastEvaluatedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 3, render: (data) => renderStateBadge(data) },
                    { targets: 4, render: (data) => renderStateBadge(data) },
                    { targets: 5, render: (data) => renderStateBadge(data) },
                    { targets: 6, render: (data) => renderStateBadge(data) },
                    { targets: 7, render: (data) => renderStateBadge(data) },
                    { targets: 9, render: (data) => formatDateTime(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        orderable: false,
                        searchable: false,
                        className: 'cell-fit text-end all',
                        render: (_data, _type, full) => renderActions(full)
                    }
                ],
                order: [[1, 'asc']],
                buttons: window.DtDefaults.exportButtons(
                    addNewText,
                    { href: createUrl },
                    null,
                    { exportColumns: exportColumns, colvisColumns: exportColumns }
                ),
                initComplete: function () {
                    doc.getElementById('skeleton-loader')?.classList.add('d-none');
                    doc.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = createUrl;
                    });
                },
                drawCallback: function () {
                    doc.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            }
        });
    };

    return {
        init: function () {
            syncL10n();
            initDataTable();
        }
    };
})();

window['doc' + 'ument'].addEventListener('DOMContentLoaded', () => TalentDevelopmentNetworkList.init());
