/**
 * Golden Reference Slim — DataTables Index Script
 * Diten ERP vNext | DevEnablement/GoldenReferenceSlim
 *
 * SLIM PATTERN (≤8 form fields):
 *   - Create / Edit → offcanvas panel (AJAX submit, no page navigation)
 *   - Delete lifecycle: clearSelection → ajax.reload(cb, false) → showToast
 *   - Save View: personalizationClient via /api/personalization/*
 *   - ColReorder: ':gt(1):not(:last-child)'
 *   - L10n: window.L10n (PascalCase)
 */
'use strict';

const GoldenReferenceSlimList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-goldenreferenceslim');
    const apiUrl = window.API?.deven;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'DevEnablement', pageKey: 'GoldenReferenceSlim' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 6];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [], referenceType: [], priority: '' };
    let L = window.L10n || {};

    // ─── Offcanvas state ────────────────────────────────────────────────────
    let editingId = null;
    let responsiveReturnModalEl = null;
    let suppressResponsiveReturn = false;

    const getOcCreateEditInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const getOcDetailsInstance = () => {
        const el = document.getElementById('offcanvasDetailsPreview');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    // ─── L10n ───────────────────────────────────────────────────────────────
    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
        }
    };

    // ─── Auth ────────────────────────────────────────────────────────────────
    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const getAntiForgeryToken = () =>
        document.querySelector('#formGoldenReferenceSlim input[name="__RequestVerificationToken"]')?.value || '';

    // ─── Normalize helpers ───────────────────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const sortNormalizedArray = (v) => normalizeArray(v).slice().sort((a, b) => a.localeCompare(b));
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [], referenceType: [], priority: '' });
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            status: normalizeArray(source.status),
            referenceType: normalizeArray(source.referenceType),
            priority: normalizeString(source.priority)
        };
    };
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;

    // ─── Filter matching ─────────────────────────────────────────────────────
    const matchesMultiFilter = (selected, actual) => {
        const norm = normalizeArray(selected);
        return !norm.length || norm.includes(normalizeString(actual));
    };
    const matchesSingleFilter = (selected, actual) => {
        const norm = normalizeString(selected);
        return !norm || normalizeString(actual) === norm;
    };
    const matchesStatusFilter = (selected, isActive) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        return norm.includes(isActive ? 'Active' : 'Passive');
    };

    // ─── Column visibility / order helpers ──────────────────────────────────
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
        if (!n || typeof api?.colReorder?.order !== 'function') return;
        api.colReorder.order(n, true);
    };

    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };

    // ─── Search input ────────────────────────────────────────────────────────
    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };

    // ─── View state ──────────────────────────────────────────────────────────
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
            console.error('[GoldenReferenceSlim SaveView] Failed to load saved views.', error);
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

    // ─── Inline filter UI ────────────────────────────────────────────────────
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.slimFilterBound === '1') return;
        dtTableEl.dataset.slimFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesStatusFilter(appliedFilters.status, row.isActive)
                && matchesMultiFilter(appliedFilters.referenceType, row.referenceType)
                && matchesSingleFilter(appliedFilters.priority, row.priority);
        });
    };

    // ─── Select2 multi-summary ───────────────────────────────────────────────
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

        const clampDropdown = () => {
            requestAnimationFrame(() => {
                const dd = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                if (!dd) return;
                const rect = dd.getBoundingClientRect();
                const pad = 8;
                let dx = 0, dy = 0;
                if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
                if (rect.left < pad) dx += pad - rect.left;
                if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
                if (rect.top < pad) dy += pad - rect.top;
                if (!dx && !dy) return;
                const cs = window.getComputedStyle(dd);
                const baseLeft = parseFloat(cs.left) || rect.left + window.scrollX;
                const baseTop = parseFloat(cs.top) || rect.top + window.scrollY;
                if (dx) dd.style.left = `${baseLeft + dx}px`;
                if (dy) dd.style.top = `${baseTop + dy}px`;
                dd.style.transform = 'none';
            });
        };

        $('#filterStatus, #filterReferenceType').each(function () {
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
            $s.on('select2:open', clampDropdown);
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(() => syncMultiSelectSummary($s));
        });

        $('#filterPriority').each(function () {
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
            $s.on('select2:open', clampDropdown);
        });
    };

    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterReferenceType').val(normalizeArray(values.referenceType)).trigger('change');
        $('#filterPriority').val(values.priority || '').trigger('change');
    };

    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.referenceType, appliedFilters.priority].filter(hasFilterValue).length;

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

    const loadLookupOptions = async () => {
        const refSelect = document.getElementById('filterReferenceType');
        const prioritySelect = document.getElementById('filterPriority');
        if (!refSelect || !prioritySelect) return;
        try {
            const res = await fetch('/GoldenReferenceSlim/lookups', {
                method: 'GET',
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            if (!res.ok) return;
            const data = await res.json();
            const refTypes = Array.isArray(data?.referenceTypes) ? data.referenceTypes : [];
            const priorities = Array.isArray(data?.priorities) ? data.priorities : [];

            refSelect.innerHTML = '';
            refTypes.forEach((item) => {
                if (!item?.value) return;
                const opt = document.createElement('option');
                opt.value = item.value;
                opt.textContent = item.text || item.value;
                refSelect.appendChild(opt);
            });

            prioritySelect.innerHTML = '';
            const showAll = document.createElement('option');
            showAll.value = '';
            showAll.textContent = L.ShowAll || '';
            prioritySelect.appendChild(showAll);
            priorities.forEach((item) => {
                if (item?.value == null) return;
                const opt = document.createElement('option');
                opt.value = String(item.value);
                opt.textContent = item.text || `${L.LevelPrefix || ''} ${item.value}`.trim();
                prioritySelect.appendChild(opt);
            });
        } catch (error) {
            console.error('[GoldenReferenceSlim Lookup] Failed.', error);
        }
    };

    const setupFilters = async (api) => {
        await loadLookupOptions();
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                referenceType: $('#filterReferenceType').val() || [],
                priority: document.getElementById('filterPriority')?.value || ''
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

    // ─── Status badge ─────────────────────────────────────────────────────────
    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });

    const getReferenceTypeMap = () => ({
        'Standard': L.ReferenceTypeStandard || 'Standard',
        'Custom': L.ReferenceTypeCustom || 'Custom',
        'Pro': L.ReferenceTypePro || 'Pro'
    });

    // ─── Quick View (Details) offcanvas ──────────────────────────────────────
    const tryParseRowJson = (el) => {
        if (!el) return null;
        const raw = el.getAttribute('data-json');
        if (!raw) return null;
        try { return JSON.parse(raw.replace(/&#39;/g, "'")); } catch (e) { return null; }
    };

    const closeResponsiveModal = (returnOnOffcanvasClose = false) => {
        const modalEl = document.querySelector('.modal.dtr-bs-modal.show');
        if (!modalEl) return false;

        if (returnOnOffcanvasClose) {
            responsiveReturnModalEl = modalEl;
            suppressResponsiveReturn = false;
        }

        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
        else modalEl.querySelector('[data-bs-dismiss="modal"], .btn-close')?.click();

        return true;
    };

    const restoreResponsiveModalAfterCancel = () => {
        if (!responsiveReturnModalEl || suppressResponsiveReturn) {
            responsiveReturnModalEl = null;
            suppressResponsiveReturn = false;
            return;
        }

        const modalEl = responsiveReturnModalEl;
        responsiveReturnModalEl = null;
        window.setTimeout(() => bootstrap.Modal.getOrCreateInstance(modalEl).show(), 120);
    };

    const populateDetailsOffcanvas = (data) => {
        if (!data) return;
        document.getElementById('oc-title').innerText = data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.code || '-';
        document.getElementById('oc-code').innerText = data.code || '-';
        document.getElementById('oc-name').innerText = data.name || '-';
        document.getElementById('oc-type').innerText = getReferenceTypeMap()[data.referenceType] || data.referenceType || '-';
        document.getElementById('oc-priority').innerText = data.priority != null ? String(data.priority) : '-';
        document.getElementById('oc-desc').innerText = data.description || '-';

        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(!!data.isActive)] || { title: L.Unknown, class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        const priorityDot = document.getElementById('oc-priority-dot');
        if (priorityDot) {
            const priority = Number(data.priority || 0);
            priorityDot.className = 'backbone-priority-dot';
            if (priority >= 70) priorityDot.classList.add('is-high');
            else if (priority > 0) priorityDot.classList.add('is-medium');
        }

        const editBtn = document.getElementById('oc-btn-edit');
        if (editBtn) {
            editBtn.dataset.editId = data.id;
        }
    };

    // ─── Create/Edit offcanvas ────────────────────────────────────────────────
    const resetCreateEditForm = () => {
        const form = document.getElementById('formGoldenReferenceSlim');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
        document.getElementById('slimItemId').value = '';
        document.getElementById('slimCode').value = '';
        document.getElementById('slimName').value = '';
        document.getElementById('slimDescription').value = '';
        document.getElementById('slimPriority').value = '0';
        document.getElementById('slimIsActive').checked = true;

        const refTypeEl = document.getElementById('slimReferenceType');
        if (refTypeEl) refTypeEl.value = '';
        if (window.jQuery && $('#slimReferenceType').hasClass('select2-hidden-accessible')) {
            $('#slimReferenceType').val('').trigger('change');
        }

        document.getElementById('formGoldenReferenceSlimAlert').classList.add('d-none');
    };

    const openCreateOffcanvas = () => {
        editingId = null;
        resetCreateEditForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSaveSlim');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        getOcCreateEditInstance()?.show();
    };

    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetCreateEditForm();
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.EditItem || L.Edit || '';
        const saveBtn = document.getElementById('btnSaveSlim');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';

        try {
            const res = await fetch(`/GoldenReferenceSlim/get/${id}`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Failed to load item.');

            const d = json.data;
            document.getElementById('slimItemId').value = d.id || '';
            document.getElementById('slimCode').value = d.code || '';
            document.getElementById('slimName').value = d.name || '';
            document.getElementById('slimDescription').value = d.description || '';
            document.getElementById('slimPriority').value = d.priority ?? 0;
            document.getElementById('slimIsActive').checked = !!d.isActive;

            if (window.jQuery && $('#slimReferenceType').hasClass('select2-hidden-accessible')) {
                $('#slimReferenceType').val(d.referenceType || '').trigger('change');
            } else {
                document.getElementById('slimReferenceType').value = d.referenceType || '';
            }
        } catch (error) {
            console.error('[GoldenReferenceSlim] Failed to load item for edit.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }

        getOcCreateEditInstance()?.show();
    };

    const initOffcanvasSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $el = $('#slimReferenceType');
        if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
        /*
         * THE PLACEHOLDER COMES FROM THE EMPTY OPTION, which is the only place it is localized.
         *
         * Measured on the running page: this read `$el.data('placeholder') || ''` and the markup carries no
         * data-placeholder, so select2 was handed an EMPTY placeholder — and an empty placeholder is not "no
         * placeholder". select2 then renders `<span class="select2-selection__placeholder"></span>` INSTEAD of
         * the option's own text, so the localized "Seçiniz..." sitting in `<option value="">` never reached the
         * screen and the field read as a blank box with an arrow.
         *
         * The full-page reference never had this because it does not override the placeholder at all. Reading
         * the option's text keeps ONE localized source for the string — the resx behind the markup — instead of
         * a second copy in a data- attribute that no language file would ever update.
         */
        $el.select2({
            dropdownParent: $('#offcanvasCreateEdit'),
            placeholder: $el.data('placeholder') || $el.find('option[value=""]').text() || '',
            allowClear: true,
            width: '100%'
        });
    };

    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formGoldenReferenceSlimAlert');
        if (!alertEl) return;
        alertEl.innerHTML = Array.isArray(errors) ? errors.map((e) => `<div>${e}</div>`).join('') : (errors || L.FormValidationError || '');
        alertEl.classList.remove('d-none');
    };

    const submitCreateEditForm = async () => {
        const form = document.getElementById('formGoldenReferenceSlim');
        if (!form) return;

        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showFormErrors([L.FormValidationError || '']);
            return;
        }

        const formData = new FormData(form);
        const isEdit = !!editingId;
        const url = isEdit ? `/GoldenReferenceSlim/edit/${editingId}` : '/GoldenReferenceSlim/create';

        const saveBtn = document.getElementById('btnSaveSlim');
        if (saveBtn) { saveBtn.disabled = true; }

        try {
            const res = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: formData
            });
            const json = await res.json();

            if (json.success) {
                suppressResponsiveReturn = true;
                responsiveReturnModalEl = null;
                getOcCreateEditInstance()?.hide();
                reloadWithSuccessToast(isEdit ? 'RecordUpdated' : 'RecordCreated');
            } else {
                showFormErrors(json.errors);
            }
        } catch (error) {
            console.error('[GoldenReferenceSlim] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred]);
        } finally {
            if (saveBtn) { saveBtn.disabled = false; }
        }
    };

    // ─── Bulk / selection ────────────────────────────────────────────────────
    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all'
    };

    const getSelectedIds = () => window.DitenDataTable.getSelectedIds(dtTableEl, bulkOptions.checkboxSelector);
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);

    // ─── Event bindings ───────────────────────────────────────────────────────
    const bindEvents = () => {
        document.addEventListener('click', (e) => {
            const quickViewBtn = e.target.closest('.js-quick-view');
            const editBtn = e.target.closest('.js-edit-item');
            const deleteBtn = e.target.closest('.delete-record');
            const actionEl = quickViewBtn || editBtn || deleteBtn;
            if (!actionEl) return;

            const inTable = !!actionEl.closest('.datatables-goldenreferenceslim');
            const inResponsiveModal = !!actionEl.closest('.modal.dtr-bs-modal');
            if (!inTable && !inResponsiveModal) return;

            if (quickViewBtn) {
                e.preventDefault();
                e.stopPropagation();

                const data = tryParseRowJson(quickViewBtn);
                if (!data) return;

                populateDetailsOffcanvas(data);
                const wasModalOpen = closeResponsiveModal(inResponsiveModal);
                window.setTimeout(() => getOcDetailsInstance()?.show(), wasModalOpen ? 160 : 0);
                return;
            }

            if (editBtn) {
                e.preventDefault();
                e.stopPropagation();

                const id = editBtn.dataset.id;
                const wasModalOpen = closeResponsiveModal(inResponsiveModal);
                if (id) window.setTimeout(() => openEditOffcanvas(String(id)), wasModalOpen ? 160 : 0);
                return;
            }

            if (!deleteBtn) return;
            e.preventDefault();
            e.stopPropagation();

            let data = tryParseRowJson(deleteBtn);
            if (!data && inTable) {
                let rowEl = deleteBtn.closest('tr');
                if (rowEl?.classList.contains('child')) rowEl = rowEl.previousElementSibling;
                data = rowEl ? dt.row(rowEl).data() : null;
            }

            if (!data?.id) return;

            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(`${apiUrl}/api/golden-reference-slim/${data.id}`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!res.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: data.name, type: 'danger', confirmButtonText: L.Delete });
        });

        document.getElementById('btnBulkDelete')?.addEventListener('click', async () => {
            const ids = getSelectedIds();
            if (!ids.length) return;
            const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
            window.showConfirm?.(confirmText, async () => {
                try {
                    const res = await fetch(`${apiUrl}/api/golden-reference-slim/bulk`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: getAuthHeaders(true),
                        body: JSON.stringify(ids)
                    });
                    if (!res.ok) throw new Error('Bulk delete failed.');
                    reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L.Delete });
        });

        document.getElementById('btnSaveSlim')?.addEventListener('click', submitCreateEditForm);

        // QuickView → Edit button opens create/edit offcanvas
        document.getElementById('oc-btn-edit')?.addEventListener('click', () => {
            const id = document.getElementById('oc-btn-edit')?.dataset.editId;
            if (id) openEditOffcanvas(id);
        });

        // Init Select2 inside offcanvas when it opens
        document.getElementById('offcanvasCreateEdit')?.addEventListener('show.bs.offcanvas', () => {
            setTimeout(initOffcanvasSelect2, 50);
        });

        document.getElementById('offcanvasCreateEdit')?.addEventListener('hidden.bs.offcanvas', restoreResponsiveModalAfterCancel);
        document.getElementById('offcanvasDetailsPreview')?.addEventListener('hidden.bs.offcanvas', restoreResponsiveModalAfterCancel);
    };

    // ─── DataTable init ───────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[GoldenReferenceSlim] window.API.deven is required.'); return; }

        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: () => window.showToast?.(L.ComingSoon, 'warning')
            },
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
                        console.error('[GoldenReferenceSlim SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        // DitenDataTable wraps the DataTables v2 constructor and shared defaults:
        // new DataTable(...)
        // window.DtDefaults.create(...)
        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: {
                url: apiUrl + '/api/golden-reference-slim',
                type: 'GET',
                xhrFields: { withCredentials: true }
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'code', name: 'code' },
                    { data: 'name', name: 'name' },
                    { data: 'referenceType', name: 'referenceType' },
                    { data: 'priority', name: 'priority' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        targets: 4,
                        render: (data) => {
                            if (!data) return '';
                            const localized = getReferenceTypeMap()[data] || data;
                            return `<span class="badge bg-label-info">${localized}</span>`;
                        }
                    },
                    {
                        targets: 6,
                        render: (data, type) => {
                            const status = getStatusMap()[String(!!data)] || { title: L.Unknown, class: 'bg-label-primary' };
                            return type === 'display' ? window.DitenDataTable.renderStatusBadge(data, getStatusMap()) : status.title;
                        }
                    },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            return window.DitenDataTable.renderActions([
                                {
                                    className: 'js-quick-view me-1',
                                    icon: 'bx bx-show',
                                    attrs: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#offcanvasDetailsPreview',
                                        'data-json': rowJson,
                                        'title': L.QuickView
                                    }
                                },
                                {
                                    className: 'js-edit-item',
                                    icon: 'bx bx-edit',
                                    text: L.Edit,
                                    attrs: { 'data-id': full.id, 'data-json': rowJson }
                                },
                                {
                                    className: 'delete-record text-danger',
                                    icon: 'bx bx-trash',
                                    text: L.Delete,
                                    attrs: { 'data-json': rowJson }
                                }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew,
                    {},
                    extraButtons,
                    { exportColumns: [2, 3, 4, 5, 6], colvisColumns: [2, 3, 4, 5, 6] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    // Bind Add New button after DT renders it — must NOT use addNewAttr onclick (DT calls it at init)
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        openCreateOffcanvas();
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
            registerTableFilters();
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => GoldenReferenceSlimList.init());
