'use strict';

const InterfaceRegistryList = (function () {
    const L = () => window.L10n || {};
    const state = {
        snapshots: [],
        batches: [],
        diffs: [],
        selectedBatchId: null,
        pendingReject: null
    };

    const rowActionHandlers = {
        quickView: ({ row }) => {
            const code = row?.interfaceCode;
            const version = row?.interfaceVersion;
            if (code) {
                const target = `/Platform/InterfaceRegistry/Details?interfaceCode=${encodeURIComponent(code)}&version=${encodeURIComponent(version)}`;
                window.location.assign(target);
            }
        }
    };

    const els = {
        error: document.getElementById('ir-error'),
        permission: document.getElementById('ir-permission'),
        loading: document.getElementById('ir-loading'),
        refresh: document.getElementById('ir-refresh'),
        catalogTable: document.getElementById('ir-catalog-table'),
        catalogCount: document.getElementById('ir-catalog-count'),
        emptyCatalog: document.getElementById('ir-empty-catalog'),
        batchCount: document.getElementById('ir-batch-count'),
        batchList: document.getElementById('ir-batch-list'),
        emptyBatches: document.getElementById('ir-empty-batches'),
        selectedBatch: document.getElementById('ir-selected-batch'),
        emptyDiffs: document.getElementById('ir-empty-diffs'),
        diffWrap: document.getElementById('ir-diff-review-list-wrap'),
        diffBody: document.getElementById('ir-diff-body'),
        reasonModal: document.getElementById('ir-reason-modal'),
        reasonTitle: document.getElementById('ir-reason-title'),
        reasonInput: document.getElementById('ir-reason'),
        reasonError: document.getElementById('ir-reason-error'),
        reasonSubmit: document.getElementById('ir-reason-submit')
    };

    const reasonModal = els.reasonModal && window.bootstrap ? new window.bootstrap.Modal(els.reasonModal) : null;
    let catalogDt = null;
    const safe = (value) => value === null || value === undefined ? '' : String(value);
    const pick = (source, camelName, pascalName) => source?.[camelName] ?? source?.[pascalName];
    const escapeHtml = (value) => safe(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    const setVisible = (el, visible) => {
        if (el) el.classList.toggle('d-none', !visible);
    };
    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress' || error?.status === 401;
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
    };
    const showError = (message, status) => {
        setVisible(els.permission, status === 403);
        if (!els.error || status === 403) return;
        els.error.textContent = message || L().ErrorOccurred || 'ErrorOccurred';
        setVisible(els.error, true);
    };
    const clearError = () => {
        setVisible(els.error, false);
        setVisible(els.permission, false);
    };
    const showToast = (message, type) => {
        if (window.showToast) window.showToast(message, type || 'info');
    };
    const parseEnvelope = async (response) => {
        const text = await response.text();
        if (!text) return {};
        try { return JSON.parse(text); } catch { return { errors: [text] }; }
    };
    const envelopeData = (json) => json.data || json.Data || json;
    const envelopeErrors = (json) => json.errors || json.Errors || [];
    const api = async (url, options) => {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        const json = await parseEnvelope(response);
        if (!response.ok || json.isSuccessful === false || json.IsSuccessful === false) {
            const errors = envelopeErrors(json);
            const error = new Error(errors.length ? errors.join(' ') : response.status === 403 ? L().PermissionDenied : L().GatewayError);
            error.status = response.status;
            throw error;
        }
        return envelopeData(json);
    };
    const changeTypeName = (value) => {
        const names = ['New', 'Changed', 'Missing', 'Deprecated', 'Unchanged'];
        return typeof value === 'number' ? names[value] || safe(value) : safe(value);
    };
    const lifecycleStatusName = (value) => {
        const names = ['Discovered', 'PendingReview', 'Confirmed', 'Active', 'Changed', 'MissingInSource', 'Deprecated', 'Retired', 'Rejected'];
        return typeof value === 'number' ? names[value] || safe(value) : safe(value);
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
    const compactId = (value) => {
        const raw = safe(value);
        return raw.length > 12 ? `${raw.slice(0, 8)}...${raw.slice(-4)}` : raw || '-';
    };
    const formatText = (template, value) => safe(template || '{0}').replace('{0}', safe(value));
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
    const changeBadge = (changeType) => {
        const normalized = changeTypeName(changeType);
        const map = {
            New: 'bg-label-success',
            Changed: 'bg-label-info',
            Missing: 'bg-label-warning',
            Deprecated: 'bg-label-warning',
            Unchanged: 'bg-label-secondary'
        };
        return `<span class="badge ${map[normalized] || 'bg-label-secondary'}">${escapeHtml(label(normalized))}</span>`;
    };

    const catalogRows = () => state.snapshots.map((snapshot) => {
            const definition = snapshot.definition || snapshot.Definition || {};
            const code = snapshot.interfaceCode || snapshot.InterfaceCode || definition.interfaceCode || definition.InterfaceCode;
            const version = snapshot.interfaceVersion || snapshot.InterfaceVersion || definition.interfaceVersion || definition.InterfaceVersion;
            const lifecycle = definition.lifecycleStatus || definition.LifecycleStatus || snapshot.lifecycleStatus || snapshot.LifecycleStatus;
            return {
                interfaceCode: safe(code),
                interfaceVersion: safe(version),
                displayName: safe(definition.displayName || definition.DisplayName),
                lifecycleStatus: lifecycleStatusName(lifecycle),
                snapshot
            };
        });

    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'InterfaceRegistry' };
    const totalColumnCount = 5;
    const baseOrder = [[1, 'asc']];
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let appliedFilters = { interfaceCode: '', status: '' };

    const emptyFilters = () => ({ interfaceCode: '', status: '' });
    const normalizeFilters = (filters) => {
        const s = filters || {};
        return {
            interfaceCode: String(s.interfaceCode || '').trim(),
            status: String(s.status || '').trim()
        };
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter(v => v !== '').length;

    const setSaveFilterVisible = (visible) => {
        const button = document.getElementById('btnSaveIrView');
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
        const codeInp = document.getElementById('filterInterfaceCode');
        const statInp = document.getElementById('filterStatus');
        if (codeInp) codeInp.value = appliedFilters.interfaceCode;
        if (statInp) {
            statInp.value = appliedFilters.status;
            if (window.jQuery?.fn?.select2) $(statInp).val(appliedFilters.status).trigger('change');
        }

        if (Array.isArray(state.columnOrder) && api.colReorder) api.colReorder.order(state.columnOrder, true);
        
        // Apply Column Filters
        api.column(1).search(appliedFilters.interfaceCode || '');
        api.column(3).search(appliedFilters.status ? `^${appliedFilters.status}$` : '', true, false);

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

            console.error('[InterfaceRegistry] Load view failed', e);
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

            console.error('[InterfaceRegistry] Save view failed', e);
            window.showToast?.(L().ErrorOccurred || 'ErrorOccurred', 'error');
        }
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('.select2').each(function () {
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
        const toolbarRow = document.querySelector('.datatables-interface-registry-catalog')?.closest('.dt-container')?.querySelector('.row:first-child');
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
            host.style.display = 'block';
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl).toggle();
    };

    const ensureCatalogDataTable = async () => {
        if (catalogDt || !els.catalogTable || typeof DataTable === 'undefined') return;

        const savedState = await loadDefaultView();

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L().Filter || '', 'data-bs-toggle': 'tooltip' },
            action: toggleInlineFilter
        };

        const saveFilterBtn = {
            text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L().SaveView || '')}</span>`,
            className: 'btn btn-label-primary dt-save-filter-btn d-none',
            attr: { id: 'btnSaveIrView', title: L().SaveView || '', 'data-bs-toggle': 'tooltip' },
            action: () => saveDefaultView(catalogDt)
        };

        const config = {
            data: [],
            processing: false,
            serverSide: false,
            stateSave: false,
            pageLength: 10,
            order: savedState?.order || baseOrder,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                {
                    data: null,
                    name: 'control',
                    orderable: false,
                    searchable: false,
                    className: 'control',
                    defaultContent: ''
                },
                {
                    data: 'interfaceCode',
                    name: 'interfaceCode',
                    render: (data, type, row) => {
                        if (type !== 'display') return data;
                        return `<div class="fw-semibold">${escapeHtml(data)}</div><small class="text-muted">${escapeHtml(row.displayName)}</small>`;
                    }
                },
                { data: 'interfaceVersion', name: 'interfaceVersion', render: escapeHtml },
                { data: 'lifecycleStatus', name: 'lifecycleStatus', render: statusBadge },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (_data, type, row) => {
                        if (type !== 'display') return '';
                        const code = safe(row.interfaceCode);
                        const version = safe(row.interfaceVersion);
                        
                        const actions = [
                            {
                                key: 'quickView',
                                className: 'js-quick-view',
                                text: L().View || '',
                                icon: 'bx bx-show',
                                attrs: { 'data-code': code, 'data-version': version }
                            }
                        ];

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            language: {
                search: '',
                searchPlaceholder: L().Search || '',
                emptyTable: L().EmptyCatalogState || '',
                zeroRecords: L().EmptyCatalogState || ''
            },
            buttons: window.DtDefaults?.exportButtons ? window.DtDefaults.exportButtons(null, null, { filterBtn, saveFilterBtn }, { skipColVis: true, exportColumns: [1, 2, 3] }) : [],
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

        catalogDt = new DataTable(els.catalogTable, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);

        $(els.catalogTable).on('column-reorder.dt columns-reordered.dt search.dt order.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(catalogDt));
        });

        // Bind Filters
        const btnApply = document.getElementById('btnFilterApply');
        const btnReset = document.getElementById('btnFilterReset');
        const filterForm = document.getElementById('irFilterForm');

        if (btnApply) {
            btnApply.addEventListener('click', () => {
                appliedFilters.interfaceCode = document.getElementById('filterInterfaceCode')?.value || '';
                appliedFilters.status = document.getElementById('filterStatus')?.value || '';

                catalogDt.column(1).search(appliedFilters.interfaceCode);
                catalogDt.column(3).search(appliedFilters.status ? `^${appliedFilters.status}$` : '', true, false);
                catalogDt.draw();

                window.DtDefaults.updateVisualState(catalogDt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(catalogDt));
            });
        }

        if (btnReset) {
            btnReset.addEventListener('click', () => {
                filterForm.reset();
                if (window.jQuery?.fn?.select2) $('.select2').val('').trigger('change');

                applySavedTableState(catalogDt, getResetBaselineState());
                window.DtDefaults.updateVisualState(catalogDt, 0);
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(catalogDt));
            });
        }

        if (window.DitenDataTable) {
            window.DitenDataTable.bindActionDispatcher({
                tableEl: els.catalogTable,
                dt: catalogDt,
                onRowAction: rowActionHandlers
            });
        }
    };

    const updateKpis = () => {
        const totalInterfaces = state.snapshots.length;
        const activeSnapshots = state.snapshots.filter(s => {
            const def = s.definition || s.Definition || {};
            const status = def.lifecycleStatus || def.LifecycleStatus || s.lifecycleStatus || s.LifecycleStatus;
            return status === 3 || status === 'Active'; // Active enum value is 3
        }).length;
        const totalBatches = state.batches.length;
        
        // This is a rough estimate based on currently loaded batches, 
        // ideally we'd have a specific stats endpoint or sum it up from all batches
        const pendingReviews = state.batches.reduce((acc, b) => acc + (b.pendingCount || b.PendingCount || 0), 0);

        if (document.getElementById('kpi-total')) document.getElementById('kpi-total').textContent = String(totalInterfaces);
        if (document.getElementById('kpi-active')) document.getElementById('kpi-active').textContent = String(activeSnapshots);
        if (document.getElementById('kpi-batches')) document.getElementById('kpi-batches').textContent = String(totalBatches);
        if (document.getElementById('kpi-pending')) document.getElementById('kpi-pending').textContent = String(pendingReviews);
    };

    const renderCatalog = async () => {
        await ensureCatalogDataTable();
        if (catalogDt) {
            catalogDt.clear();
            catalogDt.rows.add(catalogRows());
            catalogDt.draw(false);
            window.DtDefaults?.updateVisualState?.(catalogDt, getAppliedFilterCount());
        }
        if (els.catalogCount) els.catalogCount.textContent = String(state.snapshots.length);
        setVisible(els.emptyCatalog, false);
        updateKpis();
    };

    const renderBatches = () => {
        const filteredBatches = state.batches.filter(batch => {
            const status = batch.status || batch.Status;
            const normalized = lifecycleStatusName(status);
            return normalized !== 'Confirmed';
        });

        const rows = filteredBatches.map((batch) => {
            const id = batch.batchId || batch.BatchId;
            const selected = safe(id) === safe(state.selectedBatchId);
            const importedAt = batch.importedAtUtc || batch.ImportedAtUtc;
            return `<button type="button" class="list-group-item list-group-item-action js-select-batch ${selected ? 'active' : ''}" data-id="${escapeHtml(id)}">
                <div class="d-flex justify-content-between align-items-start gap-2">
                    <div>
                        <div class="fw-semibold">${escapeHtml(batch.sourceService || batch.SourceService)}</div>
                        <small>${escapeHtml(batch.sourceModuleCode || batch.SourceModuleCode)} · ${escapeHtml(formatDateTime(importedAt))}</small>
                    </div>
                    ${statusBadge(batch.status || batch.Status)}
                </div>
                <div class="small mt-2">+${escapeHtml(batch.newCount || batch.NewCount || 0)} / Δ${escapeHtml(batch.changedCount || batch.ChangedCount || 0)} / ?${escapeHtml(batch.missingCount || batch.MissingCount || 0)}</div>
            </button>`;
        });

        if (els.batchList) els.batchList.innerHTML = rows.join('');
        if (els.batchCount) els.batchCount.textContent = String(filteredBatches.length);
        setVisible(els.emptyBatches, filteredBatches.length === 0);
        updateKpis();
    };

    const renderDiffs = () => {
        const hasBatch = !!state.selectedBatchId;
        const hasDiffs = state.diffs.length > 0;
        if (els.selectedBatch) {
            els.selectedBatch.textContent = hasBatch
                ? formatText(L().SelectedDiscoveryGroupLabel, compactId(state.selectedBatchId))
                : '';
        }
        setVisible(els.emptyDiffs, !hasBatch || !hasDiffs);
        setVisible(els.diffWrap, hasBatch && hasDiffs);

        if (!els.diffBody) return;

        els.diffBody.innerHTML = state.diffs.map((item) => {
            const id = pick(item, 'diffItemId', 'DiffItemId');
            const status = pick(item, 'reviewStatus', 'ReviewStatus') ?? 'PendingReview';
            const locked = status === 'Confirmed' || status === 'Rejected';
            const interfaceCode = pick(item, 'interfaceCode', 'InterfaceCode') ?? '';
            const interfaceVersion = pick(item, 'interfaceVersion', 'InterfaceVersion') ?? '';
            const endpointKey = pick(item, 'endpointKey', 'EndpointKey') ?? '-';
            const reviewedBy = pick(item, 'reviewedBy', 'ReviewedBy') ?? '-';
            const reviewedAt = pick(item, 'reviewedAtUtc', 'ReviewedAtUtc') ?? '';

            return `
                <div class="p-3 border-bottom">
                    <div class="d-flex flex-column flex-lg-row justify-content-between align-items-start gap-3">
                        <div class="min-w-0 flex-grow-1">
                            <div class="d-flex align-items-center flex-wrap gap-2 mb-2">
                                ${changeBadge(pick(item, 'changeType', 'ChangeType') ?? '')}
                                ${statusBadge(status)}
                                <span class="fw-semibold text-break">${escapeHtml(interfaceCode)}</span>
                                <span class="text-muted small">${escapeHtml(interfaceVersion)}</span>
                            </div>
                            <div class="small text-muted text-break mb-2">
                                <i class="bx bx-git-compare me-1"></i>${escapeHtml(endpointKey)}
                            </div>
                            <div class="small text-muted mt-2">
                                ${escapeHtml(L().ReviewedBy || '')}: ${escapeHtml(reviewedBy)}${reviewedAt ? ` · ${escapeHtml(formatDateTime(reviewedAt))}` : ''}
                            </div>
                        </div>
                        <div class="d-flex align-items-center gap-2 flex-shrink-0">
                            <button type="button" class="btn btn-sm btn-label-success js-confirm-diff" data-id="${escapeHtml(id)}" ${locked ? 'disabled' : ''}>
                                <i class="bx bx-check me-1"></i>${escapeHtml(L().Confirm || '')}
                            </button>
                            <button type="button" class="btn btn-sm btn-label-danger js-reject-diff" data-id="${escapeHtml(id)}" ${locked ? 'disabled' : ''}>
                                <i class="bx bx-x me-1"></i>${escapeHtml(L().Reject || '')}
                            </button>
                        </div>
                    </div>
                </div>`;
        }).join('');
    };

    const loadCatalog = async () => {
        state.snapshots = await api('/Platform/InterfaceRegistry/api/interfaces');
        await renderCatalog();
    };

    const loadBatches = async () => {
        state.batches = await api('/Platform/InterfaceRegistry/api/discovery-batches');
        renderBatches();
    };

    const loadDiffs = async (batchId) => {
        state.selectedBatchId = batchId;
        state.diffs = await api(`/Platform/InterfaceRegistry/api/discovery-batches/${encodeURIComponent(batchId)}/diffs`);
        renderBatches();
        renderDiffs();
    };

    const loadAll = async () => {
        clearError();
        setVisible(els.loading, true);
        try {
            await Promise.all([loadCatalog(), loadBatches()]);
        } catch (error) {
            showError(error.message, error.status);
        } finally {
            setVisible(els.loading, false);
        }
    };

    const mutate = async (url, options) => {
        clearError();
        try {
            await api(url, Object.assign({ method: 'POST' }, options || {}));
            showToast(L().Saved || L().RecordSaved || 'RecordSaved', 'success');
            await loadCatalog();
            await loadBatches();
            if (state.selectedBatchId) await loadDiffs(state.selectedBatchId);
        } catch (error) {
            showError(error.message, error.status);
        }
    };

    const openReject = (kind, id) => {
        state.pendingReject = { kind, id };
        if (els.reasonInput) els.reasonInput.value = '';
        setVisible(els.reasonError, false);
        reasonModal?.show();
    };


    const bindActions = () => {
        document.addEventListener('click', (event) => {
            const selectBatch = event.target.closest('.js-select-batch');
            if (selectBatch) {
                loadDiffs(selectBatch.getAttribute('data-id'));
                return;
            }

            const confirmDiff = event.target.closest('.js-confirm-diff');
            if (confirmDiff) {
                mutate(`/Platform/InterfaceRegistry/api/diffs/${encodeURIComponent(confirmDiff.getAttribute('data-id'))}/confirm`);
                return;
            }

            const rejectDiff = event.target.closest('.js-reject-diff');
            if (rejectDiff) {
                openReject('diff', rejectDiff.getAttribute('data-id'));
            }
        });
    };

    els.refresh?.addEventListener('click', loadAll);
    els.reasonSubmit?.addEventListener('click', () => {
        const reason = els.reasonInput?.value?.trim() || '';
        if (!reason) {
            setVisible(els.reasonError, true);
            return;
        }
        const pending = state.pendingReject;
        reasonModal?.hide();
        if (!pending) return;
        const url = pending.kind === 'batch'
            ? `/Platform/InterfaceRegistry/api/discovery-batches/${encodeURIComponent(pending.id)}/reject`
            : `/Platform/InterfaceRegistry/api/diffs/${encodeURIComponent(pending.id)}/reject`;
        mutate(url, {
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ reviewReason: reason })
        });
    });

    bindActions();
    loadAll();
})();
