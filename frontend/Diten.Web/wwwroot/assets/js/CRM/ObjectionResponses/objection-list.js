/**
 * WP-VW-W3 (W3-WEB-c) — A1 · the objection library list. Golden Slim, SERVER data: paging, search, filters and Save View
 * are DitenDataTable.createList's; the same-origin proxy (/CRM/ObjectionResponses/api/objection-responses) turns them into
 * CRM's page / pageSize / q and answers { items, total, filteredTotal }. Columns as the mockup: product · category (code →
 * tone, label from BRD) · the question · claims · steps using it · status ("Onay bekliyor" while in review) · last change ·
 * actions (view; edit only when CRM says canEdit). total = 0 → the empty card (A1 boş), a filter with no hit → the table's
 * own "no result". No selection column: an answer is never bulk-deleted (it is retired on its page).
 */
'use strict';

const ObjectionList = (function () {
    const C = window.ObjectionCore;
    const root = document.getElementById('orListRoot');
    const tableEl = document.querySelector('.datatables-objectionresponses');
    if (!C || !root || !tableEl) return { init: () => { } };
    const L = window.ObjectionL10n || {};
    const base = root.dataset.apiBase;
    const canManage = root.dataset.canManage === 'true';
    const canReadProducts = root.dataset.canReadProducts === 'true';
    const byId = id => document.getElementById(id);
    const esc = v => String(v == null ? '' : v).replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const detailsUrl = id => `/CRM/ObjectionResponses/${encodeURIComponent(id)}`;
    let categories = [];

    const getJson = async url => {
        const res = await window.fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        if (!res.ok) throw new Error(String(res.status));
        return (await res.json())?.data;
    };

    const STATUS_TONES = { draft: 'secondary', in_review: 'warning', approved: 'success', retired: 'dark' };
    const statusBadge = s => `<span class="badge bg-label-${STATUS_TONES[s] || 'secondary'}">${esc(L['Status_' + s] || s || '')}</span>`;
    const categoryLabel = code => (categories.find(c => c.code === code) || {}).label || code || '';
    const categoryChip = code => code ? `<span class="badge bg-label-${C.categoryTone(code)} or-cat">${esc(categoryLabel(code))}</span>` : '<span class="text-muted">—</span>';
    const initials = name => String(name || '?').trim().slice(0, 2).toLocaleUpperCase(document.documentElement.lang || undefined);
    const date = v => {
        const d = v ? new Date(v) : null;
        return d && !Number.isNaN(d.getTime()) ? d.toLocaleDateString(document.documentElement.lang || undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '';
    };

    const filterFields = [
        { id: 'filterProduct', key: 'productId', kind: 'single' },
        { id: 'filterCategory', key: 'categoryCode', kind: 'single' },
        { id: 'filterStatus', key: 'status', kind: 'single' }
    ];

    const loadLookupOptions = async ({ fillSelect }) => {
        const [products, cats] = await Promise.all([
            canReadProducts ? getJson(`${base}/lookups/products`).catch(() => []) : Promise.resolve([]),
            getJson(`${base}/lookups/categories`).catch(() => [])
        ]);
        categories = cats || [];
        fillSelect(byId('filterProduct'), (products || []).map(p => ({ value: p.id, text: p.name || p.code || p.id })), { showAllText: L.AllProducts || '' });
        fillSelect(byId('filterCategory'), categories.map(c => ({ value: c.code, text: c.label })), { showAllText: L.AllCategories || '' });
        fillSelect(byId('filterStatus'), C.STATUSES.map(s => ({ value: s, text: L['Status_' + s] || s })), { showAllText: L.AllStatuses || '' });
    };

    // An empty library is not "no result": the empty card replaces the table until the first answer exists.
    const onResponse = json => {
        const total = Number(json?.recordsTotal ?? json?.data?.total ?? json?.total);
        const empty = Number.isFinite(total) && total === 0;
        byId('orEmpty')?.classList.toggle('d-none', !empty);
        byId('orTable')?.classList.toggle('d-none', empty);
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = detailsUrl(id); },
        edit: ({ id }) => { if (id) window.location.href = `/CRM/ObjectionResponses/Edit/${encodeURIComponent(id)}`; }
    };

    const init = async () => {
        if (!window.DitenDataTable) return;
        if (window.jQuery) window.jQuery(tableEl).on('xhr.dt', (e, settings, json) => onResponse(json));
        await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: `${base}/objection-responses`, type: 'GET', xhrFields: { withCredentials: true } },
            actions: { onRowAction: rowActionHandlers },
            toolbar: {
                addNewText: canManage ? L.NewResponse : '',
                addNewAttr: { href: '/CRM/ObjectionResponses/Create' },
                onAddNew: () => { window.location.href = '/CRM/ObjectionResponses/Create'; },
                searchPlaceholder: L.SearchPlaceholder,
                exportColumns: [1, 2, 3, 4, 5, 6, 7],
                colvisColumns: [1, 2, 3, 4, 5, 6, 7]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'CRM', pageKey: 'ObjectionResponses', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7], baseOrder: [] },
            config: {
                ordering: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'productName', name: 'productName' },
                    { data: 'categoryCode', name: 'categoryCode' },
                    { data: 'question', name: 'question' },
                    { data: 'claimIds', name: 'claimIds' },
                    { data: 'usageCount', name: 'usageCount' },
                    { data: 'status', name: 'status' },
                    { data: 'updatedAt', name: 'updatedAt' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1, orderable: false,
                        render: (data, type, full) => type === 'display'
                            ? `<div class="d-flex align-items-center gap-2"><span class="avatar avatar-sm flex-shrink-0"><span class="avatar-initial rounded bg-label-secondary">${esc(initials(data))}</span></span><div class="min-w-0"><div class="fw-medium text-heading text-truncate">${esc(data || '—')}</div>${full.productForm ? `<small class="text-muted">${esc(full.productForm)}</small>` : ''}</div></div>`
                            : (data ?? '')
                    },
                    { targets: 2, orderable: false, render: (data, type) => type === 'display' ? categoryChip(data) : categoryLabel(data) },
                    {
                        targets: 3, orderable: false,
                        render: (data, type, full) => type === 'display'
                            ? `<a class="text-heading or-question" href="${detailsUrl(full.id)}" title="${esc(data || '')}">${esc(data || '—')}</a>`
                            : (data ?? '')
                    },
                    { targets: 4, orderable: false, render: (data, type) => { const n = (data || []).length; return type === 'display' ? `<span class="text-nowrap"><i class="bx bx-check-shield text-muted me-1" aria-hidden="true"></i>${n}</span>` : n; } },
                    { targets: 5, orderable: false, render: (data, type) => { const n = data || 0; return type === 'display' ? `<span class="text-nowrap ${n ? '' : 'text-muted'}"><i class="bx bx-slideshow me-1" aria-hidden="true"></i>${n}</span>` : n; } },
                    { targets: 6, orderable: false, render: (data, type) => type === 'display' ? statusBadge(data) : (L['Status_' + data] || data || '') },
                    { targets: 7, orderable: false, render: (data, type, full) => { const at = data || full.createdAt; return type === 'display' ? `<small class="text-nowrap">${esc(date(at))}</small>` : (at ?? ''); } },
                    {
                        targets: -1, title: L.ColActions, searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data, type, full) => {
                            const actions = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, title: L.ViewRecord } }];
                            if (canManage && full.abilities && full.abilities.canEdit) {
                                actions.push({ key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L.EditRecord, attrs: { 'data-id': full.id } });
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ]
            }
        });
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ObjectionList.init());
