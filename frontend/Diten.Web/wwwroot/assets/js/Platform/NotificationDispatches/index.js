/**
 * Notification Dispatches - Platform Admin DataTables Index Script (MOD-0027-FU02).
 * READ-ONLY monitoring subset: list + detail + conditional cancel. No create/edit/bulk.
 * Proxy-profile: browser JS only calls same-origin /Platform/NotificationDispatches/api endpoints.
 * Full email bodies, recipient addresses and Bcc are NEVER requested or shown here.
 */
'use strict';

const NotificationDispatchesList = (function () {
    let dt;
    let tenantNameMap = {};

    const dtTableEl = document.querySelector('.datatables-notificationdispatches');
    const endpoint = '/Platform/NotificationDispatches/api';
    const apiBase = endpoint;
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    // Only 'Queued' dispatches are cancellable; the backend fail-closes any invalid transition (409).
    const CANCELLABLE = ['Queued'];
    // The dispatch list is tenant-scoped: targetTenantId is a required non-nullable Guid server-side.
    // Before a tenant is picked we send the empty Guid (not an empty string, which fails Guid binding → 400)
    // so the backend returns a controlled empty list instead of a validation error.
    const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
    let appliedFilters = { tenant: '', status: '', templateKey: '', dateFrom: '', dateTo: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const getAppliedFilterCount = () =>
        [appliedFilters.tenant, appliedFilters.status, appliedFilters.templateKey, appliedFilters.dateFrom, appliedFilters.dateTo]
            .filter((v) => normalizeString(v).length > 0).length;

    const unwrapLookup = (payload) => payload?.data || payload?.Data || [];
    const tenantLabel = (tenantId) => tenantNameMap[tenantId] || tenantId || '-';

    const buildAjaxUrl = () => {
        const params = new URLSearchParams();
        params.set('targetTenantId', appliedFilters.tenant || EMPTY_GUID);
        params.set('page', '1');
        params.set('pageSize', '100');
        if (appliedFilters.status) params.set('status', appliedFilters.status);
        if (appliedFilters.templateKey) params.set('templateKey', appliedFilters.templateKey);
        if (appliedFilters.dateFrom) params.set('queuedFrom', `${appliedFilters.dateFrom}T00:00:00`);
        if (appliedFilters.dateTo) params.set('queuedTo', `${appliedFilters.dateTo}T23:59:59`);
        return `${apiBase}?${params.toString()}`;
    };

    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };
    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        ['#filterTenant', '#filterStatus'].forEach((sel) => {
            $(sel).each(function () {
                const $s = $(this);
                if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
                $s.select2({
                    dropdownParent: $body,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    selectionCssClass: 'form-select form-select-sm',
                    placeholder: $s.data('placeholder') || '',
                    width: 'element',
                    allowClear: sel === '#filterStatus'
                });
            });
        });
    };

    const loadTenantOptions = async () => {
        const tenantSelect = document.getElementById('filterTenant');
        if (!tenantSelect) return;
        try {
            const res = await fetch(`${apiBase}/tenants?page=1&pageSize=100`, { credentials: 'same-origin' });
            if (!res.ok) return;
            const payload = await res.json();
            const raw = payload?.data?.items || payload?.data || payload?.Data || [];
            tenantNameMap = {};
            (Array.isArray(raw) ? raw : []).forEach((t) => {
                const id = t.id || t.Id;
                if (!id) return;
                const name = t.displayName || t.DisplayName || t.name || t.Name || id;
                tenantNameMap[id] = name;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = name;
                tenantSelect.appendChild(opt);
            });
        } catch (error) {
            console.error('[NotificationDispatches Tenants] Failed.', error);
        }
    };

    const readFilterControls = () => ({
        tenant: normalizeString($('#filterTenant').val()),
        status: normalizeString($('#filterStatus').val()),
        templateKey: normalizeString(document.getElementById('filterTemplateKey')?.value),
        dateFrom: normalizeString(document.getElementById('filterDateFrom')?.value),
        dateTo: normalizeString(document.getElementById('filterDateTo')?.value)
    });
    const clearFilterControls = () => {
        $('#filterTenant').val('').trigger('change');
        $('#filterStatus').val('').trigger('change');
        const k = document.getElementById('filterTemplateKey'); if (k) k.value = '';
        const f = document.getElementById('filterDateFrom'); if (f) f.value = '';
        const t = document.getElementById('filterDateTo'); if (t) t.value = '';
    };

    const reload = () => {
        if (!dt) return;
        dt.ajax.url(buildAjaxUrl()).load(() => {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        }, false);
    };

    const setupFilters = () => {
        initSelect2Filters();
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readFilterControls();
            reload();
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            clearFilterControls();
            appliedFilters = { tenant: '', status: '', templateKey: '', dateFrom: '', dateTo: '' };
            reload();
        });
    };

    const getStatusMap = () => ({
        Queued: { title: L.StatusQueued || 'Queued', class: 'bg-label-warning' },
        Sent: { title: L.StatusSent || 'Sent', class: 'bg-label-success' },
        Failed: { title: L.StatusFailed || 'Failed', class: 'bg-label-danger' },
        Cancelled: { title: L.StatusCancelled || 'Cancelled', class: 'bg-label-secondary' }
    });
    const formatDateTime = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString(window.CurrentLanguage || undefined);
    };

    const cancelDispatch = (row) => {
        if (!row?.id || !row?.tenantId) return;
        if (!CANCELLABLE.includes(row.status)) {
            window.showToast?.(L.CancelNotAllowed || '', 'warning');
            return;
        }
        window.showConfirm?.(L.CancelConfirm, async () => {
            try {
                const res = await fetch(`${apiBase}/${row.id}/cancel?targetTenantId=${encodeURIComponent(row.tenantId)}`, {
                    method: 'POST',
                    credentials: 'same-origin'
                });
                if (!res.ok) throw new Error('Cancel failed.');
                dt.ajax.reload(null, false);
                window.showToast?.(L.DispatchCancelled, 'success');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, { entityName: row.templateKey || row.id, type: 'danger', confirmButtonText: L.CancelDispatch });
    };

    const rowActionHandlers = {
        quickView: ({ row }) => {
            if (row?.id && row?.tenantId) {
                window.location.href = `/Platform/NotificationDispatches/Details/${row.id}?tenantId=${encodeURIComponent(row.tenantId)}`;
            }
        },
        cancel: ({ row }) => cancelDispatch(row)
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadTenantOptions();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            // Tenant-scoped server query; an empty tenant yields a controlled empty list (no fake rows).
            ajax: { url: buildAjaxUrl(), type: 'GET' },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: [[1, 'desc']],
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'queuedAt', name: 'queuedAt' },
                    { data: 'status', name: 'status' },
                    { data: 'templateKey', name: 'templateKey' },
                    { data: 'channel', name: 'channel' },
                    { data: 'providerCode', name: 'providerCode' },
                    { data: 'recipientCount', name: 'recipientCount' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type) => (type === 'display' ? formatDateTime(data) : (data || '')) },
                    {
                        targets: 2,
                        render: (data, type) => {
                            const status = getStatusMap()[data] || { title: L.Unknown, class: 'bg-label-primary' };
                            return type === 'display' ? `<span class="badge ${status.class}">${status.title}</span>` : (data || '');
                        }
                    },
                    { targets: 3, render: (data) => data ? `<span class="fw-medium text-heading">${data}</span>` : '-' },
                    { targets: 4, render: (data) => data ? `<span class="badge bg-label-info">${data}</span>` : '-' },
                    { targets: 6, className: 'text-center', render: (data) => (data ?? 0) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            const buttons = [
                                {
                                    key: 'quickView',
                                    className: 'js-quick-view me-1',
                                    icon: 'bx bx-show',
                                    attrs: { 'data-id': full.id, 'data-json': rowJson, 'title': L.QuickView }
                                }
                            ];
                            // No-shell rule: only render Cancel for a genuinely cancellable dispatch.
                            if (CANCELLABLE.includes(full.status)) {
                                buttons.push({
                                    key: 'cancel',
                                    className: 'text-danger',
                                    icon: 'bx bx-x-circle',
                                    text: L.CancelDispatch,
                                    attrs: { 'data-json': rowJson }
                                });
                            }
                            return window.DitenDataTable.renderActions(buttons);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    null,
                    null,
                    extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters();
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });
    };

    return {
        init: function () {
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => NotificationDispatchesList.init());
