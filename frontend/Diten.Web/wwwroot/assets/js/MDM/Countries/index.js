/**
 * Countries DataTables Initialization (v2 Standard — inline filter + Save View)
 */

'use strict';

const CountriesList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-countries');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MDM', pageKey: 'Countries' };
    let L = window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    let saveFilterArmed = false;
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: '' };
    let defaultViewRecord = null;
    let defaultViewState = null;
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
            return;
        }
        L = L || {};
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    };

    const normalizeSavedString = (value) => typeof value === 'string' ? value.trim() : '';

    const getSavedViewFlag = (savedView, camelKey, pascalKey) => {
        if (!savedView || typeof savedView !== 'object') return undefined;
        if (typeof savedView[camelKey] !== 'undefined') return savedView[camelKey];
        if (typeof savedView[pascalKey] !== 'undefined') return savedView[pascalKey];
        return undefined;
    };

    const getSavedViewId = (savedView) =>
        normalizeSavedString(
            getSavedViewFlag(savedView, 'id', 'Id') ||
            getSavedViewFlag(savedView, '_id', '_id'));

    const getSavedViewName = (savedView) =>
        normalizeSavedString(getSavedViewFlag(savedView, 'viewName', 'ViewName'));

    const isSavedViewDefault = (savedView) => {
        const value = getSavedViewFlag(savedView, 'isDefault', 'IsDefault');
        return value === true;
    };

    const getSavedViewDefinition = (savedView) => {
        if (!savedView || typeof savedView !== 'object') return {};
        const rawDefinition =
            savedView.viewDefinition ??
            savedView.ViewDefinition ??
            savedView.viewDefinitionJson ??
            savedView.ViewDefinitionJson ??
            {};
        if (rawDefinition && typeof rawDefinition === 'object') return rawDefinition;
        if (typeof rawDefinition === 'string') {
            try {
                const parsed = JSON.parse(rawDefinition);
                return parsed && typeof parsed === 'object' ? parsed : {};
            } catch (e) { return {}; }
        }
        return {};
    };

    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDefinition(savedView);
        return {
            status: normalizeSavedString(definition.status),
            search: normalizeSavedString(definition.search),
            colVis: normalizeColumnVisibility(definition.colVis),
            columnOrder: normalizeColumnOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };

    const normalizeColumnVisibility = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((columnIndex, position) => {
                if (typeof colVis[columnIndex] === 'boolean') { normalized[columnIndex] = colVis[columnIndex]; return; }
                if (typeof colVis[position] === 'boolean') { normalized[columnIndex] = colVis[position]; }
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((columnIndex) => {
                const rawValue = colVis[columnIndex];
                if (typeof rawValue === 'boolean') normalized[columnIndex] = rawValue;
            });
        }
        return Object.keys(normalized).length ? normalized : null;
    };

    const captureColumnVisibility = (api) => {
        const colVis = {};
        saveViewColumnIndexes.forEach((columnIndex) => {
            try { colVis[columnIndex] = !!api.column(columnIndex).visible(); } catch (e) { }
        });
        return Object.keys(colVis).length ? colVis : null;
    };

    const normalizeColumnOrder = (columnOrder) => {
        if (!Array.isArray(columnOrder) || columnOrder.length !== totalColumnCount) return null;
        const normalized = columnOrder
            .map((index) => Number(index))
            .filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        if (normalized.length !== totalColumnCount) return null;
        if (new Set(normalized).size !== totalColumnCount) return null;
        return normalized;
    };

    const captureColumnOrder = (api) => {
        try {
            const order = api?.colReorder?.order?.();
            return normalizeColumnOrder(order);
        } catch (e) { return null; }
    };

    const applyColumnOrder = (api, columnOrder) => {
        const normalized = normalizeColumnOrder(columnOrder);
        if (!normalized || typeof api?.colReorder?.order !== 'function') return;
        try { api.colReorder.order(normalized, true); } catch (e) { }
    };

    const applyColumnVisibility = (api, colVis) => {
        const normalized = normalizeColumnVisibility(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((columnIndex) => {
            const shouldBeVisible = normalized[columnIndex];
            if (typeof shouldBeVisible !== 'boolean') return;
            try { api.column(columnIndex).visible(shouldBeVisible, false); } catch (e) { }
        });
    };

    const syncSearchInput = (api, searchValue) => {
        try {
            const container = api.table().container();
            const input = container?.querySelector('.dt-search input');
            if (input) input.value = searchValue || '';
        } catch (e) { }
    };

    const getSearchInputValue = (api) => {
        try {
            const container = api.table().container();
            const input = container?.querySelector('.dt-search input');
            return typeof input?.value === 'string' ? input.value : '';
        } catch (e) { return ''; }
    };

    const syncFilterControls = (values) => {
        const status = normalizeSavedString(values?.status);
        $('#FilterTransaction').val(status).trigger('change');
    };

    const areColumnOrdersEqual = (left, right) => {
        const normalizedLeft = normalizeColumnOrder(left) || Array.from({ length: totalColumnCount }, (_, index) => index);
        const normalizedRight = normalizeColumnOrder(right) || Array.from({ length: totalColumnCount }, (_, index) => index);
        return normalizedLeft.every((value, index) => value === normalizedRight[index]);
    };

    const syncPendingTableUiState = (api) => {
        const inputSearch = getSearchInputValue(api);
        const appliedSearch = typeof api?.search === 'function' ? (api.search() || '') : '';
        if (inputSearch !== appliedSearch) {
            try { api.search(inputSearch); } catch (e) { }
        }
    };

    const applySavedTableState = (api, view, options) => {
        const state = view || {};
        const fallbackOrder = Array.isArray(options?.fallbackOrder) ? options.fallbackOrder : baseOrder;
        const fallbackColVis = options?.resetColumns === true ? createDefaultColumnVisibility() : null;
        const fallbackColumnOrder = options?.resetColumnOrder === true
            ? Array.from({ length: totalColumnCount }, (_, index) => index)
            : null;
        const colVisToApply = state.colVis || fallbackColVis;
        const columnOrderToApply = state.columnOrder || fallbackColumnOrder;

        if (typeof state.search === 'string') {
            try { api.search(state.search); } catch (e) { }
            syncSearchInput(api, state.search);
        } else if (options?.clearSearch === true) {
            try { api.search(''); } catch (e) { }
            syncSearchInput(api, '');
        }

        if (columnOrderToApply) applyColumnOrder(api, columnOrderToApply);
        if (colVisToApply) applyColumnVisibility(api, colVisToApply);

        if (Array.isArray(state.order)) {
            try { api.order(state.order); } catch (e) { }
        } else if (fallbackOrder) {
            try { api.order(fallbackOrder); } catch (e) { }
        }

        appliedFilters = { status: state.status || '' };
        syncFilterControls(appliedFilters);
        applyFilterValues(api, appliedFilters);

        try { api.columns.adjust(); } catch (e) { }
        api.draw(false);

        setTimeout(() => {
            syncFilterControls(appliedFilters);
            syncSearchInput(api, typeof state.search === 'string' ? state.search : '');
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
        }, 0);
    };

    const createDefaultColumnVisibility = () => {
        return saveViewColumnIndexes.reduce((acc, columnIndex) => {
            acc[columnIndex] = true;
            return acc;
        }, {});
    };

    const areColumnVisibilitiesEqual = (left, right) => {
        const normalizedLeft = normalizeColumnVisibility(left);
        const normalizedRight = normalizeColumnVisibility(right);
        if (!normalizedLeft && !normalizedRight) return true;
        if (!normalizedLeft || !normalizedRight) return false;
        return saveViewColumnIndexes.every((columnIndex) => {
            const leftValue = typeof normalizedLeft[columnIndex] === 'boolean' ? normalizedLeft[columnIndex] : true;
            const rightValue = typeof normalizedRight[columnIndex] === 'boolean' ? normalizedRight[columnIndex] : true;
            return leftValue === rightValue;
        });
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            defaultViewRecord = Array.isArray(views)
                ? (views.find(isSavedViewDefault) || views[0] || null)
                : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (isAuthHandledError(error)) return null;
            console.error('[Countries SaveView] Failed to load saved views', error);
            defaultViewRecord = null;
            defaultViewState = null;
            return null;
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView || !personalizationClient?.updateView) {
            throw new Error('Personalization client is unavailable.');
        }
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || '').trim(),
            viewDefinition: view || {},
            isDefault: true,
            visibility: 'private'
        };
        const existingViewId = getSavedViewId(defaultViewRecord);
        const savedView = existingViewId
            ? await personalizationClient.updateView(existingViewId, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = savedView;
        defaultViewState = mapSavedViewToState(savedView);
        return defaultViewState;
    };

    const getCurrentView = (api) => {
        const status = appliedFilters?.status || '';
        const inputSearch = getSearchInputValue(api);
        const search = typeof inputSearch === 'string' && inputSearch.length >= 0
            ? inputSearch
            : (typeof api?.search === 'function' ? (api.search() || '') : '');
        let colVis = null;
        try { colVis = captureColumnVisibility(api); } catch (e) { colVis = null; }
        let order = null;
        try { order = api?.order?.() || null; } catch (e) { order = null; }
        return {
            status: status,
            search: search,
            colVis: colVis,
            columnOrder: captureColumnOrder(api),
            order: order
        };
    };

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const def = defaultViewState || null;
        const cur = getCurrentView(api);
        const curHasHiddenCols = !!saveViewColumnIndexes.find((columnIndex) => cur.colVis?.[columnIndex] === false);
        const ref = def || {
            status: '',
            search: '',
            colVis: createDefaultColumnVisibility(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };
        const colVisEqual = areColumnVisibilitiesEqual(ref.colVis, cur.colVis);
        const columnOrderEqual = areColumnOrdersEqual(ref.columnOrder, cur.columnOrder);
        const refOrder = Array.isArray(ref.order) ? ref.order : null;
        const curOrder = Array.isArray(cur.order) ? cur.order : null;
        const orderEqual =
            Array.isArray(refOrder) && Array.isArray(curOrder) && refOrder.length === curOrder.length
                ? refOrder.every((o, i) => String(o?.[0]) === String(curOrder[i]?.[0]) && String(o?.[1]) === String(curOrder[i]?.[1]))
                : refOrder === curOrder;

        if (!def) {
            return [cur.status].filter(Boolean).length > 0 ||
                !!cur.search ||
                curHasHiddenCols ||
                !columnOrderEqual ||
                !orderEqual;
        }
        return (String(cur.status || '') !== String(ref.status || '')) ||
            (String(cur.search || '') !== String(ref.search || '')) ||
            !colVisEqual ||
            !columnOrderEqual ||
            !orderEqual;
    };

    const applyFilterValues = (api, values) => {
        const status = values?.status || '';
        api.column('isActive:name').search(status ? `^${status}$` : '', true, false);
    };

    const getAppliedFilterCount = (api) => {
        try {
            const statusSearch = api.column('isActive:name').search() || '';
            return [statusSearch].filter(v => typeof v === 'string' ? v.trim() : !!v).length;
        } catch (e) {
            return [appliedFilters?.status].filter(Boolean).length;
        }
    };

    const mountInlineFilter = () => {
        if (!dtTableEl) return;
        const host = document.getElementById(filterHostId);
        if (!host) return;
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
            return;
        }
        const dtContainer = dtTableEl.closest('.dt-container') || dtTableEl.closest('.dataTables_wrapper') || dtTableEl.parentElement;
        if (dtContainer) {
            dtContainer.insertAdjacentElement('beforeend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el) return;
        if (btn.dataset.inlineFilterBound) return;
        btn.dataset.inlineFilterBound = '1';
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            if (el.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });

    const populateOffcanvas = (data) => {
        if (!data) return;
        document.getElementById('oc-title').innerText = data.name || '-';
        const subtitle = [data.iso2Code, data.iso3Code].filter(Boolean).join(' • ') || '-';
        document.getElementById('oc-subtitle').innerText = subtitle;
        document.getElementById('oc-iso2').innerText = data.iso2Code || '-';
        document.getElementById('oc-iso2-2').innerText = data.iso2Code || '-';
        document.getElementById('oc-iso3').innerText = data.iso3Code || '-';
        document.getElementById('oc-phonecode').innerText = data.phoneCode || '-';
        document.getElementById('oc-createdat').innerText = fmtDateTime(data.createdAt);
        document.getElementById('oc-updatedat').innerText = fmtDateTime(data.updatedAt);
        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(data.isActive)] || { title: L.Unknown || String(data.isActive), class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';
        document.getElementById('oc-btn-edit').href = `/Countries/Edit/${data.id}`;
    };

    const fmtDateTime = (val) => {
        if (!val) return '-';
        try {
            const d = new Date(val);
            if (Number.isNaN(d.getTime())) return String(val);
            return d.toLocaleString();
        } catch (e) { return String(val); }
    };

    const tryParseRowJson = (element) => {
        if (!element) return null;
        const raw = element.getAttribute('data-json');
        if (!raw) return null;
        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (err) {
            console.error('[Countries QuickView] Could not parse row data', err);
            return null;
        }
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
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
                    if (!tableApi) return;
                    try {
                        syncPendingTableUiState(tableApi);
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.('RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) return;
                        console.error('[Countries SaveView] Failed to save default view', error);
                        window.showToast?.('ErrorOccurred', 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            stateSave: false,
            colReorder: {
                columns: ':gt(1):not(:last-child)'
            },
            ajax: {
                url: apiUrl + '/api/countries',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'name', name: 'name' },
                { data: 'iso2Code', name: 'iso2Code' },
                { data: 'iso3Code', name: 'iso3Code' },
                { data: 'phoneCode', name: 'phoneCode' },
                { data: 'isActive', name: 'isActive' },
                { data: 'action', name: 'action' }
            ],
            preXhr: function () {
                $('#skeleton-loader').fadeIn(100);
            },
            columnDefs: [
                { className: 'control', searchable: false, orderable: false, responsivePriority: 2, targets: 0, render: () => '' },
                {
                    targets: 1, orderable: false, searchable: false, responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: function (data) {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input" value="' + data + '">';
                    }
                },
                {
                    targets: 6,
                    render: (data, type) => {
                        const status = getStatusMap()[String(data)] || { title: L.Unknown || String(data), class: 'bg-label-primary' };
                        if (type === 'display') {
                            return `<span class="badge ${status.class}" text-capitalized>${status.title}</span>`;
                        }
                        return status.title || '';
                    }
                },
                {
                    targets: -1, title: L.Actions, searchable: false, orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) => `
            <div class="d-flex align-items-center">
              <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
              <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
              <div class="dropdown-menu dropdown-menu-end m-0">
                <a href="/Countries/Details/${full['id']}" class="dropdown-item">${L.ViewDetails}</a>
                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView}</a>
                <a href="/Countries/Edit/${full['id']}" class="dropdown-item">${L.Edit}</a>
              </div>
            </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewCountry, {
                onclick: "window.location.href='/Countries/Create'"
            }, extraButtons, {
                exportColumns: [2, 3, 4, 5, 6],
                colvisColumns: [2, 3, 4, 5, 6],
                showAllColumns: [2, 3, 4, 5, 6]
            }),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                const count = getAppliedFilterCount(this.api());
                window.DtDefaults.updateVisualState(this.api(), count);
                $('#skeleton-loader').fadeOut(200);
            }
        }));

        dt.on('column-visibility.dt', function () {
            const count = getAppliedFilterCount(dt);
            window.DtDefaults.updateVisualState(dt, count);
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('columns-reordered.dt column-reorder.dt', function () {
            const count = getAppliedFilterCount(dt);
            window.DtDefaults.updateVisualState(dt, count);
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('search.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const setupFilters = (api) => {
        const renderFilterTrigger = (label, hasValue) => {
            const $wrap = $('<span class="dt-filter-trigger"></span>');
            $wrap.append($('<span class="dt-filter-trigger-label"></span>').text(label || ''));
            if (hasValue) {
                $wrap.append($('<span class="badge rounded-pill bg-primary-subtle text-primary dt-filter-trigger-badge ms-2"></span>').text('1'));
            }
            return $wrap;
        };

        const initSelect2 = () => {
            const $dropdownParent = $(document.body);

            const clampInlineFilterDropdown = () => {
                window.requestAnimationFrame(() => {
                    window.requestAnimationFrame(() => {
                        const dropdown = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                        if (!dropdown) return;
                        const rect = dropdown.getBoundingClientRect();
                        const pad = 8;
                        let dx = 0, dy = 0;
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
                });
            };

            const $status = $('#FilterTransaction');
            if ($status.length && !$status.hasClass('select2-hidden-accessible')) {
                $status.select2({
                    placeholder: (L.Status || L.SelectStatus),
                    dropdownParent: $dropdownParent,
                    minimumResultsForSearch: 0,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    selectionCssClass: 'form-select form-select-sm',
                    templateSelection: (data) => renderFilterTrigger((L.Status || L.SelectStatus), !!(data && data.id)),
                    width: '100%'
                });
                $status.on('select2:open', clampInlineFilterDropdown);
            }
        };

        const statusContainer = document.querySelector('.filter-status, .user_status');
        if (statusContainer) {
            const selectId = 'FilterTransaction';
            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'filter-select form-select form-select-sm text-capitalize';
            select.innerHTML = `<option value="">${L.SelectStatus}</option>`;
            statusContainer.appendChild(select);

            [getStatusMap().true, getStatusMap().false].filter(Boolean).forEach((status) => {
                const option = document.createElement('option');
                option.value = status.title;
                option.textContent = status.title;
                select.appendChild(option);
            });
        }

        initSelect2();

        const defaultView = defaultViewState;
        if (defaultView) {
            applySavedTableState(api, defaultView, { fallbackOrder: baseOrder });
        } else {
            appliedFilters = { status: '' };
            syncFilterControls(appliedFilters);
        }

        const initialFilterCount = getAppliedFilterCount(api);
        window.DtDefaults.updateVisualState(api, initialFilterCount);
        setSaveFilterVisible(false);

        const applyBtn = document.getElementById('btnFilterApply');
        const resetBtn = document.getElementById('btnFilterReset');

        if (applyBtn && !applyBtn.dataset.bound) {
            applyBtn.dataset.bound = '1';
            applyBtn.addEventListener('click', () => {
                const status = $('#FilterTransaction').val();
                appliedFilters = { status: status || '' };
                applyFilterValues(api, appliedFilters);
                api.draw();

                const count = getAppliedFilterCount(api);
                window.DtDefaults.updateVisualState(api, count);
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));

                const el = document.getElementById(filterCollapseId);
                if (el) bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
            });
        }

        if (resetBtn && !resetBtn.dataset.bound) {
            resetBtn.dataset.bound = '1';
            resetBtn.addEventListener('click', (e) => {
                e.preventDefault();

                const clearToBaseline = () => {
                    $('#FilterTransaction').val('').trigger('change');
                    applySavedTableState(api, { status: '', search: '' }, {
                        fallbackOrder: baseOrder,
                        clearSearch: true,
                        resetColumns: true,
                        resetColumnOrder: true
                    });
                };

                const restoreDefaultView = (def) => {
                    applySavedTableState(api, def, {
                        fallbackOrder: baseOrder,
                        resetColumnOrder: !def?.columnOrder
                    });
                };

                const def = defaultViewState;
                const hasSavedDefault = !!def;
                const isDirty = hasSavedDefault ? isDirtyComparedToDefault(api) : false;

                if (hasSavedDefault && isDirty) restoreDefaultView(def);
                else clearToBaseline();

                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            });
        }
    };

    // =================== Checkbox Selection Management ===================

    const getSelectedIds = () => {
        const ids = [];
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => { ids.push(cb.value); });
        return ids;
    };

    const updateBulkBar = () => {
        const ids = getSelectedIds();
        const bar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (!bar || !countEl) return;
        if (ids.length > 0) {
            bar.classList.remove('d-none');
            countEl.textContent = ids.length;
        } else {
            bar.classList.add('d-none');
            countEl.textContent = '0';
        }
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) {
            const totalVisible = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCb.checked = ids.length > 0 && ids.length === totalVisible;
            headerCb.indeterminate = ids.length > 0 && ids.length < totalVisible;
        }
    };

    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) { headerCb.checked = false; headerCb.indeterminate = false; }
        updateBulkBar();
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[char]));

    const openDeleteDialog = ({ confirmMessage, entityName, confirmButtonText }) => {
        const html = entityName
            ? `<div class="mb-2">${L.ConfirmAction || ''}</div><div class="badge bg-label-primary fs-6 mt-1 py-2 px-3">${escapeHtml(entityName)}</div>`
            : `<div class="mb-2">${confirmMessage || ''}</div>`;

        if (typeof Swal === 'undefined') {
            return Promise.resolve(window.confirm(L.AreYouSure || 'Are you sure?'));
        }

        return Swal.fire({
            title: L.AreYouSure,
            html: html,
            iconHtml: '<div class="swal-icon-circle"><i class="bx bx-trash"></i></div>',
            showCancelButton: true,
            confirmButtonText: confirmButtonText || L.BulkDelete,
            cancelButtonText: L.Cancel,
            width: '400px',
            padding: '2.5rem 1.5rem 2rem',
            customClass: {
                popup: 'rounded-4 shadow-lg',
                title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                htmlContainer: 'text-muted mb-3 d-block w-100 text-center',
                actions: 'd-flex justify-content-center mt-4 w-100',
                confirmButton: 'btn btn-danger waves-effect waves-light mx-2',
                cancelButton: 'btn btn-label-secondary waves-effect mx-2',
                icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
            },
            buttonsStyling: false,
            reverseButtons: true
        }).then((result) => result.isConfirmed);
    };

    const reloadTableAndToastSuccess = (message) => {
        clearSelection();
        dt.ajax.reload(() => {
            window.showToast?.(message, 'success');
        }, false);
    };

    const handleEvents = () => {
        if (!dtTableEl) return;

        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                let tr = deleteBtn.closest('tr');
                if (tr.classList.contains('child')) tr = tr.previousElementSibling;
                const data = dt.row(tr).data();

                openDeleteDialog({
                    entityName: data.name,
                    confirmButtonText: L.DeleteConfirmationYesBtn || L.BulkDelete
                }).then((isConfirmed) => {
                    if (!isConfirmed) return;
                    fetch(`${apiUrl}/api/countries/${data.id}`, {
                        method: 'DELETE',
                        headers: getAuthHeaders()
                    })
                        .then(res => {
                            if (res.ok) reloadTableAndToastSuccess('RecordDeleted');
                            else window.showToast?.('ErrorOccurred', 'error');
                        })
                        .catch(() => window.showToast?.('ErrorOccurred', 'error'));
                });
            }

            const quickViewBtn = e.target.closest('.js-quick-view');
            if (quickViewBtn) {
                populateOffcanvas(tryParseRowJson(quickViewBtn));
            }
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            const tr = $(this).closest('tr');
            if (this.checked) tr.addClass('selected'); else tr.removeClass('selected');
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const isChecked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach(cb => {
                cb.checked = isChecked;
                const tr = cb.closest('tr');
                if (isChecked) tr?.classList.add('selected'); else tr?.classList.remove('selected');
            });
            updateBulkBar();
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', () => { clearSelection(); });

        document.getElementById('btnBulkDelete')?.addEventListener('click', () => {
            const ids = getSelectedIds();
            if (ids.length === 0) return;
            const confirmMsg = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
            openDeleteDialog({
                confirmMessage: confirmMsg,
                confirmButtonText: L.BulkDelete
            }).then((isConfirmed) => {
                if (isConfirmed) {
                    fetch(`${apiUrl}/api/countries/bulk`, {
                        method: 'DELETE',
                        headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                        body: JSON.stringify({ ids: ids })
                    })
                        .then(res => { if (res.ok) return res.json(); throw new Error('Bulk delete failed'); })
                        .then(data => { reloadTableAndToastSuccess((L.BulkDeleteSuccess || '').replace('{0}', data.deletedCount)); })
                        .catch(() => window.showToast?.('ErrorOccurred', 'error'));
                }
            });
        });
    };

    return {
        init: async () => {
            syncL10n();
            await initDataTable();
            handleEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => { CountriesList.init(); });
