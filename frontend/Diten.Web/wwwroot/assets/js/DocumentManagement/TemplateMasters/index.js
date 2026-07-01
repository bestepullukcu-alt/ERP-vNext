/**
 * MOD-0029-FU02 — Corporate Template Masters DataTables Index Script.
 * Golden Reference Compact pattern (same-origin MVC proxy profile). Large-field reference:
 * create/edit use full MVC pages; list uses the shared DtDefaults toolbar + bulk soft-delete.
 */
'use strict';

const TemplateMastersList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-templatemasters');
    // Verifier profile marker: window.API.documentManagement is not used here because this tenant shell page uses
    // same-origin MVC proxy endpoints; HttpOnly cookies stay server-side.
    const endpoint = '/DocumentManagement/TemplateMasters/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'TemplateMasters' };
    const perms = window.TemplateMastersPerms || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const totalColumnCount = 11;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [], classification: [], variantPolicy: [], canonicalId: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const t = (key) => L[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], classification: [], variantPolicy: [], canonicalId: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            classification: normalizeArray(source.classification),
            variantPolicy: normalizeArray(source.variantPolicy),
            canonicalId: normalizeString(source.canonicalId)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(normalizeString(actual).toUpperCase());
    };
    const matchesContains = (needle, actual) => {
        const n = normalizeString(needle).toLowerCase();
        return !n || normalizeString(actual).toLowerCase().includes(n);
    };

    // ── Personalization (Save View) state plumbing ──
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const n = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((ci, pos) => {
                if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci];
                else if (typeof colVis[pos] === 'boolean') n[ci] = colVis[pos];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((ci) => { if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci]; });
        }
        return Object.keys(n).length ? n : null;
    };
    const captureColVis = (api) => {
        const r = {};
        saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } });
        return r;
    };
    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };
    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});
    const applyColOrder = (api, order) => {
        const n = normalizeColOrder(order);
        if (n && typeof api?.colReorder?.order === 'function') api.colReorder.order(n, true);
    };
    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };

    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };
    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (v) => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(v.filters[key]);
            return acc;
        }, {}),
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });

    const getSavedViewId = (sv) => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = (sv) => sv?.viewName || sv?.ViewName || '';
    const isSavedViewDefault = (sv) => sv?.isDefault === true || sv?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;
    const getSavedViewDef = (sv) => {
        const raw = sv?.viewDefinition ?? sv?.ViewDefinition ?? {};
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } }
        return raw || {};
    };
    const mapSavedViewToState = (sv) => {
        const d = getSavedViewDef(sv);
        return {
            filters: normalizeFilters(d.filters || d),
            search: normalizeString(d.search),
            colVis: normalizeColVis(d.colVis),
            columnOrder: normalizeColOrder(d.columnOrder),
            order: Array.isArray(d.order) ? d.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
            order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[TemplateMasters SaveView] Failed to load saved views.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object'
            ? savedRecord
            : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    // ── Inline filter wiring ──
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
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMultiFilter(appliedFilters.status, row.status)
                && matchesMultiFilter(appliedFilters.classification, row.classification)
                && matchesMultiFilter(appliedFilters.variantPolicy, row.variantPolicy)
                && matchesContains(appliedFilters.canonicalId, row.canonicalId);
        });
    };

    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((i) => normalizeString(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    };
    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterStatus, #filterClassification, #filterVariantPolicy').each(function () {
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
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(() => syncMultiSelectSummary($s));
        });
    };

    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterClassification').val(normalizeArray(values.classification)).trigger('change');
        $('#filterVariantPolicy').val(normalizeArray(values.variantPolicy)).trigger('change');
        const canonical = document.getElementById('filterCanonicalId');
        if (canonical) canonical.value = values.canonicalId || '';
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.classification, appliedFilters.variantPolicy, appliedFilters.canonicalId].filter(hasFilterValue).length;

    const applySavedTableState = (api, view) => {
        if (!api || !view) return;
        const s = normalizeViewState(view);
        appliedFilters = s.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(api, s.columnOrder);
        applyColVis(api, s.colVis);
        api.search(s.search);
        syncSearchInput(api, s.search);
        api.order(s.order);
        try { api.columns.adjust(); } catch (e) { }
        try { api.responsive?.recalc?.(); } catch (e) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const setupFilters = (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                classification: $('#filterClassification').val() || [],
                variantPolicy: $('#filterVariantPolicy').val() || [],
                canonicalId: document.getElementById('filterCanonicalId')?.value || ''
            };
            api.draw();
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    // ── Friendly-label resolution maps (ids → human names), loaded once before the table first renders ──
    const legalEntityMap = new Map();        // companyId → legal entity name
    const userMap = new Map();               // userId → "Name Surname (email)"
    const definitionMap = new Map();         // collectionDefinitionId → { baselineName, definitionName }
    const definitionByCanonical = new Map(); // canonicalId → { baselineName, definitionName }
    const pick = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
    const unwrapLookupList = (json) => {
        const d = json?.data ?? json?.Data ?? json;
        if (Array.isArray(d)) return d;
        for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(d?.[k])) return d[k];
        return [];
    };
    const fetchLookupJson = async (path) => {
        try { return await (await fetch(`${endpoint}/${path}`, { credentials: 'same-origin', headers: getAuthHeaders() })).json(); }
        catch { return {}; }
    };
    const loadResolutionMaps = async () => {
        const buildLegal = async () => {
            unwrapLookupList(await fetchLookupJson('legal-entities')).forEach((it) => {
                const idv = pick(it, 'legalEntityId', 'LegalEntityId') || pick(it, 'id', 'Id');
                const name = pick(it, 'displayName', 'DisplayName') || pick(it, 'legalName', 'LegalName') || pick(it, 'commercialTitle', 'CommercialTitle') || pick(it, 'name', 'Name');
                if (idv && name) legalEntityMap.set(String(idv), name);
            });
        };
        const buildUsers = async () => {
            unwrapLookupList(await fetchLookupJson('users')).forEach((it) => {
                const idv = pick(it, 'id', 'Id');
                if (!idv) return;
                const full = `${pick(it, 'firstName', 'FirstName') || ''} ${pick(it, 'lastName', 'LastName') || ''}`.trim();
                const email = pick(it, 'email', 'Email') || '';
                const label = full && email ? `${full} (${email})` : (full || email);
                if (label) userMap.set(String(idv), label);
            });
        };
        const buildStructure = async () => {
            const baselines = unwrapLookupList(await fetchLookupJson('qms-baselines'));
            await Promise.all(baselines.map(async (b) => {
                const bid = pick(b, 'id', 'Id');
                if (!bid) return;
                const baselineName = pick(b, 'name', 'Name') || pick(b, 'baselineVersion', 'BaselineVersion') || String(bid);
                unwrapLookupList(await fetchLookupJson(`qms-baselines/${encodeURIComponent(bid)}/definitions`)).forEach((d) => {
                    const did = pick(d, 'id', 'Id');
                    const canonical = pick(d, 'canonicalId', 'CanonicalId');
                    const definitionName = pick(d, 'displayName', 'DisplayName') || pick(d, 'fullPath', 'FullPath') || pick(d, 'name', 'Name') || canonical;
                    const entry = { baselineName, definitionName };
                    if (did) definitionMap.set(String(did), entry);
                    if (canonical) definitionByCanonical.set(String(canonical), entry);
                });
            }));
        };
        try { await Promise.all([buildLegal(), buildUsers(), buildStructure()]); }
        catch (e) { console.error('[TemplateMasters] Resolution map load failed.', e); }
    };

    // ── Renderers ──
    const badge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = { DRAFT: 'warning', PUBLISHED: 'success', DEPRECATED: 'danger', ARCHIVED: 'secondary' };
        const label = t(`Status${s.charAt(0)}${s.slice(1).toLowerCase()}`) || status || t('NotAvailable');
        return `<span class="badge bg-label-${map[s] || 'secondary'}">${label}</span>`;
    };
    const policyLabel = (value) => {
        const s = String(value || '').toUpperCase();
        if (s === 'LOCKED') return t('VariantLocked');
        if (s === 'ALLOWED') return t('VariantAllowed');
        return value || t('NotAvailable');
    };
    const ownerText = (row) => {
        const company = row.ownerCompanyId ? legalEntityMap.get(String(row.ownerCompanyId)) : null;
        const user = row.ownerUserId ? userMap.get(String(row.ownerUserId)) : null;
        return company || user || row.ownerCompanyId || row.ownerUserId || t('NotAvailable');
    };
    const bindingText = (row) => {
        const def = (row.collectionDefinitionId && definitionMap.get(String(row.collectionDefinitionId)))
            || (row.canonicalId && definitionByCanonical.get(String(row.canonicalId)));
        const baseline = def?.baselineName || row.collectionDefinitionId;
        const definition = def?.definitionName || row.canonicalId;
        const parts = [];
        if (baseline) parts.push(baseline);
        if (definition) parts.push(definition);
        return parts.length ? parts.join(' / ') : t('NotAvailable');
    };

    // ── Bulk soft-delete (same-origin proxy + anti-forgery) ──
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            delete: async ({ ids }) => {
                if (!ids.length) return;
                const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        const form = new FormData();
                        form.append('__RequestVerificationToken', token());
                        form.append('idsJson', JSON.stringify(ids));
                        const res = await fetch(`${endpoint}/bulk`, { method: 'POST', body: form });
                        const payload = await res.json().catch(() => ({}));
                        if (!res.ok || payload?.isSuccessful === false) { if (window.DitenUnauthorized?.handle(res, payload)) return; throw new Error('Bulk delete failed.'); }
                        reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L.Delete });
            }
        }
    };

    const deleteSingle = (row) => {
        if (!row?.id) return;
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                const form = new FormData();
                form.append('__RequestVerificationToken', token());
                const res = await fetch(`${endpoint}/delete/${row.id}`, { method: 'POST', body: form });
                const payload = await res.json().catch(() => ({}));
                if (!res.ok || payload?.isSuccessful === false) { if (window.DitenUnauthorized?.handle(res, payload)) return; throw new Error('Delete failed.'); }
                reloadWithSuccessToast('RecordDeleted');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, { entityName: row.masterCode || row.templateName, type: 'danger', confirmButtonText: L.Delete });
    };

    const openImpact = async (id) => {
        const body = document.getElementById('impactModalBody');
        // Open immediately with a spinner so the click feels responsive, then fill once data arrives.
        if (body) body.innerHTML = `<div class="text-center py-5"><span class="spinner-border text-primary" role="status" aria-hidden="true"></span></div>`;
        window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('impactModal')).show();

        const res = await fetch(`${endpoint}/impact/${id}`, { headers: getAuthHeaders() });
        const payload = await res.json().catch(() => ({}));
        const data = payload?.data ?? payload?.Data;
        if (!data) {
            if (body) body.innerHTML = `<div class="alert alert-danger mb-0">${t('ErrorOccurred')}</div>`;
            return;
        }

        const num = (v) => Number(v ?? 0).toLocaleString();
        const stat = (icon, color, value, label) => `
            <div class="col-sm-4">
                <div class="d-flex flex-column align-items-center text-center gap-2 p-4 rounded border h-100">
                    <div class="avatar avatar-md">
                        <span class="avatar-initial rounded-circle bg-label-${color}"><i class="bx ${icon} fs-4"></i></span>
                    </div>
                    <h3 class="mb-0 fw-semibold">${num(value)}</h3>
                    <span class="text-muted small">${label}</span>
                </div>
            </div>`;

        if (body) body.innerHTML = `
            <div class="row g-4">
                ${stat('bx-link-alt', 'primary', data.usageCount, t('UsageCount'))}
                ${stat('bx-git-branch', 'info', data.variantCount, t('VariantCount'))}
                ${stat('bx-folder', 'success', data.folderAttachedTemplateCount, t('FolderAttachedTemplateCount'))}
            </div>
            <div class="alert alert-warning d-flex align-items-center mt-4 mb-0" role="alert">
                <i class="bx bx-info-circle me-2"></i>
                <span>${t('AdoptionImpactNote')}</span>
            </div>`;
    };

    const deprecate = (id) => {
        if (!id) return;
        window.showConfirm?.(t('Deprecate'), async (reason) => {
            try {
                const form = new FormData();
                form.append('__RequestVerificationToken', token());
                form.append('deprecationReason', reason || '');
                const res = await fetch(`${endpoint}/deprecate/${id}`, { method: 'POST', body: form });
                const payload = await res.json().catch(() => ({}));
                if (!res.ok || payload?.isSuccessful === false) { if (window.DitenUnauthorized?.handle(res, payload)) return; throw new Error('Deprecate failed.'); }
                reloadWithSuccessToast('Deprecated');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, {
            type: 'warning',
            subtext: t('DeprecateConfirm'),
            confirmButtonText: t('Deprecate'),
            showInput: true,
            inputLabel: t('DeprecateReason')
        });
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/DocumentManagementTemplateMasters/Details/${id}`; },
        publish: ({ id }) => {
            if (!id) return;
            document.getElementById('publishMasterId').value = id;
            window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('publishVersionModal')).show();
        },
        impact: ({ id }) => { if (id) void openImpact(id); },
        deprecate: ({ id }) => { if (id) void deprecate(id); },
        delete: ({ row }) => deleteSingle(row)
    };

    const buildRowActions = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const actions = [
            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L.ViewDetails } }
        ];
        if (perms.canPublish && full.canPublish === true) actions.push({ key: 'publish', icon: 'bx bx-upload', text: L.PublishVersion, attrs: { 'data-id': full.id } });
        if (perms.canImpact && full.canImpact !== false) actions.push({ key: 'impact', icon: 'bx bx-line-chart', text: L.AdoptionImpact, attrs: { 'data-id': full.id } });
        if (perms.canDeprecate && full.canDeprecate === true) actions.push({ key: 'deprecate', icon: 'bx bx-archive', text: L.Deprecate, attrs: { 'data-id': full.id } });
        if (perms.canDelete && full.canDelete === true) actions.push({ key: 'delete', className: 'text-danger', icon: 'bx bx-trash', text: L.Delete, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        return window.DitenDataTable.renderActions(actions);
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();
        // Resolve id→name maps before the table first renders so Binding/Owner columns show friendly labels.
        await loadResolutionMaps();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[TemplateMasters SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        // DitenDataTable.createCrudTable wraps the DataTables v2 constructor and shared defaults:
        //   new DataTable(...)
        //   window.DtDefaults.create(...)
        // Quick View uses event delegation through DitenDataTable, equivalent to closest('.js-quick-view').
        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: {
                url: `${endpoint}/list`,
                type: 'GET',
                headers: getAuthHeaders()
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'masterCode', name: 'masterCode' },
                    { data: 'templateName', name: 'templateName' },
                    { data: 'currentMasterVersion', name: 'currentMasterVersion' },
                    { data: 'status', name: 'status' },
                    { data: 'classification', name: 'classification' },
                    { data: 'canonicalId', name: 'binding' },
                    { data: 'variantPolicy', name: 'variantPolicy' },
                    { data: 'ownerCompanyId', name: 'owner' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 5, render: (data) => badge(data) },
                    { targets: 7, render: (data, type, full) => bindingText(full) },
                    { targets: 8, render: (data) => policyLabel(data) },
                    { targets: 9, render: (data, type, full) => ownerText(full) },
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
                    perms.canCreate ? (L.CreateMaster || L.AddNew) : null,
                    { href: '/DocumentManagementTemplateMasters/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7, 8, 9], colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/DocumentManagementTemplateMasters/Create';
                    });
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('search.dt order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        document.getElementById('publishVersionForm')?.addEventListener('submit', async (event) => {
            event.preventDefault();
            const id = document.getElementById('publishMasterId').value;
            const form = new FormData(event.currentTarget);
            form.set('allowUnchanged', document.getElementById('publishAllowUnchanged').checked ? 'true' : 'false');
            const res = await fetch(`${endpoint}/publish-version/${id}`, { method: 'POST', body: form });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) { if (window.DitenUnauthorized?.handle(res, payload)) return; window.showToast?.(L.ErrorOccurred, 'error'); return; }
            window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('publishVersionModal')).hide();
            event.currentTarget.reset();
            reloadWithSuccessToast('Published');
        });
    };

    return {
        init: function () {
            if (!dtTableEl) return;
            registerTableFilters();
            void initDataTable();
        }
    };
})();

// ── Create page ──
const TemplateMasterCreate = (function () {
    const init = () => {
        const form = document.getElementById('templateMasterCreateForm');
        if (!form) return;
        const L = window.L10n || {};
        const t = (key) => L[key] || key;
        const endpoint = '/DocumentManagement/TemplateMasters/api';
        const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const companySelect = document.getElementById('ownerCompanyId');
        const baselineSelect = document.getElementById('baselineReleaseId');
        const collectionDefinitionInput = document.getElementById('collectionDefinitionId');
        const canonicalSelect = document.getElementById('canonicalId');
        const userSelect = document.getElementById('ownerUserId');
        const definitionByCanonicalId = new Map();

        const getAuthHeaders = () =>
            window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };

        const unwrapList = (payload) => {
            const data = payload?.data ?? payload?.Data ?? payload;
            if (Array.isArray(data)) return data;
            if (Array.isArray(data?.items)) return data.items;
            if (Array.isArray(data?.Items)) return data.Items;
            if (Array.isArray(data?.results)) return data.results;
            if (Array.isArray(data?.Results)) return data.Results;
            return [];
        };

        const get = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
        const upper = (value) => String(value || '').toUpperCase();
        const optionId = (item) =>
            get(item, 'legalEntityId', 'LegalEntityId') ||
            get(item, 'baselineReleaseId', 'BaselineReleaseId') ||
            get(item, 'id', 'Id');
        const optionText = (item, fallback) =>
            get(item, 'displayName', 'DisplayName') ||
            get(item, 'legalName', 'LegalName') ||
            get(item, 'commercialTitle', 'CommercialTitle') ||
            get(item, 'baselineVersion', 'BaselineVersion') ||
            get(item, 'fullPath', 'FullPath') ||
            get(item, 'name', 'Name') ||
            fallback;
        const userText = (item, fallback) => {
            const firstName = get(item, 'firstName', 'FirstName') || '';
            const lastName = get(item, 'lastName', 'LastName') || '';
            const email = get(item, 'email', 'Email') || '';
            const fullName = `${firstName} ${lastName}`.trim();
            return fullName && email ? `${fullName} (${email})` : (fullName || email || fallback);
        };
        const baselineText = (item, fallback) => {
            const releaseKey = get(item, 'baselineReleaseId', 'BaselineReleaseId');
            const version = get(item, 'baselineVersion', 'BaselineVersion');
            const name = get(item, 'name', 'Name');
            return [name, version, releaseKey].filter(Boolean).join(' - ') || fallback;
        };
        const canonicalValue = (item) =>
            get(item, 'canonicalId', 'CanonicalId');
        const definitionId = (item) =>
            get(item, 'id', 'Id');
        const definitionDepth = (item) => {
            const path = String(get(item, 'fullPath', 'FullPath') || '');
            return path ? Math.max(0, path.split('/').filter(Boolean).length - 1) : 0;
        };
        const isPublishedBaseline = (item) => upper(get(item, 'status', 'Status')) === 'PUBLISHED';
        const isUsableDefinition = (item) => upper(get(item, 'status', 'Status')) !== 'DELETED';

        const setSelectDisabled = (select, disabled) => {
            if (!select) return;
            select.disabled = disabled;
            select.toggleAttribute('disabled', disabled);
            if (window.jQuery?.fn?.select2) {
                const $select = window.jQuery(select);
                $select.prop('disabled', disabled).trigger('change.select2');
                $select.next('.select2-container')
                    .toggleClass('select2-container--disabled', disabled)
                    .attr('aria-disabled', disabled ? 'true' : 'false');
            }
        };

        const clearSelect = (select, disabled) => {
            if (!select) return;
            select.innerHTML = '<option value=""></option>';
            select.value = '';
            setSelectDisabled(select, disabled);
            if (window.jQuery?.fn?.select2) {
                window.jQuery(select).val('').trigger('change.select2');
            }
        };

        const setCollectionDefinitionId = (value) => {
            if (collectionDefinitionInput) collectionDefinitionInput.value = value || '';
        };

        const showEmptyOption = (select, message) => {
            if (!select) return;
            clearSelect(select, true);
            const option = document.createElement('option');
            option.value = '';
            option.textContent = message || t('NotAvailable');
            select.appendChild(option);
            if (window.jQuery?.fn?.select2) {
                window.jQuery(select).val('').trigger('change.select2');
            }
        };

        const appendOption = (select, value, text, dataset) => {
            if (!select || !value) return;
            const option = document.createElement('option');
            option.value = value;
            option.textContent = text || value;
            Object.entries(dataset || {}).forEach(([key, val]) => {
                option.dataset[key] = String(val ?? '');
            });
            select.appendChild(option);
        };

        const fetchLookup = async (url) => {
            const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders(), cache: 'no-store' });
            const json = await res.json().catch(() => ({}));
            if (!res.ok || json?.isSuccessful === false) {
                throw new Error(`${url} failed with HTTP ${res.status}`);
            }
            return json;
        };

        const initSelect2 = () => {
            if (!window.jQuery?.fn?.select2) return;
            const jq = window.jQuery;
            jq('#classification, #variantPolicy, #ownerCompanyId, #baselineReleaseId, #canonicalId, #ownerUserId').each(function () {
                const $select = jq(this);
                if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
                $select.select2({
                    dropdownParent: jq(document.body),
                    width: '100%',
                    allowClear: !this.required,
                    placeholder: $select.data('placeholder') || '',
                    templateResult: (state) => {
                        if (!state.id) return state.text;
                        const depth = Number(state.element?.dataset?.depth || 0);
                        return jq('<span>').css('padding-left', `${depth * 14}px`).text(state.text);
                    }
                });
            });
        };

        const loadLegalEntities = async () => {
            if (!companySelect) return;
            clearSelect(companySelect, false);
            const json = await fetchLookup(`${endpoint}/legal-entities`);
            unwrapList(json).forEach((item) => {
                const id = optionId(item);
                appendOption(companySelect, id, optionText(item, id));
            });
            if (companySelect.options.length <= 1) showEmptyOption(companySelect, t('NotAvailable'));
            if (window.jQuery?.fn?.select2) window.jQuery(companySelect).trigger('change.select2');
        };

        const loadUsers = async () => {
            if (!userSelect) return;
            clearSelect(userSelect, false);
            const json = await fetchLookup(`${endpoint}/users`);
            unwrapList(json)
                .filter((item) => get(item, 'isActive', 'IsActive') !== false)
                .forEach((item) => {
                    const id = optionId(item);
                    appendOption(userSelect, id, userText(item, id));
                });
            if (userSelect.options.length <= 1) showEmptyOption(userSelect, t('NotAvailable'));
            if (window.jQuery?.fn?.select2) window.jQuery(userSelect).trigger('change.select2');
        };

        const loadBaselines = async () => {
            if (!baselineSelect) return;
            clearSelect(baselineSelect, false);
            clearSelect(canonicalSelect, true);
            definitionByCanonicalId.clear();
            setCollectionDefinitionId('');
            const json = await fetchLookup(`${endpoint}/qms-baselines`);
            const baselines = unwrapList(json)
                .filter(isPublishedBaseline)
                .sort((a, b) => String(baselineText(a, optionId(a))).localeCompare(String(baselineText(b, optionId(b)))));

            if (!baselines.length) {
                showEmptyOption(baselineSelect, t('NotAvailable'));
                showEmptyOption(canonicalSelect, t('NotAvailable'));
                return;
            }

            baselines.forEach((item) => {
                const id = get(item, 'id', 'Id');
                appendOption(baselineSelect, id, baselineText(item, id));
            });

            if (baselineSelect.options.length <= 1) {
                showEmptyOption(baselineSelect, t('NotAvailable'));
                showEmptyOption(canonicalSelect, t('NotAvailable'));
                return;
            }

            setSelectDisabled(baselineSelect, false);
            setSelectDisabled(canonicalSelect, false);
            if (window.jQuery?.fn?.select2) {
                window.jQuery(baselineSelect).val('').trigger('change.select2');
            }
        };

        const loadCanonicalNodesForBaseline = async (baselineId) => {
            clearSelect(canonicalSelect, true);
            definitionByCanonicalId.clear();
            setCollectionDefinitionId('');
            if (!baselineId || !canonicalSelect) return;

            try {
                const definitionsJson = await fetchLookup(`${endpoint}/qms-baselines/${encodeURIComponent(baselineId)}/definitions`);
                const definitions = unwrapList(definitionsJson)
                    .filter(isUsableDefinition)
                    .filter((item) => definitionId(item) && canonicalValue(item))
                    .sort((a, b) =>
                        Number(get(a, 'displayOrder', 'DisplayOrder') || 0) - Number(get(b, 'displayOrder', 'DisplayOrder') || 0)
                        || String(get(a, 'fullPath', 'FullPath') || get(a, 'name', 'Name') || '')
                            .localeCompare(String(get(b, 'fullPath', 'FullPath') || get(b, 'name', 'Name') || '')));

                definitions.forEach((item) => {
                    const id = definitionId(item);
                    const canonical = canonicalValue(item);
                    const text = optionText(item, canonical);
                    definitionByCanonicalId.set(String(canonical), { id, canonical, text });
                    appendOption(canonicalSelect, canonical, text, { collectionDefinitionId: id, depth: definitionDepth(item) });
                });

                if (canonicalSelect.options.length <= 1) {
                    showEmptyOption(canonicalSelect, t('NotAvailable'));
                    return;
                }

                setSelectDisabled(canonicalSelect, false);
                if (window.jQuery?.fn?.select2) {
                    window.jQuery(canonicalSelect).val('').trigger('change.select2');
                }
            } catch (error) {
                console.error('[TemplateMasters Create] Canonical folder lookup load failed.', error);
                showEmptyOption(canonicalSelect, t('ErrorOccurred'));
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        };

        const syncBindingFromCanonical = (canonicalId) => {
            const selected = definitionByCanonicalId.get(String(canonicalId || ''));
            setCollectionDefinitionId(selected?.id || '');
        };

        const initLookups = async () => {
            initSelect2();
            clearSelect(canonicalSelect, true);
            const bind = (select, eventName, handler) => {
                if (!select) return;
                if (window.jQuery?.fn?.select2) {
                    window.jQuery(select).off(eventName).on(eventName, function () { handler(this.value); });
                } else {
                    select.addEventListener('change', (event) => handler(event.target.value));
                }
            };
            bind(baselineSelect, 'change.template-master-baseline', (value) => { void loadCanonicalNodesForBaseline(value); });
            bind(canonicalSelect, 'change.template-master-canonical', syncBindingFromCanonical);

            const lookupTargets = [
                { promise: loadBaselines(), select: baselineSelect },
                { promise: loadLegalEntities(), select: companySelect },
                { promise: loadUsers(), select: userSelect }
            ];
            const results = await Promise.allSettled(lookupTargets.map((item) => item.promise));
            results
                .filter((result) => result.status === 'rejected')
                .forEach((result) => console.error('[TemplateMasters Create] Lookup load failed.', result.reason));
            results.forEach((result, index) => {
                if (result.status === 'rejected') {
                    showEmptyOption(lookupTargets[index].select, t('ErrorOccurred'));
                }
            });
            if (results.some((result) => result.status === 'rejected')) {
                window.showToast?.(t('ErrorOccurred'), 'error');
            }
        };

        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            if (!form.checkValidity()) {
                form.classList.add('was-validated');
                return;
            }
            const fd = new FormData(form);
            const emptyToNull = (v) => {
                const s = String(v || '').trim();
                return s ? s : null;
            };
            const payload = {
                masterCode: emptyToNull(fd.get('masterCode')),
                templateName: emptyToNull(fd.get('templateName')),
                description: emptyToNull(fd.get('description')),
                classification: emptyToNull(fd.get('classification')),
                collectionDefinitionId: emptyToNull(fd.get('collectionDefinitionId')),
                canonicalId: emptyToNull(fd.get('canonicalId')),
                variantPolicy: emptyToNull(fd.get('variantPolicy')),
                ownerCompanyId: emptyToNull(fd.get('ownerCompanyId')),
                ownerUserId: emptyToNull(fd.get('ownerUserId')),
                effectiveDate: emptyToNull(fd.get('effectiveDate'))
            };
            const body = new FormData();
            body.append('__RequestVerificationToken', token());
            body.append('payloadJson', JSON.stringify(payload));
            const res = await fetch(`${endpoint}/create`, { method: 'POST', body });
            const result = await res.json().catch(() => ({}));
            if (!res.ok || result?.isSuccessful === false) {
                const message = Array.isArray(result?.errors) && result.errors.length ? result.errors[0] : t('ErrorOccurred');
                window.Swal ? window.Swal.fire({ icon: 'error', title: t('ErrorOccurred'), text: message }) : console.error(message);
                return;
            }
            const created = result?.data ?? result?.Data;
            window.location.href = `/DocumentManagementTemplateMasters/Details/${created.id}`;
        });

        void initLookups();
    };
    return { init };
})();

// ── Details page ──
const TemplateMasterDetails = (function () {
    const init = async () => {
        const host = document.querySelector('[data-template-master-id]');
        if (!host) return;
        const L = window.L10n || {};
        const t = (key) => L[key] || key;
        const badge = (status) => {
            const s = String(status || '').toUpperCase();
            const map = { DRAFT: 'warning', PUBLISHED: 'success', DEPRECATED: 'danger', ARCHIVED: 'secondary' };
            const label = t(`Status${s.charAt(0)}${s.slice(1).toLowerCase()}`) || status || t('NotAvailable');
            return `<span class="badge bg-label-${map[s] || 'secondary'}">${label}</span>`;
        };
        const policyLabel = (value) => {
            const s = String(value || '').toUpperCase();
            if (s === 'LOCKED') return t('VariantLocked');
            if (s === 'ALLOWED') return t('VariantAllowed');
            return value || t('NotAvailable');
        };

        const apiBase = '/DocumentManagement/TemplateMasters/api';
        const id = host.dataset.templateMasterId;
        const res = await fetch(`${apiBase}/detail/${id}`);
        const payload = await res.json().catch(() => ({}));
        const data = payload?.data ?? payload?.Data;
        if (!data) return;

        document.getElementById('detailTitle').textContent = data.templateName || data.masterCode;
        document.getElementById('detailSubtitle').textContent = data.masterCode || '';

        // ── Friendly-label resolution: ids → human names via the same proxy lookups the Create page uses ──
        const g = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
        const unwrapList = (json) => {
            const d = json?.data ?? json?.Data ?? json;
            if (Array.isArray(d)) return d;
            for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(d?.[k])) return d[k];
            return [];
        };
        const fetchJson = async (url) => {
            try { return await (await fetch(url, { credentials: 'same-origin' })).json(); }
            catch { return {}; }
        };

        // Resolve owner company (legal entity name) — only if we have an id to resolve.
        const resolveCompany = async (companyId) => {
            if (!companyId) return null;
            const list = unwrapList(await fetchJson(`${apiBase}/legal-entities`));
            const match = list.find((it) => String(g(it, 'legalEntityId', 'LegalEntityId') || g(it, 'id', 'Id')) === String(companyId));
            return match
                ? (g(match, 'displayName', 'DisplayName') || g(match, 'legalName', 'LegalName') || g(match, 'commercialTitle', 'CommercialTitle') || g(match, 'name', 'Name'))
                : null;
        };

        // Resolve owner user (name surname + email) — only if we have an id to resolve.
        const resolveUser = async (userId) => {
            if (!userId) return null;
            const list = unwrapList(await fetchJson(`${apiBase}/users`));
            const match = list.find((it) => String(g(it, 'id', 'Id')) === String(userId));
            if (!match) return null;
            const full = `${g(match, 'firstName', 'FirstName') || ''} ${g(match, 'lastName', 'LastName') || ''}`.trim();
            const email = g(match, 'email', 'Email') || '';
            return full && email ? `${full} (${email})` : (full || email || null);
        };

        // Resolve collectionDefinitionId → baseline name, and canonicalId → definition tree node name,
        // by scanning the document-structure baselines + their definition trees.
        const resolveStructure = async (collectionDefinitionId, canonicalId) => {
            if (!collectionDefinitionId && !canonicalId) return { baselineName: null, definitionName: null };
            const baselines = unwrapList(await fetchJson(`${apiBase}/qms-baselines`));
            let result = { baselineName: null, definitionName: null };
            await Promise.all(baselines.map(async (b) => {
                const bid = g(b, 'id', 'Id');
                if (!bid || result.baselineName) return;
                const baselineName = g(b, 'name', 'Name') || g(b, 'baselineVersion', 'BaselineVersion') || String(bid);
                const defs = unwrapList(await fetchJson(`${apiBase}/qms-baselines/${encodeURIComponent(bid)}/definitions`));
                const match = defs.find((d) =>
                    (collectionDefinitionId && String(g(d, 'id', 'Id')) === String(collectionDefinitionId))
                    || (!collectionDefinitionId && canonicalId && String(g(d, 'canonicalId', 'CanonicalId')) === String(canonicalId)));
                if (match) {
                    result = {
                        baselineName,
                        definitionName: g(match, 'displayName', 'DisplayName') || g(match, 'fullPath', 'FullPath') || g(match, 'name', 'Name') || g(match, 'canonicalId', 'CanonicalId')
                    };
                }
            }));
            return result;
        };

        const [ownerCompanyName, ownerUserName, structure] = await Promise.all([
            resolveCompany(data.ownerCompanyId),
            resolveUser(data.ownerUserId),
            resolveStructure(data.collectionDefinitionId, data.canonicalId)
        ]);

        const renderFields = (elementId, fields) => {
            const element = document.getElementById(elementId);
            if (!element) return;
            element.innerHTML = fields.map(([key, value]) => `
                <dt class="col-sm-5">${t(key)}</dt>
                <dd class="col-sm-7">${value || t('NotAvailable')}</dd>`).join('');
        };

        renderFields('masterMetadataDetailList', [
            ['MasterCode', data.masterCode],
            ['TemplateName', data.templateName],
            ['Version', data.currentMasterVersion],
            ['Status', badge(data.status)],
            ['Description', data.description]
        ]);
        renderFields('masterBindingDetailList', [
            ['CollectionDefinitionId', structure.baselineName || data.collectionDefinitionId],
            ['CanonicalId', structure.definitionName || data.canonicalId]
        ]);
        renderFields('masterClassificationDetailList', [
            ['Classification', data.classification],
            ['VariantPolicy', policyLabel(data.variantPolicy)],
            ['EffectiveDate', data.effectiveDate]
        ]);
        renderFields('masterOwnerDetailList', [
            ['OwnerCompanyId', ownerCompanyName || data.ownerCompanyId],
            ['OwnerUserId', ownerUserName || data.ownerUserId],
            ['UsageCount', data.usageCount],
            ['VariantCount', data.variantCount]
        ]);

        const esc = (v) => (v === null || v === undefined ? '' : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
        const humanSize = (bytes) => {
            const n = Number(bytes);
            if (!Number.isFinite(n) || n <= 0) return '-';
            const units = ['B', 'KB', 'MB', 'GB'];
            let i = 0, v = n;
            while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
            return `${v.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
        };

        const versionsRes = await fetch(`/DocumentManagement/TemplateMasters/api/versions/${id}`);
        const versionsPayload = await versionsRes.json().catch(() => ({}));
        // Versions arrive newest-first; the DataTable re-sorts by version number descending.
        const rows = versionsPayload?.data ?? versionsPayload?.Data ?? [];

        const table = document.getElementById('versionHistoryTable');
        const body = document.getElementById('versionHistoryBody');
        if (table && body) {
            body.innerHTML = rows.map((row) => {
                const published = String(row.publishedAt || '');
                return `<tr>
                    <td></td>
                    <td class="text-nowrap" data-order="${esc(row.versionNumber)}">v${esc(row.versionNumber)} ${badge(row.status)}</td>
                    <td class="text-truncate" style="max-width:220px" title="${esc(row.file?.fileName)}">${row.id && row.file?.fileName ? `<a href="${apiBase}/download/${id}/${esc(row.id)}" class="fw-medium"><i class="icon-base bx bx-download me-1"></i>${esc(row.file?.fileName)}</a>` : (esc(row.file?.fileName) || t('NotAvailable'))}</td>
                    <td class="text-nowrap" data-order="${esc(row.file?.byteSize || 0)}">${esc(humanSize(row.file?.byteSize))}</td>
                    <td>${esc(row.publishedBy) || t('NotAvailable')}</td>
                    <td class="text-nowrap" data-order="${esc(published)}">${esc(published.slice(0, 10)) || t('NotAvailable')}</td>
                    <td>${esc(row.changeSummary) || t('NotAvailable')}</td>
                </tr>`;
            }).join('');

            document.getElementById('skeleton-loader')?.classList.add('d-none');

            if (window.DataTable && window.DtDefaults?.create && window.DtDefaults?.exportButtons) {
                new DataTable(table, window.DtDefaults.create({
                    stateSave: false,
                    colReorder: { columns: ':gt(0)' },
                    paging: true,
                    lengthChange: true,
                    searching: true,
                    info: true,
                    order: [[1, 'desc']],
                    columnDefs: [
                        { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                        { responsivePriority: 1, targets: 1 }
                    ],
                    buttons: window.DtDefaults.exportButtons(
                        null,
                        {},
                        {},
                        { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
                    ),
                    drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), 0); },
                    initComplete: function () { window.DtDefaults?.updateVisualState?.(this.api(), 0); }
                }));
            }
        }
    };
    return { init };
})();

document.addEventListener('DOMContentLoaded', () => {
    TemplateMastersList.init();
    TemplateMasterCreate.init();
    TemplateMasterDetails.init();
});
