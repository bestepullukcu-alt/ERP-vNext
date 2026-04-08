/**
 * Items DataTables Page Script
 * Diten ERP vNext - MDM/Items
 */
'use strict';

const ItemsList = (function () {
    let dt;
    let defaultViewState = null;
    let defaultViewRecord = null;
    let saveFilterArmed = false;
    let filterOptionsLoaded = false;

    const dtTableEl = document.querySelector('.datatables-items');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MDM', pageKey: 'Items' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9];
    const totalColumnCount = 11;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5, 9];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { itemType: [], category: [], lifecycleState: [], trackingPolicy: '', status: '' };
    let L = window.L10n || {};

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
        }
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(';').shift();
        }

        return null;
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (error) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getAuthHeaders = (includeJsonContentType = false) => {
        const token = getCookie('access_token');
        const headers = {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };

        if (includeJsonContentType) {
            headers['Content-Type'] = 'application/json';
        }

        return headers;
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
    const escapeRegex = (value) => String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const encodeHtml = (value) => String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    const matchesMultiFilter = (selectedValues, actualValue) => {
        const normalizedSelected = normalizeArray(selectedValues);
        if (!normalizedSelected.length) {
            return true;
        }

        return normalizedSelected.includes(normalizeString(actualValue));
    };
    const matchesSingleFilter = (selectedValue, actualValue) => {
        const normalizedSelected = normalizeString(selectedValue);
        if (!normalizedSelected) {
            return true;
        }

        return normalizeString(actualValue) === normalizedSelected;
    };
    const matchesStatusFilter = (selectedValue, actualValue) => {
        const normalizedSelected = normalizeString(selectedValue);
        if (!normalizedSelected) {
            return true;
        }

        return String(Boolean(actualValue)) === normalizedSelected;
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
            acc[columnIndex] = defaultVisibleColumnIndexes.includes(columnIndex);
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
        itemType: normalizeArray(appliedFilters.itemType),
        category: normalizeArray(appliedFilters.category),
        lifecycleState: normalizeArray(appliedFilters.lifecycleState),
        trackingPolicy: normalizeString(appliedFilters.trackingPolicy),
        status: normalizeString(appliedFilters.status),
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
            itemType: normalizeArray(definition.itemType),
            category: normalizeArray(definition.category),
            lifecycleState: normalizeArray(definition.lifecycleState),
            trackingPolicy: normalizeString(definition.trackingPolicy),
            status: normalizeString(definition.status),
            search: normalizeString(definition.search),
            colVis: normalizeColumnVisibility(definition.colVis),
            columnOrder: normalizeColumnOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };

    const serializeView = (view) => JSON.stringify({
        itemType: sortNormalizedArray(view?.itemType),
        category: sortNormalizedArray(view?.category),
        lifecycleState: sortNormalizedArray(view?.lifecycleState),
        trackingPolicy: normalizeString(view?.trackingPolicy),
        status: normalizeString(view?.status),
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
            itemType: [],
            category: [],
            lifecycleState: [],
            trackingPolicy: '',
            status: '',
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
            if (error?.authHandled) {
                return null;
            }

            console.error('[Items SaveView] Failed to load saved views.', error);
            return null;
        }
    };

    const saveDefaultView = async (view) => {
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
            if (collapseEl.classList.contains('show')) {
                instance.hide();
            } else {
                instance.show();
            }
        });
    };

    const populateSelect = (selector, options, config = {}) => {
        const el = document.querySelector(selector);
        if (!el) {
            return;
        }

        const currentValue = el.multiple
            ? (window.jQuery ? ($(el).val() || []) : Array.from(el.selectedOptions).map((option) => option.value))
            : el.value;

        const optionMarkup = options.map((option) => {
            const optionValue = encodeHtml(typeof config.getValue === 'function' ? config.getValue(option) : (option.name ?? option.id ?? ''));
            const optionLabel = encodeHtml(typeof config.getLabel === 'function' ? config.getLabel(option) : (option.name ?? option.label ?? optionValue));
            return `<option value="${optionValue}">${optionLabel}</option>`;
        }).join('');

        el.innerHTML = el.multiple ? optionMarkup : `<option value=""></option>${optionMarkup}`;
        if (window.jQuery) {
            $(el).val(currentValue).trigger('change.select2');
        } else if (el.multiple && Array.isArray(currentValue)) {
            Array.from(el.options).forEach((option) => {
                option.selected = currentValue.includes(option.value);
            });
        } else if (currentValue) {
            el.value = currentValue;
        }
    };

    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.itemsFilterBound === '1') {
            return;
        }

        dtTableEl.dataset.itemsFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _searchData, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) {
                return true;
            }

            const effectiveRow = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!effectiveRow) {
                return true;
            }

            return matchesMultiFilter(appliedFilters.itemType, effectiveRow.itemType)
                && matchesMultiFilter(appliedFilters.category, effectiveRow.category)
                && matchesMultiFilter(appliedFilters.lifecycleState, effectiveRow.lifecycleState)
                && matchesSingleFilter(appliedFilters.trackingPolicy, effectiveRow.trackingPolicy)
                && matchesStatusFilter(appliedFilters.status, effectiveRow.isActive);
        });
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

        // Clear (×) butonu: değer varsa inject et, yoksa kaldır
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="Clear" title="Temizle">×</span>');
            $clearBtn.on('mousedown', function (e) {
                e.preventDefault();
                e.stopPropagation();
                $select.val(null).trigger('change');
            });
            $actions.append($clearBtn);
        }
    };

    const loadFilterOptions = async () => {
        if (filterOptionsLoaded) {
            return;
        }

        try {
            const [itemTypesResponse, categoriesResponse, lifecycleResponse, trackingPolicyResponse] = await Promise.all([
                fetch(`${apiUrl}/api/item-types`, { headers: getAuthHeaders() }),
                fetch(`${apiUrl}/api/item-categories`, { headers: getAuthHeaders() }),
                fetch(`${apiUrl}/api/lifecycle-states`, { headers: getAuthHeaders() }),
                fetch(`${apiUrl}/api/tracking-policies`, { headers: getAuthHeaders() })
            ]);

            const itemTypes = itemTypesResponse.ok ? ((await itemTypesResponse.json()).data || []) : [];
            const categories = categoriesResponse.ok ? ((await categoriesResponse.json()).data || []) : [];
            const lifecycleStates = lifecycleResponse.ok ? ((await lifecycleResponse.json()).data || []) : [];
            const trackingPolicies = trackingPolicyResponse.ok ? ((await trackingPolicyResponse.json()).data || []) : [];
            const trackingPolicyOptions = ['Batch', 'Serial', 'None']
                .map((name) => trackingPolicies.find((policy) => normalizeString(policy.name).toLowerCase() === name.toLowerCase()) || { name })
                .filter((policy, index, collection) => collection.findIndex((entry) => normalizeString(entry.name) === normalizeString(policy.name)) === index);

            populateSelect('#filterItemType', itemTypes);
            populateSelect('#filterCategory', categories);
            populateSelect('#filterLifecycleState', lifecycleStates);
            populateSelect('#filterTrackingPolicy', trackingPolicyOptions);

            if (window.jQuery && $.fn.select2) {
                const $dropdownParent = $(document.body);

                const clampDropdown = () => {
                    requestAnimationFrame(() => {
                        const dropdown = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                        if (!dropdown) return;

                        const rect = dropdown.getBoundingClientRect();
                        const pad = 8;
                        let dx = 0;
                        let dy = 0;

                        if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
                        if (rect.left < pad) dx += pad - rect.left;
                        if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
                        if (rect.top < pad) dy += pad - rect.top;

                        if (!dx && !dy) return;

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

                $('#filterItemType, #filterCategory, #filterLifecycleState').each(function () {
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

                $('#filterTrackingPolicy, #filterStatus').each(function () {
                    const $select = $(this);

                    if ($select.hasClass('select2-hidden-accessible')) {
                        $select.select2('destroy');
                    }

                    $select.select2({
                        dropdownParent: $dropdownParent,
                        dropdownCssClass: 'dt-inline-filter-dropdown',
                        selectionCssClass: 'form-select form-select-sm',
                        placeholder: $select.data('placeholder') || '',
                        minimumResultsForSearch: Infinity,
                        width: 'element',
                        allowClear: true
                    });

                    $select.on('select2:open', clampDropdown);
                });
            }

            filterOptionsLoaded = true;
        } catch (error) {
            console.error('[Items Filters] Could not load filter options.', error);
        }
    };

    const applyFilterValues = (api) => {
        api.draw();
    };

    const syncFilterControls = (values) => {
        $('#filterItemType').val(normalizeArray(values.itemType)).trigger('change');
        $('#filterCategory').val(normalizeArray(values.category)).trigger('change');
        $('#filterLifecycleState').val(normalizeArray(values.lifecycleState)).trigger('change');
        $('#filterTrackingPolicy').val(values.trackingPolicy || '').trigger('change');
        $('#filterStatus').val(values.status || '').trigger('change');
    };

    const applySavedTableState = (api, view, options) => {
        const state = view || {};
        const fallbackOrder = Array.isArray(options?.fallbackOrder) ? options.fallbackOrder : baseOrder;
        const fallbackColVis = options?.resetColumns === true ? createDefaultColumnVisibility() : null;
        const fallbackColumnOrder = options?.resetColumnOrder === true
            ? Array.from({ length: totalColumnCount }, (_, index) => index)
            : null;

        appliedFilters = {
            itemType: normalizeArray(state.itemType),
            category: normalizeArray(state.category),
            lifecycleState: normalizeArray(state.lifecycleState),
            trackingPolicy: normalizeString(state.trackingPolicy),
            status: state.status || ''
        };

        syncFilterControls(appliedFilters);
        applyFilterValues(api);

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
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
        }, 0);
    };

    const getAppliedFilterCount = () => {
        return [appliedFilters.itemType, appliedFilters.category, appliedFilters.lifecycleState, appliedFilters.trackingPolicy, appliedFilters.status]
            .filter((value) => hasFilterValue(value)).length;
    };

    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });

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
            console.error('[Items QuickView] Failed to parse row data.', error);
            return null;
        }
    };

    const populateOffcanvas = (data) => {
        if (!data) {
            return;
        }

        document.getElementById('oc-title').innerText = data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.itemType || '-';
        document.getElementById('oc-code').innerText = data.code || '-';
        document.getElementById('oc-name').innerText = data.name || '-';
        document.getElementById('oc-category').innerText = data.category || '-';
        document.getElementById('oc-baseUom').innerText = data.baseUom || '-';
        document.getElementById('oc-description').innerText = data.shortDescription || '-';
        document.getElementById('oc-btn-edit').href = `/Items/Edit/${data.id}`;

        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(data.isActive)] || { title: L.Unknown, class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';
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

    const setupFilters = async (api) => {
        await loadFilterOptions();
        if (defaultViewState) {
            applySavedTableState(api, defaultViewState);
        } else {
            syncFilterControls(appliedFilters);
            window.DtDefaults.updateVisualState(api, 0);
        }

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                itemType: $('#filterItemType').val() || [],
                category: $('#filterCategory').val() || [],
                lifecycleState: $('#filterLifecycleState').val() || [],
                trackingPolicy: document.getElementById('filterTrackingPolicy')?.value || '',
                status: document.getElementById('filterStatus')?.value || ''
            };
            applyFilterValues(api);
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            if (saveFilterArmed) {
                setSaveFilterVisible(isDirtyComparedToDefault(api));
            }

            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) {
                bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
            }
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();

            const clearToBaseline = () => {
                applySavedTableState(api, { itemType: [], category: [], lifecycleState: [], trackingPolicy: '', status: '', search: '' }, {
                    fallbackOrder: baseOrder,
                    clearSearch: true,
                    resetColumns: true,
                    resetColumnOrder: true
                });
            };

            const restoreDefaultView = (view) => {
                applySavedTableState(api, view, {
                    fallbackOrder: baseOrder,
                    resetColumnOrder: !view?.columnOrder
                });
            };

            const hasSavedDefault = !!defaultViewState;
            const isDirty = hasSavedDefault ? isDirtyComparedToDefault(api) : false;

            if (hasSavedDefault && isDirty) {
                restoreDefaultView(defaultViewState);
            } else {
                clearToBaseline();
            }

            if (saveFilterArmed) {
                setSaveFilterVisible(isDirtyComparedToDefault(api));
            }
        });
    };

    const bindEvents = () => {
        dtTableEl.addEventListener('click', (event) => {
            const quickViewBtn = event.target.closest('.js-quick-view');
            if (quickViewBtn) {
                populateOffcanvas(tryParseRowJson(quickViewBtn));
            }

            const deleteBtn = event.target.closest('.delete-record');
            if (!deleteBtn) {
                return;
            }

            let rowEl = deleteBtn.closest('tr');
            if (rowEl.classList.contains('child')) {
                rowEl = rowEl.previousElementSibling;
            }

            const row = dt.row(rowEl);
            const data = row.data();
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const response = await fetch(`${apiUrl}/api/items/${data.id}`, {
                        method: 'DELETE',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) {
                        throw new Error('Delete failed.');
                    }

                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, data.name);
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
                    const response = await fetch(`${apiUrl}/api/items/bulk`, {
                        method: 'DELETE',
                        headers: getAuthHeaders(true),
                        body: JSON.stringify({ ids })
                    });

                    if (!response.ok) {
                        throw new Error('Bulk delete failed.');
                    }

                    reloadWithSuccessToast('BulkDeleteSuccess', String(ids.length));
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, String(ids.length));
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
                action: function () {
                    window.showToast?.(L.ComingSoon, 'warning');
                }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false' }
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (event, api) {
                    const tableApi = api || dt;
                    if (!tableApi) {
                        return;
                    }

                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved, 'success');
                    } catch (error) {
                        if (error?.authHandled) {
                            return;
                        }

                        console.error(error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/items',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'code', name: 'code' },
                { data: 'name', name: 'name' },
                { data: 'itemType', name: 'itemType' },
                { data: 'category', name: 'category' },
                { data: 'baseUom', name: 'baseUom' },
                { data: 'trackingPolicy', name: 'trackingPolicy' },
                { data: 'lifecycleState', name: 'lifecycleState' },
                { data: 'isActive', name: 'isActive' },
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
                    render: (data) => `<span class="fw-medium text-heading">${data}</span>`
                },
                {
                    targets: [6, 7, 8],
                    visible: false
                },
                {
                    targets: 9,
                    render: (data, type) => {
                        const status = getStatusMap()[String(data)] || { title: L.Unknown, class: 'bg-label-primary' };
                        return type === 'display'
                            ? `<span class="badge ${status.class}">${status.title}</span>`
                            : status.title;
                    }
                },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) => `
                        <div class="d-flex align-items-center">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="/Items/Details/${full.id}" class="dropdown-item">${L.ViewDetails}</a>
                                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, '&#39;')}'>${L.QuickView}</a>
                                <a href="/Items/Edit/${full.id}" class="dropdown-item">${L.Edit}</a>
                            </div>
                        </div>
                    `
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                L.AddNewItems,
                { onclick: "window.location.href='/Items/Create'" },
                extraButtons,
                {
                    exportColumns: [2, 3, 4, 5, 6, 7, 8, 9],
                    colvisColumns: [2, 3, 4, 5, 6, 7, 8, 9]
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
                setTimeout(() => {
                    saveFilterArmed = true;
                }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        }));

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) {
                setSaveFilterVisible(isDirtyComparedToDefault(dt));
            }
        });

        dt.on('search.dt order.dt', function () {
            if (saveFilterArmed) {
                setSaveFilterVisible(isDirtyComparedToDefault(dt));
            }
        });

        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) {
                setSaveFilterVisible(isDirtyComparedToDefault(dt));
            }
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

document.addEventListener('DOMContentLoaded', () => ItemsList.init());
