/**
 * MOD-0290-FU02 Brand register — tenant-shell Golden Compact list.
 *
 * WP-MDM-BRAND-ACCEPT-01 FIX1 (item 6) — the register stands on the list component (DitenDataTable.createList, server
 * mode): the factory owns the table, the inline filter, Save View, the toolbar and the server wire (start / length /
 * search / orderBy / orderDir and the filters → the same-origin MVC proxy → MDM). This file keeps only the register's
 * own business: renderers, the row menu, the archive confirmation and the indicator.
 *
 * Same-origin MVC proxy only — the MdmService port (5059) never appears in browser code.
 */
'use strict';

const BrandsList = (function () {
    const endpoint = '/MasterData/Brands/api';
    const tableEl = document.getElementById('dt-brands');
    const register = document.getElementById('brandRegister');
    const L = window.BrandL10n || {};
    const flag = (name) => register?.getAttribute(`data-${name}`) === 'true';
    const canCreate = flag('can-create');
    const canUpdate = flag('can-update');
    const canArchive = flag('can-archive');
    const personalizationContext = { moduleKey: 'MasterData', pageKey: 'Brands' };
    // The data columns, in the order _DataTable.cshtml declares them (0 = control, 10 = actions). Save View, export and
    // the column chooser all read THESE indexes, so a saved view can never point at a shifted column.
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    const baseOrder = [[2, 'asc']];
    let list = null;

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (ch) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[ch]));
    const badge = (value, cls = 'primary') => `<span class="badge bg-label-${cls}">${escapeHtml(value || '—')}</span>`;
    const statusLabel = (value) => L[`Status_${value}`] || value || '—';

    // FIX1 (item 13) — dates in the language the interface is in, not the browser's.
    const uiLanguage = () => document.documentElement.lang || undefined;
    const formatDate = (value) => value
        ? new Intl.DateTimeFormat(uiLanguage(), { dateStyle: 'medium' }).format(new Date(value))
        : '—';
    const formatDateTime = (value) => value
        ? new Intl.DateTimeFormat(uiLanguage(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : '—';

    // FIX1 (item 8) — a reference with no master to resolve it reads as such, never as its id.
    const unresolvedReference = (value) => value
        ? `<span class="text-muted fst-italic">${escapeHtml(L.Unresolved)}</span>`
        : '—';

    // ── refusals (FIX1 item 7) — the proxy answers every refusal with a sentence in the reader's language ──────────
    const warnIfUnmapped = (payload, status) => {
        if (payload?.unmapped === true) console.warn('Brands: a refusal without a sentence mapping', status);
    };
    const statusSentence = (status) => L[`RefusedStatus_${status}`] || L.RefusedStatus_Other || L.ErrorState;
    const failureSentence = (payload, status) => {
        warnIfUnmapped(payload, status);
        const errors = payload?.errors || payload?.Errors || [];
        if (Array.isArray(errors) && errors.find(Boolean)) return errors.find(Boolean);
        return statusSentence(status);
    };
    const readXhrPayload = (xhr) => {
        if (xhr?.responseJSON && typeof xhr.responseJSON === 'object') return xhr.responseJSON;
        try { return JSON.parse(xhr?.responseText || '{}'); } catch (error) { return {}; }
    };
    // The list's own failed read (jQuery, through the factory): the same sentence and the same unmapped warning.
    const onListError = function (xhr) {
        const status = Number(xhr?.status) || 0;
        if (status === 401) {
            if (!this?._retried) window.DtDefaults?.handleUnauthorized?.(this);
            return;
        }
        if (xhr?.statusText === 'abort') return;
        window.showToast?.(failureSentence(readXhrPayload(xhr), status), 'error');
    };

    // FIX1 (item 11) — the archive proxy validates the page's anti-forgery token.
    const antiForgeryToken = () =>
        document.querySelector('#brandListToken input[name="__RequestVerificationToken"]')?.value || '';

    // ── the row menu (FIX1 item 13: offered only to who the controller says may act) ─────────────────────────────
    // The quick view is dispatched by the list component (`data-row-action` → quickView); the full page is a link in it.
    const rowActionPresentation = {
        details: { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show' }
    };
    const renderActions = (row) => {
        const id = escapeHtml(row.brandId);
        const details = rowActionPresentation.details;
        const items = [`<a href="javascript:void(0);" class="dropdown-item ${details.className}" data-row-action="${details.key}" data-id="${id}"><i class="${details.icon} me-2"></i>${escapeHtml(L.View)}</a>`];
        // Archived brands are read-only — no edit, no archive. There is no delete action at all.
        if (!row.isArchived && canUpdate) {
            items.push(`<a class="dropdown-item" href="/MasterData/Brands/${id}/Edit"><i class="bx bx-edit me-2"></i>${escapeHtml(L.EditBrand)}</a>`);
        }
        if (!row.isArchived && canArchive) {
            items.push(`<button type="button" class="dropdown-item text-warning" data-row-action="archive" data-id="${id}"><i class="bx bx-archive-in me-2"></i>${escapeHtml(L.ArchiveBrand)}</button>`);
        }
        return `<div class="dropdown"><button type="button" class="btn btn-sm btn-icon" data-bs-toggle="dropdown" aria-label="${escapeHtml(L.Actions)}"><i class="bx bx-dots-vertical-rounded"></i></button><div class="dropdown-menu dropdown-menu-end">${items.join('')}</div></div>`;
    };

    /*
     * FIX1 (item 3) — archiving is IRREVERSIBLE (there is no un-archive; the owner decides later whether one exists), so
     * the confirmation says so and how many of the brand's products are still in use — read fresh from the detail.
     */
    const archiveSentence = (liveProducts) => {
        const consequence = liveProducts > 0
            ? String(L.ArchiveBrandLiveProducts || '').replace('{0}', String(liveProducts))
            : L.ArchiveBrandNoLiveProducts;
        return [L.ArchiveBrandIrreversible, consequence].filter(Boolean).join(' ');
    };
    const readLiveProducts = async (id) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/detail`, { credentials: 'same-origin', headers: getAuthHeaders() });
        let payload = {};
        try { payload = await response.json(); } catch (error) { }
        if (!response.ok) throw new Error(failureSentence(payload, response.status));
        const count = payload?.data?.liveProductCount;
        return Number.isInteger(count) ? count : 0;
    };
    const archive = async ({ id }) => {
        if (!id) return;
        let liveProducts;
        try {
            liveProducts = await readLiveProducts(id);
        } catch (error) {
            window.showToast?.(error.message, 'error');
            return;
        }
        window.showConfirm?.(L.ArchiveBrand, async () => {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/archive`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: Object.assign(getAuthHeaders(), { RequestVerificationToken: antiForgeryToken() })
            });
            let payload = {};
            try { payload = await response.json(); } catch (error) { }
            if (!response.ok) {
                window.showToast?.(failureSentence(payload, response.status), 'error');
                return;
            }
            window.showToast?.(L.RecordArchived, 'success');
            list?.dt?.ajax?.reload?.(null, false);
        }, { type: 'warning', subtext: archiveSentence(liveProducts), confirmButtonText: L.ArchiveBrand });
    };

    // FIX1 (item 6) — the quick view shows the row as the register read it; nothing is fetched or changed here.
    const setText = (elementId, value) => {
        const element = document.getElementById(elementId);
        if (element) element.textContent = value === null || value === undefined || value === '' ? '—' : String(value);
    };
    const populatePreview = (row) => {
        if (!row) return;
        setText('oc-title', row.brandName);
        setText('oc-subtitle', row.brandCode);
        setText('oc-code', row.brandCode);
        setText('oc-name', row.brandName);
        setText('oc-status', statusLabel(row.brandStatus));
        setText('oc-effective-from', formatDate(row.effectiveFrom));
        setText('oc-effective-to', formatDate(row.effectiveTo));
        setText('oc-archived', row.isArchived ? L.Yes : L.No);
        setText('oc-updated-at', formatDateTime(row.updatedAt));
        const link = document.getElementById('oc-open-details');
        if (link) link.setAttribute('href', `/MasterData/Brands/${encodeURIComponent(row.brandId)}`);
    };
    // A quick view opened from the responsive row modal is the same row action — the list component delegates both;
    // this only keeps a plain click on the item from following its void link.
    document.addEventListener('click', (event) => {
        const trigger = event.target.closest('.js-quick-view');
        if (trigger && trigger.getAttribute('href') === 'javascript:void(0);') event.preventDefault();
    });

    // FIX1 (item 3) — the indicator comes with every page of the register.
    const showIndicator = (json) => {
        const value = json?.data?.archivedWithLiveProducts;
        const host = document.getElementById('kpiArchivedWithLiveProducts');
        if (host) host.textContent = Number.isInteger(value) ? String(value) : '—';
    };

    /*
     * No silent partial export: until the register has a server export (every matching row), each Print / CSV / Excel /
     * PDF / Copy entry says plainly that it writes only the page on screen (the GSKU register's rule, read-only use of
     * the shared buttons).
     */
    const exportEntrySelector = '.dt-controlled-copy, .buttons-csv, .buttons-excel, .buttons-copy, .buttons-print, .buttons-pdf';
    const exportEntryIcon = '.bx-printer, .bx-file, .bxs-file-export, .bxs-file-pdf, .bx-copy';
    const labelExportsCurrentPageOnly = (api) => {
        const nodes = api?.buttons?.()?.nodes?.();
        Array.from(nodes || []).forEach((node) => {
            if (!node?.matches || node.matches('.dt-server-export')) return;
            if (!node.matches(exportEntrySelector) && !node.querySelector(exportEntryIcon)) return;
            if (node.querySelector('.dt-current-page-only')) return;
            const note = document.createElement('small');
            note.className = 'd-block text-muted dt-current-page-only';
            note.textContent = L.CurrentPageOnly || '';
            node.appendChild(note);
            node.setAttribute('title', L.CurrentPageOnly || '');
        });
    };

    // The createList options of the register — one place, so the wire and the saved-view keys are measurable.
    const buildListOptions = () => ({
        tableEl,
        dataMode: 'server',
        ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), error: onListError },
        onResponse: showIndicator,
        actions: { onRowAction: { archive } },
        quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populatePreview, actionKey: rowActionPresentation.details.key },
        toolbar: {
            addNewText: canCreate ? L.CreateBrand : '',
            onAddNew: () => { window.location.href = '/MasterData/Brands/Create'; },
            exportColumns: saveViewColumnIndexes,
            colvisColumns: saveViewColumnIndexes
        },
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: 'inlineFilterCollapse',
            applyBtn: 'btnFilterApply',
            resetBtn: 'btnFilterReset',
            fields: [
                { id: 'filterBrandStatus', key: 'brandStatus', kind: 'single' },
                { id: 'filterIncludeArchived', key: 'includeArchived', kind: 'single' },
                { id: 'filterBusinessUnitId', key: 'businessUnitId', kind: 'text' },
                { id: 'filterTherapeuticAreaId', key: 'therapeuticAreaId', kind: 'text' }
            ]
        },
        savedView: {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            saveViewColumnIndexes,
            defaultVisibleColumnIndexes: saveViewColumnIndexes,
            baseOrder
        },
        config: {
            pageLength: 20,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            initComplete: function () { labelExportsCurrentPageOnly(this.api()); },
            language: { emptyTable: L.EmptyState, processing: L.Loading },
            // `data` names are MDM's orderBy whitelist; the two reference columns are not sortable, so never sent.
            columns: [
                { data: 'brandId', name: 'control' },
                { data: 'brandCode', name: 'brandCode' },
                { data: 'brandName', name: 'brandName' },
                { data: 'brandStatus', name: 'brandStatus' },
                { data: 'businessUnitId', name: 'businessUnitId' },
                { data: 'therapeuticAreaId', name: 'therapeuticAreaId' },
                { data: 'effectiveFrom', name: 'effectiveFrom' },
                { data: 'effectiveTo', name: 'effectiveTo' },
                { data: 'isArchived', name: 'isArchived' },
                { data: 'updatedAt', name: 'updatedAt' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => escapeHtml(data) },
                { targets: 2, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 3, render: (data) => badge(statusLabel(data), data === 'archived' ? 'secondary' : 'primary') },
                { targets: [4, 5], orderable: false, render: unresolvedReference },
                { targets: [6, 7], render: formatDate },
                { targets: 8, render: (data) => badge(data ? L.Yes : L.No, data ? 'warning' : 'success') },
                { targets: 9, render: formatDateTime },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ]
        }
    });

    // Fail closed: when the contract cannot be read, the register says so and offers nothing to change.
    const loadContract = async () => {
        try {
            const response = await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() });
            let payload = {};
            try { payload = await response.json(); } catch (error) { }
            if (!response.ok) throw new Error(failureSentence(payload, response.status));
            const contract = payload?.data;
            if (!contract?.isReady || !contract?.features?.supportsBrandManagement) throw new Error(L.BrandProductContractUnavailable);
            const select = document.getElementById('filterBrandStatus');
            if (select) {
                select.innerHTML = `<option value="">${escapeHtml(L.BrandStatus)}</option>`
                    + (contract.vocabulary?.brandStatuses || [])
                        .map((x) => `<option value="${escapeHtml(x)}">${escapeHtml(statusLabel(x))}</option>`).join('');
            }
            return true;
        } catch (error) {
            const host = document.getElementById('brandContractError');
            if (host) { host.textContent = error.message || L.BrandProductContractUnavailable; host.classList.remove('d-none'); }
            return false;
        }
    };

    const init = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        if (!await loadContract()) return;
        list = await window.DitenDataTable.createList(buildListOptions());
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => { BrandsList.init(); });
