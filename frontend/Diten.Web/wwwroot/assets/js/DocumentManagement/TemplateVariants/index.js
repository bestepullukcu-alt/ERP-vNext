/**
 * MOD-0029-FU03 — Template Variant Drift DataTables Index Script.
 * Golden Reference Compact pattern (same-origin MVC proxy profile). Actions: View / Compare / Rebase only.
 * Drift status is computed server-side (read-time) and filtered client-side. No delete/approval/workflow surface.
 */
'use strict';

const TemplateVariantsList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-templatevariants');
    // Verifier profile marker: window.API.documentManagement is not used here because this tenant shell page uses
    // same-origin MVC proxy endpoints; HttpOnly cookies stay server-side.
    const endpoint = '/DocumentManagement/TemplateVariants/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'TemplateVariants' };
    const perms = window.TemplateVariantsPerms || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { master: [], scopeType: [], driftStatus: [], approvalStatus: [] };
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
    const emptyFilters = () => ({ master: [], scopeType: [], driftStatus: [], approvalStatus: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            master: normalizeArray(source.master),
            scopeType: normalizeArray(source.scopeType),
            driftStatus: normalizeArray(source.driftStatus),
            approvalStatus: normalizeArray(source.approvalStatus)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(normalizeString(actual).toUpperCase());
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
            console.error('[TemplateVariants SaveView] Failed to load saved views.', error);
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
            return matchesMultiFilter(appliedFilters.master, row.templateMasterId)
                && matchesMultiFilter(appliedFilters.scopeType, row.scopeType)
                && matchesMultiFilter(appliedFilters.driftStatus, row.driftStatus)
                && matchesMultiFilter(appliedFilters.approvalStatus, row.approvalStatus);
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
        $('#filterMaster, #filterScopeType, #filterDriftStatus, #filterApprovalStatus').each(function () {
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
        $('#filterMaster').val(normalizeArray(values.master)).trigger('change');
        $('#filterScopeType').val(normalizeArray(values.scopeType)).trigger('change');
        $('#filterDriftStatus').val(normalizeArray(values.driftStatus)).trigger('change');
        $('#filterApprovalStatus').val(normalizeArray(values.approvalStatus)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.master, appliedFilters.scopeType, appliedFilters.driftStatus, appliedFilters.approvalStatus].filter(hasFilterValue).length;

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
                master: $('#filterMaster').val() || [],
                scopeType: $('#filterScopeType').val() || [],
                driftStatus: $('#filterDriftStatus').val() || [],
                approvalStatus: $('#filterApprovalStatus').val() || []
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
    const legalEntityMap = new Map();   // companyId → legal entity name
    const userMap = new Map();          // userId → "Name Surname (email)"
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
        const buildMasterFilter = async () => {
            const masters = unwrapLookupList(await fetchLookupJson('options'));
            const select = document.getElementById('filterMaster');
            if (!select) return;
            masters.forEach((m) => {
                const id = pick(m, 'templateMasterId', 'TemplateMasterId') || pick(m, 'id', 'Id');
                const code = pick(m, 'masterCode', 'MasterCode') || '';
                const name = pick(m, 'templateName', 'TemplateName') || '';
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = [code, name].filter(Boolean).join(' — ') || id;
                select.appendChild(opt);
            });
        };
        try { await Promise.all([buildLegal(), buildUsers(), buildMasterFilter()]); }
        catch (e) { console.error('[TemplateVariants] Resolution map load failed.', e); }
    };

    // ── Renderers ──
    const driftBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = { INSYNC: 'success', REBASEREQUIRED: 'warning', DRIFTED: 'info', BLOCKED: 'danger' };
        const labelKey = { INSYNC: 'DriftInSync', REBASEREQUIRED: 'DriftRebaseRequired', DRIFTED: 'DriftDrifted', BLOCKED: 'DriftBlocked' }[s];
        const label = (labelKey && t(labelKey)) || status || t('NotAvailable');
        return `<span class="badge bg-label-${map[s] || 'secondary'}">${label}</span>`;
    };
    const approvalBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = { NOTREQUIRED: 'secondary', PENDING: 'warning', APPROVED: 'success', REJECTED: 'danger', BLOCKED: 'dark' };
        const labelKey = { NOTREQUIRED: 'ApprovalNotRequired', PENDING: 'ApprovalPending', APPROVED: 'ApprovalApproved', REJECTED: 'ApprovalRejected', BLOCKED: 'ApprovalBlocked' }[s];
        const label = (labelKey && t(labelKey)) || status || t('NotAvailable');
        return `<span class="badge bg-label-${map[s] || 'secondary'}">${label}</span>`;
    };
    const scopeLabel = (value) => {
        const s = String(value || '').toUpperCase();
        if (s === 'COMPANY') return t('ScopeCompany');
        if (s === 'BUSINESSUNIT') return t('ScopeBusinessUnit');
        if (s === 'SITE') return t('ScopeSite');
        return value || t('NotAvailable');
    };
    const variantCell = (row) => {
        const code = row.variantCode || t('NotAvailable');
        const name = row.variantName || '';
        return `<span class="fw-medium text-heading">${code}</span>${name ? `<br><small class="text-muted">${name}</small>` : ''}`;
    };
    const derivedMasterText = (row) => {
        const code = row.masterCode || row.templateMasterId;
        const name = row.masterTemplateName || '';
        const ver = Number(row.masterCurrentVersion ?? 0);
        const head = [code, name].filter(Boolean).join(' — ');
        return head ? `${head} <span class="text-muted">(v${ver})</span>` : t('NotAvailable');
    };
    const lastRebasedText = (row) => {
        if (!row.lastRebasedMasterVersionNumber) return t('NotAvailable');
        const date = String(row.lastRebasedAt || '').slice(0, 10);
        return `v${row.lastRebasedMasterVersionNumber}${date ? ` · ${date}` : ''}`;
    };
    const scopeText = (row) => {
        const name = row.scopeId ? legalEntityMap.get(String(row.scopeId)) : null;
        const anchor = name || row.scopeId || t('NotAvailable');
        return `${scopeLabel(row.scopeType)}<br><small class="text-muted">${anchor}</small>`;
    };
    const ownerText = (row) => {
        const company = row.ownerCompanyId ? legalEntityMap.get(String(row.ownerCompanyId)) : null;
        const user = row.ownerUserId ? userMap.get(String(row.ownerUserId)) : null;
        return company || user || row.ownerCompanyId || row.ownerUserId || t('NotAvailable');
    };

    // ── Bulk surface scaffolding (Golden v2 contract). MOD-0029-FU03 exposes no destructive bulk action, so the
    //    bulk bar renders no [data-bulk-action] buttons and this handler is never reached. ──
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
                try {
                    const form = new FormData();
                    form.append('__RequestVerificationToken', token());
                    form.append('idsJson', JSON.stringify(ids));
                    const res = await fetch(`${endpoint}/bulk`, { method: 'POST', body: form });
                    const payload = await res.json().catch(() => ({}));
                    if (!res.ok || payload?.isSuccessful === false) { if (window.DitenUnauthorized?.handle(res, payload)) return; throw new Error('Bulk delete is not supported.'); }
                    reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }
        }
    };

    // ── Compare (metadata-level placeholder modal) ──
    const openCompare = async (id) => {
        const body = document.getElementById('compareModalBody');
        if (body) body.innerHTML = `<div class="text-center py-5"><span class="spinner-border text-primary" role="status" aria-hidden="true"></span></div>`;
        window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('compareModal')).show();

        const res = await fetch(`${endpoint}/compare/${id}`, { headers: getAuthHeaders() });
        const payload = await res.json().catch(() => ({}));
        const data = payload?.data ?? payload?.Data;
        if (!data) {
            if (body) body.innerHTML = `<div class="alert alert-danger mb-0">${t('ErrorOccurred')}</div>`;
            return;
        }
        if (body) body.innerHTML = renderCompare(data);
    };
    const renderCompare = (data) => {
        const row = (label, value) => `<dt class="col-sm-5">${label}</dt><dd class="col-sm-7">${value ?? t('NotAvailable')}</dd>`;
        return `
            <dl class="row mb-0">
                ${row(t('Variant'), `${data.variantCode || ''} ${driftBadge(data.driftStatus)}`)}
                ${row(t('DerivedMaster'), `${data.masterCode || ''} — ${data.masterTemplateName || ''}`)}
                ${row(t('MasterStatus'), data.masterStatus)}
                ${row(t('VariantStatus'), data.variantStatus)}
                ${row(t('MasterCurrentVersion'), data.masterCurrentVersion)}
                ${row(t('VariantLastRebasedVersion'), data.variantLastRebasedVersionNumber)}
                ${row(t('HasLocalChanges'), data.hasLocalChanges ? t('Yes') : t('No'))}
                ${row(t('Approval'), approvalBadge(data.approvalStatus))}
            </dl>
            <div class="alert alert-info d-flex align-items-center mt-4 mb-0" role="alert">
                <i class="bx bx-info-circle me-2"></i><span>${t('CompareNote')}</span>
            </div>`;
    };

    // ── Rebase (metadata-only, confirmation) ──
    const rebase = (id) => {
        if (!id) return;
        window.showConfirm?.(t('Rebase'), async () => {
            try {
                const form = new FormData();
                form.append('__RequestVerificationToken', token());
                const res = await fetch(`${endpoint}/rebase/${id}`, { method: 'POST', body: form });
                const payload = await res.json().catch(() => ({}));
                if (!res.ok || payload?.isSuccessful === false) { if (window.DitenUnauthorized?.handle(res, payload)) return; throw new Error('Rebase failed.'); }
                reloadWithSuccessToast('Rebased');
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, { type: 'warning', subtext: t('RebaseConfirm'), confirmButtonText: t('Rebase') });
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/DocumentManagementTemplateVariants/Details/${id}`; },
        compare: ({ id }) => { if (id) void openCompare(id); },
        rebase: ({ id }) => { if (id) void rebase(id); }
    };

    const buildRowActions = (full) => {
        const actions = [
            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L.ViewDetails } }
        ];
        if (perms.canCompare && full.canCompare !== false) actions.push({ key: 'compare', icon: 'bx bx-git-compare', text: L.Compare, attrs: { 'data-id': full.id } });
        if (perms.canRebase && full.canRebase === true) actions.push({ key: 'rebase', icon: 'bx bx-git-pull-request', text: L.Rebase, attrs: { 'data-id': full.id } });
        return window.DitenDataTable.renderActions(actions);
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();
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
                        console.error('[TemplateVariants SaveView] Failed to save default view.', error);
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
                    { data: 'variantCode', name: 'variant' },
                    { data: 'masterCode', name: 'derivedMaster' },
                    { data: 'lastRebasedMasterVersionNumber', name: 'lastRebased' },
                    { data: 'driftStatus', name: 'driftStatus' },
                    { data: 'scopeType', name: 'scope' },
                    { data: 'ownerCompanyId', name: 'owner' },
                    { data: 'approvalStatus', name: 'approval' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data, type, full) => variantCell(full) },
                    { targets: 3, render: (data, type, full) => derivedMasterText(full) },
                    { targets: 4, render: (data, type, full) => lastRebasedText(full) },
                    { targets: 5, render: (data) => driftBadge(data) },
                    { targets: 6, render: (data, type, full) => scopeText(full) },
                    { targets: 7, render: (data, type, full) => ownerText(full) },
                    { targets: 8, render: (data) => approvalBadge(data) },
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
                    perms.canCreate ? (L.CreateVariant || L.AddNew) : null,
                    { href: '/DocumentManagementTemplateVariants/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7, 8], colvisColumns: [2, 3, 4, 5, 6, 7, 8] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/DocumentManagementTemplateVariants/Create';
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
const TemplateVariantCreate = (function () {
    const init = () => {
        const form = document.getElementById('templateVariantCreateForm');
        if (!form) return;
        const L = window.L10n || {};
        const t = (key) => L[key] || key;
        const endpoint = '/DocumentManagement/TemplateVariants/api';
        const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const masterSelect = document.getElementById('templateMasterId');
        const masterVersionInput = document.getElementById('templateMasterVersionId');
        const scopeSelect = document.getElementById('scopeId');
        const companySelect = document.getElementById('ownerCompanyId');
        const userSelect = document.getElementById('ownerUserId');
        const structureSelect = document.getElementById('documentationStructureId');
        const folderSelect = document.getElementById('targetCollectionInstanceId');
        const contentSourceSelect = document.getElementById('contentSource');
        const localFileInput = document.getElementById('localContentFile');
        const localFileGroup = document.getElementById('localContentFileGroup');
        const contentSourceHelp = document.getElementById('contentSourceHelp');
        const masterVersionByMaster = new Map();

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
        const emptyToNull = (v) => { const s = String(v || '').trim(); return s ? s : null; };

        const appendOption = (select, value, text) => {
            if (!select || !value) return;
            const option = document.createElement('option');
            option.value = value;
            option.textContent = text || value;
            select.appendChild(option);
        };
        const resetSelect = (select) => {
            if (!select) return;
            select.innerHTML = '<option value=""></option>';
            if (window.jQuery?.fn?.select2) window.jQuery(select).val('').trigger('change.select2');
        };

        const fetchLookup = async (url) => {
            const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders(), cache: 'no-store' });
            const json = await res.json().catch(() => ({}));
            if (!res.ok || json?.isSuccessful === false) throw new Error(`${url} failed with HTTP ${res.status}`);
            return json;
        };

        const initSelect2 = () => {
            if (!window.jQuery?.fn?.select2) return;
            const jq = window.jQuery;
            jq('#templateMasterId, #status, #scopeType, #scopeId, #ownerCompanyId, #ownerUserId, #documentationStructureId, #targetCollectionInstanceId, #contentSource').each(function () {
                const $select = jq(this);
                if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
                $select.select2({
                    dropdownParent: jq(document.body),
                    width: '100%',
                    allowClear: !this.required,
                    placeholder: $select.data('placeholder') || ''
                });
            });
        };

        const loadMasters = async () => {
            if (!masterSelect) return;
            const json = await fetchLookup(`${endpoint}/options`);
            unwrapList(json).forEach((m) => {
                const id = get(m, 'templateMasterId', 'TemplateMasterId') || get(m, 'id', 'Id');
                const code = get(m, 'masterCode', 'MasterCode') || '';
                const name = get(m, 'templateName', 'TemplateName') || '';
                const versionId = get(m, 'currentVersionId', 'CurrentVersionId');
                const versionNumber = get(m, 'currentMasterVersion', 'CurrentMasterVersion');
                if (!id) return;
                masterVersionByMaster.set(String(id), versionId);
                appendOption(masterSelect, id, `${[code, name].filter(Boolean).join(' — ')} (v${versionNumber ?? 0})`);
            });
            if (window.jQuery?.fn?.select2) window.jQuery(masterSelect).val('').trigger('change.select2');
        };

        const loadLegalEntities = async (select) => {
            if (!select) return;
            const json = await fetchLookup(`${endpoint}/legal-entities`);
            unwrapList(json).forEach((item) => {
                const id = get(item, 'legalEntityId', 'LegalEntityId') || get(item, 'id', 'Id');
                const name = get(item, 'displayName', 'DisplayName') || get(item, 'legalName', 'LegalName') || get(item, 'commercialTitle', 'CommercialTitle') || get(item, 'name', 'Name') || id;
                appendOption(select, id, name);
            });
            if (window.jQuery?.fn?.select2) window.jQuery(select).trigger('change.select2');
        };

        const loadUsers = async () => {
            if (!userSelect) return;
            const json = await fetchLookup(`${endpoint}/users`);
            unwrapList(json).filter((item) => get(item, 'isActive', 'IsActive') !== false).forEach((item) => {
                const id = get(item, 'id', 'Id');
                const full = `${get(item, 'firstName', 'FirstName') || ''} ${get(item, 'lastName', 'LastName') || ''}`.trim();
                const email = get(item, 'email', 'Email') || '';
                appendOption(userSelect, id, full && email ? `${full} (${email})` : (full || email || id));
            });
            if (window.jQuery?.fn?.select2) window.jQuery(userSelect).trigger('change.select2');
        };

        const scopeMatchesFolder = (folder) => {
            const scopeType = String(document.getElementById('scopeType')?.value || '').toUpperCase();
            const scopeId = String(scopeSelect?.value || '');
            if (!scopeId) return false;
            if (scopeType === 'COMPANY') return String(get(folder, 'companyId', 'CompanyId')) === scopeId;
            const expected = scopeType === 'BUSINESSUNIT' ? 'BUSINESS_UNIT' : 'SITE';
            const bindings = get(folder, 'scopeBindings', 'ScopeBindings') || [];
            return Array.isArray(bindings) && bindings.some((b) => {
                const type = String(get(b, 'scopeType', 'ScopeType') || '').replace('-', '_').toUpperCase();
                const id = String(get(b, 'scopeId', 'ScopeId') || '');
                const status = String(get(b, 'bindingStatus', 'BindingStatus') || 'ACTIVE').toUpperCase();
                return type === expected && id === scopeId && status === 'ACTIVE';
            });
        };

        const loadTargetFolders = async () => {
            resetSelect(structureSelect);
            resetSelect(folderSelect);
            const companyId = emptyToNull(scopeSelect?.value) || emptyToNull(companySelect?.value);
            if (!companyId) return;

            const [structuresJson, foldersJson] = await Promise.all([
                fetchLookup(`${endpoint}/documentation-structures?companyId=${encodeURIComponent(companyId)}`),
                fetchLookup(`${endpoint}/collection-instances?companyId=${encodeURIComponent(companyId)}`)
            ]);
            unwrapList(structuresJson).forEach((item) => {
                const id = get(item, 'activeStructureId', 'ActiveStructureId') || get(item, 'rootCollectionInstanceId', 'RootCollectionInstanceId');
                const name = get(item, 'displayName', 'DisplayName') || get(item, 'name', 'Name') || id;
                appendOption(structureSelect, id, name);
            });
            unwrapList(foldersJson)
                .filter((item) => get(item, 'isUsable', 'IsUsable') !== false)
                .filter(scopeMatchesFolder)
                .forEach((item) => {
                    const id = get(item, 'collectionInstanceId', 'CollectionInstanceId') || get(item, 'id', 'Id');
                    const path = get(item, 'fullPath', 'FullPath') || get(item, 'name', 'Name') || id;
                    appendOption(folderSelect, id, path);
                });
            if (window.jQuery?.fn?.select2) {
                window.jQuery(structureSelect).trigger('change.select2');
                window.jQuery(folderSelect).trigger('change.select2');
            }
        };

        const syncMasterVersion = () => {
            const masterId = masterSelect?.value || '';
            if (masterVersionInput) masterVersionInput.value = masterVersionByMaster.get(String(masterId)) || '';
        };

        const syncContentSource = () => {
            const isLocal = String(contentSourceSelect?.value || '').toUpperCase() === 'LOCAL_UPLOAD';
            localFileGroup?.classList.toggle('d-none', !isLocal);
            if (localFileInput) {
                localFileInput.required = isLocal;
                if (!isLocal) localFileInput.value = '';
            }
            if (contentSourceHelp) {
                contentSourceHelp.textContent = isLocal ? t('LocalUploadHelp') : t('MasterContentHelp');
            }
        };

        const initLookups = async () => {
            initSelect2();
            const bind = (select, handler) => {
                if (!select) return;
                if (window.jQuery?.fn?.select2) window.jQuery(select).off('change.tv').on('change.tv', handler);
                else select.addEventListener('change', handler);
            };
            bind(masterSelect, syncMasterVersion);
            bind(contentSourceSelect, syncContentSource);
            bind(scopeSelect, () => { void loadTargetFolders(); });
            bind(companySelect, () => { void loadTargetFolders(); });
            bind(document.getElementById('scopeType'), () => { resetSelect(folderSelect); void loadTargetFolders(); });

            const results = await Promise.allSettled([loadMasters(), loadLegalEntities(scopeSelect), loadLegalEntities(companySelect), loadUsers()]);
            results.filter((r) => r.status === 'rejected').forEach((r) => console.error('[TemplateVariants Create] Lookup load failed.', r.reason));
            if (results.some((r) => r.status === 'rejected')) window.showToast?.(t('ErrorOccurred'), 'error');
            syncMasterVersion();
            syncContentSource();
        };

        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            syncMasterVersion();
            if (!form.checkValidity()) {
                form.classList.add('was-validated');
                return;
            }
            const fd = new FormData(form);
            const payload = {
                templateMasterId: emptyToNull(fd.get('templateMasterId')),
                templateMasterVersionId: emptyToNull(fd.get('templateMasterVersionId')),
                variantCode: emptyToNull(fd.get('variantCode')),
                variantName: emptyToNull(fd.get('variantName')),
                description: emptyToNull(fd.get('description')),
                scopeType: emptyToNull(fd.get('scopeType')),
                scopeId: emptyToNull(fd.get('scopeId')),
                targetCollectionInstanceId: emptyToNull(fd.get('targetCollectionInstanceId')),
                contentSource: emptyToNull(fd.get('contentSource')),
                ownerCompanyId: emptyToNull(fd.get('ownerCompanyId')),
                ownerUserId: emptyToNull(fd.get('ownerUserId')),
                status: emptyToNull(fd.get('status'))
            };
            const body = new FormData();
            body.append('__RequestVerificationToken', token());
            body.append('payloadJson', JSON.stringify(payload));
            if (String(payload.contentSource || '').toUpperCase() === 'LOCAL_UPLOAD' && localFileInput?.files?.[0]) {
                body.append('localContentFile', localFileInput.files[0]);
            }
            const res = await fetch(`${endpoint}/create`, { method: 'POST', body });
            const result = await res.json().catch(() => ({}));
            if (!res.ok || result?.isSuccessful === false) {
                const message = Array.isArray(result?.errors) && result.errors.length ? result.errors[0] : t('ErrorOccurred');
                window.showToast?.(message, 'error');
                return;
            }
            const created = result?.data ?? result?.Data;
            window.location.href = `/DocumentManagementTemplateVariants/Details/${created.id}`;
        });

        void initLookups();
    };
    return { init };
})();

// ── Details page ──
const TemplateVariantDetails = (function () {
    const init = async () => {
        const host = document.querySelector('[data-template-variant-id]');
        if (!host) return;
        const L = window.L10n || {};
        const t = (key) => L[key] || key;
        const endpoint = '/DocumentManagement/TemplateVariants/api';
        const id = host.dataset.templateVariantId;
        const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};

        const driftBadge = (status) => {
            const s = String(status || '').toUpperCase();
            const map = { INSYNC: 'success', REBASEREQUIRED: 'warning', DRIFTED: 'info', BLOCKED: 'danger' };
            const labelKey = { INSYNC: 'DriftInSync', REBASEREQUIRED: 'DriftRebaseRequired', DRIFTED: 'DriftDrifted', BLOCKED: 'DriftBlocked' }[s];
            return `<span class="badge bg-label-${map[s] || 'secondary'}">${(labelKey && t(labelKey)) || status || t('NotAvailable')}</span>`;
        };
        const approvalBadge = (status) => {
            const s = String(status || '').toUpperCase();
            const map = { NOTREQUIRED: 'secondary', PENDING: 'warning', APPROVED: 'success', REJECTED: 'danger', BLOCKED: 'dark' };
            const labelKey = { NOTREQUIRED: 'ApprovalNotRequired', PENDING: 'ApprovalPending', APPROVED: 'ApprovalApproved', REJECTED: 'ApprovalRejected', BLOCKED: 'ApprovalBlocked' }[s];
            return `<span class="badge bg-label-${map[s] || 'secondary'}">${(labelKey && t(labelKey)) || status || t('NotAvailable')}</span>`;
        };
        const scopeLabel = (value) => {
            const s = String(value || '').toUpperCase();
            if (s === 'COMPANY') return t('ScopeCompany');
            if (s === 'BUSINESSUNIT') return t('ScopeBusinessUnit');
            if (s === 'SITE') return t('ScopeSite');
            return value || t('NotAvailable');
        };
        const contentSourceLabel = (value) => {
            const s = String(value || '').toUpperCase();
            if (s === 'MASTER_VERSION' || s === 'MASTERVERSION') return t('MasterVersionContent');
            if (s === 'LOCAL_UPLOAD' || s === 'LOCALUPLOAD') return t('LocalVariantUpload');
            return value || t('NotAvailable');
        };

        const g = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
        const unwrapList = (json) => {
            const d = json?.data ?? json?.Data ?? json;
            if (Array.isArray(d)) return d;
            for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(d?.[k])) return d[k];
            return [];
        };
        const fetchJson = async (url) => { try { return await (await fetch(url, { credentials: 'same-origin' })).json(); } catch { return {}; } };

        const res = await fetch(`${endpoint}/detail/${id}`);
        const payload = await res.json().catch(() => ({}));
        const data = payload?.data ?? payload?.Data;
        if (!data) return;

        document.getElementById('detailTitle').textContent = data.variantName || data.variantCode;
        document.getElementById('detailSubtitle').textContent = data.variantCode || '';

        const resolveCompany = async (companyId) => {
            if (!companyId) return null;
            const list = unwrapList(await fetchJson(`${endpoint}/legal-entities`));
            const match = list.find((it) => String(g(it, 'legalEntityId', 'LegalEntityId') || g(it, 'id', 'Id')) === String(companyId));
            return match ? (g(match, 'displayName', 'DisplayName') || g(match, 'legalName', 'LegalName') || g(match, 'commercialTitle', 'CommercialTitle') || g(match, 'name', 'Name')) : null;
        };
        const resolveUser = async (userId) => {
            if (!userId) return null;
            const list = unwrapList(await fetchJson(`${endpoint}/users`));
            const match = list.find((it) => String(g(it, 'id', 'Id')) === String(userId));
            if (!match) return null;
            const full = `${g(match, 'firstName', 'FirstName') || ''} ${g(match, 'lastName', 'LastName') || ''}`.trim();
            const email = g(match, 'email', 'Email') || '';
            return full && email ? `${full} (${email})` : (full || email || null);
        };

        const [ownerCompanyName, ownerUserName, scopeName] = await Promise.all([
            resolveCompany(data.ownerCompanyId),
            resolveUser(data.ownerUserId),
            resolveCompany(data.scopeId)
        ]);

        const renderFields = (elementId, fields) => {
            const element = document.getElementById(elementId);
            if (!element) return;
            element.innerHTML = fields.map(([key, value]) => `
                <dt class="col-sm-5">${t(key)}</dt>
                <dd class="col-sm-7">${value || t('NotAvailable')}</dd>`).join('');
        };

        renderFields('variantIdentityDetailList', [
            ['VariantCode', data.variantCode],
            ['VariantName', data.variantName],
            ['Status', data.status],
            ['DerivedMaster', `${data.masterCode || ''} ${data.masterTemplateName ? '— ' + data.masterTemplateName : ''}`],
            ['Description', data.description]
        ]);
        renderFields('variantScopeDetailList', [
            ['ScopeType', scopeLabel(data.scopeType)],
            ['ScopeId', scopeName || data.scopeId]
        ]);
        renderFields('variantContentDetailList', [
            ['VariantContentSource', contentSourceLabel(data.contentSource)],
            ['TargetFolder', data.collectionPath],
            ['LinkedTemplateDocument', data.linkedTemplateDocumentTitle || data.linkedTemplateDocumentId],
            ['TemplateDocumentCurrentVersion', data.templateDocumentCurrentVersion],
            ['ContentLinked', data.contentLinked ? t('Yes') : t('No')]
        ]);
        renderFields('variantOwnerDetailList', [
            ['OwnerCompanyId', ownerCompanyName || data.ownerCompanyId],
            ['OwnerUserId', ownerUserName || data.ownerUserId]
        ]);
        renderFields('variantGovernanceDetailList', [
            ['DriftStatus', driftBadge(data.driftStatus)],
            ['MasterCurrentVersion', data.masterCurrentVersion],
            ['VariantLastRebasedVersion', data.lastRebasedMasterVersionNumber],
            ['LastRebased', String(data.lastRebasedAt || '').slice(0, 10)],
            ['HasLocalChanges', data.hasLocalChanges ? t('Yes') : t('No')],
            ['Approval', approvalBadge(data.approvalStatus)]
        ]);

        // Compare modal (reuses the same metadata-level placeholder as the list surface)
        const renderCompare = (cmp) => {
            const row = (label, value) => `<dt class="col-sm-5">${label}</dt><dd class="col-sm-7">${value ?? t('NotAvailable')}</dd>`;
            return `
                <dl class="row mb-0">
                    ${row(t('Variant'), `${cmp.variantCode || ''} ${driftBadge(cmp.driftStatus)}`)}
                    ${row(t('DerivedMaster'), `${cmp.masterCode || ''} — ${cmp.masterTemplateName || ''}`)}
                    ${row(t('MasterStatus'), cmp.masterStatus)}
                    ${row(t('VariantStatus'), cmp.variantStatus)}
                    ${row(t('MasterCurrentVersion'), cmp.masterCurrentVersion)}
                    ${row(t('VariantLastRebasedVersion'), cmp.variantLastRebasedVersionNumber)}
                    ${row(t('HasLocalChanges'), cmp.hasLocalChanges ? t('Yes') : t('No'))}
                    ${row(t('Approval'), approvalBadge(cmp.approvalStatus))}
                    ${row(t('VariantContentSource'), contentSourceLabel(cmp.contentSource))}
                    ${row(t('LinkedTemplateDocument'), cmp.linkedTemplateDocumentTitle || cmp.linkedTemplateDocumentId)}
                    ${row(t('TargetFolder'), cmp.collectionPath)}
                    ${row(t('TemplateDocumentCurrentVersion'), cmp.templateDocumentCurrentVersion)}
                    ${row(t('ContentLinked'), cmp.contentLinked ? t('Yes') : t('No'))}
                </dl>
                <div class="alert alert-info d-flex align-items-center mt-4 mb-0" role="alert">
                    <i class="bx bx-info-circle me-2"></i><span>${t('CompareNote')}</span>
                </div>`;
        };

        document.getElementById('btnDetailCompare')?.addEventListener('click', async () => {
            const body = document.getElementById('compareModalBody');
            if (body) body.innerHTML = `<div class="text-center py-5"><span class="spinner-border text-primary" role="status" aria-hidden="true"></span></div>`;
            window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('compareModal')).show();
            const cres = await fetch(`${endpoint}/compare/${id}`, { headers: getAuthHeaders() });
            const cpayload = await cres.json().catch(() => ({}));
            const cmp = cpayload?.data ?? cpayload?.Data;
            if (body) body.innerHTML = cmp ? renderCompare(cmp) : `<div class="alert alert-danger mb-0">${t('ErrorOccurred')}</div>`;
        });

        document.getElementById('btnDetailRebase')?.addEventListener('click', () => {
            window.showConfirm?.(t('Rebase'), async () => {
                try {
                    const form = new FormData();
                    form.append('__RequestVerificationToken', token());
                    const rres = await fetch(`${endpoint}/rebase/${id}`, { method: 'POST', body: form });
                    const rpayload = await rres.json().catch(() => ({}));
                    if (!rres.ok || rpayload?.isSuccessful === false) throw new Error('Rebase failed.');
                    window.showToast?.(t('Rebased'), 'success');
                    window.location.reload();
                } catch (error) {
                    console.error(error);
                    window.showToast?.(t('ErrorOccurred'), 'error');
                }
            }, { type: 'warning', subtext: t('RebaseConfirm'), confirmButtonText: t('Rebase') });
        });
    };
    return { init };
})();

document.addEventListener('DOMContentLoaded', () => {
    TemplateVariantsList.init();
    TemplateVariantCreate.init();
    TemplateVariantDetails.init();
});
