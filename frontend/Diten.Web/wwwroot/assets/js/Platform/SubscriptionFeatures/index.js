'use strict';

/**
 * Subscription Features — Two-tab page (R2)
 *   Tab 1: Categories → GoldenReference DataTable + offcanvas create/edit + archive
 *   Tab 2: Features    → GoldenReference DataTable; create/edit/details = full-page routes
 */
(function () {
    const l10n = () => window.L10n || {};
    const state = { categories: [], categoryMap: {} };

    const els = {
        error: document.getElementById('sf-error'),
        // Category offcanvas
        categoryOffcanvasEl: document.getElementById('offcanvasCategoryCreateEdit'),
        categoryLabel: document.getElementById('offcanvasCategoryLabel'),
        categoryAlert: document.getElementById('sf-category-alert'),
        categoryForm: document.getElementById('sf-category-form'),
        categoryId: document.getElementById('sf-category-id'),
        categoryRowVersion: document.getElementById('sf-category-row-version'),
        categoryCode: document.getElementById('sf-category-code'),
        categoryName: document.getElementById('sf-category-name'),
        categoryDescription: document.getElementById('sf-category-description'),
        categorySort: document.getElementById('sf-category-sort'),
        categoryStatus: document.getElementById('sf-category-status'),
        saveCategory: document.getElementById('sf-save-category')
    };

    const getCategoryOffcanvas = () =>
        els.categoryOffcanvasEl && window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(els.categoryOffcanvasEl) : null;

    const setVisible = (el, visible) => { if (el) el.classList.toggle('d-none', !visible); };
    const safe = (v) => (v === null || v === undefined ? '' : String(v));
    const escapeHtml = (v) => safe(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    const showError = (message) => {
        if (!els.error) return;
        els.error.textContent = message || l10n().ErrorOccurred || 'Error';
        setVisible(els.error, true);
    };
    const showToast = (message, type) => { if (window.showToast) window.showToast(message, type || 'info'); else if (type === 'error') showError(message); };

    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return {};
        try { return JSON.parse(text); } catch { return { errors: [text] }; }
    };
    const api = async (url, options) => {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        const json = await parseEnvelope(response);
        if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
            const errors = json.errors || json.Errors || [];
            const message = errors.length ? errors.join(' ') : response.status === 403 ? l10n().PermissionDenied : l10n().GatewayError;
            const error = new Error(message || 'request_failed');
            error.status = response.status;
            error.errors = errors;
            throw error;
        }
        return json.data || json.Data || json;
    };

    const tryParseRowJson = (el) => {
        if (!el) return null;
        const raw = el.getAttribute('data-json');
        if (!raw) return null;
        try { return JSON.parse(raw.replace(/&#39;/g, "'")); } catch { return null; }
    };

    const featureStatusBadge = (status) => {
        const map = { Active: 'bg-label-success', Inactive: 'bg-label-secondary', Draft: 'bg-label-info', Deprecated: 'bg-label-warning', Archived: 'bg-label-dark' };
        const label = l10n()[safe(status)] || safe(status);
        return `<span class="badge ${map[safe(status)] || 'bg-label-secondary'}">${escapeHtml(label)}</span>`;
    };
    const categoryStatusBadge = (status) => {
        const map = { Active: 'bg-label-success', Inactive: 'bg-label-secondary', Archived: 'bg-label-dark' };
        const labelMap = { Active: l10n().Active, Inactive: l10n().Inactive, Archived: l10n().Archived };
        const label = labelMap[safe(status)] || safe(status);
        return `<span class="badge ${map[safe(status)] || 'bg-label-secondary'}">${escapeHtml(label)}</span>`;
    };

    const loadCategories = async () => {
        const data = await api('/Platform/SubscriptionFeatures/api/categories').catch(() => []);
        state.categories = Array.isArray(data) ? data : [];
        state.categoryMap = {};
        state.categories.forEach((c) => { state.categoryMap[safe(c.id || c.Id)] = safe(c.displayName || c.DisplayName); });
        return state.categories;
    };

    // ─── Inline-filter helpers (shared) ───────────────────────────────────────
    const initFilterSelect2 = (selector) => {
        if (!window.jQuery || !$.fn.select2) return;
        const $s = $(selector);
        if (!$s.length) return;
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2({
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            placeholder: $s.data('placeholder') || '',
            minimumResultsForSearch: Infinity,
            width: 'element',
            closeOnSelect: false
        });
    };
    const mountFilter = (tabSelector, hostId) => {
        const host = document.getElementById(hostId);
        const filterBtn = document.querySelector(`${tabSelector} .dt-filter-btn`);
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row');
        if (host && toolbarRow) { toolbarRow.insertAdjacentElement('afterend', host); host.classList.add('px-3'); }
    };
    const toggleCollapse = (id) => { const el = document.getElementById(id); if (el) bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).toggle(); };
    const hideCollapse = (id) => { const el = document.getElementById(id); if (el) bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide(); };

    // ════════════════════════════════════════════════════════════════════════
    //  CATEGORIES TAB
    // ════════════════════════════════════════════════════════════════════════
    const catState = { dt: null, statusFilter: [], filterBound: false };
    const catTableEl = document.querySelector('.datatables-feature-categories');

    const registerCategoryFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || catState.filterBound) return;
        catState.filterBound = true;
        $.fn.dataTable.ext.search.push((settings, _d, _i, rowData) => {
            if (settings.nTable !== catTableEl) return true;
            const sel = catState.statusFilter;
            return !sel.length || sel.includes(safe(rowData?.status || rowData?.Status));
        });
    };

    const initCategoryTable = () => {
        if (!catTableEl || catState.dt || !window.DitenDataTable || !window.DtDefaults) return;
        registerCategoryFilter();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: l10n().Filter, 'aria-controls': 'catInlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleCollapse('catInlineFilterCollapse')
            }
        };
        catState.dt = window.DitenDataTable.createCrudTable({
            tableEl: catTableEl,
            bulk: {},
            ajax: { url: '/Platform/SubscriptionFeatures/api/categories', type: 'GET' },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: null, name: 'control', defaultContent: '' },
                    { data: 'categoryCode', name: 'categoryCode' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'sortOrder', name: 'sortOrder' },
                    { data: 'status', name: 'status' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (d) => `<span class="fw-medium text-heading">${escapeHtml(d ?? '')}</span>` },
                    { targets: 3, className: 'text-center', render: (d) => escapeHtml(d ?? 0) },
                    { targets: 4, render: (d, type) => type === 'display' ? categoryStatusBadge(d) : safe(d) },
                    {
                        targets: -1, title: l10n().Actions, orderable: false, searchable: false, className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
                            const archived = safe(full.status) === 'Archived';
                            return window.DitenDataTable.renderActions([
                                { className: 'js-cat-edit', icon: 'bx bx-edit', text: l10n().Edit, attrs: { 'data-json': rowJson, 'title': l10n().Edit } },
                                { className: 'js-cat-archive text-danger', icon: 'bx bx-archive-in', text: l10n().ArchiveCategory, visible: !archived, attrs: { 'data-json': rowJson } }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(l10n().AddNew, {}, extraButtons, { exportColumns: [1, 2, 3, 4], colvisColumns: [1, 2, 3, 4] }),
                initComplete: function () {
                    setVisible(document.getElementById('cat-skeleton-loader'), false);
                    document.getElementById('cat-table-wrap')?.classList.remove('d-none');
                    mountFilter('#tabCategories', 'catInlineFilterHost');
                    initFilterSelect2('#catFilterStatus');
                    document.getElementById('catBtnFilterApply')?.addEventListener('click', () => { catState.statusFilter = $('#catFilterStatus').val() || []; catState.dt?.draw(); hideCollapse('catInlineFilterCollapse'); });
                    document.getElementById('catBtnFilterReset')?.addEventListener('click', () => { catState.statusFilter = []; $('#catFilterStatus').val(null).trigger('change'); catState.dt?.draw(); });
                    document.querySelector('#tabCategories .add-new')?.addEventListener('click', (e) => { e.preventDefault(); openCreateCategory(); });
                    try { this.api().columns.adjust(); } catch (e) { }
                }
            }
        });
    };

    // ─── Category offcanvas ────────────────────────────────────────────────────
    const clearInvalid = (form) => {
        if (!form) return;
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form.querySelectorAll('.invalid-feedback').forEach((el) => { el.textContent = ''; });
    };
    const setInvalid = (input, message) => {
        if (!input) return;
        input.classList.add('is-invalid');
        const fb = input.parentElement ? input.parentElement.querySelector('.invalid-feedback') : null;
        if (fb) fb.textContent = message;
    };
    const resetCategoryForm = () => {
        if (els.categoryForm) els.categoryForm.reset();
        clearInvalid(els.categoryForm);
        setVisible(els.categoryAlert, false);
        if (els.categoryId) els.categoryId.value = '';
        if (els.categoryRowVersion) els.categoryRowVersion.value = '';
        if (els.categoryStatus) els.categoryStatus.value = 'Active';
        if (els.categoryCode) els.categoryCode.readOnly = false;
    };
    const openCreateCategory = () => {
        resetCategoryForm();
        if (els.categoryLabel) els.categoryLabel.textContent = l10n().CreateCategory || '';
        getCategoryOffcanvas()?.show();
    };
    const openEditCategory = (data) => {
        if (!data) return;
        resetCategoryForm();
        if (els.categoryLabel) els.categoryLabel.textContent = l10n().EditCategory || '';
        if (els.categoryId) els.categoryId.value = safe(data.id || data.Id);
        if (els.categoryRowVersion) els.categoryRowVersion.value = safe(data.rowVersion || data.RowVersion);
        if (els.categoryCode) { els.categoryCode.value = safe(data.categoryCode || data.CategoryCode); els.categoryCode.readOnly = true; }
        if (els.categoryName) els.categoryName.value = safe(data.displayName || data.DisplayName);
        if (els.categoryDescription) els.categoryDescription.value = safe(data.description || data.Description);
        if (els.categorySort) els.categorySort.value = data.sortOrder ?? data.SortOrder ?? '';
        const status = safe(data.status || data.Status);
        if (els.categoryStatus) els.categoryStatus.value = status === 'Archived' ? 'Inactive' : (status || 'Active');
        getCategoryOffcanvas()?.show();
    };
    const reloadCategories = () => { catState.dt?.ajax.reload(null, false); loadCategories().catch(() => { }); };

    const saveCategory = async () => {
        clearInvalid(els.categoryForm);
        setVisible(els.categoryAlert, false);
        let valid = true;
        if (!els.categoryCode.value.trim()) { setInvalid(els.categoryCode, l10n().RequiredField); valid = false; }
        if (!els.categoryName.value.trim()) { setInvalid(els.categoryName, l10n().RequiredField); valid = false; }
        if (!valid) return;
        const id = safe(els.categoryId.value);
        const isEdit = !!id;
        els.saveCategory.disabled = true;
        try {
            if (isEdit) {
                await api(`/Platform/SubscriptionFeatures/api/categories/${id}`, {
                    method: 'PUT', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ displayName: els.categoryName.value.trim(), description: els.categoryDescription.value.trim() || null, sortOrder: els.categorySort.value === '' ? null : Number(els.categorySort.value), status: els.categoryStatus.value, rowVersion: els.categoryRowVersion.value || null })
                });
            } else {
                await api('/Platform/SubscriptionFeatures/api/categories', {
                    method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ categoryCode: els.categoryCode.value.trim(), displayName: els.categoryName.value.trim(), description: els.categoryDescription.value.trim() || null, sortOrder: els.categorySort.value === '' ? null : Number(els.categorySort.value), status: els.categoryStatus.value })
                });
            }
            showToast(isEdit ? l10n().RecordUpdated : l10n().RecordCreated, 'success');
            getCategoryOffcanvas()?.hide();
            reloadCategories();
        } catch (error) {
            if (els.categoryAlert) { els.categoryAlert.textContent = (error.errors && error.errors.length ? error.errors.join(' ') : error.message) || l10n().GatewayError; setVisible(els.categoryAlert, true); }
        } finally { els.saveCategory.disabled = false; }
    };

    const archiveCategory = (data) => {
        if (!data?.id && !data?.Id) return;
        const id = safe(data.id || data.Id);
        window.showConfirm?.(l10n().CategoryArchiveConfirm, async () => {
            try {
                await api(`/Platform/SubscriptionFeatures/api/categories/${id}/archive`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: safe(data.rowVersion || data.RowVersion) || null }) });
                showToast(l10n().RecordUpdated, 'success');
                reloadCategories();
            } catch (error) { showToast((error.errors && error.errors.length ? error.errors.join(' ') : error.message) || l10n().GatewayError, 'error'); }
        }, { entityName: safe(data.displayName || data.DisplayName), type: 'danger', confirmButtonText: l10n().ArchiveCategory });
    };

    // ════════════════════════════════════════════════════════════════════════
    //  FEATURES TAB (GoldenReference DataTable, lazy)
    // ════════════════════════════════════════════════════════════════════════
    const featState = { dt: null, statusFilter: [], categoryFilter: [], filterBound: false, loaded: false };
    const featTableEl = document.querySelector('.datatables-subscription-features');

    const registerFeatureFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || featState.filterBound) return;
        featState.filterBound = true;
        $.fn.dataTable.ext.search.push((settings, _d, _i, rowData) => {
            if (settings.nTable !== featTableEl) return true;
            const s = featState.statusFilter, c = featState.categoryFilter;
            const okStatus = !s.length || s.includes(safe(rowData?.status || rowData?.Status));
            const okCat = !c.length || c.includes(safe(rowData?.categoryId || rowData?.CategoryId));
            return okStatus && okCat;
        });
    };

    const fillFeatureCategoryFilter = () => {
        const sel = document.getElementById('featFilterCategory');
        if (!sel) return;
        sel.innerHTML = state.categories.map((c) => `<option value="${escapeHtml(c.id || c.Id)}">${escapeHtml(c.displayName || c.DisplayName)}</option>`).join('');
    };

    const initFeatureTable = async () => {
        if (!featTableEl || featState.dt || !window.DitenDataTable || !window.DtDefaults) return;
        registerFeatureFilter();
        await loadCategories();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: l10n().Filter, 'aria-controls': 'featInlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleCollapse('featInlineFilterCollapse')
            }
        };

        featState.dt = window.DitenDataTable.createCrudTable({
            tableEl: featTableEl,
            bulk: {},
            ajax: {
                url: '/Platform/SubscriptionFeatures/api?page=1&pageSize=500&sort=sortOrder',
                type: 'GET',
                dataSrc: (json) => (((json && json.data) || json || {}).items) || []
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: null, name: 'control', defaultContent: '' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'featureCode', name: 'featureCode' },
                    { data: 'categoryId', name: 'categoryId' },
                    { data: 'status', name: 'status' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, searchable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1, render: (d, type, full) => {
                            const core = (full.isCoreFeature || full.IsCoreFeature) ? ` <span class="badge bg-label-info">${escapeHtml(l10n().IsCoreFeature || 'Core')}</span>` : '';
                            return `<span class="fw-medium text-heading">${escapeHtml(d ?? '')}</span>${core}`;
                        }
                    },
                    { targets: 2, render: (d, type, full) => `<span>${escapeHtml(d ?? '')}</span><br><small class="text-muted">${escapeHtml(full.featureSlug || full.FeatureSlug || '')}</small>` },
                    { targets: 3, render: (d) => escapeHtml(state.categoryMap[safe(d)] || '-') },
                    { targets: 4, render: (d, type) => type === 'display' ? featureStatusBadge(d) : safe(d) },
                    {
                        targets: -1, title: l10n().Actions, orderable: false, searchable: false, className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
                            const archived = safe(full.status || full.Status) === 'Archived';
                            return window.DitenDataTable.renderActions([
                                { className: 'js-feat-edit', icon: 'bx bx-edit', text: l10n().Edit, attrs: { 'data-id': full.id || full.Id, 'title': l10n().Edit } },
                                { className: 'js-feat-details', icon: 'bx bx-show', text: l10n().QuickView, attrs: { 'data-id': full.id || full.Id } },
                                { className: 'js-feat-deactivate', icon: 'bx bx-block', text: l10n().Deactivate, visible: !archived, attrs: { 'data-json': rowJson } },
                                { className: 'js-feat-archive text-danger', icon: 'bx bx-archive-in', text: l10n().Archive, visible: !archived, attrs: { 'data-json': rowJson } }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(l10n().CreateFeature, {}, extraButtons, { exportColumns: [1, 2, 3, 4], colvisColumns: [1, 2, 3, 4] }),
                initComplete: function () {
                    setVisible(document.getElementById('feat-skeleton-loader'), false);
                    document.getElementById('feat-table-wrap')?.classList.remove('d-none');
                    fillFeatureCategoryFilter();
                    mountFilter('#tabFeatures', 'featInlineFilterHost');
                    initFilterSelect2('#featFilterStatus');
                    initFilterSelect2('#featFilterCategory');
                    document.getElementById('featBtnFilterApply')?.addEventListener('click', () => {
                        featState.statusFilter = $('#featFilterStatus').val() || [];
                        featState.categoryFilter = $('#featFilterCategory').val() || [];
                        featState.dt?.draw();
                        hideCollapse('featInlineFilterCollapse');
                    });
                    document.getElementById('featBtnFilterReset')?.addEventListener('click', () => {
                        featState.statusFilter = []; featState.categoryFilter = [];
                        $('#featFilterStatus').val(null).trigger('change');
                        $('#featFilterCategory').val(null).trigger('change');
                        featState.dt?.draw();
                    });
                    document.querySelector('#tabFeatures .add-new')?.addEventListener('click', (e) => { e.preventDefault(); window.location.href = '/Platform/SubscriptionFeatures/Create'; });
                    try { this.api().columns.adjust(); this.api().responsive?.recalc?.(); } catch (e) { }
                }
            }
        });
    };

    const postFeatureStatus = async (data, action) => {
        if (!data) return;
        const id = safe(data.id || data.Id);
        try {
            await api(`/Platform/SubscriptionFeatures/api/${id}/${action}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: safe(data.rowVersion || data.RowVersion) || null }) });
            showToast(l10n().RecordUpdated, 'success');
            featState.dt?.ajax.reload(null, false);
        } catch (error) { showToast((error.errors && error.errors.length ? error.errors.join(' ') : error.message) || l10n().GatewayError, 'error'); }
    };

    // ─── Events ───────────────────────────────────────────────────────────────
    const bindEvents = () => {
        els.saveCategory && els.saveCategory.addEventListener('click', saveCategory);

        document.addEventListener('click', (event) => {
            const catEdit = event.target.closest('.js-cat-edit');
            if (catEdit) { event.preventDefault(); openEditCategory(tryParseRowJson(catEdit)); return; }
            const catArchive = event.target.closest('.js-cat-archive');
            if (catArchive) { event.preventDefault(); archiveCategory(tryParseRowJson(catArchive)); return; }

            const featEdit = event.target.closest('.js-feat-edit');
            if (featEdit) { event.preventDefault(); const id = featEdit.getAttribute('data-id'); if (id) window.location.href = `/Platform/SubscriptionFeatures/Edit/${id}`; return; }
            const featDetails = event.target.closest('.js-feat-details');
            if (featDetails) { event.preventDefault(); const id = featDetails.getAttribute('data-id'); if (id) window.location.href = `/Platform/SubscriptionFeatures/Details/${id}`; return; }
            const featArchive = event.target.closest('.js-feat-archive');
            if (featArchive) {
                event.preventDefault();
                const data = tryParseRowJson(featArchive);
                window.showConfirm?.(l10n().AreYouSure, () => postFeatureStatus(data, 'archive'), { entityName: safe(data?.displayName || data?.DisplayName), type: 'danger', confirmButtonText: l10n().Archive });
                return;
            }
            const featDeactivate = event.target.closest('.js-feat-deactivate');
            if (featDeactivate) {
                event.preventDefault();
                const data = tryParseRowJson(featDeactivate);
                window.showConfirm?.(l10n().AreYouSure, () => postFeatureStatus(data, 'deactivate'), { entityName: safe(data?.displayName || data?.DisplayName), type: 'warning', confirmButtonText: l10n().Deactivate });
            }
        });

        // Lazy-load Features tab on first show
        document.querySelector('[data-bs-target="#tabFeatures"]')?.addEventListener('shown.bs.tab', () => {
            if (featState.loaded) { try { featState.dt?.columns.adjust(); featState.dt?.responsive?.recalc?.(); } catch (e) { } return; }
            featState.loaded = true;
            initFeatureTable();
        });
    };

    const flushPendingToast = () => {
        try {
            const t = sessionStorage.getItem('sf-toast');
            if (!t) return;
            sessionStorage.removeItem('sf-toast');
            showToast(t === 'updated' ? l10n().RecordUpdated : l10n().RecordCreated, 'success');
        } catch (e) { }
    };

    document.addEventListener('DOMContentLoaded', () => {
        bindEvents();
        initCategoryTable();
        flushPendingToast();
    });
})();
