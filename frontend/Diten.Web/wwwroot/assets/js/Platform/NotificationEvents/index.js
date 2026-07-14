/**
 * Notification Events - Platform Admin DataTables Index Script (MOD-0027-FU03).
 * READ-ONLY catalog: list + detail + sync-from-manifest + conditional archive. No create/edit/bulk.
 * Proxy-profile: browser JS only calls same-origin /Platform/NotificationEvents/api endpoints.
 * Events are manifest-driven; the list is never seeded with fake rows and sync always hits the real backend.
 */
'use strict';

const NotificationEventsList = (function () {
    let dt;

    const dtTableEl = document.querySelector('.datatables-notificationevents');
    const endpoint = '/Platform/NotificationEvents/api';
    const apiBase = endpoint;
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const CANCELLABLE_ARCHIVE = ['Draft', 'Active', 'Deprecated']; // anything not already Archived
    let appliedFilters = { ownerModule: '', channel: '', status: '', usageType: '', canTenantOverride: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const getAppliedFilterCount = () =>
        [appliedFilters.ownerModule, appliedFilters.channel, appliedFilters.status, appliedFilters.usageType, appliedFilters.canTenantOverride]
            .filter((v) => normalizeString(v).length > 0).length;

    const unwrapLookup = (payload) => payload?.data || payload?.Data || [];

    const buildAjaxUrl = () => {
        const params = new URLSearchParams();
        params.set('page', '1');
        params.set('pageSize', '200');
        if (appliedFilters.ownerModule) params.set('ownerModuleId', appliedFilters.ownerModule);
        if (appliedFilters.channel) params.set('channel', appliedFilters.channel);
        if (appliedFilters.status) params.set('status', appliedFilters.status);
        if (appliedFilters.usageType) params.set('usageType', appliedFilters.usageType);
        if (appliedFilters.canTenantOverride) params.set('canTenantOverride', appliedFilters.canTenantOverride);
        return `${apiBase}/events?${params.toString()}`;
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
        ['#filterChannel', '#filterStatus', '#filterUsageType', '#filterCanTenantOverride'].forEach((sel) => {
            $(sel).each(function () {
                const $s = $(this);
                if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
                $s.select2({
                    dropdownParent: $body,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    selectionCssClass: 'form-select form-select-sm',
                    placeholder: $s.data('placeholder') || '',
                    minimumResultsForSearch: Infinity,
                    width: 'element',
                    allowClear: true
                });
            });
        });
    };

    const loadChannelLookup = async () => {
        const select = document.getElementById('filterChannel');
        if (!select) return;
        try {
            const res = await fetch(`${apiBase}/lookups/notification-channels`, { credentials: 'same-origin' });
            if (!res.ok) return;
            unwrapLookup(await res.json()).forEach((item) => {
                if (!item?.value) return;
                const opt = document.createElement('option');
                opt.value = item.value;
                opt.textContent = item.name || item.code || item.value;
                select.appendChild(opt);
            });
        } catch (error) {
            console.error('[NotificationEvents Lookup] Failed.', error);
        }
    };

    const readFilters = () => ({
        ownerModule: normalizeString(document.getElementById('filterOwnerModule')?.value),
        channel: normalizeString($('#filterChannel').val()),
        status: normalizeString($('#filterStatus').val()),
        usageType: normalizeString($('#filterUsageType').val()),
        canTenantOverride: normalizeString($('#filterCanTenantOverride').val())
    });
    const clearFilters = () => {
        const o = document.getElementById('filterOwnerModule'); if (o) o.value = '';
        $('#filterChannel').val('').trigger('change');
        $('#filterStatus').val('').trigger('change');
        $('#filterUsageType').val('').trigger('change');
        $('#filterCanTenantOverride').val('').trigger('change');
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
            appliedFilters = readFilters();
            reload();
            const c = document.getElementById(filterCollapseId);
            if (c) bootstrap.Collapse.getOrCreateInstance(c, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            clearFilters();
            appliedFilters = { ownerModule: '', channel: '', status: '', usageType: '', canTenantOverride: '' };
            reload();
        });
    };

    const statusMap = () => ({
        Draft: { title: L.StatusDraft || 'Draft', class: 'bg-label-warning' },
        Active: { title: L.StatusActive || 'Active', class: 'bg-label-success' },
        Deprecated: { title: L.StatusDeprecated || 'Deprecated', class: 'bg-label-secondary' },
        Archived: { title: L.StatusArchived || 'Archived', class: 'bg-label-dark' }
    });
    const usageLabel = (u) => (u === 'ManualSelection' ? (L.UsageManualSelection || u) : (L.UsageSystemEvent || u));

    const archiveEvent = (row) => {
        if (!row?.id || row.status === 'Archived') return;
        window.showConfirm?.(L.ArchiveConfirm, async () => {
            try {
                const res = await fetch(`${apiBase}/events/${row.id}/archive`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) throw new Error('Archive failed.');
                dt.ajax.reload(null, false);
                window.showToast?.(L.EventArchived, 'success');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, { entityName: row.eventCode, type: 'danger', confirmButtonText: L.Archive });
    };

    const rowActionHandlers = {
        quickView: ({ row }) => {
            if (row?.eventCode) window.location.href = `/Platform/NotificationEvents/Details/${encodeURIComponent(row.eventCode)}`;
        },
        archive: ({ row }) => archiveEvent(row)
    };

    const renderSyncResult = (result) => {
        const panel = document.getElementById('syncResultPanel');
        const summary = document.getElementById('syncResultSummary');
        const issues = document.getElementById('syncResultIssues');
        if (!panel || !summary || !issues) return;
        const fmt = (tpl, ...a) => (tpl || '').replace(/\{(\d+)\}/g, (_, i) => a[Number(i)] ?? '');
        if ((result?.eventsDeclared ?? 0) === 0) {
            summary.textContent = L.NoEventsDeclared || '';
            issues.innerHTML = '';
        } else {
            summary.textContent = fmt(L.SyncSummary, result.providersScanned, result.eventsDeclared, result.synced, result.updated, result.withIssues);
            issues.innerHTML = '';
            (result.items || []).filter((i) => (i.issues || []).length).forEach((i) => {
                const li = document.createElement('li');
                li.textContent = `${i.eventCode}: ${i.issues.join(' ')}`;
                issues.appendChild(li);
            });
        }
        panel.classList.remove('d-none');
    };

    const runSync = async () => {
        try {
            const res = await fetch(`${apiBase}/events/sync-from-manifest`, { method: 'POST', credentials: 'same-origin' });
            const body = await res.json().catch(() => null);
            if (!res.ok) { window.showToast?.(L.ErrorOccurred, 'error'); return; }
            renderSyncResult(body?.data ?? body?.Data);
            dt.ajax.reload(null, false);
        } catch (error) {
            console.error('[NotificationEvents Sync] Failed.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadChannelLookup();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            syncBtn: {
                text: '<i class="icon-base bx bx-sync icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SyncFromManifest || '') + '</span>',
                className: 'btn btn-label-primary dt-sync-btn',
                attr: { title: L.SyncFromManifest, 'data-bs-toggle': 'tooltip' },
                action: () => runSync()
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: { url: buildAjaxUrl(), type: 'GET' },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: [[1, 'asc']],
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'eventCode', name: 'eventCode' },
                    { data: 'ownerModuleId', name: 'ownerModuleId' },
                    { data: 'channel', name: 'channel' },
                    { data: 'defaultTemplateKey', name: 'defaultTemplateKey' },
                    { data: 'status', name: 'status' },
                    { data: 'canTenantOverride', name: 'canTenantOverride' },
                    { data: 'usageType', name: 'usageType' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => data ? `<span class="fw-medium font-monospace">${data}</span>` : '-' },
                    { targets: 3, render: (data) => data ? `<span class="badge bg-label-info">${data}</span>` : '-' },
                    { targets: 4, render: (data) => data ? `<span class="font-monospace small">${data}</span>` : '-' },
                    {
                        targets: 5,
                        render: (data, type) => {
                            const s = statusMap()[data] || { title: L.Unknown, class: 'bg-label-primary' };
                            return type === 'display' ? `<span class="badge ${s.class}">${s.title}</span>` : (data || '');
                        }
                    },
                    { targets: 6, className: 'text-center', render: (data) => data ? `<span class="badge bg-label-success">${L.Yes || 'Yes'}</span>` : `<span class="text-muted">${L.No || 'No'}</span>` },
                    { targets: 7, render: (data) => usageLabel(data) },
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
                            if (CANCELLABLE_ARCHIVE.includes(full.status)) {
                                buttons.push({
                                    key: 'archive',
                                    className: 'text-danger',
                                    icon: 'bx bx-archive-in',
                                    text: L.Archive,
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
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
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

document.addEventListener('DOMContentLoaded', () => NotificationEventsList.init());
