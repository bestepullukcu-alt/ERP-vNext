/**
 * Compositions DataTables Page Script
 * Diten ERP vNext - MDM/Compositions
 */
'use strict';

const CompositionsList = (function () {
    let dt;
    let defaultViewState = null;
    let defaultViewRecord = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-compositions');
    const pageApiUrl = '/Compositions/Data';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MDM', pageKey: 'Compositions' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { product: [], dosageForm: [], lifecycleState: [] };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
            return;
        }

        L = L || {};
    };

    const normalizeString = (value) => (typeof value === 'string' ? value.trim() : '');
    const normalizeArray = (value) => {
        if (Array.isArray(value)) {
            return Array.from(new Set(value.map((item) => normalizeString(String(item))).filter(Boolean)));
        }

        const normalized = normalizeString(value);
        return normalized ? [normalized] : [];
    };
    const sortNormalizedArray = (value) => normalizeArray(value).slice().sort((left, right) => left.localeCompare(right));
    const hasFilterValue = (value) => Array.isArray(value) ? normalizeArray(value).length > 0 : normalizeString(value).length > 0;
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        return parts.length === 2 ? parts.pop().split(';').shift() : null;
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (error) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const normalizeColumnVisibility = (colVis) => {
        if (!colVis) {
            return null;
        }

        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((columnIndex, position) => {
                if (typeof colVis[columnIndex] === 'boolean') {
                    normalized[columnIndex] = colVis[columnIndex];
                } else if (typeof colVis[position] === 'boolean') {
                    normalized[columnIndex] = colVis[position];
                }
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((columnIndex) => {
                if (typeof colVis[columnIndex] === 'boolean') {
                    normalized[columnIndex] = colVis[columnIndex];
                }
            });
        }

        return Object.keys(normalized).length ? normalized : null;
    };

    const captureColumnVisibility = (api) => {
        const colVis = {};
        saveViewColumnIndexes.forEach((columnIndex) => {
            try {
                colVis[columnIndex] = !!api.column(columnIndex).visible();
            } catch (error) { }
        });
        return colVis;
    };

    const normalizeColumnOrder = (columnOrder) => {
        if (!Array.isArray(columnOrder) || columnOrder.length !== totalColumnCount) {
            return null;
        }

        const normalized = columnOrder
            .map((index) => Number(index))
            .filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);

        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount
            ? normalized
            : null;
    };

    const captureColumnOrder = (api) => {
        try {
            return normalizeColumnOrder(api?.colReorder?.order?.());
        } catch (error) {
            return null;
        }
    };

    const createDefaultColumnVisibility = () => {
        return saveViewColumnIndexes.reduce((acc, columnIndex) => {
            acc[columnIndex] = true;
            return acc;
        }, {});
    };

    const applyColumnOrder = (api, columnOrder) => {
        const normalized = normalizeColumnOrder(columnOrder);
        if (!normalized || typeof api?.colReorder?.order !== 'function') {
            return;
        }

        api.colReorder.order(normalized, true);
    };

    const applyColumnVisibility = (api, colVis) => {
        const normalized = normalizeColumnVisibility(colVis);
        if (!normalized) {
            return;
        }

        saveViewColumnIndexes.forEach((columnIndex) => {
            if (typeof normalized[columnIndex] === 'boolean') {
                try {
                    api.column(columnIndex).visible(normalized[columnIndex], false);
                } catch (error) { }
            }
        });
    };

    const getSearchInputValue = (api) => {
        try {
            return api.table().container().querySelector('.dt-search input')?.value || '';
        } catch (error) {
            return '';
        }
    };

    const syncSearchInput = (api, searchValue) => {
        try {
            const input = api.table().container().querySelector('.dt-search input');
            if (input) {
                input.value = searchValue || '';
            }
        } catch (error) { }
    };

    const getCurrentView = (api) => ({
        product: normalizeArray(appliedFilters.product),
        dosageForm: normalizeArray(appliedFilters.dosageForm),
        lifecycleState: normalizeArray(appliedFilters.lifecycleState),
        search: normalizeString(getSearchInputValue(api) || api.search()),
        colVis: captureColumnVisibility(api),
        columnOrder: captureColumnOrder(api),
        order: api.order()
    });

    const getSavedViewId = (savedView) => savedView?.id || savedView?.Id || savedView?._id || null;
    const getSavedViewName = (savedView) => savedView?.viewName || savedView?.ViewName || '';
    const isSavedViewDefault = (savedView) => savedView?.isDefault === true || savedView?.IsDefault === true;

    const getSavedViewDefinition = (savedView) => {
        const rawDefinition = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? {};
        if (typeof rawDefinition === 'string') {
            try {
                return JSON.parse(rawDefinition);
            } catch (error) {
                return {};
            }
        }

        return rawDefinition || {};
    };

    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDefinition(savedView);
        return {
            product: normalizeArray(definition.product),
            dosageForm: normalizeArray(definition.dosageForm),
            lifecycleState: normalizeArray(definition.lifecycleState),
            search: normalizeString(definition.search),
            colVis: normalizeColumnVisibility(definition.colVis),
            columnOrder: normalizeColumnOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };

    const serializeView = (view) => JSON.stringify({
        product: sortNormalizedArray(view?.product),
        dosageForm: sortNormalizedArray(view?.dosageForm),
        lifecycleState: sortNormalizedArray(view?.lifecycleState),
        search: normalizeString(view?.search),
        colVis: normalizeColumnVisibility(view?.colVis) || createDefaultColumnVisibility(),
        columnOrder: normalizeColumnOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) {
            return;
        }

        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            product: [],
            dosageForm: [],
            lifecycleState: [],
            search: '',
            colVis: createDefaultColumnVisibility(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };

        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;

        if (!personalizationClient?.getViews) {
            return null;
        }

        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            defaultViewRecord = Array.isArray(views) ? (views.find(isSavedViewDefault) || views[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (isAuthHandledError(error)) {
                return null;
            }

            console.error('[Compositions SaveView] Failed to load saved views.', error);
            return null;
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) {
            return null;
        }

        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Save View').trim(),
            viewDefinition: view,
            isDefault: true,
            visibility: 'private'
        };

        const existingViewId = getSavedViewId(defaultViewRecord);
        defaultViewRecord = existingViewId
            ? await personalizationClient.updateView(existingViewId, payload)
            : await personalizationClient.saveView(payload);
        defaultViewState = mapSavedViewToState(defaultViewRecord);
        return defaultViewState;
    };

    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound) {
            return;
        }

        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
        btn.addEventListener('click', (event) => {
            event.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false });
            collapseEl.classList.contains('show') ? instance.hide() : instance.show();
        });
    };

    const matchesMultiFilter = (selectedValues, actualValue) => {
        const normalizedSelected = normalizeArray(selectedValues);
        if (!normalizedSelected.length) {
            return true;
        }

        return normalizedSelected.includes(normalizeString(actualValue));
    };

    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) {
            return;
        }

        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');

        if (!$summary.length) {
            $summary = $('<span class="dt-inline-filter-multi__summary"></span>');
            $selection.prepend($summary);
        }

        if (!$actions.length) {
            $actions = $('<span class="dt-inline-filter-multi__actions"></span>');
            $selection.append($actions);
        }

        if (!$count.length) {
            $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>');
            $actions.append($count);
        }

        if (!$arrow.length) {
            $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>');
            $selection.append($arrow);
        }

        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || [])
            .map((item) => normalizeString(item.text))
            .filter(Boolean);

        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0);
        $count.text(String(selectedValues.length));

        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $(`<span class="dt-multi-clear-btn" role="button" aria-label="${L.Reset}" title="${L.Reset}">×</span>`);
            $clearBtn.on('mousedown', function (event) {
                event.preventDefault();
                event.stopPropagation();
                $select.val(null).trigger('change');
            });
            $actions.append($clearBtn);
        }
    };

    const initFilterSelects = () => {
        if (!window.jQuery || !$.fn.select2) {
            return;
        }

        const $dropdownParent = $(document.body);

        const clampDropdown = () => {
            requestAnimationFrame(() => {
                const dropdown = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                if (!dropdown) {
                    return;
                }

                const rect = dropdown.getBoundingClientRect();
                const pad = 8;
                let dx = 0;
                let dy = 0;

                if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
                if (rect.left < pad) dx += pad - rect.left;
                if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
                if (rect.top < pad) dy += pad - rect.top;

                if (!dx && !dy) {
                    return;
                }

                const cs = window.getComputedStyle(dropdown);
                const cssLeft = parseFloat(cs.left);
                const cssTop = parseFloat(cs.top);
                const baseLeft = Number.isFinite(cssLeft) ? cssLeft : (rect.left + window.scrollX);
                const baseTop = Number.isFinite(cssTop) ? cssTop : (rect.top + window.scrollY);

                if (dx) dropdown.style.left = `${baseLeft + dx}px`;
                if (dy) dropdown.style.top = `${baseTop + dy}px`;
                dropdown.style.transform = 'none';
            });
        };

        $('#filterProduct, #filterDosageForm, #filterLifecycleState').each(function () {
            const $select = $(this);

            if ($select.hasClass('select2-hidden-accessible')) {
                $select.select2('destroy');
            }

            $select.select2({
                dropdownParent: $dropdownParent,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $select.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });

            $select.on('select2:open', clampDropdown);
            $select.on('change.select2-summary', function () {
                syncMultiSelectSummary($select);
            });
            requestAnimationFrame(() => syncMultiSelectSummary($select));
        });
    };

    const syncFilterControls = (values) => {
        $('#filterProduct').val(normalizeArray(values.product)).trigger('change');
        $('#filterDosageForm').val(normalizeArray(values.dosageForm)).trigger('change');
        $('#filterLifecycleState').val(normalizeArray(values.lifecycleState)).trigger('change');
    };

    const applySavedTableState = (api, view, options) => {
        const state = view || {};
        const fallbackOrder = Array.isArray(options?.fallbackOrder) ? options.fallbackOrder : baseOrder;
        const fallbackColVis = options?.resetColumns === true ? createDefaultColumnVisibility() : null;
        const fallbackColumnOrder = options?.resetColumnOrder === true
            ? Array.from({ length: totalColumnCount }, (_, index) => index)
            : null;

        appliedFilters = {
            product: normalizeArray(state.product),
            dosageForm: normalizeArray(state.dosageForm),
            lifecycleState: normalizeArray(state.lifecycleState)
        };

        syncFilterControls(appliedFilters);

        if (typeof state.search === 'string') {
            api.search(state.search);
            syncSearchInput(api, state.search);
        } else if (options?.clearSearch) {
            api.search('');
            syncSearchInput(api, '');
        }

        applyColumnOrder(api, state.columnOrder || fallbackColumnOrder);
        applyColumnVisibility(api, state.colVis || fallbackColVis);
        if (Array.isArray(state.order)) {
            api.order(state.order);
        } else {
            api.order(fallbackOrder);
        }

        api.draw(false);
        setTimeout(() => {
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
        }, 0);
    };

    const getAppliedFilterCount = () => {
        return [appliedFilters.product, appliedFilters.dosageForm, appliedFilters.lifecycleState]
            .filter((value) => hasFilterValue(value)).length;
    };

    const getLifecycleMeta = (code, fallback) => {
        const normalizedCode = normalizeString(code).toUpperCase();
        switch (normalizedCode) {
            case 'ACTIVE':
                return { title: fallback || L.Active || code, class: 'bg-label-success' };
            case 'BLOCKED':
                return { title: fallback || code, class: 'bg-label-warning' };
            case 'OBSOLETE':
                return { title: fallback || code, class: 'bg-label-dark' };
            case 'DRAFT':
                return { title: fallback || code, class: 'bg-label-secondary' };
            default:
                return { title: fallback || L.Unknown || '-', class: 'bg-label-secondary' };
        }
    };

    const setupFilters = (api) => {
        initFilterSelects();
        if (defaultViewState) {
            applySavedTableState(api, defaultViewState);
        } else {
            syncFilterControls(appliedFilters);
            window.DtDefaults.updateVisualState(api, 0);
        }

        const applyBtn = document.getElementById('btnFilterApply');
        const resetBtn = document.getElementById('btnFilterReset');

        if (applyBtn && !applyBtn.dataset.bound) {
            applyBtn.dataset.bound = '1';
            applyBtn.addEventListener('click', () => {
                appliedFilters = {
                    product: $('#filterProduct').val() || [],
                    dosageForm: $('#filterDosageForm').val() || [],
                    lifecycleState: $('#filterLifecycleState').val() || []
                };
                api.draw();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                if (saveFilterArmed) {
                    setSaveFilterVisible(isDirtyComparedToDefault(api));
                }

                const collapseEl = document.getElementById(filterCollapseId);
                if (collapseEl) {
                    bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
                }
            });
        }

        if (resetBtn && !resetBtn.dataset.bound) {
            resetBtn.dataset.bound = '1';
            resetBtn.addEventListener('click', (event) => {
                event.preventDefault();

                if (defaultViewState && isDirtyComparedToDefault(api)) {
                    applySavedTableState(api, defaultViewState, {
                        fallbackOrder: baseOrder,
                        resetColumnOrder: !defaultViewState?.columnOrder
                    });
                } else {
                    applySavedTableState(api, { product: [], dosageForm: [], lifecycleState: [], search: '' }, {
                        fallbackOrder: baseOrder,
                        clearSearch: true,
                        resetColumns: true,
                        resetColumnOrder: true
                    });
                }

                if (saveFilterArmed) {
                    setSaveFilterVisible(isDirtyComparedToDefault(api));
                }
            });
        }
    };

    const getSelectedIds = () => Array.from(dtTableEl.querySelectorAll('.dt-checkboxes:checked')).map((checkbox) => checkbox.value);

    const updateBulkBar = () => {
        const ids = getSelectedIds();
        const bulkBar = document.getElementById('bulkActionBar');
        const bulkCount = document.getElementById('bulkSelectedCount');
        if (!bulkBar || !bulkCount) {
            return;
        }

        bulkBar.classList.toggle('d-none', ids.length === 0);
        bulkCount.textContent = String(ids.length);

        const headerCheckbox = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (headerCheckbox) {
            const total = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCheckbox.checked = ids.length > 0 && ids.length === total;
            headerCheckbox.indeterminate = ids.length > 0 && ids.length < total;
        }
    };

    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes').forEach((checkbox) => {
            checkbox.checked = false;
            checkbox.closest('tr')?.classList.remove('selected');
        });

        const headerCheckbox = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (headerCheckbox) {
            headerCheckbox.checked = false;
            headerCheckbox.indeterminate = false;
        }

        updateBulkBar();
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        clearSelection();
        dt.ajax.reload(() => {
            const message = interpolationValue
                ? (L[messageKey] || '').replace('{0}', interpolationValue)
                : (L[messageKey] || messageKey);
            window.showToast?.(message, 'success');
        }, false);
    };

    const tryParseRowJson = (element) => {
        if (!element) {
            return null;
        }

        const raw = element.getAttribute('data-json');
        if (!raw) {
            return null;
        }

        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (error) {
            console.error('[Compositions QuickView] Could not parse row data.', error);
            return null;
        }
    };

    const populateOffcanvas = (data) => {
        if (!data) {
            return;
        }

        document.getElementById('oc-title').innerText = data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.productName || '-';

        const lifecycle = getLifecycleMeta(data.lifecycleStateCode, data.lifecycleState);
        const statusEl = document.getElementById('oc-status');
        statusEl.className = `badge ${lifecycle.class}`;
        statusEl.innerText = lifecycle.title || '-';

        document.getElementById('oc-code').innerText = data.formulationCode || '-';
        document.getElementById('oc-product').innerText = data.productName || '-';
        document.getElementById('oc-dosageForm').innerText = data.dosageForm || '-';
        document.getElementById('oc-strength').innerText = data.strength || '-';
        document.getElementById('oc-version').innerText = String(data.version ?? '-');
        document.getElementById('oc-revision').innerText = String(data.revision ?? '-');
        document.getElementById('oc-fillAmount').innerText = data.technicalFillAmount
            ? `${data.technicalFillAmount} ${data.technicalFillUnit || ''}`.trim()
            : '-';
        document.getElementById('oc-componentCount').innerText = Array.isArray(data.components)
            ? String(data.components.length)
            : '-';

        document.getElementById('oc-btn-edit').href = `/Compositions/Edit/${data.id}`;

        const deleteBtn = document.getElementById('oc-btn-delete');
        if (deleteBtn) {
            deleteBtn.dataset.compositionId = data.id || '';
            deleteBtn.dataset.compositionName = data.name || '';
        }
    };

    const openCompositionView = async (id, fallbackData) => {
        try {
            const response = await fetch(`${pageApiUrl}/${id}`, {
                headers: getAuthHeaders(),
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error('Failed to fetch composition details.');
            }

            const detail = await response.json();
            populateOffcanvas(detail);
        } catch (error) {
            if (fallbackData) {
                populateOffcanvas(fallbackData);
            } else {
                console.error(error);
                window.showToast?.(L.ErrorOccurred, 'error');
                return;
            }
        }

        const offcanvasEl = document.getElementById('offcanvasDetailsPreview');
        if (offcanvasEl) {
            bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl).show();
        }
    };

    const deleteComposition = async (id, successMessageKey, interpolationValue, onSuccess) => {
        const response = await fetch(`${pageApiUrl}/${id}`, {
            method: 'DELETE',
            headers: getAuthHeaders(),
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new Error('Delete failed.');
        }

        if (typeof onSuccess === 'function') {
            onSuccess();
        }

        reloadWithSuccessToast(successMessageKey, interpolationValue);
    };

    const bindEvents = () => {
        document.addEventListener('click', (event) => {
            const quickViewBtn = event.target.closest('.js-quick-view');
            if (quickViewBtn) {
                const rowData = tryParseRowJson(quickViewBtn);
                if (rowData?.id) {
                    openCompositionView(rowData.id, rowData);
                }
            }

            const deleteBtn = event.target.closest('.delete-record');
            if (!deleteBtn) {
                return;
            }

            let rowEl = deleteBtn.closest('tr');
            if (rowEl?.classList.contains('child')) {
                rowEl = rowEl.previousElementSibling;
            }

            const row = dt.row(rowEl);
            const data = row.data();
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    await deleteComposition(data.id, 'RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, {
                entityName: data.name,
                type: 'danger'
            });
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            $(this).closest('tr').toggleClass('selected', this.checked);
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const checked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
                checkbox.checked = checked;
                checkbox.closest('tr')?.classList.toggle('selected', checked);
            });
            updateBulkBar();
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', clearSelection);

        document.getElementById('btnBulkDelete')?.addEventListener('click', async () => {
            const ids = getSelectedIds();
            if (!ids.length) {
                return;
            }

            const confirmText = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
            window.showConfirm?.(confirmText, async () => {
                try {
                    const results = await Promise.all(ids.map(async (id) => {
                        const response = await fetch(`${pageApiUrl}/${id}`, {
                            method: 'DELETE',
                            headers: getAuthHeaders(),
                            credentials: 'same-origin'
                        });

                        if (!response.ok) {
                            throw new Error('Bulk delete failed.');
                        }
                    }));

                    if (results) {
                        reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                    }
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, {
                entityName: `${ids.length} ${L.SelectedCount}`,
                type: 'danger'
            });
        });

        document.getElementById('oc-btn-delete')?.addEventListener('click', function () {
            const compositionId = this.dataset.compositionId;
            const compositionName = this.dataset.compositionName;
            if (!compositionId) {
                return;
            }

            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    await deleteComposition(compositionId, 'RecordDeleted', null, () => {
                        const offcanvasEl = document.getElementById('offcanvasDetailsPreview');
                        bootstrap.Offcanvas.getInstance(offcanvasEl)?.hide();
                    });
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, {
                entityName: compositionName,
                type: 'danger'
            });
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl) {
            return;
        }

        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast?.(L.ComingSoon, 'warning'); }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-controls': filterCollapseId,
                    'aria-expanded': 'false'
                }
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) {
                        return;
                    }

                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || 'RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) {
                            return;
                        }

                        console.error('[Compositions SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: pageApiUrl,
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'formulationCode', name: 'formulationCode' },
                { data: 'name', name: 'name' },
                { data: 'versionLabel', name: 'version' },
                { data: 'dosageForm', name: 'dosageForm' },
                { data: 'strength', name: 'strength' },
                { data: 'lifecycleStateCode', name: 'lifecycleState' },
                { data: 'id', name: 'action' }
            ],
            columnDefs: [
                {
                    targets: 0,
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    render: () => ''
                },
                {
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">`
                },
                {
                    targets: 2,
                    responsivePriority: 1,
                    render: (data) => `<span class="fw-medium text-heading">${data || '-'}</span>`
                },
                {
                    targets: 4,
                    render: (data) => `<span class="badge bg-label-secondary">${data || '-'}</span>`
                },
                {
                    targets: 7,
                    render: (data, type, row) => {
                        const lifecycle = getLifecycleMeta(data, row.lifecycleState);
                        return type === 'display'
                            ? `<span class="badge ${lifecycle.class}">${lifecycle.title}</span>`
                            : normalizeString(data || lifecycle.title);
                    }
                },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit text-end',
                    responsivePriority: 4,
                    render: (data, type, full) => {
                        if (type !== 'display') {
                            return data;
                        }

                        return `<div class="d-flex align-items-center justify-content-end">
              <a href="/Compositions/Details/${full.id}" class="btn btn-icon text-primary me-1"><i class="bx bx-show-alt icon-md"></i></a>
              <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
              <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
              <div class="dropdown-menu dropdown-menu-end m-0">
                <a href="/Compositions/Details/${full.id}" class="dropdown-item">${L.Details || 'Details'}</a>
                <a href="/Compositions/Edit/${full.id}" class="dropdown-item">${L.Edit}</a>
                <a href="javascript:;" class="dropdown-item delete-record text-danger">${L.Delete || 'Delete'}</a>
              </div>
            </div>`;
                    }
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewCompositions,
                { onclick: "window.location.href='/Compositions/Create'" },
                extraButtons,
                {
                    exportColumns: [2, 3, 4, 5, 6, 7],
                    colvisColumns: [2, 3, 4, 5, 6, 7]
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                const api = this.api();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
                updateBulkBar();
            }
        }));

        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) {
                return true;
            }

            const effectiveRow = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!effectiveRow) {
                return true;
            }

            return matchesMultiFilter(appliedFilters.product, effectiveRow.productId)
                && matchesMultiFilter(appliedFilters.dosageForm, effectiveRow.dosageForm)
                && matchesMultiFilter(appliedFilters.lifecycleState, effectiveRow.lifecycleStateCode);
        });

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('search.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    return {
        init: function () {
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    CompositionsList.init();
});
