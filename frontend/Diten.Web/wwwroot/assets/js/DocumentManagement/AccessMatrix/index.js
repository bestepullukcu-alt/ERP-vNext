/**
 * MOD-0029-FU04 — Document Access Matrix DataTables Index Script.
 * Golden Reference Compact pattern (same-origin MVC proxy profile). Surfaces View / Create-Edit / Delete and an
 * Effective Access Preview. No approval/review/workflow/e-signature surface.
 */
'use strict';

const AccessMatrixList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-accessmatrix');
    // Verifier profile marker: window.API.documentManagement is not used here because this tenant shell page uses
    // the same-origin MVC proxy profile required by the pack.
    const endpoint = '/DocumentManagement/AccessMatrix/api';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'AccessMatrix' };
    const perms = window.AccessMatrixPerms || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const totalColumnCount = 11;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { targetType: [], principalType: [], effect: [], action: [], status: [], inheritance: [] };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const t = (key) => L[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ targetType: [], principalType: [], effect: [], action: [], status: [], inheritance: [] });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            targetType: normalizeArray(source.targetType),
            principalType: normalizeArray(source.principalType),
            effect: normalizeArray(source.effect),
            action: normalizeArray(source.action),
            status: normalizeArray(source.status),
            inheritance: normalizeArray(source.inheritance)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(normalizeString(actual).toUpperCase());
    };
    const matchesActionFilter = (selected, actions) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        if (!norm.length) return true;
        const set = (actions || []).map((a) => normalizeString(a).toUpperCase());
        return norm.some((a) => set.includes(a));
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
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => { acc[key] = normalizeFilterValue(v.filters[key]); return acc; }, {}),
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
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
    });
    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(), search: '', colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
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
            console.error('[AccessMatrix SaveView] Failed to load saved views.', error);
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
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
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
            return matchesMultiFilter(appliedFilters.targetType, row.targetType)
                && matchesMultiFilter(appliedFilters.principalType, row.principalType)
                && matchesMultiFilter(appliedFilters.effect, row.effect)
                && matchesActionFilter(appliedFilters.action, row.actions)
                && matchesMultiFilter(appliedFilters.status, row.status)
                && matchesMultiFilter(appliedFilters.inheritance, row.inheritFromParent ? 'INHERITED' : 'EXPLICIT');
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
        $('#filterTargetType, #filterPrincipalType, #filterEffect, #filterAction, #filterStatus, #filterInheritance').each(function () {
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
        $('#filterTargetType').val(normalizeArray(values.targetType)).trigger('change');
        $('#filterPrincipalType').val(normalizeArray(values.principalType)).trigger('change');
        $('#filterEffect').val(normalizeArray(values.effect)).trigger('change');
        $('#filterAction').val(normalizeArray(values.action)).trigger('change');
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterInheritance').val(normalizeArray(values.inheritance)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.targetType, appliedFilters.principalType, appliedFilters.effect, appliedFilters.action, appliedFilters.status, appliedFilters.inheritance].filter(hasFilterValue).length;

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
                targetType: $('#filterTargetType').val() || [],
                principalType: $('#filterPrincipalType').val() || [],
                effect: $('#filterEffect').val() || [],
                action: $('#filterAction').val() || [],
                status: $('#filterStatus').val() || [],
                inheritance: $('#filterInheritance').val() || []
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
    const userMap = new Map();    // userId → "Name Surname (email)"
    const roleMap = new Map();    // roleId → role name
    const companyMap = new Map(); // companyId → legal entity name
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
        const buildRoles = async () => {
            unwrapLookupList(await fetchLookupJson('roles')).forEach((it) => {
                const idv = pick(it, 'id', 'Id');
                const name = pick(it, 'name', 'Name') || pick(it, 'roleName', 'RoleName');
                if (idv && name) roleMap.set(String(idv), name);
            });
        };
        const buildCompanies = async () => {
            unwrapLookupList(await fetchLookupJson('legal-entities')).forEach((it) => {
                const idv = pick(it, 'legalEntityId', 'LegalEntityId') || pick(it, 'id', 'Id');
                const name = pick(it, 'displayName', 'DisplayName') || pick(it, 'legalName', 'LegalName') || pick(it, 'commercialTitle', 'CommercialTitle') || pick(it, 'name', 'Name');
                if (idv && name) companyMap.set(String(idv), name);
            });
        };
        try { await Promise.all([buildUsers(), buildRoles(), buildCompanies()]); }
        catch (e) { console.error('[AccessMatrix] Resolution map load failed.', e); }
    };

    // ── Renderers ──
    const upper = (v) => String(v || '').toUpperCase();
    const targetTypeLabel = (v) => t(`Target${String(v || '').charAt(0).toUpperCase()}${String(v || '').slice(1)}`) || v || t('NotAvailable');
    const principalTypeLabel = (v) => t(`Principal${String(v || '').charAt(0).toUpperCase()}${String(v || '').slice(1)}`) || v || t('NotAvailable');
    const actionLabel = (v) => t(`Action${v}`) || v;
    const effectBadge = (v) => {
        const s = upper(v);
        const cls = s === 'DENY' ? 'danger' : 'success';
        return `<span class="badge bg-label-${cls}">${s === 'DENY' ? t('EffectDeny') : t('EffectAllow')}</span>`;
    };
    const statusBadge = (v) => {
        const s = upper(v);
        const map = { ACTIVE: 'success', DISABLED: 'warning', ARCHIVED: 'secondary' };
        const label = { ACTIVE: t('StatusActive'), DISABLED: t('StatusDisabled'), ARCHIVED: t('StatusArchived') }[s] || v;
        return `<span class="badge bg-label-${map[s] || 'secondary'}">${label}</span>`;
    };
    const targetCell = (row) => {
        // Company targets store the company id; resolve it to the legal entity name like other id columns.
        const companyName = upper(row.targetType) === 'COMPANY' ? companyMap.get(String(row.targetId)) : null;
        const label = companyName || row.targetLabel || row.targetId;
        return `<span class="fw-medium text-heading">${targetTypeLabel(row.targetType)}</span><br><small class="text-muted text-truncate d-inline-block" style="max-width:240px" title="${label || ''}">${label || ''}</small>`;
    };
    const principalName = (row) => {
        const pt = upper(row.principalType);
        if (pt === 'USER') return userMap.get(String(row.principalId));
        if (pt === 'ROLE') return roleMap.get(String(row.principalId));
        if (pt === 'COMPANY') return companyMap.get(String(row.principalId));
        return null;
    };
    const principalCell = (row) => {
        const display = principalName(row) || row.principalId || t('NotAvailable');
        return `<span class="fw-medium">${principalTypeLabel(row.principalType)}</span><br><small class="text-muted text-truncate d-inline-block" style="max-width:220px" title="${display}">${display}</small>`;
    };
    const actionsCell = (row) => {
        const list = row.actions || [];
        if (!list.length) return t('NotAvailable');
        const shown = list.slice(0, 3).map((a) => `<span class="badge bg-label-primary me-1">${actionLabel(a)}</span>`).join('');
        const more = list.length > 3 ? `<span class="badge bg-label-secondary">+${list.length - 3}</span>` : '';
        return shown + more;
    };
    const inheritanceBadge = (row) => row.inheritFromParent
        ? `<span class="badge bg-label-info">${t('InheritancePropagates')}</span>`
        : `<span class="badge bg-label-secondary">${t('InheritanceDirect')}</span>`;
    const validityCell = (row) => {
        const from = row.validFrom ? String(row.validFrom).slice(0, 10) : '';
        const to = row.validTo ? String(row.validTo).slice(0, 10) : '';
        const expired = row.isExpired ? ` <span class="badge bg-label-danger">${t('Expired')}</span>` : '';
        if (!from && !to) return `${t('NotAvailable')}${expired}`;
        return `${from || '…'} → ${to || '…'}${expired}`;
    };
    const updatedCell = (row) => {
        const d = String(row.updatedAt || row.createdAt || '').slice(0, 10);
        return d || t('NotAvailable');
    };

    // ── Bulk (gated by manage) ──
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
        }, { entityName: row.targetLabel || row.targetId, type: 'danger', confirmButtonText: L.Delete });
    };

    // ── Effective preview modal ──
    const openPreview = (row) => {
        document.getElementById('previewTargetType').value = row.targetType || '';
        document.getElementById('previewTargetId').value = row.targetId || '';
        const principalType = document.getElementById('previewPrincipalType');
        const principalId = document.getElementById('previewPrincipalId');
        if (principalType) principalType.value = row.principalType === 'Group' ? 'User' : (row.principalType || 'User');
        if (principalId) principalId.value = row.principalId || '';
        const body = document.getElementById('previewModalBody');
        if (body) body.innerHTML = `<div class="alert alert-info mb-0">${t('EffectivePreviewNote')}</div>`;
        window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('previewModal')).show();
    };
    const runPreview = async () => {
        const targetType = document.getElementById('previewTargetType').value;
        const targetId = document.getElementById('previewTargetId').value;
        const principalType = document.getElementById('previewPrincipalType').value;
        const principalId = (document.getElementById('previewPrincipalId').value || '').trim();
        const body = document.getElementById('previewModalBody');
        if (!principalId) { window.showToast?.(t('ValidationFailed'), 'warning'); return; }
        if (body) body.innerHTML = `<div class="text-center py-4"><span class="spinner-border text-primary" role="status" aria-hidden="true"></span></div>`;
        const qs = `?targetType=${encodeURIComponent(targetType)}&targetId=${encodeURIComponent(targetId)}&principalType=${encodeURIComponent(principalType)}&principalId=${encodeURIComponent(principalId)}`;
        const res = await fetch(`${endpoint}/effective${qs}`, { headers: getAuthHeaders() });
        const payload = await res.json().catch(() => ({}));
        const data = payload?.data ?? payload?.Data;
        if (!data) { if (body) body.innerHTML = `<div class="alert alert-danger mb-0">${t('ErrorOccurred')}</div>`; return; }
        const allowed = (data.allowedActions || []);
        const badges = allowed.length
            ? allowed.map((a) => `<span class="badge bg-label-success me-1 mb-1">${actionLabel(a)}</span>`).join('')
            : `<span class="text-muted">${t('NoAllowedActions')}</span>`;
        if (body) body.innerHTML = `
            <div class="mb-2"><span class="fw-medium">${t('AllowedActions')}:</span></div>
            <div class="mb-3">${badges}</div>
            <div class="d-flex align-items-center gap-2 text-muted small"><i class="bx bx-info-circle"></i><span>${t('EnforcementMode')}: ${data.mode || ''}</span></div>`;
    };

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/DocumentManagementAccessMatrix/Details/${id}`; },
        edit: ({ id }) => { if (id) window.location.href = `/DocumentManagementAccessMatrix/Edit/${id}`; },
        preview: ({ row }) => openPreview(row),
        delete: ({ row }) => deleteSingle(row)
    };

    const buildRowActions = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const actions = [
            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L.ViewDetails } }
        ];
        if (perms.canPreview) actions.push({ key: 'preview', icon: 'bx bx-shield-quarter', text: L.EffectivePreview, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        if (perms.canManage) actions.push({ key: 'edit', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id } });
        if (perms.canManage) actions.push({ key: 'delete', className: 'text-danger', icon: 'bx bx-trash', text: L.Delete, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        return window.DitenDataTable.renderActions(actions);
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();
        // Resolve principal/company ids → human names before the table first renders.
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
                        console.error('[AccessMatrix SaveView] Failed to save default view.', error);
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
            ajax: { url: `${endpoint}/list`, type: 'GET', headers: getAuthHeaders() },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'targetType', name: 'target' },
                    { data: 'principalType', name: 'principal' },
                    { data: 'effect', name: 'actions' },
                    { data: 'effect', name: 'effect' },
                    { data: 'inheritFromParent', name: 'inheritance' },
                    { data: 'validTo', name: 'validity' },
                    { data: 'status', name: 'status' },
                    { data: 'updatedAt', name: 'updated' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data, type, full) => targetCell(full) },
                    { targets: 3, render: (data, type, full) => principalCell(full) },
                    { targets: 4, render: (data, type, full) => actionsCell(full) },
                    { targets: 5, render: (data) => effectBadge(data) },
                    { targets: 6, render: (data, type, full) => inheritanceBadge(full) },
                    { targets: 7, render: (data, type, full) => validityCell(full) },
                    { targets: 8, render: (data) => statusBadge(data) },
                    { targets: 9, render: (data, type, full) => updatedCell(full) },
                    {
                        targets: -1, title: L.RowActions, searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data, type, full) => buildRowActions(full)
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    perms.canManage ? (L.CreatePolicy || L.AddNew) : null,
                    { href: '/DocumentManagementAccessMatrix/Create' },
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6, 7, 8, 9], colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/DocumentManagementAccessMatrix/Create';
                    });
                    document.getElementById('btnRunPreview')?.addEventListener('click', () => void runPreview());
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount()); }
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

// ── Create / Edit page ──
const AccessMatrixForm = (function () {
    const init = () => {
        const form = document.getElementById('accessPolicyForm');
        if (!form) return;
        const L = window.L10n || {};
        const t = (key) => L[key] || key;
        const endpoint = '/DocumentManagement/AccessMatrix/api';
        const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const mode = form.dataset.mode || 'create';
        const editId = document.querySelector('[data-access-policy-id]')?.dataset.accessPolicyId || '';

        const targetType = document.getElementById('targetType');
        const targetCompany = document.getElementById('targetCompany');
        const targetCompanyGroup = document.getElementById('targetCompanyGroup');
        const targetPick = document.getElementById('targetPick');
        const targetPickGroup = document.getElementById('targetPickGroup');
        const targetId = document.getElementById('targetId');
        const principalType = document.getElementById('principalType');
        const principalPick = document.getElementById('principalPick');
        const principalId = document.getElementById('principalId');

        const COMPANY_SCOPED = ['Company', 'CollectionDefinition', 'CollectionInstance', 'TemplateDocument', 'ControlledDocument'];
        const PICK_FROM_OPTIONS = ['TemplateDocument', 'ControlledDocument', 'TemplateMaster', 'TemplateVariant'];
        const PICK_FROM_COMPANY = ['CollectionDefinition', 'CollectionInstance'];
        const DOC_TYPES = ['TemplateDocument', 'ControlledDocument'];

        const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };
        const get = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
        const emptyToNull = (v) => { const s = String(v || '').trim(); return s ? s : null; };
        const unwrapList = (payload) => {
            const data = payload?.data ?? payload?.Data ?? payload;
            if (Array.isArray(data)) return data;
            for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(data?.[k])) return data[k];
            return [];
        };
        const fetchLookup = async (url) => {
            const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders(), cache: 'no-store' });
            return res.json().catch(() => ({}));
        };
        const appendOption = (select, value, text) => {
            if (!select || !value) return;
            const opt = document.createElement('option');
            opt.value = value; opt.textContent = text || value;
            select.appendChild(opt);
        };
        const refreshSelect2 = (el) => { if (el && window.jQuery?.fn?.select2) window.jQuery(el).trigger('change.select2'); };
        const showGroup = (group, visible) => { if (group) group.classList.toggle('d-none', !visible); };
        const companyName = (e) => get(e, 'displayName', 'DisplayName') || get(e, 'legalName', 'LegalName') || get(e, 'commercialTitle', 'CommercialTitle') || get(e, 'name', 'Name');
        const companyIdOf = (e) => get(e, 'legalEntityId', 'LegalEntityId') || get(e, 'id', 'Id');

        let targetOptions = [];
        let legalEntities = [];
        let users = [];
        let roles = [];
        let tenantTargetId = '';

        const initSelect2 = () => {
            if (!window.jQuery?.fn?.select2) return;
            const jq = window.jQuery;
            jq('#targetType, #targetCompany, #targetPick, #principalType, #principalPick, #actions, #effect, #status').each(function () {
                const $s = jq(this);
                if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
                $s.select2({ dropdownParent: jq(document.body), width: '100%', allowClear: !this.required, placeholder: $s.data('placeholder') || '' });
            });
        };

        const populateCompanySelect = (select) => {
            if (!select) return;
            select.innerHTML = '<option value=""></option>';
            legalEntities.forEach((e) => appendOption(select, companyIdOf(e), companyName(e)));
            refreshSelect2(select);
        };

        const syncTargetId = () => {
            if (!targetId) return;
            const tt = targetType?.value || '';
            let value = '';
            if (tt === 'Tenant') value = tenantTargetId;
            else if (tt === 'Company') value = targetCompany?.value || '';
            else if (targetPick) value = targetPick.value || '';
            targetId.value = value;
        };

        const refreshTargetPick = async () => {
            const tt = targetType?.value || '';
            if (!targetPick) return;
            targetPick.innerHTML = '<option value=""></option>';

            const companyId = targetCompany?.value || '';

            if (PICK_FROM_OPTIONS.includes(tt)) {
                // Tenant-wide options; documents are additionally filtered by the selected company (via the option Scope).
                targetOptions
                    .filter((o) => get(o, 'targetType', 'TargetType') === tt)
                    .filter((o) => !DOC_TYPES.includes(tt) || !companyId || String(get(o, 'scope', 'Scope') || '') === String(companyId))
                    .forEach((o) => appendOption(targetPick, get(o, 'targetId', 'TargetId'), get(o, 'label', 'Label')));
            } else if (PICK_FROM_COMPANY.includes(tt)) {
                if (companyId && tt === 'CollectionInstance') {
                    // CollectionInstanceListItemModel — the folder target id is the folder's own Id.
                    const json = await fetchLookup(`${endpoint}/collection-instances?companyId=${encodeURIComponent(companyId)}`);
                    unwrapList(json).forEach((o) => {
                        const id = get(o, 'id', 'Id') || get(o, 'collectionInstanceId', 'CollectionInstanceId');
                        const label = get(o, 'fullPath', 'FullPath') || get(o, 'name', 'Name') || id;
                        appendOption(targetPick, id, label);
                    });
                } else if (companyId && tt === 'CollectionDefinition') {
                    // Documentation structures derived from the company's folders: distinct BaselineReleaseId (which is
                    // exactly the resolver's structure-level inheritance ancestor). Robust even when no standalone
                    // "active structure" record exists for the company.
                    const json = await fetchLookup(`${endpoint}/collection-instances?companyId=${encodeURIComponent(companyId)}`);
                    const byRelease = new Map();
                    unwrapList(json).forEach((o) => {
                        const brId = get(o, 'baselineReleaseId', 'BaselineReleaseId');
                        if (!brId) return;
                        const key = String(brId);
                        const isRoot = !get(o, 'parentCanonicalId', 'ParentCanonicalId');
                        if (!byRelease.has(key) || isRoot) {
                            byRelease.set(key, { id: brId, label: get(o, 'name', 'Name') || get(o, 'fullPath', 'FullPath') || get(o, 'instanceToken', 'InstanceToken') || brId });
                        }
                    });
                    byRelease.forEach((v) => appendOption(targetPick, v.id, v.label));
                }
            }
            refreshSelect2(targetPick);
        };

        const onTargetTypeChange = async () => {
            const tt = targetType?.value || '';
            showGroup(targetCompanyGroup, COMPANY_SCOPED.includes(tt));
            showGroup(targetPickGroup, PICK_FROM_OPTIONS.includes(tt) || PICK_FROM_COMPANY.includes(tt));
            await refreshTargetPick();
            syncTargetId();
        };

        const refreshPrincipalPick = () => {
            if (!principalPick) return;
            principalPick.innerHTML = '<option value=""></option>';
            const pt = principalType?.value || '';
            if (pt === 'User') {
                users.filter((u) => get(u, 'isActive', 'IsActive') !== false).forEach((u) => {
                    const full = `${get(u, 'firstName', 'FirstName') || ''} ${get(u, 'lastName', 'LastName') || ''}`.trim();
                    const email = get(u, 'email', 'Email') || '';
                    appendOption(principalPick, get(u, 'id', 'Id'), full && email ? `${full} (${email})` : (full || email));
                });
            } else if (pt === 'Role') {
                roles.forEach((r) => appendOption(principalPick, get(r, 'id', 'Id'),
                    get(r, 'name', 'Name') || get(r, 'roleName', 'RoleName') || get(r, 'id', 'Id')));
            } else if (pt === 'Company') {
                legalEntities.forEach((e) => appendOption(principalPick, companyIdOf(e), companyName(e)));
            }
            refreshSelect2(principalPick);
            syncPrincipalId();
        };
        const syncPrincipalId = () => { if (principalId) principalId.value = principalPick?.value || ''; };

        const bind = (select, handler) => {
            if (!select) return;
            if (window.jQuery?.fn?.select2) window.jQuery(select).off('change.am').on('change.am', handler);
            else select.addEventListener('change', handler);
        };

        const loadEdit = async () => {
            if (mode !== 'edit' || !editId) return;
            const payload = await fetchLookup(`${endpoint}/detail/${editId}`);
            const d = payload?.data ?? payload?.Data;
            if (!d) return;
            const setVal = (id, val) => { const el = document.getElementById(id); if (el) { el.value = val ?? ''; if (el.classList.contains('form-select')) refreshSelect2(el); } };
            const tt = d.targetType;
            setVal('targetType', tt);

            // Pre-select the company first so the company-scoped target picker is populated with the matching options.
            // The backend resolves the target's owning company for every resource type (folder / document / template /
            // master / variant / company), so this also pre-fills CollectionInstance/CollectionDefinition folder targets.
            if (targetCompany && d.targetCompanyId) {
                targetCompany.value = String(d.targetCompanyId); refreshSelect2(targetCompany);
            }
            await onTargetTypeChange(); // rebuilds the target picker options for the (now company-scoped) type
            // Pre-select the saved target in the picker, then keep the read-only id field authoritative.
            if (targetPick && (PICK_FROM_OPTIONS.includes(tt) || PICK_FROM_COMPANY.includes(tt))) {
                targetPick.value = d.targetId ?? ''; refreshSelect2(targetPick);
            }
            if (targetId) targetId.value = d.targetId ?? '';

            setVal('principalType', d.principalType);
            refreshPrincipalPick();
            // Pre-select the saved principal in the picker (options are already loaded), keep the id authoritative.
            if (principalPick) { principalPick.value = d.principalId ?? ''; refreshSelect2(principalPick); }
            if (principalId) principalId.value = d.principalId ?? '';
            setVal('effect', d.effect); setVal('status', d.status);
            if (Array.isArray(d.actions) && window.jQuery?.fn?.select2) window.jQuery('#actions').val(d.actions).trigger('change.select2');
            const inherit = document.getElementById('inheritFromParent'); if (inherit) inherit.checked = !!d.inheritFromParent;
            if (d.validFrom) setVal('validFrom', String(d.validFrom).slice(0, 16));
            if (d.validTo) setVal('validTo', String(d.validTo).slice(0, 16));
            setVal('reason', d.reason);
        };

        const initLookups = async () => {
            initSelect2();
            bind(targetType, () => { void onTargetTypeChange(); });
            bind(targetCompany, () => { void refreshTargetPick().then(syncTargetId); });
            bind(targetPick, syncTargetId);
            bind(principalType, refreshPrincipalPick);
            bind(principalPick, syncPrincipalId);

            const [opts, le, us, rl] = await Promise.allSettled([
                fetchLookup(`${endpoint}/target-options`),
                fetchLookup(`${endpoint}/legal-entities`),
                fetchLookup(`${endpoint}/users`),
                fetchLookup(`${endpoint}/roles`)
            ]);
            if (opts.status === 'fulfilled') targetOptions = unwrapList(opts.value);
            if (le.status === 'fulfilled') legalEntities = unwrapList(le.value);
            if (us.status === 'fulfilled') users = unwrapList(us.value);
            if (rl.status === 'fulfilled') roles = unwrapList(rl.value);
            tenantTargetId = (targetOptions.find((o) => get(o, 'targetType', 'TargetType') === 'Tenant')
                && (get(targetOptions.find((o) => get(o, 'targetType', 'TargetType') === 'Tenant'), 'targetId', 'TargetId'))) || '';

            populateCompanySelect(targetCompany);
            await onTargetTypeChange();
            refreshPrincipalPick();
            await loadEdit();
        };

        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
            const fd = new FormData(form);
            const dt = (v) => { const s = String(v || '').trim(); return s ? new Date(s).toISOString() : null; };
            const payload = {
                targetType: emptyToNull(fd.get('targetType')),
                targetId: emptyToNull(fd.get('targetId')),
                principalType: emptyToNull(fd.get('principalType')),
                principalId: emptyToNull(fd.get('principalId')),
                actions: (window.jQuery ? (window.jQuery('#actions').val() || []) : []),
                effect: emptyToNull(fd.get('effect')),
                inheritFromParent: document.getElementById('inheritFromParent')?.checked === true,
                validFrom: dt(fd.get('validFrom')),
                validTo: dt(fd.get('validTo')),
                status: emptyToNull(fd.get('status')),
                reason: emptyToNull(fd.get('reason'))
            };
            const body = new FormData();
            body.append('__RequestVerificationToken', token());
            body.append('payloadJson', JSON.stringify(payload));
            const url = mode === 'edit' && editId ? `${endpoint}/update/${editId}` : `${endpoint}/create`;
            const res = await fetch(url, { method: 'POST', body });
            const result = await res.json().catch(() => ({}));
            if (!res.ok || result?.isSuccessful === false) {
                if (window.DitenUnauthorized?.handle(res, result)) return;
                const message = Array.isArray(result?.errors) && result.errors.length ? result.errors[0] : t('ErrorOccurred');
                window.showToast?.(message, 'error');
                return;
            }
            const saved = result?.data ?? result?.Data;
            window.location.href = `/DocumentManagementAccessMatrix/Details/${saved.id}`;
        });

        void initLookups();
    };
    return { init };
})();

// ── Details page ──
const AccessMatrixDetails = (function () {
    const init = async () => {
        const host = document.querySelector('.access-matrix-details[data-access-policy-id]');
        if (!host) return;
        const L = window.L10n || {};
        const t = (key) => L[key] || key;
        const endpoint = '/DocumentManagement/AccessMatrix/api';
        const id = host.dataset.accessPolicyId;

        const upper = (v) => String(v || '').toUpperCase();
        const targetTypeLabel = (v) => t(`Target${String(v || '').charAt(0).toUpperCase()}${String(v || '').slice(1)}`) || v;
        const principalTypeLabel = (v) => t(`Principal${String(v || '').charAt(0).toUpperCase()}${String(v || '').slice(1)}`) || v;
        const actionLabel = (v) => t(`Action${v}`) || v;
        const effectBadge = (v) => `<span class="badge bg-label-${upper(v) === 'DENY' ? 'danger' : 'success'}">${upper(v) === 'DENY' ? t('EffectDeny') : t('EffectAllow')}</span>`;
        const statusBadge = (v) => {
            const s = upper(v); const map = { ACTIVE: 'success', DISABLED: 'warning', ARCHIVED: 'secondary' };
            const label = { ACTIVE: t('StatusActive'), DISABLED: t('StatusDisabled'), ARCHIVED: t('StatusArchived') }[s] || v;
            return `<span class="badge bg-label-${map[s] || 'secondary'}">${label}</span>`;
        };

        const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
        const g = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
        const unwrapList = (json) => {
            const dd = json?.data ?? json?.Data ?? json;
            if (Array.isArray(dd)) return dd;
            for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(dd?.[k])) return dd[k];
            return [];
        };
        const fetchJson = async (path) => { try { return await (await fetch(`${endpoint}/${path}`, { credentials: 'same-origin', headers: getAuthHeaders() })).json(); } catch { return {}; } };

        const res = await fetch(`${endpoint}/detail/${id}`, { credentials: 'same-origin' });
        const payload = await res.json().catch(() => ({}));
        const d = payload?.data ?? payload?.Data;
        if (!d) return;

        // Resolve principal id → human name (and Company targets → legal entity name).
        const resolvePrincipal = async () => {
            const pt = upper(d.principalType);
            if (pt === 'USER') {
                const m = unwrapList(await fetchJson('users')).find((it) => String(g(it, 'id', 'Id')) === String(d.principalId));
                if (!m) return null;
                const full = `${g(m, 'firstName', 'FirstName') || ''} ${g(m, 'lastName', 'LastName') || ''}`.trim();
                const email = g(m, 'email', 'Email') || '';
                return full && email ? `${full} (${email})` : (full || email || null);
            }
            if (pt === 'ROLE') {
                const m = unwrapList(await fetchJson('roles')).find((it) => String(g(it, 'id', 'Id')) === String(d.principalId));
                return m ? (g(m, 'name', 'Name') || g(m, 'roleName', 'RoleName')) : null;
            }
            if (pt === 'COMPANY') {
                const m = unwrapList(await fetchJson('legal-entities')).find((it) => String(g(it, 'legalEntityId', 'LegalEntityId') || g(it, 'id', 'Id')) === String(d.principalId));
                return m ? (g(m, 'displayName', 'DisplayName') || g(m, 'legalName', 'LegalName') || g(m, 'name', 'Name')) : null;
            }
            return null;
        };
        const resolveTargetCompany = async () => {
            if (upper(d.targetType) !== 'COMPANY') return null;
            const m = unwrapList(await fetchJson('legal-entities')).find((it) => String(g(it, 'legalEntityId', 'LegalEntityId') || g(it, 'id', 'Id')) === String(d.targetId));
            return m ? (g(m, 'displayName', 'DisplayName') || g(m, 'legalName', 'LegalName') || g(m, 'name', 'Name')) : null;
        };
        const [principalDisplayName, targetCompanyName] = await Promise.all([resolvePrincipal(), resolveTargetCompany()]);
        const targetDisplay = targetCompanyName || d.targetLabel || d.targetId;

        document.getElementById('detailTitle').textContent = targetDisplay;
        document.getElementById('detailSubtitle').textContent = `${targetTypeLabel(d.targetType)} · ${principalTypeLabel(d.principalType)}`;

        const renderFields = (elementId, fields) => {
            const element = document.getElementById(elementId);
            if (!element) return;
            element.innerHTML = fields.map(([key, value]) => `
                <dt class="col-sm-5">${t(key)}</dt>
                <dd class="col-sm-7">${value || t('NotAvailable')}</dd>`).join('');
        };

        renderFields('targetDetailList', [
            ['TargetType', targetTypeLabel(d.targetType)],
            ['Target', targetDisplay],
            ['TargetId', d.targetId]
        ]);
        renderFields('principalDetailList', [
            ['PrincipalType', principalTypeLabel(d.principalType)],
            ['Principal', principalDisplayName || d.principalId],
            ['PrincipalId', d.principalId]
        ]);
        renderFields('permissionsDetailList', [
            ['Actions', (d.actions || []).map((a) => `<span class="badge bg-label-primary me-1 mb-1">${actionLabel(a)}</span>`).join('') || t('NotAvailable')],
            ['Effect', effectBadge(d.effect)],
            ['InheritFromParent', d.inheritFromParent ? t('Yes') : t('No')]
        ]);
        renderFields('validityDetailList', [
            ['Status', statusBadge(d.status)],
            ['ValidFrom', d.validFrom ? String(d.validFrom).slice(0, 10) : ''],
            ['ValidTo', d.validTo ? String(d.validTo).slice(0, 10) : ''],
            ['Expired', d.isExpired ? t('Yes') : t('No')],
            ['Reason', d.reason]
        ]);
    };
    return { init };
})();

document.addEventListener('DOMContentLoaded', () => {
    AccessMatrixList.init();
    AccessMatrixForm.init();
    AccessMatrixDetails.init();
});
