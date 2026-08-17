'use strict';

const InterfaceRegistryDetails = (function () {
    const L = () => window.L10n || {};
    const els = {
        root: document.getElementById('ir-detail-root'),
        loading: document.getElementById('ir-detail-loading'),
        error: document.getElementById('ir-detail-error'),
        permission: document.getElementById('ir-detail-permission'),
        // Profile elements
        code: document.getElementById('detailCode'),
        displayName: document.getElementById('detailDisplayName'),
        status: document.getElementById('detailStatus'),
        version: document.getElementById('detailVersion'),
        service: document.getElementById('detailService'),
        owner: document.getElementById('detailOwner'),
        stability: document.getElementById('detailStability'),
        visibility: document.getElementById('detailVisibility'),
        confirmedAt: document.getElementById('detailConfirmedAt'),
        // List elements
        notes: document.getElementById('detailNotes'),
        endpointList: document.getElementById('ir-endpoint-list'),
        consumerTable: document.getElementById('dtConsumers'),
        endpointCount: document.getElementById('endpointCount'),
        consumerCount: document.getElementById('consumerCount')
    };

    let dtConsumers = null;

    const safe = (value) => value === null || value === undefined ? '' : String(value);
    const escapeHtml = (value) => safe(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const methodConfig = {
        GET: { bg: 'bg-success', border: 'border-success', light: 'bg-label-success' },
        POST: { bg: 'bg-primary', border: 'border-primary', light: 'bg-label-primary' },
        PUT: { bg: 'bg-warning', border: 'border-warning', light: 'bg-label-warning' },
        DELETE: { bg: 'bg-danger', border: 'border-danger', light: 'bg-label-danger' },
        PATCH: { bg: 'bg-info', border: 'border-info', light: 'bg-label-info' }
    };

    const renderEndpoints = (endpoints) => {
        if (!els.endpointList) return;
        if (!endpoints || !endpoints.length) {
            els.endpointList.innerHTML = `<div class="text-center text-muted p-4">${label('NoEndpoints')}</div>`;
            return;
        }

        els.endpointList.innerHTML = endpoints.map(ep => {
            const method = safe(ep.httpMethod || ep.HttpMethod).toUpperCase();
            const cfg = methodConfig[method] || { bg: 'bg-secondary', border: 'border-secondary', light: 'bg-label-secondary' };
            const route = safe(ep.routePath || ep.RoutePath);
            const version = safe(ep.version || ep.Version);
            const perm = safe(ep.permissionKey || ep.PermissionKey || '-');

            return `
                <div class="d-flex align-items-center border rounded p-2 gap-3 shadow-xs ${cfg.light}" style="border-width: 1px !important; border-style: solid !important; border-color: inherit;">
                    <div class="badge ${cfg.bg} text-white text-center shadow-sm" style="min-width: 75px; font-family: monospace; font-weight: 800; font-size: 0.85rem;">${method}</div>
                    <div class="flex-grow-1 text-truncate">
                        <code class="fw-bold text-dark" style="font-size: 0.95rem;">${escapeHtml(route)}</code>
                    </div>
                    <div class="d-flex align-items-center gap-3 text-muted small">
                        <span title="Version"><i class="bx bx-git-branch me-1"></i>${escapeHtml(version)}</span>
                        ${perm !== '-' ? `<span title="Permission" class="text-truncate" style="max-width: 120px;"><i class="bx bx-lock-alt me-1"></i>${escapeHtml(perm)}</span>` : ''}
                    </div>
                </div>
            `;
        }).join('');
    };

    const setVisible = (el, visible) => {
        if (el) el.classList.toggle('d-none', !visible);
    };
    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return {};
        try { return JSON.parse(text); } catch { return { errors: [text] }; }
    };
    const envelopeData = (json) => json.data || json.Data || json;
    const envelopeErrors = (json) => json.errors || json.Errors || [];
    const api = async (url) => {
        const response = await fetch(url, { credentials: 'same-origin' });
        const json = await parseEnvelope(response);
        if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
            const errors = envelopeErrors(json);
            const error = new Error(errors.length ? errors.join(' ') : response.status === 403 ? L().PermissionDenied : L().GatewayError);
            error.status = response.status;
            throw error;
        }
        return envelopeData(json);
    };
    const formatDateTime = (value) => {
        const raw = safe(value);
        if (!raw) return '-';
        const date = new Date(raw);
        if (Number.isNaN(date.getTime())) return raw;
        return new Intl.DateTimeFormat(undefined, {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        }).format(date);
    };
    const lifecycleStatusName = (value) => {
        const names = ['Discovered', 'PendingReview', 'Confirmed', 'Active', 'Changed', 'MissingInSource', 'Deprecated', 'Retired', 'Rejected'];
        return typeof value === 'number' ? names[value] || safe(value) : safe(value);
    };
    const label = (value) => L()[safe(value)] || safe(value) || '-';
    const statusBadge = (status) => {
        const normalized = lifecycleStatusName(status);
        const map = {
            Active: 'bg-label-success',
            Confirmed: 'bg-label-success',
            PendingReview: 'bg-label-warning',
            PartiallyConfirmed: 'bg-label-info',
            Rejected: 'bg-label-danger',
            Deprecated: 'bg-label-warning',
            Failed: 'bg-label-danger',
            Discovered: 'bg-label-info',
            Changed: 'bg-label-info',
            MissingInSource: 'bg-label-warning',
            Retired: 'bg-label-secondary'
        };
        return `<span class="badge ${map[normalized] || 'bg-label-secondary'}">${escapeHtml(label(normalized))}</span>`;
    };
    const showError = (message, status) => {
        setVisible(els.permission, status === 403);
        if (!els.error || status === 403) return;
        els.error.textContent = message || L().ErrorOccurred || 'ErrorOccurred';
        setVisible(els.error, true);
    };
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress' || error?.status === 401;
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
    };

    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'InterfaceRegistryDetails' };
    const totalColumnCount = 6;
    const baseOrder = [[2, 'asc']];
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let appliedFilters = { module: '', service: '', required: '' };

    const emptyFilters = () => ({ module: '', service: '', required: '' });
    const normalizeFilters = (filters) => {
        const s = filters || {};
        return {
            module: String(s.module || '').trim(),
            service: String(s.service || '').trim(),
            required: String(s.required || '').trim()
        };
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter(v => v !== '').length;

    const setSaveFilterVisible = (visible) => {
        const button = document.getElementById('btnSaveConsumerView');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const getCurrentView = (api) => ({
        filters: Object.assign({}, appliedFilters),
        search: String(api.table().container().querySelector('.dt-search input')?.value || api.search() || '').trim(),
        colVis: {},
        columnOrder: api.colReorder?.order() || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: api.order()
    });

    const getResetBaselineState = () => ({
        filters: emptyFilters(),
        search: '',
        colVis: {},
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });

    const serializeView = (view) => JSON.stringify({
        filters: normalizeFilters(view?.filters),
        search: String(view?.search || '').trim(),
        columnOrder: Array.isArray(view?.columnOrder) ? view.columnOrder : Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });

    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(),
            search: '',
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
            order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const applySavedTableState = (api, state) => {
        if (!api || !state) return;
        appliedFilters = normalizeFilters(state.filters);
        
        // Sync UI
        const modInp = document.getElementById('filterConsumerModule');
        const srvInp = document.getElementById('filterConsumerService');
        const reqInp = document.getElementById('filterConsumerRequired');
        if (modInp) modInp.value = appliedFilters.module;
        if (srvInp) srvInp.value = appliedFilters.service;
        if (reqInp) {
            reqInp.value = appliedFilters.required;
            if (window.jQuery?.fn?.select2) $(reqInp).val(appliedFilters.required).trigger('change');
        }

        if (Array.isArray(state.columnOrder) && api.colReorder) api.colReorder.order(state.columnOrder, true);
        
        // Apply Column Filters
        api.column(2).search(appliedFilters.module || '');
        api.column(3).search(appliedFilters.service || '');
        api.column(5).search(appliedFilters.required || '');

        if (state.search !== undefined) {
            api.search(state.search);
            const searchInp = api.table().container().querySelector('.dt-search input');
            if (searchInp) searchInp.value = state.search;
        }
        if (Array.isArray(state.order)) api.order(state.order);
        
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = items.find(v => v.isDefault || v.IsDefault) || items[0] || null;
            if (defaultViewRecord) {
                const def = defaultViewRecord.viewDefinition || defaultViewRecord.ViewDefinition;
                defaultViewState = typeof def === 'string' ? JSON.parse(def) : def;
                return defaultViewState;
            }
        } catch (e) {
            if (isAuthHandledError(e)) {
                handleUnauthorized();
                return null;
            }

            console.error('[InterfaceRegistryDetails] Load view failed', e);
        }
        return null;
    };

    const saveDefaultView = async (api) => {
        if (!personalizationClient?.saveView) return;
        const viewState = getCurrentView(api);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: defaultViewRecord?.viewName || defaultViewRecord?.ViewName || 'Default',
            viewDefinition: viewState,
            isDefault: true,
            visibility: 'private'
        };
        try {
            const existingId = defaultViewRecord?.id || defaultViewRecord?.Id;
            const res = existingId 
                ? await personalizationClient.updateView(existingId, payload)
                : await personalizationClient.saveView(payload);
            defaultViewRecord = res?.data || res?.Data || res;
            defaultViewState = viewState;
            setSaveFilterVisible(false);
            window.showToast?.(L().RecordSaved || L().Saved || 'RecordSaved', 'success');
        } catch (e) {
            if (isAuthHandledError(e)) {
                handleUnauthorized();
                return;
            }

            console.error('[InterfaceRegistryDetails] Save view failed', e);
            window.showToast?.(L().ErrorOccurred || 'ErrorOccurred', 'error');
        }
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost .select2').each(function () {
            const $select = $(this);
            $select.select2({
                minimumResultsForSearch: Infinity,
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                width: 'element',
                placeholder: $select.attr('data-placeholder') || '',
                allowClear: true
            });
        });
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const toolbarRow = document.getElementById('dt-consumers')?.closest('.dt-container')?.querySelector('.row:first-child');
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
            host.style.display = 'block';
        }
    };

    const initDataTables = async () => {
        if (typeof DataTable === 'undefined' || !window.DtDefaults) return;

        const tableEl = document.getElementById('dt-consumers');
        if (!dtConsumers && tableEl) {
            const L = () => window.L10n || {};
            
            const savedState = await loadDefaultView();

            const filterBtn = {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L().Filter || '', 'aria-controls': 'consumerFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => {
                    const collapseEl = document.getElementById('consumerFilterCollapse');
                    if (!collapseEl) return;
                    bootstrap.Collapse.getOrCreateInstance(collapseEl).toggle();
                }
            };

            const saveFilterBtn = {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L().SaveView || '')}</span>`,
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { id: 'btnSaveConsumerView', title: L().SaveView || '', 'data-bs-toggle': 'tooltip' },
                action: () => saveDefaultView(dtConsumers)
            };

            const config = {
                data: [],
                columns: [
                    { data: null, defaultContent: '', orderable: false, searchable: false, className: 'control', responsivePriority: 2 },
                    { data: null, defaultContent: '', orderable: false, searchable: false, className: 'cell-fit dt-checkboxes-cell', render: () => '<input type="checkbox" class="dt-checkboxes form-check-input">' },
                    { data: (row) => row.consumerModuleCode || row.ConsumerModuleCode, render: (data) => `<span class="fw-semibold text-primary">${escapeHtml(data)}</span>` },
                    { data: (row) => row.consumerService || row.ConsumerService, render: (data) => `<span class="text-muted">${escapeHtml(data)}</span>` },
                    { data: (row) => row.consumedVersionRange || row.ConsumedVersionRange || '-', render: (data) => `<span class="badge bg-label-secondary">${escapeHtml(data)}</span>` },
                    { 
                        data: (row) => (row.required ?? row.Required), 
                        render: (data) => data
                            ? `<span class="badge bg-label-danger">${escapeHtml(L().Required || '')}</span>`
                            : `<span class="badge bg-label-secondary">${escapeHtml(L().Optional || '')}</span>`
                    }
                ],
                order: savedState?.order || baseOrder,
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                buttons: window.DtDefaults.exportButtons(null, null, { filterBtn, saveFilterBtn }, { exportColumns: [2, 3, 4, 5] }),
                initComplete: function () {
                    const api = this.api();
                    mountInlineFilter();
                    initSelect2Filters();
                    if (savedState) applySavedTableState(api, savedState);
                    window.DtDefaults?.updateVisualState(api, getAppliedFilterCount());
                    setTimeout(() => { saveFilterArmed = true; }, 200);
                },
                drawCallback: function () {
                    const api = this.api();
                    window.DtDefaults?.updateVisualState(api, getAppliedFilterCount());
                    window.DtDefaults?.refreshButtonGroupRadii?.();
                    if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
                }
            };

            dtConsumers = new DataTable(tableEl, window.DtDefaults.create(config));

            $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt', () => {
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dtConsumers));
            });

            // Filter Panel Logic
            const filterForm = document.getElementById('consumerFilterForm');
            const btnApply = document.getElementById('btnConsumerFilterApply');
            const btnReset = document.getElementById('btnConsumerFilterReset');

            if (btnApply) {
                btnApply.addEventListener('click', () => {
                    appliedFilters.module = document.getElementById('filterConsumerModule')?.value || '';
                    appliedFilters.service = document.getElementById('filterConsumerService')?.value || '';
                    appliedFilters.required = document.getElementById('filterConsumerRequired')?.value || '';

                    dtConsumers.column(2).search(appliedFilters.module);
                    dtConsumers.column(3).search(appliedFilters.service);
                    dtConsumers.column(5).search(appliedFilters.required);
                    dtConsumers.draw();

                    window.DtDefaults.updateVisualState(dtConsumers, getAppliedFilterCount());
                    if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dtConsumers));
                });
            }

            if (btnReset) {
                btnReset.addEventListener('click', () => {
                    filterForm.reset();
                    if (window.jQuery?.fn?.select2) $('#inlineFilterHost .select2').val('').trigger('change');

                    applySavedTableState(dtConsumers, getResetBaselineState());
                    window.DtDefaults.updateVisualState(dtConsumers, 0);
                    if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dtConsumers));
                });
            }
        }
    };

    const stabilityBadge = (stability) => {
        const map = {
            Experimental: 'bg-label-warning',
            Stable: 'bg-label-success',
            Deprecated: 'bg-label-danger'
        };
        return `<span class="badge ${map[safe(stability)] || 'bg-label-secondary'}">${escapeHtml(label(stability))}</span>`;
    };
    const visibilityBadge = (visibility) => {
        const map = {
            Internal: 'bg-label-danger',
            Platform: 'bg-label-info',
            Tenant: 'bg-label-primary',
            Public: 'bg-label-success'
        };
        return `<span class="badge ${map[safe(visibility)] || 'bg-label-secondary'}">${escapeHtml(label(visibility))}</span>`;
    };

    const renderDetail = async (snapshot) => {
        if (!els.root) return;
        const definition = snapshot.definition || snapshot.Definition || {};
        const endpoints = definition.endpoints || definition.Endpoints || [];
        const consumers = definition.consumers || definition.Consumers || [];
        const confirmedAt = snapshot.confirmedAtUtc || snapshot.ConfirmedAtUtc;

        // Profile
        if (els.code) els.code.textContent = safe(definition.interfaceCode || definition.InterfaceCode);
        if (els.displayName) els.displayName.textContent = safe(definition.displayName || definition.DisplayName);
        if (els.status) els.status.innerHTML = statusBadge(definition.lifecycleStatus || definition.LifecycleStatus);
        
        if (els.version) els.version.textContent = safe(definition.interfaceVersion || definition.InterfaceVersion);
        if (els.service) els.service.textContent = safe(definition.providerService || definition.ProviderService);
        if (els.owner) els.owner.textContent = safe(definition.ownerModuleCode || definition.OwnerModuleCode);
        if (els.stability) els.stability.innerHTML = stabilityBadge(definition.stability || definition.Stability);
        if (els.visibility) els.visibility.innerHTML = visibilityBadge(definition.visibility || definition.Visibility);
        if (els.confirmedAt) els.confirmedAt.textContent = formatDateTime(confirmedAt);

        // Content
        if (els.notes) els.notes.textContent = safe(definition.compatibilityNotes || definition.CompatibilityNotes || L().CompatibilityNotes || '-');
        if (els.endpointCount) els.endpointCount.textContent = String(endpoints.length);
        if (els.consumerCount) els.consumerCount.textContent = String(consumers.length);

        renderEndpoints(endpoints);
        await initDataTables();

        if (dtConsumers) {
            dtConsumers.clear().rows.add(consumers).draw();
        }
    };

    const load = async () => {
        if (!els.root) return;
        const code = els.root.getAttribute('data-interface-code') || '';
        const version = els.root.getAttribute('data-interface-version') || '';
        setVisible(els.loading, true);
        setVisible(els.error, false);
        setVisible(els.permission, false);
        try {
            const url = `/Platform/InterfaceRegistry/api/interfaces/${encodeURIComponent(code)}/snapshot?version=${encodeURIComponent(version)}`;
            const snapshot = await api(url);
            await renderDetail(snapshot);
        } catch (error) {
            showError(error.message, error.status);
        } finally {
            setVisible(els.loading, false);
        }
    };

    load();
})();
