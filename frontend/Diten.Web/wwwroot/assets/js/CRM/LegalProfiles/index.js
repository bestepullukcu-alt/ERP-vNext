/**
 * WP-KP-5a-UI — Country Legal Profiles list (country × language; WP-KP-5a). Golden Compact, SERVER data.
 *
 * THE LIST IS A COMPONENT: paging, search, sort, filters, Save View and the inline filter bar are
 * DitenDataTable.createList's. The same-origin proxy (/CRM/LegalProfiles/api/legal-profiles) answers the server-mode
 * contract { items, total, filteredTotal } over the CRM rows; the filter fields therefore carry no row matcher.
 *
 * NO SELECTION COLUMN (`HasSelection = false`): a legal profile is never bulk-deleted — it is archived on its page.
 * "Yalnız aktif" is the status filter set to `active` and applied through the factory's own Apply.
 */
'use strict';

const LegalProfilesList = (function () {
    let list = null;

    const endpoint = '/CRM/LegalProfiles/api';
    const dtTableEl = document.querySelector('.datatables-legalprofiles');
    const root = document.getElementById('regulatoryListRoot');
    const canManage = root?.dataset.canManage === 'true';
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const esc = (v) => String(v ?? '').replace(/[&<>'"]/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const detailsUrl = (id) => `/CRM/LegalProfiles/${encodeURIComponent(id)}`;

    const STATUS_TONES = { draft: 'bg-label-secondary', 'in-review': 'bg-label-warning', active: 'bg-label-success', superseded: 'bg-label-info', archived: 'bg-label-dark' };
    const statusLabel = (s) => L()['Status_' + String(s || '').replace(/-/g, '_')] || s || '';
    const statusBadge = (s) => `<span class="badge ${STATUS_TONES[s] || 'bg-label-secondary'}">${esc(statusLabel(s))}</span>`;

    const filterFields = [
        { id: 'filterCountry', key: 'countryCode', kind: 'single' },
        { id: 'filterLanguage', key: 'languageCode', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterIncludeArchived', key: 'includeArchived', kind: 'single' }
    ];

    // Same-origin proxy calls: no token is ever built here (the MVC proxy carries the HttpOnly session server-side).
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (!res.ok) throw new Error(String(res.status));
        return (await res.json())?.data;
    };

    const loadLookupOptions = async ({ fillSelect }) => {
        const countries = await getJson(`${endpoint}/lookups/countries`).catch(() => []);
        fillSelect(byId('filterCountry'), (countries || []).map((c) => ({ value: c.code, text: `${c.code} — ${c.name}` })), { showAllText: L().ShowAll || '' });
        const languages = new Map();
        (countries || []).forEach((c) => (c.languages || []).forEach((l) => languages.set(l.code, l.name)));
        fillSelect(byId('filterLanguage'), Array.from(languages, ([code, name]) => ({ value: code, text: `${code} — ${name}` })), { showAllText: L().ShowAll || '' });
    };

    // "Yalnız aktif" — the status filter becomes [active] and the factory's Apply runs (Save View follows it).
    const onlyActive = () => {
        const status = byId('filterStatus');
        if (!status) return;
        if (window.jQuery) window.jQuery(status).val(['active']).trigger('change');
        else Array.from(status.options).forEach((o) => { o.selected = o.value === 'active'; });
        byId('btnFilterApply')?.click();
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = detailsUrl(id); },
        edit: ({ id }) => { if (id) window.location.href = `/CRM/LegalProfiles/Edit/${encodeURIComponent(id)}`; }
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: `${endpoint}/legal-profiles`, type: 'GET', xhrFields: { withCredentials: true } },
            actions: { onRowAction: rowActionHandlers },
            toolbar: {
                addNewText: canManage ? L().AddNew : '',
                addNewAttr: { href: '/CRM/LegalProfiles/Create' },
                onAddNew: () => { window.location.href = '/CRM/LegalProfiles/Create'; },
                exportColumns: [1, 2, 3, 4, 5, 6],
                colvisColumns: [1, 2, 3, 4, 5, 6],
                extraButtons: {
                    onlyActiveBtn: {
                        text: `<i class="icon-base bx bx-check-circle icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(L().OnlyActive || '')}</span>`,
                        className: 'btn btn-label-success js-only-active',
                        attr: { title: L().OnlyActive || '' },
                        action: onlyActive
                    }
                }
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'CRM', pageKey: 'LegalProfiles', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6], baseOrder: [[1, 'asc']] },
            config: {
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'code', name: 'code' },
                    { data: 'countryCode', name: 'countryCode' },
                    { data: 'languageCode', name: 'languageCode' },
                    { data: 'version', name: 'version' },
                    { data: 'status', name: 'status' },
                    { data: 'updatedAt', name: 'updatedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => type === 'display' ? `<a class="fw-medium text-heading" href="${detailsUrl(full.id)}">${esc(data)}</a>` : (data ?? '') },
                    { targets: [2, 3], render: (data, type) => type === 'display' ? esc(data) : (data ?? '') },
                    { targets: 4, render: (data) => data ? `v${esc(data)}` : '' },
                    { targets: 5, render: (data, type) => type === 'display' ? statusBadge(data) : statusLabel(data) },
                    { targets: 6, render: (data, type) => type === 'display' && data ? esc(new Date(data).toLocaleString()) : (data ?? '') },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const actions = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L().QuickView } }];
                            if (canManage && full.canEdit) actions.push({ key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-id': full.id } });
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ]
            }
        });
    };

    return { init: () => { initDataTable(); } };
})();

document.addEventListener('DOMContentLoaded', () => LegalProfilesList.init());
