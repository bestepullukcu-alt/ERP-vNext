/**
 * MOD-0193 BOM & Routings — DataTables Index Script (Golden Compact, data_mode: server).
 *
 * The list is the shared component (DitenDataTable.createList): paging, search, sort and the status filter run on the
 * service — the factory sends start/length/search/orderBy/orderDir + status, and reads back { items, total,
 * filteredTotal }. The filter field therefore carries no `matches`: the browser never filters a row.
 * Reads and the export go straight through the Gateway (window.API.manufacturing + /api/bom/versions[/export]);
 * deleting a draft goes through the same-origin adapter with the row's version (/Manufacturing/Boms/api/{id}/delete).
 *
 * LEGAL ENTITY (MVP-1 pattern): the list is scoped to the legal entity chosen in #bomLegalEntity — the list request
 * carries it as X-Legal-Entity-Id (jQuery beforeSend), the export (a fetch the factory makes, which cannot take a page
 * header) as the `legalEntityId` query the service accepts on GET, and every link and adapter call names it. Changing
 * the choice rebuilds the page. The service proves it with MDM; this page only chooses.
 */
'use strict';

const BomsList = (function () {
    let list = null;

    const dtTableEl = document.querySelector('.datatables-boms');
    const apiUrl = window.API?.manufacturing;
    const L = () => window.L10n || {};
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    const antiForgery = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    // The tenant header (shared helper) plus this page's antiforgery token — the delete goes through the same-origin adapter.
    const getAuthHeaders = (includeJson = false) =>
        Object.assign({ 'RequestVerificationToken': antiForgery() }, window.DitenDataTable?.getAuthHeaders?.(includeJson) || {});

    const getStatusMap = () => ({
        Draft: { title: L().StatusDraft, class: 'bg-label-secondary' },
        Effective: { title: L().StatusEffective, class: 'bg-label-success' },
        Superseded: { title: L().StatusSuperseded, class: 'bg-label-dark' }
    });
    const statusTitle = (value) => getStatusMap()[value]?.title || value || L().Unknown || '';
    const formatDate = (value) => {
        if (!value) return '';
        const d = new Date(value);
        return isNaN(d.getTime()) ? String(value) : d.toLocaleString();
    };

    const legalEntitySelect = document.getElementById('bomLegalEntity');
    const legalEntityNote = document.getElementById('bomLegalEntityNote');
    const storageKey = 'diten.manufacturing.boms.legalEntity';
    const currentLegalEntity = () => legalEntitySelect?.value || '';
    const withLegalEntity = (path) => `${path}${path.includes('?') ? '&' : '?'}legalEntityId=${encodeURIComponent(currentLegalEntity())}`;
    const remember = (value) => { try { window.localStorage.setItem(storageKey, value); } catch (e) { /* per-viewer convenience only */ } };
    const remembered = () => { try { return window.localStorage.getItem(storageKey) || ''; } catch (e) { return ''; } };

    const loadLegalEntities = async () => {
        const res = await fetch('/Manufacturing/Boms/api/legal-entities', { credentials: 'same-origin' });
        const body = await res.json().catch(() => null);
        if (!res.ok || !Array.isArray(body)) {
            legalEntityNote.textContent = body?.message || L().ErrorOccurred || '';
            legalEntityNote.hidden = false;
            return [];
        }
        body.forEach((item) => {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = item.code ? `${item.name} (${item.code})` : item.name;
            legalEntitySelect.appendChild(option);
        });
        const wanted = remembered();
        legalEntitySelect.value = body.some((e) => e.id === wanted) ? wanted : (body.length === 1 ? body[0].id : '');
        return body;
    };

    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi' }
    ];

    const deleteRow = ({ row }) => {
        if (!row?.bomVersionId || row.status !== 'Draft') return;
        const text = (L().DeleteConfirm || L().AreYouSure || '').replace('{0}', String(row.version));
        window.showConfirm?.(text, async () => {
            try {
                const res = await fetch(withLegalEntity(`/Manufacturing/Boms/api/${encodeURIComponent(row.bomVersionId)}/delete?rowVersion=${encodeURIComponent(row.rowVersion)}`), {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: getAuthHeaders()
                });
                if (!res.ok) {
                    const body = await res.json().catch(() => null);
                    window.showToast?.(body?.message || L().ErrorOccurred, 'error');
                    return;
                }
                list.reload('RecordDeleted');
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: String(row.version), type: 'danger', confirmButtonText: L().Delete });
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = withLegalEntity(`/Manufacturing/Boms/Details/${encodeURIComponent(id)}`); },
        edit: ({ id }) => { if (id) window.location.href = withLegalEntity(`/Manufacturing/Boms/Edit/${encodeURIComponent(id)}`); },
        delete: deleteRow
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Boms] window.API.manufacturing is required.'); return; }

        const options = await loadLegalEntities();
        if (!options.length) {
            if (legalEntityNote.hidden) {
                legalEntityNote.textContent = L().NoLegalEntities || '';
                legalEntityNote.hidden = false;
            }
            return;
        }

        // Several legal entities and none chosen yet: wait for the choice; a list without one would be refused (400).
        if (!currentLegalEntity()) {
            await new Promise((resolve) => legalEntitySelect.addEventListener('change', resolve, { once: true }));
        }

        remember(currentLegalEntity());
        // Another legal entity is another list: its rows, its export and its links. Rebuild the page on it rather than
        // patching a live table whose export target was fixed when it was built.
        legalEntitySelect.addEventListener('change', () => {
            if (!currentLegalEntity()) return;
            remember(currentLegalEntity());
            window.location.reload();
        });

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: {
                url: apiUrl + '/api/bom/versions',
                type: 'GET',
                xhrFields: { withCredentials: true },
                beforeSend: (xhr) => xhr.setRequestHeader('X-Legal-Entity-Id', currentLegalEntity())
            },
            // BL-452: CSV/Excel = every matching row with the visible columns, filters, search and order — of the chosen
            // legal entity (the export is a fetch the factory makes, so the legal entity rides in the query).
            export: { mode: 'server', url: withLegalEntity(apiUrl + '/api/bom/versions/export'), fileName: 'bom-versions' },
            actions: { onRowAction: rowActionHandlers },
            toolbar: {
                addNewText: L().AddNew,
                addNewAttr: { href: '/Manufacturing/Boms/Create' },
                onAddNew: () => { window.location.href = withLegalEntity('/Manufacturing/Boms/Create'); },
                exportColumns: [1, 2, 3, 4, 5, 6, 7, 8],
                colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8]
            },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'Manufacturing',
                pageKey: 'Boms',
                saveViewColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6, 7, 8],
                baseOrder: [[8, 'desc']]
            },
            config: {
                columns: [
                    { data: 'bomVersionId', name: 'control' },
                    { data: 'itemId', name: 'itemId' },
                    { data: 'version', name: 'version' },
                    { data: 'status', name: 'status' },
                    { data: 'description', name: 'description' },
                    { data: 'componentCount', name: 'componentCount' },
                    { data: 'stepCount', name: 'stepCount' },
                    { data: 'effectiveFrom', name: 'effectiveFrom' },
                    { data: 'updatedAt', name: 'updatedAt' },
                    { data: 'bomVersionId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading text-break">${escapeHtml(data)}</span>` },
                    {
                        targets: 3,
                        render: (data, type) => type === 'display'
                            ? `<span class="badge ${getStatusMap()[data]?.class || 'bg-label-primary'}">${escapeHtml(statusTitle(data))}</span>`
                            : statusTitle(data)
                    },
                    { targets: 4, render: (data) => escapeHtml(data || '') },
                    // Counts are not a service sort key: not orderable, so they never become one.
                    { targets: [5, 6], orderable: false, searchable: false },
                    { targets: [7, 8], searchable: false, render: (data, type) => type === 'display' ? escapeHtml(formatDate(data) || '-') : (data || '') },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full); // renderActions escapes attribute values itself
                            const actions = [
                                { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.bomVersionId, 'title': L().QuickView } }
                            ];
                            if (full.status === 'Draft') {
                                actions.push({ key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-id': full.bomVersionId, 'data-json': rowJson } });
                                actions.push({ key: 'delete', className: 'text-danger', icon: 'bx bx-trash', text: L().Delete, attrs: { 'data-json': rowJson } });
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ]
            }
        });
    };

    return {
        init: function () {
            initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => BomsList.init());
