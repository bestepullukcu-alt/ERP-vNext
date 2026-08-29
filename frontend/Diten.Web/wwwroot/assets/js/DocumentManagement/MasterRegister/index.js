/**
 * MOD-0029-FU24 — Document Master Register TenantShell UI (GMG-QMS-SOP-0001 §18 LOG-0001, §20).
 * Golden Reference Compact pattern, same-origin MVC proxy profile:
 *   • every request goes to /DocumentManagement/MasterRegister/api/* (no direct Platform 5057 call),
 *   • no tenant id is ever read or sent from the browser (the MVC proxy resolves it server-side),
 *   • no destructive action is exposed (no delete/purge, no lifecycle/approval/gate/signature mutation).
 * Scope: list + create + metadata edit + General detail. Governance detail tabs are FU25/FU26/FU27/FU28/FU34.
 */
'use strict';

const MasterRegisterCommon = (function () {
    const endpoint = '/DocumentManagement/MasterRegister/api';

    const L = () => window.L10n || {};
    const t = (key) => L()[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));

    const na = () => t('NotAvailable');
    const text = (value) => (value === null || value === undefined || value === '' ? na() : esc(value));

    const formatDate = (value) => {
        if (!value) return na();
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? na() : d.toLocaleDateString();
    };
    const formatDateTime = (value) => {
        if (!value) return na();
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return na();
        const parts = new Intl.DateTimeFormat('en-US', {
            month: 'short', day: '2-digit', year: '2-digit',
            hour: '2-digit', minute: '2-digit', hour12: true
        }).formatToParts(d).reduce((result, part) => {
            result[part.type] = part.value;
            return result;
        }, {});
        return `${parts.month} ${parts.day}, ${parts.year} ${parts.hour}:${parts.minute} ${parts.dayPeriod}`;
    };
    const toDateOnly = (value) => {
        if (!value) return null;
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? null : new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
    };

    const unwrap = (payload) => payload?.data ?? payload?.Data ?? null;
    const unwrapList = (payload) => {
        const data = unwrap(payload) ?? payload;
        if (Array.isArray(data)) return data;
        for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(data?.[k])) return data[k];
        return [];
    };

    /**
     * Envelope-aware failure handling. 401 → login handoff via DitenUnauthorized, 403 → localized forbidden toast,
     * 400/422 → server validation messages, anything else → generic error. Never leaks raw server text as HTML.
     */
    const describeFailure = (res, payload) => {
        const errors = payload?.errors || payload?.Errors;
        const serverMessage = Array.isArray(errors) ? errors.filter(Boolean).join(' • ') : (typeof errors === 'string' ? errors : '');
        if (res?.status === 401) return t('Unauthorized');
        if (res?.status === 403) return serverMessage || t('Forbidden');
        if (res?.status === 400 || res?.status === 422) return serverMessage || t('ValidationFailed');
        return serverMessage || t('ErrorOccurred');
    };
    const handleFailure = (res, payload, fallbackKey) => {
        if (window.DitenUnauthorized?.handle(res, payload)) return true;
        window.showToast?.(describeFailure(res, payload) || t(fallbackKey || 'ErrorOccurred'), 'error');
        return true;
    };

    const badge = (color, label) => `<span class="badge bg-label-${color}">${esc(label)}</span>`;

    const lifecycleBadge = (value) => {
        const key = String(value || '');
        const map = {
            Draft: 'secondary', InReview: 'info', ApprovedPendingEffective: 'primary', Effective: 'success',
            UnderRevision: 'warning', Suspended: 'danger', Superseded: 'secondary', Retired: 'dark', ObsoleteCopy: 'dark'
        };
        return key ? badge(map[key] || 'secondary', t(`Lifecycle${key}`)) : na();
    };
    const registerBadge = (value) => {
        const key = String(value || '');
        const map = {
            Draft: 'secondary', Active: 'success', Archived: 'dark',
            CorrectionPending: 'warning', Superseded: 'secondary', Retired: 'dark'
        };
        return key ? badge(map[key] || 'secondary', t(`Register${key}`)) : na();
    };
    const criticalityBadge = (value) => {
        const key = String(value || '');
        const map = { Critical: 'danger', Major: 'warning', Minor: 'info', UrgentTemporary: 'primary' };
        return key ? badge(map[key] || 'secondary', t(`Criticality${key}`)) : na();
    };
    const classLabel = (value) => (value ? t(`Class${value}`) : na());
    const typeLabel = (value) => (value ? t(`Type${value}`) : na());
    const kindBadge = (value) => {
        const key = String(value || '');
        return key ? badge(key === 'Record' ? 'info' : 'primary', t(`Kind${key}`)) : na();
    };
    const allocationBadge = (isSystemAllocated) =>
        badge(isSystemAllocated ? 'primary' : 'secondary', isSystemAllocated ? t('AllocationSystem') : t('AllocationManual'));
    const boolBadge = (value) => badge(value ? 'success' : 'secondary', value ? t('Yes') : t('No'));

    return {
        endpoint, L, t, token, getAuthHeaders, esc, na, text,
        formatDate, formatDateTime, toDateOnly, unwrap, unwrapList,
        describeFailure, handleFailure,
        lifecycleBadge, registerBadge, criticalityBadge, classLabel, typeLabel, kindBadge, allocationBadge, boolBadge
    };
})();

// ── Index (list) ─────────────────────────────────────────────────────────────
const MasterRegisterList = (function () {
    const C = MasterRegisterCommon;
    let dt;

    const dtTableEl = document.querySelector('.datatables-masterregister');
    // Verifier profile marker: same-origin MVC proxy endpoints only; HttpOnly cookies stay server-side.
    const perms = window.MasterRegisterPerms || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const totalColumnCount = 14;
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    const baseOrder = [[2, 'asc']];

    const emptyFilters = () => ({
        documentClass: [], criticality: [], lifecycleStatus: [], registerStatus: [], documentKind: [],
        effectiveFrom: '', effectiveTo: '', reviewFrom: '', reviewTo: ''
    });
    let appliedFilters = emptyFilters();

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const hasFilterValue = (v) => (Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0);
    const matchesMulti = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(normalizeString(actual).toUpperCase());
    };
    const matchesRange = (from, to, value) => {
        const v = C.toDateOnly(value);
        const f = from ? C.toDateOnly(from) : null;
        const tt = to ? C.toDateOnly(to) : null;
        if (f === null && tt === null) return true;
        if (v === null) return false;
        if (f !== null && v < f) return false;
        if (tt !== null && v > tt) return false;
        return true;
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter(hasFilterValue).length;

    // ── Inline filter wiring (DataTable v2 toolbar contract) ──
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

    const registerTableFilters = () => {
        if (!dtTableEl || !window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl.dataset.compactFilterBound === '1') return;
        dtTableEl.dataset.compactFilterBound = '1';
        // Client-side predicate: the FU06 list endpoint accepts single-value filters only, so the multi-select
        // filter panel is evaluated here over the loaded page (documented as a FU24 gap).
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMulti(appliedFilters.documentClass, row.documentClass)
                && matchesMulti(appliedFilters.criticality, row.criticality)
                && matchesMulti(appliedFilters.lifecycleStatus, row.lifecycleStatus)
                && matchesMulti(appliedFilters.registerStatus, row.registerStatus)
                && matchesMulti(appliedFilters.documentKind, row.documentKind)
                && matchesRange(appliedFilters.effectiveFrom, appliedFilters.effectiveTo, row.effectiveDate)
                && matchesRange(appliedFilters.reviewFrom, appliedFilters.reviewTo, row.nextReviewDueDate);
        });
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterDocumentClass, #filterCriticality, #filterLifecycleStatus, #filterRegisterStatus, #filterDocumentKind').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
        });
    };

    const readFilterControls = () => ({
        documentClass: $('#filterDocumentClass').val() || [],
        criticality: $('#filterCriticality').val() || [],
        lifecycleStatus: $('#filterLifecycleStatus').val() || [],
        registerStatus: $('#filterRegisterStatus').val() || [],
        documentKind: $('#filterDocumentKind').val() || [],
        effectiveFrom: document.getElementById('filterEffectiveFrom')?.value || '',
        effectiveTo: document.getElementById('filterEffectiveTo')?.value || '',
        reviewFrom: document.getElementById('filterReviewFrom')?.value || '',
        reviewTo: document.getElementById('filterReviewTo')?.value || ''
    });
    const clearFilterControls = () => {
        $('#filterDocumentClass, #filterCriticality, #filterLifecycleStatus, #filterRegisterStatus, #filterDocumentKind').val(null).trigger('change');
        ['filterEffectiveFrom', 'filterEffectiveTo', 'filterReviewFrom', 'filterReviewTo']
            .forEach((id) => { const el = document.getElementById(id); if (el) el.value = ''; });
    };

    const setupFilters = (api) => {
        initSelect2Filters();
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readFilterControls();
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            appliedFilters = emptyFilters();
            clearFilterControls();
            api.search('');
            api.order(baseOrder);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
        });
    };

    // ── Row actions: read + metadata edit only. No delete, no lifecycle/gate/signature action. ──
    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/DocumentManagementMasterRegister/Details/${id}`; },
        edit: ({ id }) => { if (id) window.location.href = `/DocumentManagementMasterRegister/Edit/${id}`; }
    };

    const buildRowActions = (full) => {
        const L = C.L();
        const actions = [
            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L.ViewDetails } }
        ];
        if (perms.canManage) {
            actions.push({ key: 'edit', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id } });
        }
        return window.DitenDataTable.renderActions(actions);
    };

    const initDataTable = () => {
        if (!dtTableEl) return;
        const L = C.L();

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
            ajax: {
                url: `${C.endpoint}/list`,
                type: 'GET',
                headers: C.getAuthHeaders(),
                error: (xhr) => {
                    let payload = {};
                    try { payload = JSON.parse(xhr?.responseText || '{}'); } catch (e) { payload = {}; }
                    C.handleFailure({ status: xhr?.status }, payload, 'LoadFailed');
                }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                language: { emptyTable: L.NoDocumentsFound, zeroRecords: L.NoDocumentsFound },
                order: baseOrder,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'permanentUid', name: 'permanentUid' },
                    { data: 'documentCode', name: 'documentCode' },
                    { data: 'documentTitle', name: 'documentTitle' },
                    { data: 'documentClass', name: 'documentClass' },
                    { data: 'criticality', name: 'criticality' },
                    { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                    { data: 'registerStatus', name: 'registerStatus' },
                    { data: 'isSystemAllocated', name: 'allocation' },
                    { data: 'effectiveDate', name: 'effectiveDate' },
                    { data: 'nextReviewDueDate', name: 'nextReviewDueDate' },
                    { data: 'createdAt', name: 'createdAt' },
                    { data: 'documentKind', name: 'documentKind' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${C.text(data)}</span>` },
                    { targets: 2, render: (data) => C.text(data) },
                    { targets: 3, render: (data) => C.text(data) },
                    { targets: 4, render: (data) => C.esc(C.classLabel(data)) },
                    { targets: 5, orderable: false, render: (data) => C.criticalityBadge(data) },
                    { targets: 6, orderable: false, render: (data) => C.lifecycleBadge(data) },
                    { targets: 7, orderable: false, render: (data) => C.registerBadge(data) },
                    { targets: 8, orderable: false, searchable: false, render: (data) => C.allocationBadge(data === true) },
                    { targets: 9, searchable: false, render: (data) => C.formatDate(data) },
                    { targets: 10, searchable: false, render: (data) => C.formatDate(data) },
                    { targets: 11, searchable: false, render: (data) => C.formatDate(data) },
                    { targets: 12, orderable: false, render: (data) => C.kindBadge(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => buildRowActions(full)
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    perms.canCreateControlledDocument ? (L.NewControlledDocument || L.UnifiedCreateTitle || L.DocumentMasterRegisterCreate || L.AddNew) : null,
                    { href: '/DocumentManagementMasterRegister/CreateControlledDocument' },
                    extraButtons,
                    { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/DocumentManagementMasterRegister/CreateControlledDocument';
                    });
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });

        dt.on('column-visibility.dt column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });
    };

    return {
        init: function () {
            if (!dtTableEl) return;
            if (totalColumnCount !== dtTableEl.querySelectorAll('thead th').length) {
                console.warn('[MasterRegister] Column count drift between markup and script.');
            }
            registerTableFilters();
            initDataTable();
        }
    };
})();

// ── Create / Edit form ───────────────────────────────────────────────────────
const MasterRegisterForm = (function () {
    const C = MasterRegisterCommon;

    const val = (id) => document.getElementById(id)?.value?.trim() || '';
    const checked = (id) => document.getElementById(id)?.checked === true;
    const setVal = (id, value) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = value ?? '';
        if (window.jQuery && $(el).hasClass('select2-hidden-accessible')) $(el).trigger('change');
    };
    const nullable = (value) => (value === '' ? null : value);
    const nullableInt = (value) => {
        if (value === '') return null;
        const n = Number(value);
        return Number.isFinite(n) ? Math.trunc(n) : null;
    };

    const showAlert = (message) => {
        const box = document.getElementById('formAlert');
        if (!box) return;
        box.textContent = message;
        box.classList.remove('d-none');
    };
    const hideAlert = () => document.getElementById('formAlert')?.classList.add('d-none');

    /**
     * Governed option lists (PSS-012 published values), same-origin proxied. Three behaviours matter here:
     *  • the set code comes from the markup (server-side config) — never hardcoded in this file,
     *  • if the set is missing/unpublished/empty the select is swapped for a free-text input, so a reference-data
     *    gap can never make the form unfillable,
     *  • a stored value that is not in the published list is kept as a selected option, so opening and saving an
     *    older record never silently wipes its Owner Function.
     */
    const referenceSelects = () => Array.from(document.querySelectorAll('#masterRegisterForm select[data-reference-set]'));

    // Wire shape of BusinessReferenceDataPublishedValuesModel: { setCode, versionNumber, publishedAt, items: [
    // { code, label, description, isActive, sortOrder, attributes } ] } — already deprecation-filtered and sorted
    // server-side. Mirrors the working MOD-0028 QmsBaselines designer unwrap.
    const unwrapReferenceItems = (payload) => {
        const data = payload?.data ?? payload?.Data;
        const items = data?.items ?? data?.Items ?? [];
        return (Array.isArray(items) ? items : [])
            .filter((i) => i?.isActive !== false && i?.IsActive !== false)
            .map((i) => {
                const code = i?.code ?? i?.Code;
                return { code: code, label: i?.label ?? i?.Label ?? code };
            })
            .filter((i) => i.code);
    };

    const enableFreeTextFallback = (select) => {
        const fallback = document.getElementById(`${select.id}Fallback`);
        const hint = document.getElementById(`${select.id}FallbackHint`);
        if (!fallback) return;
        // The fallback input carries the field name so the payload builder keeps reading one value.
        fallback.value = select.value || '';
        select.classList.add('d-none');
        select.removeAttribute('name');
        fallback.id = select.id;
        select.id = `${select.id}Select`;
        fallback.classList.remove('d-none');
        hint?.classList.remove('d-none');
    };

    const loadReferenceOptions = async () => {
        await Promise.all(referenceSelects().map(async (select) => {
            const setCode = select.dataset.referenceSet;
            if (!setCode) return;

            let items = [];
            try {
                const res = await fetch(`${C.endpoint}/reference-data/${encodeURIComponent(setCode)}`, {
                    credentials: 'same-origin', headers: C.getAuthHeaders()
                });
                const payload = await res.json().catch(() => ({}));
                if (res.ok && payload?.isSuccessful !== false) items = unwrapReferenceItems(payload);
            } catch (error) {
                console.error(`[MasterRegister] Reference set "${setCode}" could not be loaded.`, error);
            }

            if (!items.length) {
                enableFreeTextFallback(select);
                return;
            }

            const placeholder = select.dataset.placeholder || '';
            select.innerHTML = [`<option value="">${C.esc(placeholder)}</option>`]
                .concat(items.map((i) => `<option value="${C.esc(i.code)}">${C.esc(i.label)}</option>`))
                .join('');

            if (window.jQuery?.fn?.select2) {
                const $select = window.jQuery(select);
                if ($select.data('select2')) $select.select2('destroy');
                $select.select2({ placeholder: placeholder, allowClear: true, width: '100%' });
            }
        }));
    };

    /**
     * Governing language is a governed list too, but served by its own endpoint (not a reference set), so it gets a
     * dedicated loader mirroring loadReferenceOptions: populate + select2, or fall back to a free-text input when the
     * list is unavailable. Value is the language code (e.g. "en"), which is exactly what the backend stores.
     */
    const loadGoverningLanguages = async () => {
        const select = document.getElementById('governingLanguage');
        if (!select || select.tagName !== 'SELECT') return;

        let items = [];
        try {
            const res = await fetch(`${C.endpoint}/governed-languages`, {
                credentials: 'same-origin', headers: C.getAuthHeaders()
            });
            const payload = await res.json().catch(() => ({}));
            if (res.ok && payload?.isSuccessful !== false) items = C.unwrapList(payload);
        } catch (error) {
            console.error('[MasterRegister] Governed languages could not be loaded.', error);
        }

        if (!items.length) {
            enableFreeTextFallback(select);
            return;
        }

        const placeholder = select.dataset.placeholder || '';
        const optionOf = (item) => {
            const code = [item?.value, item?.Value, item?.code, item?.Code].find(Boolean);
            const label = [item?.name, item?.Name, item?.label, item?.Label].find(Boolean) || code;
            return code ? `<option value="${C.esc(code)}">${C.esc(label)}</option>` : '';
        };
        select.innerHTML = [`<option value="">${C.esc(placeholder)}</option>`].concat(items.map(optionOf)).join('');

        if (window.jQuery?.fn?.select2) {
            const $select = window.jQuery(select);
            if ($select.data('select2')) $select.select2('destroy');
            $select.select2({ placeholder: placeholder, allowClear: true, width: '100%' });
        }
    };

    /** Keeps a legacy free-text value selectable after the options are loaded (see loadReferenceOptions). */
    const preserveLegacyOption = (selectId, value) => {
        const el = document.getElementById(selectId);
        if (!el || el.tagName !== 'SELECT' || !value) return;
        if (Array.from(el.options).some((o) => o.value === value)) return;
        const opt = document.createElement('option');
        opt.value = value;
        opt.textContent = `${value} — ${C.t('LegacyValue')}`;
        el.appendChild(opt);
    };

    const loadLookups = async () => {
        const fill = (selectId, items, idKeys, labelFn) => {
            const el = document.getElementById(selectId);
            if (!el) return;
            items.forEach((item) => {
                const id = idKeys.map((k) => item?.[k]).find(Boolean);
                const label = labelFn(item);
                if (!id || !label) return;
                const opt = document.createElement('option');
                opt.value = String(id);
                opt.textContent = label;
                el.appendChild(opt);
            });
        };
        const fetchJson = async (path) => {
            try { return await (await fetch(`${C.endpoint}/${path}`, { credentials: 'same-origin', headers: C.getAuthHeaders() })).json(); }
            catch { return {}; }
        };
        const [entities, users] = await Promise.all([
            fetchJson('legal-entities'),
            fetchJson('users'),
            loadReferenceOptions(),
            loadGoverningLanguages()
        ]);
        fill('ownerCompanyId', C.unwrapList(entities), ['legalEntityId', 'LegalEntityId', 'id', 'Id'],
            (i) => i?.displayName || i?.DisplayName || i?.legalName || i?.LegalName || i?.commercialTitle || i?.CommercialTitle || i?.name || i?.Name);
        fill('processOwnerUserId', C.unwrapList(users), ['id', 'Id'], (i) => {
            const full = `${i?.firstName || i?.FirstName || ''} ${i?.lastName || i?.LastName || ''}`.trim();
            const email = i?.email || i?.Email || '';
            return full && email ? `${full} (${email})` : (full || email);
        });
        fill('authorUserId', C.unwrapList(users), ['id', 'Id'], (i) => {
            const full = `${i?.firstName || i?.FirstName || ''} ${i?.lastName || i?.LastName || ''}`.trim();
            const email = i?.email || i?.Email || '';
            return full && email ? `${full} (${email})` : (full || email);
        });
    };

    const buildCreatePayload = () => ({
        documentTitle: val('documentTitle'),
        documentClass: val('documentClass'),
        criticality: val('criticality'),
        documentType: nullable(val('documentType')),
        permanentUid: nullable(val('permanentUid')),
        documentCode: nullable(val('documentCode')),
        legacyCode: nullable(val('legacyCode')),
        processOwnerRole: nullable(val('processOwnerRole')),
        processOwnerUserId: nullable(val('processOwnerUserId')),
        authorUserId: nullable(val('authorUserId')),
        ownerFunction: nullable(val('ownerFunction')),
        ownerCompanyId: nullable(val('ownerCompanyId')),
        governingLanguage: nullable(val('governingLanguage')),
        reviewCycleMonths: nullableInt(val('reviewCycleMonths')),
        retentionClass: nullable(val('retentionClass')),
        isControlledDocument: checked('isControlledDocument'),
        isRecord: checked('isRecord'),
        isExternalDocument: checked('isExternalDocument'),
        isTemplate: checked('isTemplate'),
        isVariant: checked('isVariant'),
        parentDocumentUid: nullable(val('parentDocumentUid')),
        parentDocumentCode: nullable(val('parentDocumentCode')),
        sourceSystem: nullable(val('sourceSystem')),
        sourceLegacyId: nullable(val('sourceLegacyId'))
    });

    // Update is metadata-only: allocation, lifecycle status, effective date, approval evidence and release-gate
    // results are intentionally absent — the backend rejects protected-field changes (PROTECTED_FIELD_CHANGE).
    const buildUpdatePayload = () => ({
        documentTitle: val('documentTitle'),
        documentClass: val('documentClass'),
        criticality: val('criticality'),
        documentType: nullable(val('documentType')),
        legacyCode: nullable(val('legacyCode')),
        processOwnerRole: nullable(val('processOwnerRole')),
        processOwnerUserId: nullable(val('processOwnerUserId')),
        authorUserId: nullable(val('authorUserId')),
        ownerFunction: nullable(val('ownerFunction')),
        ownerCompanyId: nullable(val('ownerCompanyId')),
        governingLanguage: nullable(val('governingLanguage')),
        reviewCycleMonths: nullableInt(val('reviewCycleMonths')),
        retentionClass: nullable(val('retentionClass')),
        // ApprovedRepository* is owned by the FU16 Repository Assessment process (Repository & Copies tab) and is not
        // editable here, so it is deliberately omitted from the metadata update payload.
        parentDocumentUid: nullable(val('parentDocumentUid')),
        parentDocumentCode: nullable(val('parentDocumentCode'))
    });

    const hydrateEdit = async (entryId) => {
        const res = await fetch(`${C.endpoint}/detail/${entryId}`, { credentials: 'same-origin', headers: C.getAuthHeaders() });
        const payload = await res.json().catch(() => ({}));
        if (!res.ok || payload?.isSuccessful === false) {
            C.handleFailure(res, payload, 'LoadFailed');
            showAlert(C.describeFailure(res, payload));
            return;
        }
        const d = C.unwrap(payload);
        if (!d) return;

        setVal('documentTitle', d.documentTitle);
        setVal('permanentUid', d.permanentUid);
        setVal('documentCode', d.documentCode);
        setVal('legacyCode', d.legacyCode);
        preserveLegacyOption('governingLanguage', d.governingLanguage);
        setVal('governingLanguage', d.governingLanguage);
        setVal('documentClass', d.documentClass);
        setVal('criticality', d.criticality);
        setVal('documentType', d.documentType);
        preserveLegacyOption('ownerFunction', d.ownerFunction);
        setVal('ownerFunction', d.ownerFunction);
        setVal('ownerCompanyId', d.ownerCompanyId);
        setVal('processOwnerRole', d.processOwnerRole);
        setVal('processOwnerUserId', d.processOwnerUserId);
        setVal('authorUserId', d.authorUserId);
        if (d.authorUserId) {
            // Author identity is write-once. Keep it visible for audit context, but prevent later edits.
            $('#authorUserId').prop('disabled', true).trigger('change.select2');
        }
        setVal('retentionClass', d.retentionClass);
        setVal('reviewCycleMonths', d.reviewCycleMonths);
        setVal('parentDocumentUid', d.parentDocumentUid);
        setVal('parentDocumentCode', d.parentDocumentCode);

        // Parent reference is only meaningful for a variant. IsVariant is read-only in edit, so drive the section's
        // visibility off the loaded record instead of showing it unconditionally.
        const parentSection = document.getElementById('parentReferenceSection');
        if (parentSection) parentSection.classList.toggle('d-none', !d.isVariant);

        // Informational (disabled) kind + scope — fixed at creation, shown for context only.
        const kindInfo = document.getElementById('documentKindInfo');
        if (kindInfo) kindInfo.value = d.isRecord ? C.t('KindRecord') : C.t('KindControlledDocument');
        const scopeInfo = document.getElementById('documentScopeInfo');
        if (scopeInfo) scopeInfo.value = d.documentScope ? C.t(`DocumentScope${d.documentScope}`) : '';
        // A record has no periodic-review lifecycle — hide the review-cycle field for it.
        document.getElementById('reviewCycleMonthsField')?.classList.toggle('d-none', d.isRecord === true);

        const flags = document.getElementById('flagsReadOnlyList');
        if (flags) {
            flags.innerHTML = [
                ['IsControlledDocument', d.isControlledDocument],
                ['IsRecord', d.isRecord],
                ['IsExternalDocument', d.isExternalDocument],
                ['IsTemplate', d.isTemplate],
                ['IsVariant', d.isVariant]
            ].map(([key, value]) =>
                `<dt class="col-sm-6 fw-normal text-muted">${C.esc(C.t(key))}</dt><dd class="col-sm-6">${C.boolBadge(value === true)}</dd>`
            ).join('');
        }
    };

    const submit = async (form) => {
        hideAlert();
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            window.showToast?.(C.t('ValidationFailed'), 'error');
            return;
        }

        const isEdit = form.dataset.formMode === 'edit';
        const entryId = form.dataset.entryId || '';
        const payload = isEdit ? buildUpdatePayload() : buildCreatePayload();
        const url = isEdit ? `${C.endpoint}/update/${entryId}` : `${C.endpoint}/create`;

        const body = new FormData();
        body.append('__RequestVerificationToken', C.token());
        body.append('payloadJson', JSON.stringify(payload));

        try {
            const res = await fetch(url, { method: 'POST', credentials: 'same-origin', body });
            const responsePayload = await res.json().catch(() => ({}));
            if (!res.ok || responsePayload?.isSuccessful === false) {
                C.handleFailure(res, responsePayload, 'SaveFailed');
                showAlert(C.describeFailure(res, responsePayload));
                return;
            }
            window.showToast?.(C.t('SaveSucceeded'), 'success');
            const saved = C.unwrap(responsePayload);
            const savedId = saved?.id || saved?.Id || entryId;
            window.location.href = savedId
                ? `/DocumentManagementMasterRegister/Details/${savedId}`
                : '/DocumentManagementMasterRegister';
        } catch (error) {
            console.error('[MasterRegister] Save failed.', error);
            window.showToast?.(C.t('SaveFailed'), 'error');
            showAlert(C.t('SaveFailed'));
        }
    };

    return {
        init: function () {
            const form = document.getElementById('masterRegisterForm');
            if (!form) return;

            void loadLookups().then(() => {
                if (form.dataset.formMode === 'edit' && form.dataset.entryId) {
                    void hydrateEdit(form.dataset.entryId);
                }
            });

            // Parent reference is only meaningful for a variant (backend: VARIANT_PARENT_MISSING).
            const isVariant = document.getElementById('isVariant');
            const parentSection = document.getElementById('parentReferenceSection');
            if (isVariant && parentSection) {
                isVariant.addEventListener('change', () => parentSection.classList.toggle('d-none', !isVariant.checked));
            }

            form.addEventListener('submit', (event) => {
                event.preventDefault();
                void submit(form);
            });
        }
    };
})();

// ── Details (General tab only) ───────────────────────────────────────────────
const MasterRegisterDetails = (function () {
    const C = MasterRegisterCommon;
    const perms = window.MasterRegisterDetailPerms || {};
    let currentDetail = null;
    const companyNames = new Map();
    const userNames = new Map();

    const field = (label, valueHtml, icon, columnClass = 'col-12', valueAttributes = '') => `
        <div class="${columnClass}">
            <div class="backbone-preview-field">
                <i class="bx ${C.esc(icon)}"></i>
                <div class="min-w-0">
                    <div class="backbone-preview-label">${C.esc(label)}</div>
                    <div class="backbone-preview-value mt-1 text-break" ${valueAttributes}>${valueHtml}</div>
                </div>
            </div>
        </div>`;
    const fill = (id, rows) => {
        const el = document.getElementById(id);
        if (el) el.innerHTML = rows.join('');
    };

    const showRelationshipAlert = (message) => {
        const box = document.getElementById('controlledDocumentAlert');
        if (!box) return;
        box.textContent = message;
        box.classList.remove('d-none');
    };

    const normalizedId = (value) => String(value || '').toLowerCase();
    const resolvedName = (map, id) => {
        const label = map.get(normalizedId(id));
        return label ? C.text(label) : C.text(id);
    };

    const loadDisplayLookups = async () => {
        const load = async (path) => {
            try {
                const res = await fetch(`${C.endpoint}/${path}`, {
                    credentials: 'same-origin',
                    headers: C.getAuthHeaders()
                });
                if (!res.ok) return [];
                return C.unwrapList(await res.json().catch(() => ({})));
            } catch {
                return [];
            }
        };

        const [companies, users] = await Promise.all([load('legal-entities'), load('users')]);
        companies.forEach((item) => {
            const id = item?.legalEntityId || item?.LegalEntityId || item?.id || item?.Id;
            const label = item?.displayName || item?.DisplayName || item?.legalName || item?.LegalName
                || item?.commercialTitle || item?.CommercialTitle || item?.name || item?.Name;
            if (id && label) companyNames.set(normalizedId(id), label);
        });
        users.forEach((item) => {
            const id = item?.id || item?.Id;
            const firstName = item?.firstName || item?.FirstName || '';
            const lastName = item?.lastName || item?.LastName || '';
            const fullName = `${firstName} ${lastName}`.trim();
            const email = item?.email || item?.Email || '';
            const label = fullName && email ? `${fullName} (${email})` : (fullName || email);
            if (id && label) userNames.set(normalizedId(id), label);
        });
    };

    const renderRelationship = async (d) => {
        const host = document.getElementById('controlledDocumentRelationship');
        const actions = document.getElementById('controlledDocumentActions');
        if (!host || !actions) return;

        actions.innerHTML = '';
        if (!d.controlledDocumentId) {
            host.innerHTML = `
                <div class="d-flex align-items-start gap-3">
                    <span class="avatar-initial rounded bg-label-secondary p-3"><i class="icon-base bx bx-unlink"></i></span>
                    <div>
                        <div class="fw-medium">${C.esc(C.t('ControlledDocumentNotLinked'))}</div>
                        <div class="text-muted small">${C.esc(C.t('ControlledDocumentNotLinkedHint'))}</div>
                    </div>
                </div>`;
            if (perms.canLinkControlledDocument && perms.canViewControlledDocument) {
                actions.innerHTML = `<button type="button" class="btn btn-primary" id="btnOpenControlledDocumentLink">
                    <i class="icon-base bx bx-link me-1"></i>${C.esc(C.t('LinkControlledDocument'))}
                </button>`;
                document.getElementById('btnOpenControlledDocumentLink')?.addEventListener('click', () => void openLinkModal());
            }
            return;
        }

        if (!perms.canViewControlledDocument) {
            host.innerHTML = `<div class="text-muted">${C.esc(C.t('LinkedControlledDocument'))}: ${C.esc(d.controlledDocumentId)}</div>`;
            return;
        }

        try {
            const res = await fetch(`${C.endpoint}/${d.id}/controlled-documents/${d.controlledDocumentId}`, {
                credentials: 'same-origin', headers: C.getAuthHeaders()
            });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                host.innerHTML = `<div class="text-muted">${C.esc(C.t('LinkedControlledDocument'))}: ${C.esc(d.controlledDocumentId)}</div>`;
                showRelationshipAlert(C.describeFailure(res, payload));
                return;
            }
            const doc = C.unwrap(payload);
            const currentVersion = doc?.currentVersionNumber ?? doc?.CurrentVersionNumber;
            const currentVersionHost = document.querySelector('[data-current-version-value]');
            if (currentVersionHost && currentVersion !== null && currentVersion !== undefined) {
                currentVersionHost.textContent = String(currentVersion);
            }
            host.innerHTML = `
                <div class="row g-4">
                    ${field(C.t('DocumentTitle'), C.text(doc?.title), 'bx-file', 'col-12 col-md-6')}
                    ${field(C.t('DocumentType'), C.text(doc?.documentType), 'bx-category', 'col-12 col-md-6')}
                    ${field(C.t('ControlledDocumentVersion'), C.text(doc?.currentVersionNumber), 'bx-git-branch', 'col-12 col-md-6')}
                    ${field(C.t('ControlledDocumentStatus'), C.text(doc?.status), 'bx-check-shield', 'col-12 col-md-6')}
                    ${field(C.t('ControlledDocumentFolder'), C.text(doc?.collectionPath), 'bx-folder-open', 'col-12 col-md-6')}
                </div>`;
            actions.innerHTML = `
                <a class="btn btn-sm btn-primary" href="/DocumentManagementControlledDocuments/Details/${encodeURIComponent(d.controlledDocumentId)}">
                    <i class="icon-base bx bx-link-external me-1"></i>${C.esc(C.t('OpenControlledDocument'))}
                </a>
                <a class="btn btn-sm btn-label-primary" href="/DocumentManagementControlledDocuments/VersionHistory/${encodeURIComponent(d.controlledDocumentId)}">
                    <i class="icon-base bx bx-history me-1"></i>${C.esc(C.t('ViewVersionHistory'))}
                </a>`;
        } catch (error) {
            console.error('[MasterRegister] Controlled document load failed.', error);
            showRelationshipAlert(C.t('ControlledDocumentLoadFailed'));
        }
    };

    const openLinkModal = async () => {
        const modalEl = document.getElementById('linkControlledDocumentModal');
        const select = document.getElementById('controlledDocumentSelect');
        const reason = document.getElementById('controlledDocumentLinkReason');
        const confirm = document.getElementById('btnConfirmControlledDocumentLink');
        const alert = document.getElementById('linkControlledDocumentAlert');
        if (!modalEl || !select || !reason || !confirm || !currentDetail) return;

        if (alert) alert.classList.add('d-none');
        select.innerHTML = '<option value=""></option>';
        reason.value = '';
        confirm.disabled = true;
        bootstrap.Modal.getOrCreateInstance(modalEl).show();

        try {
            const res = await fetch(`${C.endpoint}/${currentDetail.id}/controlled-documents`, {
                credentials: 'same-origin', headers: C.getAuthHeaders()
            });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                if (alert) { alert.textContent = C.describeFailure(res, payload); alert.classList.remove('d-none'); }
                return;
            }
            const docs = C.unwrapList(payload).filter((doc) =>
                String(doc.documentScope || '').toUpperCase() === String(currentDetail.documentScope || '').toUpperCase()
                && String(doc.scopeOwnerId || '').toLowerCase() === String(currentDetail.scopeOwnerId || '').toLowerCase()
                && String(doc.collectionInstanceId || '').toLowerCase() === String(currentDetail.collectionInstanceId || '').toLowerCase()
                && String(doc.folderId || '').toLowerCase() === String(currentDetail.folderId || '').toLowerCase());
            docs.forEach((doc) => {
                const option = document.createElement('option');
                option.value = doc.id || doc.Id;
                option.textContent = `${doc.title || doc.Title || option.value} — ${doc.documentKey || doc.DocumentKey || ''}`;
                select.appendChild(option);
            });
            if (!docs.length && alert) {
                alert.textContent = C.t('ControlledDocumentListEmpty');
                alert.classList.remove('d-none');
            }
            if (window.jQuery?.fn?.select2) {
                const $select = window.jQuery(select);
                if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
                $select.select2({
                    dropdownParent: window.jQuery(modalEl),
                    width: '100%',
                    allowClear: true,
                    placeholder: C.t('SelectControlledDocument')
                });
            }
        } catch (error) {
            console.error('[MasterRegister] Controlled document list failed.', error);
            if (alert) { alert.textContent = C.t('ControlledDocumentLoadFailed'); alert.classList.remove('d-none'); }
        }
    };

    const bindRelationshipActions = () => {
        const select = document.getElementById('controlledDocumentSelect');
        const reason = document.getElementById('controlledDocumentLinkReason');
        const confirm = document.getElementById('btnConfirmControlledDocumentLink');
        if (!select || !reason || !confirm) return;
        const update = () => { confirm.disabled = !select.value || !reason.value.trim(); };
        select.addEventListener('change', update);
        reason.addEventListener('input', update);
        if (window.jQuery) window.jQuery(select).on('select2:select select2:clear', update);
        confirm.addEventListener('click', async () => {
            if (!select.value || !reason.value.trim() || !currentDetail) return;
            const form = new FormData();
            form.append('__RequestVerificationToken', C.token());
            form.append('payloadJson', JSON.stringify({
                controlledDocumentId: select.value,
                reconciliationReason: reason.value.trim()
            }));
            confirm.disabled = true;
            const res = await fetch(`${C.endpoint}/${currentDetail.id}/controlled-document/link`, {
                method: 'POST', credentials: 'same-origin', body: form
            });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                const alert = document.getElementById('linkControlledDocumentAlert');
                if (alert) { alert.textContent = C.describeFailure(res, payload); alert.classList.remove('d-none'); }
                confirm.disabled = false;
                return;
            }
            currentDetail = C.unwrap(payload) || currentDetail;
            bootstrap.Modal.getOrCreateInstance(document.getElementById('linkControlledDocumentModal')).hide();
            window.showToast?.(C.t('ControlledDocumentLinkSucceeded'), 'success');
            await renderRelationship(currentDetail);
        });
    };

    const render = (d) => {
        const t = C.t;
        document.getElementById('detailTitle').textContent = d.documentTitle || t('DocumentMasterRegisterDetails');
        document.getElementById('detailSubtitle').textContent =
            [d.permanentUid, d.documentCode].filter(Boolean).join(' • ');

        fill('detailIdentificationList', [
            field(t('PermanentUid'), C.text(d.permanentUid), 'bx-fingerprint', 'col-12 col-md-6'),
            field(t('DocumentCode'), C.text(d.documentCode), 'bx-barcode', 'col-12 col-md-6'),
            field(t('LegacyCode'), C.text(d.legacyCode), 'bx-history', 'col-12 col-md-6'),
            field(t('Allocation'), C.allocationBadge(d.isSystemAllocated === true), 'bx-cog', 'col-12 col-md-6'),
            field(t('GoverningLanguage'), C.text(d.governingLanguage), 'bx-globe', 'col-12 col-md-6'),
            field(t('CurrentVersionLabel'), C.text(d.currentVersionLabel ?? d.currentVersionNumber), 'bx-git-branch', 'col-12 col-md-6', 'data-current-version-value')
        ]);

        fill('detailClassificationList', [
            field(t('DocumentClass'), C.esc(C.classLabel(d.documentClass)), 'bx-category', 'col-12 col-md-6'),
            field(t('DocumentType'), C.esc(C.typeLabel(d.documentType)), 'bx-file', 'col-12 col-md-6'),
            field(t('Criticality'), C.criticalityBadge(d.criticality), 'bx-error-circle', 'col-12 col-md-6'),
            field(t('LifecycleStatus'), C.lifecycleBadge(d.lifecycleStatus), 'bx-refresh', 'col-12 col-md-6'),
            field(t('RegisterStatus'), C.registerBadge(d.registerStatus), 'bx-check-shield', 'col-12 col-md-6')
        ]);

        fill('detailOwnershipList', [
            field(t('OwnerFunction'), C.text(d.ownerFunction), 'bx-buildings'),
            field(t('OwnerCompany'), resolvedName(companyNames, d.ownerCompanyId), 'bx-briefcase'),
            field(t('ProcessOwnerRole'), C.text(d.processOwnerRole), 'bx-id-card'),
            field(t('ProcessOwnerUser'), resolvedName(userNames, d.processOwnerUserId), 'bx-user')
        ]);

        fill('detailFlagsList', [
            field(t('IsControlledDocument'), C.boolBadge(d.isControlledDocument === true), 'bx-lock-alt'),
            field(t('IsRecord'), C.boolBadge(d.isRecord === true), 'bx-archive'),
            field(t('IsExternalDocument'), C.boolBadge(d.isExternalDocument === true), 'bx-link-external'),
            field(t('IsTemplate'), C.boolBadge(d.isTemplate === true), 'bx-copy'),
            field(t('IsVariant'), C.boolBadge(d.isVariant === true), 'bx-git-branch'),
            field(t('ParentDocument'), C.text([d.parentDocumentUid, d.parentDocumentCode].filter(Boolean).join(' / ')), 'bx-sitemap')
        ]);

        fill('detailGovernanceList', [
            field(t('EffectiveDate'), C.esc(C.formatDateTime(d.effectiveDate)), 'bx-calendar-check'),
            field(t('ReviewCycleMonths'), C.text(d.reviewCycleMonths), 'bx-calendar-event'),
            field(t('NextReviewDueDate'), C.esc(C.formatDateTime(d.nextReviewDueDate)), 'bx-calendar-exclamation'),
            field(t('LastPeriodicReviewDate'), C.esc(C.formatDateTime(d.lastPeriodicReviewDate)), 'bx-history'),
            field(t('RetentionClass'), C.text(d.retentionClass), 'bx-archive'),
            field(t('ApprovalEvidenceStatus'), C.text(d.approvalEvidenceStatus), 'bx-check-double'),
            field(t('LastReleaseGateEvaluationStatus'), C.text(d.lastReleaseGateEvaluationStatus), 'bx-shield-quarter')
        ]);

        fill('detailProvenanceList', [
            field(t('SourceSystem'), C.text(d.sourceSystem), 'bx-data'),
            field(t('SourceLegacyId'), C.text(d.sourceLegacyId), 'bx-hash'),
            field(t('LinkedControlledDocument'), C.text(d.controlledDocumentId), 'bx-link'),
            field(t('ApprovedRepository'), C.text([d.approvedRepositoryName, d.approvedRepositoryPath].filter(Boolean).join(' — ')), 'bx-folder-open'),
            field(t('CreatedAt'), `${C.esc(C.formatDateTime(d.createdAt))} — ${C.text(d.createdBy)}`, 'bx-plus-circle'),
            field(t('UpdatedAt'), `${C.esc(C.formatDateTime(d.updatedAt))} — ${C.text(d.updatedBy)}`, 'bx-edit-alt')
        ]);
        void renderRelationship(d);
    };

    return {
        init: async function () {
            const host = document.querySelector('.master-register-details');
            if (!host) return;
            const id = host.dataset.masterRegisterId;
            if (!id) return;
            bindRelationshipActions();

            try {
                await loadDisplayLookups();
                const res = await fetch(`${C.endpoint}/detail/${id}`, { credentials: 'same-origin', headers: C.getAuthHeaders() });
                const payload = await res.json().catch(() => ({}));
                if (!res.ok || payload?.isSuccessful === false) {
                    C.handleFailure(res, payload, 'LoadFailed');
                    const box = document.getElementById('detailAlert');
                    if (box) { box.textContent = C.describeFailure(res, payload); box.classList.remove('d-none'); }
                    return;
                }
                const data = C.unwrap(payload);
                if (data) {
                    currentDetail = data;
                    render(data);
                }
            } catch (error) {
                console.error('[MasterRegister] Detail load failed.', error);
                window.showToast?.(C.t('LoadFailed'), 'error');
            }
        }
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    MasterRegisterList.init();
    MasterRegisterForm.init();
    void MasterRegisterDetails.init();
});
