'use strict';

const ModuleCatalogList = (function () {
    let dt;
    let hierarchy = null;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let permissionDenied = false;
    let currentItems = [];
    let currentDetailModuleId = '';
    let currentDetailPages = [];
    let L = window.L10n || {};

    const dtTableEl = document.querySelector('.datatables-module-catalog');
    const apiUrl = window.API?.platform || window.ApiBaseUrl || '';
    const endpoints = {
        base: `${apiUrl}/api/platform/catalog`,
        hierarchy: '/hierarchy',
        modules: '/modules',
        import: '/import',
        domainLandscapes: '/domain-landscapes',
        suitePlatforms: '/suite-platforms',
        capabilityGroups: '/capability-groups',
        modulePages: (moduleId) => `/modules/${encodeURIComponent(moduleId)}/pages`,
        modulePage: (moduleId, pageCode) => `/modules/${encodeURIComponent(moduleId)}/pages/${encodeURIComponent(pageCode)}`,
        moduleDetail: (moduleId) => `/modules/${encodeURIComponent(moduleId)}`
    };
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'ModuleCatalog' };
    const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8, 9, 10];
    const totalColumnCount = 12;
    const baseOrder = [[2, 'asc']];
    const filterCollapseId = 'inlineFilterCollapse';
    const importModalEl = document.getElementById('importCatalogModal');
    const editorModalEl = document.getElementById('catalogEditorModal');
    let editorState = { type: '', mode: 'create', record: null };

    let appliedFilters = {
        domainLandscapeId: '',
        suitePlatformId: '',
        capabilityGroupId: '',
        status: '',
        isTenantAssignable: '',
        isPlatformCore: ''
    };

    const syncL10n = () => {
        L = window.L10n || {};
    };

    const isAuthHandledError = (error) => error?.authHandled === true || error?.code === 'auth-refresh-in-progress';

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    const normalizeString = (value) => typeof value === 'string' ? value.trim() : '';

    const parseBooleanOption = (value) => value === 'true' ? true : (value === 'false' ? false : null);

    const getStatusLabel = (status) => {
        const normalized = normalizeString(status);
        switch (normalized) {
            case 'Draft':
                return L.Draft || normalized;
            case 'Active':
                return L.ActiveStatus || normalized;
            case 'Deprecated':
                return L.Deprecated || normalized;
            case 'Retired':
                return L.Retired || normalized;
            default:
                return normalized || L.Unknown || '-';
        }
    };

    const createDefaultColumnVisibility = () =>
        saveViewColumnIndexes.reduce((acc, index) => {
            acc[index] = true;
            return acc;
        }, {});

    const normalizeColumnVisibility = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((index, position) => {
                if (typeof colVis[index] === 'boolean') normalized[index] = colVis[index];
                else if (typeof colVis[position] === 'boolean') normalized[index] = colVis[position];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((index) => {
                if (typeof colVis[index] === 'boolean') normalized[index] = colVis[index];
            });
        }

        return Object.keys(normalized).length ? normalized : null;
    };

    const captureColumnVisibility = (api) => {
        const colVis = {};
        saveViewColumnIndexes.forEach((index) => {
            try { colVis[index] = !!api.column(index).visible(); } catch (error) { }
        });
        return colVis;
    };

    const applyColumnVisibility = (api, colVis) => {
        const normalized = normalizeColumnVisibility(colVis);
        if (!normalized) return;
        saveViewColumnIndexes.forEach((index) => {
            if (typeof normalized[index] === 'boolean') {
                try { api.column(index).visible(normalized[index], false); } catch (error) { }
            }
        });
    };

    const normalizeColumnOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount ? normalized : null;
    };

    const captureColumnOrder = (api) => {
        try { return normalizeColumnOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };

    const applyColumnOrder = (api, order) => {
        const normalized = normalizeColumnOrder(order);
        if (!normalized || typeof api?.colReorder?.order !== 'function') return;
        try { api.colReorder.order(normalized, true); } catch (error) { }
    };

    const getSearchInputValue = (api) => {
        try { return normalizeString(api.table().container()?.querySelector('.dt-search input')?.value || ''); }
        catch (error) { return ''; }
    };

    const syncSearchInput = (api, value) => {
        try {
            const input = api.table().container()?.querySelector('.dt-search input');
            if (input) input.value = value || '';
        } catch (error) { }
    };

    const getSavedViewDefinition = (savedView) => {
        const raw = savedView?.viewDefinition ?? savedView?.ViewDefinition ?? savedView?.viewDefinitionJson ?? savedView?.ViewDefinitionJson ?? {};
        if (raw && typeof raw === 'object') return raw;
        if (typeof raw === 'string') {
            try { return JSON.parse(raw) || {}; } catch (error) { return {}; }
        }
        return {};
    };

    const mapSavedViewToState = (savedView) => {
        const definition = getSavedViewDefinition(savedView);
        return {
            search: normalizeString(definition.search),
            domainLandscapeId: normalizeString(definition.domainLandscapeId),
            suitePlatformId: normalizeString(definition.suitePlatformId),
            capabilityGroupId: normalizeString(definition.capabilityGroupId),
            status: normalizeString(definition.status),
            isTenantAssignable: normalizeString(definition.isTenantAssignable),
            isPlatformCore: normalizeString(definition.isPlatformCore),
            colVis: normalizeColumnVisibility(definition.colVis),
            columnOrder: normalizeColumnOrder(definition.columnOrder),
            order: Array.isArray(definition.order) ? definition.order : null
        };
    };

    const serializeView = (view) => JSON.stringify({
        search: normalizeString(view?.search),
        domainLandscapeId: normalizeString(view?.domainLandscapeId),
        suitePlatformId: normalizeString(view?.suitePlatformId),
        capabilityGroupId: normalizeString(view?.capabilityGroupId),
        status: normalizeString(view?.status),
        isTenantAssignable: normalizeString(view?.isTenantAssignable),
        isPlatformCore: normalizeString(view?.isPlatformCore),
        colVis: normalizeColumnVisibility(view?.colVis) || createDefaultColumnVisibility(),
        columnOrder: normalizeColumnOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });

    const getCurrentView = (api) => ({
        search: getSearchInputValue(api),
        domainLandscapeId: appliedFilters.domainLandscapeId,
        suitePlatformId: appliedFilters.suitePlatformId,
        capabilityGroupId: appliedFilters.capabilityGroupId,
        status: appliedFilters.status,
        isTenantAssignable: appliedFilters.isTenantAssignable,
        isPlatformCore: appliedFilters.isPlatformCore,
        colVis: captureColumnVisibility(api),
        columnOrder: captureColumnOrder(api),
        order: typeof api.order === 'function' ? api.order() : baseOrder
    });

    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            search: '',
            domainLandscapeId: '',
            suitePlatformId: '',
            capabilityGroupId: '',
            status: '',
            isTenantAssignable: '',
            isPlatformCore: '',
            colVis: createDefaultColumnVisibility(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };

        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || []);
            defaultViewRecord = items.find((view) => view.isDefault || view.IsDefault) || null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
        } catch (error) {
            if (!isAuthHandledError(error)) {
                console.warn('[ModuleCatalog] Default view could not be loaded.', error);
            }
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: 'Default',
            isDefault: true,
            viewDefinition: view
        };

        if (defaultViewRecord) {
            const id = defaultViewRecord.id || defaultViewRecord.Id;
            defaultViewRecord = await personalizationClient.updateView(id, Object.assign({}, defaultViewRecord, payload));
        } else {
            defaultViewRecord = await personalizationClient.saveView(payload);
        }

        defaultViewState = view;
        return defaultViewRecord;
    };

    const getSelectedIds = () =>
        Array.from(dtTableEl.querySelectorAll('tbody .dt-checkboxes:checked')).map((checkbox) => checkbox.value);

    const clearSelection = () => {
        dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach((checkbox) => {
            checkbox.checked = false;
            checkbox.closest('tr')?.classList.remove('selected');
        });

        const header = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (header) {
            header.checked = false;
            header.indeterminate = false;
        }

        updateBulkBar();
    };

    const updateBulkBar = () => {
        const selectedIds = getSelectedIds();
        const bulkBar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (countEl) countEl.innerText = String(selectedIds.length);
        bulkBar?.classList.toggle('d-none', selectedIds.length === 0);

        const checkboxes = Array.from(dtTableEl.querySelectorAll('tbody .dt-checkboxes'));
        const header = dtTableEl.querySelector('.dt-checkboxes-select-all');
        if (header) {
            header.checked = checkboxes.length > 0 && selectedIds.length === checkboxes.length;
            header.indeterminate = selectedIds.length > 0 && selectedIds.length < checkboxes.length;
        }
    };

    const setStateMessage = (kind, message) => {
        const host = document.getElementById('catalogStateHost');
        if (!host) return;
        if (!message) {
            host.innerHTML = '';
            return;
        }

        host.innerHTML = `<div class="alert alert-${kind} mb-0">${escapeHtml(message)}</div>`;
    };

    const setPermissionReadOnly = () => {
        permissionDenied = true;
        document.getElementById('btnImportSubmit')?.setAttribute('disabled', 'disabled');
        document.getElementById('importPayload')?.setAttribute('disabled', 'disabled');
        document.getElementById('oc-btn-edit')?.setAttribute('disabled', 'disabled');
        document.querySelectorAll('[data-catalog-action]').forEach((button) => {
            if (button.getAttribute('data-catalog-action') !== 'refresh') {
                button.setAttribute('disabled', 'disabled');
                button.classList.add('disabled');
            }
        });
        setStateMessage('warning', L.PermissionDenied);
    };

    const createRequestError = (response, payload, text) => {
        const message = payload?.detail || payload?.message || payload?.title || text || L.BackendError || 'Backend error';
        const error = new Error(message);
        error.status = response.status;
        error.payload = payload;
        error.rawText = text;
        return error;
    };

    const requestJson = async (url, options) => {
        const response = await fetch(url, Object.assign({
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' }
        }, options || {}));

        if (response.status === 401 || response.status === 403) {
            setPermissionReadOnly();
            const authError = new Error('auth-refresh-in-progress');
            authError.authHandled = true;
            authError.code = 'auth-refresh-in-progress';
            throw authError;
        }

        const text = await response.text();
        let payload = null;
        try { payload = text ? JSON.parse(text) : null; } catch (error) { payload = null; }

        if (!response.ok) {
            throw createRequestError(response, payload, text);
        }

        return payload?.data ?? payload;
    };

    const buildCatalogUrl = (path) => `${endpoints.base}${path}`;

    const setSummary = (summary) => {
        document.getElementById('kpi-domains').innerText = String(summary?.totalDomains || 0);
        document.getElementById('kpi-suites').innerText = String(summary?.totalSuites || 0);
        document.getElementById('kpi-capabilities').innerText = String(summary?.totalCapabilityGroups || 0);
        document.getElementById('kpi-modules').innerText = String(summary?.totalModules || 0);
        document.getElementById('kpi-assignable').innerText = String(summary?.tenantAssignableModules || 0);
        document.getElementById('kpi-lifecycle').innerText = String(summary?.deprecatedOrRetiredModules || 0);
    };

    const renderHierarchySummary = () => {
        const host = document.getElementById('hierarchyTree');
        const stateHost = document.getElementById('hierarchyState');
        const countEl = document.getElementById('hierarchy-domain-count');
        if (!host) return;

        const domains = Array.isArray(hierarchy?.domainLandscapes) ? hierarchy.domainLandscapes : [];
        const suites = Array.isArray(hierarchy?.suitePlatforms) ? hierarchy.suitePlatforms : [];
        const capabilities = Array.isArray(hierarchy?.capabilityGroups) ? hierarchy.capabilityGroups : [];
        if (countEl) countEl.innerText = String(domains.length);

        if (!domains.length) {
            host.innerHTML = '';
            if (stateHost) {
                stateHost.innerHTML = `<div class="alert alert-secondary py-2 mb-0">${escapeHtml(L.NoHierarchyData || L.EmptyCatalog || '-')}</div>`;
            }
            return;
        }

        if (stateHost) stateHost.innerHTML = '';
        host.innerHTML = domains.map((domain) => {
            const domainSuites = suites.filter((suite) => suite.domainLandscapeId === domain.id);
            return `<div class="module-hierarchy-domain mb-3">
                <div class="module-hierarchy-item d-flex align-items-center justify-content-between gap-2">
                    <div class="text-truncate">
                        <span class="fw-medium text-heading">${escapeHtml(domain.name)}</span>
                        <small class="d-block text-muted module-id-mono">${escapeHtml(domain.code || domain.id || '-')}</small>
                    </div>
                    <span class="badge bg-label-primary">${domainSuites.length}</span>
                </div>
                ${domainSuites.map((suite) => {
                    const suiteCapabilities = capabilities.filter((capability) => capability.suitePlatformId === suite.id);
                    return `<div class="module-hierarchy-suite">
                        <div class="module-hierarchy-item d-flex align-items-center justify-content-between gap-2">
                            <div class="text-truncate">
                                <span class="small fw-medium">${escapeHtml(suite.name)}</span>
                                <small class="d-block text-muted module-id-mono">${escapeHtml(suite.code || suite.id || '-')}</small>
                            </div>
                            <span class="badge bg-label-info">${suiteCapabilities.length}</span>
                        </div>
                        ${suiteCapabilities.slice(0, 6).map((capability) => `<div class="module-hierarchy-capability module-hierarchy-item">
                            <div class="text-truncate">
                                <span class="small">${escapeHtml(capability.name)}</span>
                                <small class="d-block text-muted module-id-mono">${escapeHtml(capability.code || capability.id || '-')}</small>
                            </div>
                        </div>`).join('')}
                    </div>`;
                }).join('')}
            </div>`;
        }).join('');
    };

    const fillSelect = (elementId, items, valueSelector, textSelector, placeholder) => {
        const select = document.getElementById(elementId);
        if (!select) return;

        const currentValue = select.value;
        select.innerHTML = '';

        const defaultOption = document.createElement('option');
        defaultOption.value = '';
        defaultOption.textContent = placeholder;
        select.appendChild(defaultOption);

        items.forEach((item) => {
            const option = document.createElement('option');
            option.value = valueSelector(item);
            option.textContent = textSelector(item);
            select.appendChild(option);
        });

        select.value = currentValue;
    };

    const syncDependentFilters = () => {
        if (!hierarchy) return;

        const domainLandscapeId = document.getElementById('filterDomain').value;
        const suitePlatformId = document.getElementById('filterSuite').value;
        const suites = hierarchy.suitePlatforms.filter((item) => !domainLandscapeId || item.domainLandscapeId === domainLandscapeId);
        const capabilityGroups = hierarchy.capabilityGroups.filter((item) => {
            if (suitePlatformId) return item.suitePlatformId === suitePlatformId;
            if (domainLandscapeId) return item.domainLandscapeId === domainLandscapeId;
            return true;
        });

        fillSelect('filterSuite', suites, (item) => item.id, (item) => item.name, L.AllOption || L.AnyOption);
        fillSelect('filterCapabilityGroup', capabilityGroups, (item) => item.id, (item) => item.name, L.AllOption || L.AnyOption);
    };

    const loadHierarchy = async () => {
        hierarchy = await requestJson(buildCatalogUrl(endpoints.hierarchy));
        setSummary(hierarchy.summary);
        renderHierarchySummary();
        fillSelect('filterDomain', hierarchy.domainLandscapes, (item) => item.id, (item) => item.name, L.AllOption || L.AnyOption);
        fillSelect('filterSuite', hierarchy.suitePlatforms, (item) => item.id, (item) => item.name, L.AllOption || L.AnyOption);
        fillSelect('filterCapabilityGroup', hierarchy.capabilityGroups, (item) => item.id, (item) => item.name, L.AllOption || L.AnyOption);
        syncDependentFilters();
    };

    const buildQueryString = () => {
        const params = new URLSearchParams();
        Object.keys(appliedFilters).forEach((key) => {
            const value = normalizeString(appliedFilters[key]);
            if (value) {
                params.set(key, value);
            }
        });

        return params.toString();
    };

    const loadModules = async () => {
        const suffix = buildQueryString();
        const result = await requestJson(buildCatalogUrl(`${endpoints.modules}${suffix ? `?${suffix}` : ''}`));
        currentItems = Array.isArray(result?.items) ? result.items : [];
        return currentItems;
    };

    const moduleStatusBadge = (status) => {
        const map = {
            Draft: 'bg-label-warning',
            Active: 'bg-label-success',
            Deprecated: 'bg-label-secondary',
            Retired: 'bg-label-danger'
        };

        return `<span class="badge ${map[status] || 'bg-label-secondary'}">${escapeHtml(getStatusLabel(status))}</span>`;
    };

    const renderBooleanBadge = (value) =>
        `<span class="badge ${value ? 'bg-label-success' : 'bg-label-secondary'} module-chip">
            <i class="icon-base bx ${value ? 'bx-check' : 'bx-minus'} icon-sm me-1"></i>${escapeHtml(value ? L.Yes : L.No)}
        </span>`;

    const renderModuleIdentity = (row) => `<div class="d-flex align-items-center gap-3">
        <div class="avatar avatar-sm bg-label-primary rounded">
            <span class="avatar-initial"><i class="icon-base bx bx-package"></i></span>
        </div>
        <div class="min-w-0">
            <a href="javascript:void(0);" class="fw-medium text-heading js-quick-view" data-module-id="${escapeHtml(row.moduleId)}">${escapeHtml(row.moduleName || '-')}</a>
            <small class="d-block text-muted module-id-mono text-truncate">${escapeHtml(row.moduleId || '-')}</small>
        </div>
    </div>`;

    const renderTextChip = (value, fallback) =>
        `<span class="badge bg-label-secondary module-chip text-truncate">${escapeHtml(value || fallback || '-')}</span>`;

    const getPagesCount = (row) => {
        const value = row?.pagesCount ?? row?.pageDefinitionsCount ?? row?.modulePagesCount;
        return Number.isInteger(Number(value)) ? Number(value) : null;
    };

    const renderPagesCount = (row) => {
        const count = getPagesCount(row);
        return count === null
            ? '<span class="badge bg-label-secondary">-</span>'
            : `<span class="badge bg-label-primary">${count}</span>`;
    };

    const populateOffcanvas = (detail) => {
        document.getElementById('oc-title').innerText = detail.moduleName || '-';
        document.getElementById('oc-subtitle').innerText = detail.moduleId || '-';
        document.getElementById('oc-status').outerHTML = moduleStatusBadge(detail.status).replace('<span', '<span id="oc-status"');
        const pagesCountEl = document.getElementById('oc-pages-count');
        if (pagesCountEl) pagesCountEl.innerText = String(currentDetailPages.length || getPagesCount(detail) || 0);
        const editButton = document.getElementById('oc-btn-edit');
        if (editButton) {
            editButton.removeAttribute('disabled');
            editButton.removeAttribute('title');
            editButton.dataset.moduleId = detail.moduleId || '';
        }

        const rows = [
            [L.Domain, detail.domainLandscapeName],
            [L.Suite, detail.suitePlatformName],
            [L.CapabilityGroup, detail.capabilityGroupName],
            [L.Placement, detail.placement || '-'],
            [L.SupportModel, detail.supportModel || '-'],
            [L.DependencyGate, detail.dependencyGate || '-'],
            [L.DeliveryOutcome, detail.deliveryOutcome || '-'],
            [L.IsPlatformCore, detail.isPlatformCore ? L.Yes : L.No],
            [L.IsTenantAssignable, detail.isTenantAssignable ? L.Yes : L.No],
            [L.IsActive, detail.isActive === false ? L.No : L.Yes],
            [L.CreatedDate, detail.createdDate || '-'],
            [L.ModifiedDate, detail.modifiedDate || '-']
        ];

        document.getElementById('oc-details-list').innerHTML = rows.map(([label, value]) =>
            `<dt class="col-5 fw-medium text-heading mb-2">${escapeHtml(label)}</dt><dd class="col-7 mb-2 text-break">${escapeHtml(value || '-')}</dd>`
        ).join('');
    };

    const renderPagesState = (message, kind) => {
        const host = document.getElementById('modulePagesState');
        if (!host) return;
        host.innerHTML = message ? `<div class="alert alert-${kind || 'secondary'} py-2 mb-0">${escapeHtml(message)}</div>` : '';
    };

    const renderModulePages = (pages) => {
        const tbody = document.querySelector('#modulePagesTable tbody');
        if (!tbody) return;

        currentDetailPages = Array.isArray(pages) ? pages : [];
        const pagesCountEl = document.getElementById('oc-pages-count');
        if (pagesCountEl) pagesCountEl.innerText = String(currentDetailPages.length);
        if (!currentDetailPages.length) {
            tbody.innerHTML = '';
            renderPagesState(L.NoPages || 'No page definitions yet.', 'secondary');
            return;
        }

        renderPagesState('', '');
        tbody.innerHTML = currentDetailPages.map((page) => `
            <tr>
                <td class="fw-medium">${escapeHtml(page.pageCode)}</td>
                <td>${escapeHtml(page.pageName)}</td>
                <td>${escapeHtml(page.pageType)}</td>
                <td class="text-break">${escapeHtml(page.routePath || '-')}</td>
                <td class="text-break">${escapeHtml(page.requiredPermissionKey || '-')}</td>
                <td>${renderBooleanBadge(page.isNavigationCandidate)}</td>
                <td>${renderBooleanBadge(page.isActive)}</td>
                <td class="text-end">
                    <button type="button" class="btn btn-sm btn-label-secondary js-edit-page" data-page-code="${escapeHtml(page.pageCode)}">${escapeHtml(L.EditPage || L.EditMetadata)}</button>
                </td>
            </tr>`).join('');
    };

    const loadModulePages = async (moduleId) => {
        try {
            const pages = await requestJson(buildCatalogUrl(endpoints.modulePages(moduleId)));
            renderModulePages(Array.isArray(pages) ? pages : []);
        } catch (error) {
            if (!isAuthHandledError(error)) {
                renderPagesState(mapBackendMessage(error.message || L.BackendError), 'danger');
            }
        }
    };

    const openDetail = async (moduleId) => {
        const detail = await requestJson(buildCatalogUrl(endpoints.moduleDetail(moduleId)));
        currentDetailModuleId = detail.moduleId || moduleId;
        await loadModulePages(currentDetailModuleId);
        populateOffcanvas(detail);
        const overviewTab = document.getElementById('module-overview-tab');
        if (overviewTab) bootstrap.Tab.getOrCreateInstance(overviewTab).show();
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
    };

    const getDomainLandscapes = () => Array.isArray(hierarchy?.domainLandscapes) ? hierarchy.domainLandscapes : [];
    const getSuitePlatforms = () => Array.isArray(hierarchy?.suitePlatforms) ? hierarchy.suitePlatforms : [];
    const getCapabilityGroups = () => Array.isArray(hierarchy?.capabilityGroups) ? hierarchy.capabilityGroups : [];

    const ensureHierarchyLoaded = async () => {
        if (!hierarchy) {
            await loadHierarchy();
        }
    };

    const getEditorField = (name) => document.querySelector(`#catalogEditorForm [name="${name}"]`);

    const clearEditorErrors = () => {
        document.getElementById('catalogEditorAlert').innerHTML = '';
        document.querySelectorAll('#catalogEditorForm .is-invalid').forEach((field) => field.classList.remove('is-invalid'));
        document.querySelectorAll('#catalogEditorForm .invalid-feedback').forEach((feedback) => { feedback.textContent = ''; });
    };

    const showEditorAlert = (message, kind) => {
        const host = document.getElementById('catalogEditorAlert');
        if (!host) return;
        host.innerHTML = message ? `<div class="alert alert-${kind || 'danger'} mb-3">${escapeHtml(message)}</div>` : '';
    };

    const showFieldError = (field, message) => {
        const element = getEditorField(field);
        if (!element) {
            showEditorAlert(message, 'danger');
            return;
        }

        element.classList.add('is-invalid');
        const feedback = element.closest('.mb-3, .col-md-6, .col-12')?.querySelector('.invalid-feedback');
        if (feedback) feedback.textContent = message;
    };

    const fieldLabel = (label, required) =>
        `${escapeHtml(label)}${required ? ' <span class="text-danger">*</span>' : ''}`;

    const inputField = (name, label, value, options) => {
        const opts = options || {};
        return `<div class="${opts.wrapper || 'col-md-6'}">
            <label class="form-label" for="catalog-${name}">${fieldLabel(label, opts.required)}</label>
            <input id="catalog-${name}" name="${name}" type="text" class="form-control" value="${escapeHtml(value || '')}" ${opts.readonly ? 'readonly' : ''}>
            <div class="invalid-feedback"></div>
        </div>`;
    };

    const textareaField = (name, label, value) => `<div class="col-12">
        <label class="form-label" for="catalog-${name}">${escapeHtml(label)}</label>
        <textarea id="catalog-${name}" name="${name}" class="form-control" rows="3">${escapeHtml(value || '')}</textarea>
        <div class="invalid-feedback"></div>
    </div>`;

    const checkboxField = (name, label, checked, hint) => `<div class="col-md-6">
        <div class="form-check mt-4">
            <input id="catalog-${name}" name="${name}" type="checkbox" class="form-check-input" ${checked ? 'checked' : ''}>
            <label class="form-check-label" for="catalog-${name}">${escapeHtml(label)}</label>
        </div>
        ${hint ? `<div class="form-text">${escapeHtml(hint)}</div>` : ''}
        <div class="invalid-feedback"></div>
    </div>`;

    const renderOptions = (items, selectedValue, placeholder, valueSelector, textSelector) => {
        const selected = normalizeString(selectedValue);
        return `<option value="">${escapeHtml(placeholder)}</option>${items.map((item) => {
            const value = String(valueSelector(item));
            return `<option value="${escapeHtml(value)}" ${value === selected ? 'selected' : ''}>${escapeHtml(textSelector(item))}</option>`;
        }).join('')}`;
    };

    const selectField = (name, label, items, selectedValue, placeholder, options) => {
        const opts = options || {};
        return `<div class="${opts.wrapper || 'col-md-6'}">
            <label class="form-label" for="catalog-${name}">${fieldLabel(label, opts.required)}</label>
            <select id="catalog-${name}" name="${name}" class="form-select">
                ${renderOptions(items, selectedValue, placeholder, opts.valueSelector || ((item) => item.id), opts.textSelector || ((item) => item.name))}
            </select>
            <div class="invalid-feedback"></div>
        </div>`;
    };

    const getSuitesForDomain = (domainLandscapeId) =>
        getSuitePlatforms().filter((item) => !domainLandscapeId || item.domainLandscapeId === domainLandscapeId);

    const getCapabilitiesForSuite = (domainLandscapeId, suitePlatformId) =>
        getCapabilityGroups().filter((item) => {
            if (suitePlatformId) return item.suitePlatformId === suitePlatformId;
            if (domainLandscapeId) return item.domainLandscapeId === domainLandscapeId;
            return true;
        });

    const refreshEditorDependentSelects = () => {
        const domainSelect = getEditorField('domainLandscapeId');
        const suiteSelect = getEditorField('suitePlatformId');
        const capabilitySelect = getEditorField('capabilityGroupId');
        if (!domainSelect || !suiteSelect) return;

        const domainLandscapeId = domainSelect.value;
        const currentSuite = suiteSelect.value;
        const suites = getSuitesForDomain(domainLandscapeId);
        suiteSelect.innerHTML = renderOptions(suites, currentSuite, L.SelectSuite || L.Suite, (item) => item.id, (item) => item.name);
        if (!suites.some((item) => item.id === currentSuite)) suiteSelect.value = '';

        if (capabilitySelect) {
            const currentCapability = capabilitySelect.value;
            const capabilities = getCapabilitiesForSuite(domainLandscapeId, suiteSelect.value);
            capabilitySelect.innerHTML = renderOptions(capabilities, currentCapability, L.SelectCapabilityGroup || L.CapabilityGroup, (item) => item.id, (item) => item.name);
            if (!capabilities.some((item) => item.id === currentCapability)) capabilitySelect.value = '';
        }
    };

    const renderEditorForm = () => {
        const record = editorState.record || {};
        const form = document.getElementById('catalogEditorForm');
        if (!form) return;

        if (editorState.type === 'domain') {
            form.innerHTML = `<div class="row g-3">
                ${inputField('name', L.Name, record.name, { required: true })}
                ${inputField('code', L.Code, record.code)}
                ${textareaField('description', L.Description, record.description)}
                ${checkboxField('isActive', L.IsActive, record.isActive !== false)}
            </div>`;
            return;
        }

        if (editorState.type === 'suite') {
            form.innerHTML = `<div class="row g-3">
                ${selectField('domainLandscapeId', L.Domain, getDomainLandscapes(), record.domainLandscapeId, L.SelectDomain || L.Domain, { required: true })}
                ${inputField('name', L.Name, record.name, { required: true })}
                ${inputField('code', L.Code, record.code)}
                ${textareaField('description', L.Description, record.description)}
                ${checkboxField('isActive', L.IsActive, record.isActive !== false)}
            </div>`;
            return;
        }

        if (editorState.type === 'capability') {
            form.innerHTML = `<div class="row g-3">
                ${selectField('domainLandscapeId', L.Domain, getDomainLandscapes(), record.domainLandscapeId, L.SelectDomain || L.Domain, { required: true })}
                ${selectField('suitePlatformId', L.Suite, getSuitesForDomain(record.domainLandscapeId), record.suitePlatformId, L.SelectSuite || L.Suite, { required: true })}
                ${inputField('name', L.Name, record.name, { required: true })}
                ${inputField('code', L.Code, record.code)}
                ${textareaField('description', L.Description, record.description)}
                ${checkboxField('isActive', L.IsActive, record.isActive !== false)}
            </div>`;
            return;
        }

        if (editorState.type === 'page') {
            form.innerHTML = `<div class="row g-3">
                ${inputField('pageCode', L.PageCode, record.pageCode, { required: true, readonly: editorState.mode === 'edit' })}
                ${inputField('pageName', L.PageName, record.pageName, { required: true })}
                ${selectField('pageType', L.PageType, [
                    { id: 'List', name: L.PageTypeList },
                    { id: 'Detail', name: L.PageTypeDetail },
                    { id: 'Create', name: L.PageTypeCreate },
                    { id: 'Edit', name: L.PageTypeEdit },
                    { id: 'Wizard', name: L.PageTypeWizard },
                    { id: 'Dashboard', name: L.PageTypeDashboard },
                    { id: 'Report', name: L.PageTypeReport },
                    { id: 'Admin', name: L.PageTypeAdmin },
                    { id: 'Other', name: L.PageTypeOther }
                ], record.pageType || 'Other', L.PageType, {})}
                ${inputField('routePath', L.RoutePath, record.routePath)}
                ${inputField('requiredPermissionKey', L.RequiredPermissionKey, record.requiredPermissionKey, { wrapper: 'col-12' })}
                ${textareaField('description', L.Description, record.description)}
                ${checkboxField('isNavigationCandidate', L.NavigationCandidate, record.isNavigationCandidate !== false)}
                ${checkboxField('isActive', L.IsActive, record.isActive !== false)}
            </div>`;
            return;
        }

        form.innerHTML = `<div class="row g-3">
            ${inputField('moduleId', L.ModuleId, record.moduleId, { required: true, readonly: editorState.mode === 'edit' })}
            ${inputField('moduleName', L.ModuleName, record.moduleName, { required: true })}
            ${selectField('domainLandscapeId', L.Domain, getDomainLandscapes(), record.domainLandscapeId, L.SelectDomain || L.Domain, { required: true })}
            ${selectField('suitePlatformId', L.Suite, getSuitesForDomain(record.domainLandscapeId), record.suitePlatformId, L.SelectSuite || L.Suite, { required: true })}
            ${selectField('capabilityGroupId', L.CapabilityGroup, getCapabilitiesForSuite(record.domainLandscapeId, record.suitePlatformId), record.capabilityGroupId, L.SelectCapabilityGroup || L.CapabilityGroup, { required: true })}
            ${inputField('dependencyGate', L.DependencyGate, record.dependencyGate)}
            ${inputField('deliveryOutcome', L.DeliveryOutcome, record.deliveryOutcome, { wrapper: 'col-12' })}
            ${inputField('placement', L.Placement, record.placement)}
            ${inputField('supportModel', L.SupportModel, record.supportModel)}
            ${selectField('status', L.Status, [
                { id: 'Draft', name: L.StatusDraft || L.Draft },
                { id: 'Active', name: L.StatusActive || L.ActiveStatus },
                { id: 'Deprecated', name: L.StatusDeprecated || L.Deprecated },
                { id: 'Retired', name: L.StatusRetired || L.Retired }
            ], record.status || 'Draft', L.Status, {})}
            ${checkboxField('isPlatformCore', L.IsPlatformCore, record.isPlatformCore === true, L.IsPlatformCoreHint)}
            ${checkboxField('isTenantAssignable', L.IsTenantAssignable, record.isTenantAssignable !== false)}
        </div>`;
    };

    const setEditorSaving = (saving) => {
        const button = document.getElementById('btnCatalogEditorSave');
        const spinner = document.getElementById('catalogEditorSaving');
        button?.toggleAttribute('disabled', saving);
        spinner?.classList.toggle('d-none', !saving);
    };

    const openEditor = async (type, mode, record) => {
        if (permissionDenied && mode !== 'view') {
            setStateMessage('warning', L.PermissionDenied);
            return;
        }

        await ensureHierarchyLoaded();
        editorState = { type, mode, record: record || {} };
        const titleMap = {
            domain: L.CreateDomain,
            suite: L.CreateSuite,
            capability: L.CreateCapabilityGroup,
            module: mode === 'edit' ? L.EditModule : L.CreateModule,
            page: mode === 'edit' ? L.EditPage : L.CreatePage
        };
        document.getElementById('catalogEditorTitle').innerText = titleMap[type] || L.EditMetadata;
        clearEditorErrors();
        renderEditorForm();
        refreshEditorDependentSelects();
        bootstrap.Modal.getOrCreateInstance(editorModalEl).show();
    };

    const openModuleEditor = async (moduleId) => {
        const detail = await requestJson(buildCatalogUrl(endpoints.moduleDetail(moduleId)));
        await openEditor('module', 'edit', detail);
    };

    const readEditorPayload = () => {
        const value = (name) => normalizeString(getEditorField(name)?.value);
        const checked = (name) => getEditorField(name)?.checked === true;

        if (editorState.type === 'domain') {
            return {
                id: editorState.record?.id,
                name: value('name'),
                code: value('code') || null,
                description: value('description') || null,
                isActive: checked('isActive')
            };
        }

        if (editorState.type === 'suite') {
            return {
                id: editorState.record?.id,
                name: value('name'),
                domainLandscapeId: value('domainLandscapeId'),
                code: value('code') || null,
                description: value('description') || null,
                isActive: checked('isActive')
            };
        }

        if (editorState.type === 'capability') {
            return {
                id: editorState.record?.id,
                name: value('name'),
                domainLandscapeId: value('domainLandscapeId'),
                suitePlatformId: value('suitePlatformId'),
                code: value('code') || null,
                description: value('description') || null,
                isActive: checked('isActive')
            };
        }

        if (editorState.type === 'page') {
            return {
                moduleId: currentDetailModuleId,
                pageCode: value('pageCode'),
                pageName: value('pageName'),
                description: value('description') || null,
                routePath: value('routePath') || null,
                pageType: value('pageType') || 'Other',
                requiredPermissionKey: value('requiredPermissionKey') || null,
                isNavigationCandidate: checked('isNavigationCandidate'),
                isActive: checked('isActive')
            };
        }

        return {
            moduleId: value('moduleId'),
            moduleName: value('moduleName'),
            domainLandscapeId: value('domainLandscapeId'),
            suitePlatformId: value('suitePlatformId'),
            capabilityGroupId: value('capabilityGroupId'),
            dependencyGate: value('dependencyGate') || null,
            deliveryOutcome: value('deliveryOutcome') || null,
            placement: value('placement') || null,
            supportModel: value('supportModel') || null,
            status: value('status') || null,
            isPlatformCore: checked('isPlatformCore'),
            isTenantAssignable: checked('isTenantAssignable')
        };
    };

    const validateEditorPayload = (payload) => {
        clearEditorErrors();
        const requiredByType = {
            domain: ['name'],
            suite: ['domainLandscapeId', 'name'],
            capability: ['domainLandscapeId', 'suitePlatformId', 'name'],
            module: ['moduleId', 'moduleName', 'domainLandscapeId', 'suitePlatformId', 'capabilityGroupId'],
            page: ['pageCode', 'pageName']
        };

        let valid = true;
        (requiredByType[editorState.type] || []).forEach((field) => {
            if (!normalizeString(payload[field])) {
                showFieldError(field, L.RequiredField);
                valid = false;
            }
        });

        if (editorState.type === 'capability' || editorState.type === 'module') {
            const suite = getSuitePlatforms().find((item) => item.id === payload.suitePlatformId);
            if (suite && suite.domainLandscapeId !== payload.domainLandscapeId) {
                showFieldError('suitePlatformId', L.InvalidHierarchy);
                valid = false;
            }
        }

        if (editorState.type === 'module') {
            const capability = getCapabilityGroups().find((item) => item.id === payload.capabilityGroupId);
            if (capability && (capability.domainLandscapeId !== payload.domainLandscapeId || capability.suitePlatformId !== payload.suitePlatformId)) {
                showFieldError('capabilityGroupId', L.InvalidHierarchy);
                valid = false;
            }
        }

        if (editorState.type === 'page' && !currentDetailModuleId) {
            showEditorAlert(L.InvalidModuleId || L.ParentNotFound, 'danger');
            valid = false;
        }

        return valid;
    };

    const mapBackendMessage = (message) => {
        const text = normalizeString(message);
        const lower = text.toLowerCase();
        if (lower.includes('moduleid') && (lower.includes('duplicate') || lower.includes('already') || lower.includes('exists'))) return L.DuplicateModuleId;
        if (lower.includes('pagecode') && (lower.includes('duplicate') || lower.includes('already') || lower.includes('exists'))) return L.DuplicatePageCode;
        if (lower.includes('moduleid') && lower.includes('immutable')) return L.ImmutableModuleId;
        if (lower.includes('moduleid') && lower.includes('could not be found')) return L.InvalidModuleId;
        if (lower.includes('pagecode') && lower.includes('immutable')) return L.DuplicatePageCode;
        if (lower.includes('code') && (lower.includes('duplicate') || lower.includes('already') || lower.includes('exists'))) return L.DuplicateCode;
        if (lower.includes('hierarchy') || lower.includes('under selected') || lower.includes('does not belong') || lower.includes('inconsistent')) return L.InvalidHierarchy;
        if (lower.includes('not found')) return L.ParentNotFound;
        if (lower.includes('required') || lower.includes('notempty') || lower.includes('not empty')) return L.RequiredField;
        return text || L.BackendError;
    };

    const normalizeBackendField = (field) => {
        const clean = normalizeString(field).split('.').pop();
        return clean ? clean.charAt(0).toLowerCase() + clean.slice(1) : '';
    };

    const applyBackendErrors = (error) => {
        const payload = error?.payload;
        const errors = payload?.errors || payload?.Errors;
        let handled = false;

        if (errors && typeof errors === 'object') {
            Object.keys(errors).forEach((key) => {
                const field = normalizeBackendField(key);
                const messages = Array.isArray(errors[key]) ? errors[key] : [errors[key]];
                showFieldError(field, mapBackendMessage(messages[0]));
                handled = true;
            });
        }

        if (!handled) {
            showEditorAlert(mapBackendMessage(error?.message || error?.rawText), 'danger');
        }
    };

    const getEditorEndpoint = (payload) => {
        const mode = editorState.mode;
        if (editorState.type === 'domain') {
            return {
                method: mode === 'edit' ? 'PUT' : 'POST',
                path: mode === 'edit' ? `${endpoints.domainLandscapes}/${encodeURIComponent(payload.id)}` : endpoints.domainLandscapes
            };
        }

        if (editorState.type === 'suite') {
            return {
                method: mode === 'edit' ? 'PUT' : 'POST',
                path: mode === 'edit' ? `${endpoints.suitePlatforms}/${encodeURIComponent(payload.id)}` : endpoints.suitePlatforms
            };
        }

        if (editorState.type === 'capability') {
            return {
                method: mode === 'edit' ? 'PUT' : 'POST',
                path: mode === 'edit' ? `${endpoints.capabilityGroups}/${encodeURIComponent(payload.id)}` : endpoints.capabilityGroups
            };
        }

        if (editorState.type === 'page') {
            return {
                method: mode === 'edit' ? 'PUT' : 'POST',
                path: mode === 'edit' ? endpoints.modulePage(payload.moduleId, payload.pageCode) : endpoints.modulePages(payload.moduleId)
            };
        }

        return {
            method: mode === 'edit' ? 'PUT' : 'POST',
            path: mode === 'edit' ? endpoints.moduleDetail(payload.moduleId) : endpoints.modules
        };
    };

    const saveEditor = async () => {
        const payload = readEditorPayload();
        if (!validateEditorPayload(payload)) return;

        const endpoint = getEditorEndpoint(payload);
        setEditorSaving(true);
        try {
            await requestJson(buildCatalogUrl(endpoint.path), {
                method: endpoint.method,
                body: JSON.stringify(payload)
            });
            bootstrap.Modal.getOrCreateInstance(editorModalEl).hide();
            window.showToast?.(L.SaveSuccess || L.RecordSaved, 'success');
            await bootstrapRefresh();

            const openDetailEl = document.getElementById('offcanvasDetailsPreview');
            if (currentDetailModuleId && openDetailEl?.classList.contains('show')) {
                await openDetail(currentDetailModuleId);
            }
        } catch (error) {
            if (isAuthHandledError(error)) return;
            if (error?.status === 0) {
                showEditorAlert(L.NetworkError, 'danger');
            } else {
                applyBackendErrors(error);
            }
        } finally {
            setEditorSaving(false);
        }
    };

    const parseImportPayload = () => {
        const raw = normalizeString(document.getElementById('importPayload').value);
        if (!raw) {
            throw new Error(L.InvalidJson);
        }

        let parsed;
        try {
            parsed = JSON.parse(raw);
        } catch (error) {
            throw new Error(L.InvalidJson);
        }

        if (Array.isArray(parsed)) {
            return { rows: parsed };
        }

        if (parsed && Array.isArray(parsed.rows)) {
            return parsed;
        }

        throw new Error(L.InvalidJson);
    };

    const renderImportResult = (result) => {
        const failedRows = Array.isArray(result?.failedRows) ? result.failedRows : [];
        document.getElementById('importResultHost').innerHTML = `
            <div class="row g-3 mb-3">
                <div class="col-md-3"><div class="alert alert-success mb-0">${escapeHtml(L.Created)}: ${result.createdCount || 0}</div></div>
                <div class="col-md-3"><div class="alert alert-info mb-0">${escapeHtml(L.Updated)}: ${result.updatedCount || 0}</div></div>
                <div class="col-md-3"><div class="alert alert-secondary mb-0">${escapeHtml(L.Skipped)}: ${result.skippedCount || 0}</div></div>
                <div class="col-md-3"><div class="alert alert-danger mb-0">${escapeHtml(L.Failed)}: ${result.failedCount || 0}</div></div>
            </div>
            ${failedRows.length ? `
            <div class="table-responsive">
                <table class="table table-sm">
                    <thead>
                        <tr>
                            <th>${escapeHtml(L.RowNumber)}</th>
                            <th>${escapeHtml(L.ModuleId || 'Module ID')}</th>
                            <th>${escapeHtml(L.ModuleName || 'Module Name')}</th>
                            <th>${escapeHtml(L.ErrorMessage)}</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${failedRows.map((row) => `
                            <tr>
                                <td>${row.rowNumber}</td>
                                <td>${escapeHtml(row.moduleId || '-')}</td>
                                <td>${escapeHtml(row.moduleName || '-')}</td>
                                <td>${escapeHtml(row.errorMessage || '-')}</td>
                            </tr>`).join('')}
                    </tbody>
                </table>
            </div>` : ''}`;
    };

    const reloadTableData = async () => {
        const rows = await loadModules();
        dt.clear();
        dt.rows.add(rows);
        dt.draw();

        if (!hierarchy?.summary?.totalModules) {
            setStateMessage('secondary', L.EmptyCatalog);
        } else if (!rows.length) {
            setStateMessage('secondary', L.NoResults);
        } else if (permissionDenied) {
            setStateMessage('warning', L.PermissionDenied);
        } else {
            setStateMessage('', '');
        }
    };

    const bootstrapRefresh = async () => {
        try {
            setStateMessage('info', L.LoadingCatalog);
            await loadHierarchy();
            await reloadTableData();
        } catch (error) {
            if (!isAuthHandledError(error)) {
                setStateMessage('danger', error.message || L.BackendError || 'Backend error');
            }
        }
    };

    const runCatalogAction = async (action) => {
        if (permissionDenied && action !== 'refresh') {
            setStateMessage('warning', L.PermissionDenied);
            return;
        }

        if (action === 'create-module') await openEditor('module', 'create', {});
        if (action === 'create-domain') await openEditor('domain', 'create', {});
        if (action === 'create-suite') await openEditor('suite', 'create', {});
        if (action === 'create-capability') await openEditor('capability', 'create', {});
        if (action === 'import') bootstrap.Modal.getOrCreateInstance(importModalEl).show();
        if (action === 'refresh') await bootstrapRefresh();
    };

    const getStagedFilters = () => ({
        domainLandscapeId: normalizeString(document.getElementById('filterDomain').value),
        suitePlatformId: normalizeString(document.getElementById('filterSuite').value),
        capabilityGroupId: normalizeString(document.getElementById('filterCapabilityGroup').value),
        status: normalizeString(document.getElementById('filterStatus').value),
        isTenantAssignable: normalizeString(document.getElementById('filterIsTenantAssignable').value),
        isPlatformCore: normalizeString(document.getElementById('filterIsPlatformCore').value)
    });

    const syncFilterControls = (filters) => {
        document.getElementById('filterDomain').value = filters.domainLandscapeId || '';
        syncDependentFilters();
        document.getElementById('filterSuite').value = filters.suitePlatformId || '';
        syncDependentFilters();
        document.getElementById('filterCapabilityGroup').value = filters.capabilityGroupId || '';
        document.getElementById('filterStatus').value = filters.status || '';
        document.getElementById('filterIsTenantAssignable').value = filters.isTenantAssignable || '';
        document.getElementById('filterIsPlatformCore').value = filters.isPlatformCore || '';
    };

    const getAppliedFilterCount = () =>
        Object.keys(appliedFilters).filter((key) => normalizeString(appliedFilters[key])).length;

    const applySavedTableState = async (api, state) => {
        appliedFilters = {
            domainLandscapeId: normalizeString(state.domainLandscapeId),
            suitePlatformId: normalizeString(state.suitePlatformId),
            capabilityGroupId: normalizeString(state.capabilityGroupId),
            status: normalizeString(state.status),
            isTenantAssignable: normalizeString(state.isTenantAssignable),
            isPlatformCore: normalizeString(state.isPlatformCore)
        };

        syncFilterControls(appliedFilters);
        applyColumnOrder(api, state.columnOrder);
        applyColumnVisibility(api, state.colVis);
        api.order(Array.isArray(state.order) ? state.order : baseOrder);
        api.search(state.search || '');
        syncSearchInput(api, state.search || '');
        await reloadTableData();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        if (!host) return;
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
        }
    };

    const bindInlineFilterToggle = () => {
        const button = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!button || !collapseEl || button.dataset.inlineFilterBound) return;
        button.dataset.inlineFilterBound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => button.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => button.setAttribute('aria-expanded', 'false'));
        button.addEventListener('click', (event) => {
            event.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false });
            if (collapseEl.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            importBtn: {
                title: L.ImportCatalog,
                action: function () {
                    if (permissionDenied) {
                        setStateMessage('warning', L.PermissionDenied);
                        return;
                    }

                    bootstrap.Modal.getOrCreateInstance(importModalEl).show();
                }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-label': L.Filter,
                    'aria-controls': filterCollapseId,
                    'aria-expanded': 'false',
                    'data-bs-toggle': 'tooltip'
                }
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView || '')}</span>`,
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'aria-label': L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (event, api) {
                    try {
                        await saveDefaultView(getCurrentView(api || dt));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || 'RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) return;
                        window.showToast?.(L.ErrorOccurred || 'ErrorOccurred', 'error');
                    }
                }
            }
        };

        const tableButtons = window.DtDefaults.exportButtons(
            L.CreateModule,
            { 'data-catalog-action': 'create-module' },
            extraButtons,
            { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }
        );

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            data: [],
            stateSave: false,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            order: baseOrder,
            columns: [
                { data: 'id', name: 'control' },
                { data: 'moduleId', name: 'checkbox' },
                { data: 'moduleName', name: 'moduleName' },
                { data: 'domainLandscapeName', name: 'domainLandscapeName' },
                { data: 'suitePlatformName', name: 'suitePlatformName' },
                { data: 'capabilityGroupName', name: 'capabilityGroupName' },
                { data: 'status', name: 'status' },
                { data: 'isPlatformCore', name: 'isPlatformCore' },
                { data: 'isTenantAssignable', name: 'isTenantAssignable' },
                { data: 'supportModel', name: 'supportModel' },
                { data: 'pagesCount', name: 'pagesCount' },
                { data: 'moduleId', name: 'action', responsivePriority: 1 }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                {
                    targets: 1,
                    searchable: false,
                    orderable: false,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${escapeHtml(data)}">`
                },
                {
                    targets: 2,
                    render: (data, type, full) => renderModuleIdentity(full)
                },
                {
                    targets: [3, 4, 5],
                    responsivePriority: 8,
                    className: 'min-desktop',
                    render: (data) => renderTextChip(data)
                },
                {
                    targets: 6,
                    responsivePriority: 2,
                    render: (data) => moduleStatusBadge(data)
                },
                {
                    targets: [7, 8],
                    responsivePriority: 5,
                    className: 'min-tablet-l',
                    render: (data) => renderBooleanBadge(data)
                },
                {
                    targets: 9,
                    responsivePriority: 10,
                    className: 'min-desktop',
                    render: (data) => `<span class="text-muted">${escapeHtml(data || '-')}</span>`
                },
                {
                    targets: 10,
                    responsivePriority: 7,
                    className: 'min-tablet-l text-center',
                    render: (data, type, full) => renderPagesCount(full)
                },
                {
                    targets: 11,
                    searchable: false,
                    orderable: false,
                    responsivePriority: 1,
                    className: 'cell-fit text-end all',
                    render: (data, type, full) => `<div class="d-flex align-items-center justify-content-end">
                        <button type="button" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${escapeHtml(L.Actions || '')}">
                            <i class="icon-base bx bx-dots-vertical-rounded icon-md"></i>
                        </button>
                        <div class="dropdown-menu dropdown-menu-end m-0">
                            <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-module-id="${escapeHtml(full.moduleId)}">
                                <i class="icon-base bx bx-show icon-sm me-2"></i>${escapeHtml(L.ViewDetails || '')}
                            </a>
                            <a href="javascript:void(0);" class="dropdown-item js-edit-module" data-module-id="${escapeHtml(full.moduleId)}">
                                <i class="icon-base bx bx-edit icon-sm me-2"></i>${escapeHtml(L.EditMetadata || '')}
                            </a>
                        </div>
                    </div>`
                }
            ],
            buttons: tableButtons,
            initComplete: async function () {
                const api = this.api();
                mountInlineFilter();
                bindInlineFilterToggle();
                if (defaultViewState) {
                    await loadHierarchy();
                    await applySavedTableState(api, defaultViewState);
                } else {
                    await bootstrapRefresh();
                }
                setTimeout(() => { saveFilterArmed = true; }, 0);
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            },
            drawCallback: function () {
                updateBulkBar();
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        }));

        dt.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        document.addEventListener('click', async (event) => {
            const quickView = event.target.closest('.js-quick-view');
            if (quickView) {
                try {
                    await openDetail(quickView.getAttribute('data-module-id'));
                } catch (error) {
                    if (!isAuthHandledError(error)) {
                        window.showToast?.(error.message || L.BackendError, 'error');
                    }
                }
            }

            const editModule = event.target.closest('.js-edit-module');
            if (editModule) {
                try {
                    await openModuleEditor(editModule.getAttribute('data-module-id'));
                } catch (error) {
                    if (!isAuthHandledError(error)) {
                        window.showToast?.(mapBackendMessage(error.message || L.BackendError), 'error');
                    }
                }
            }

            const editPage = event.target.closest('.js-edit-page');
            if (editPage) {
                const pageCode = editPage.getAttribute('data-page-code');
                const page = currentDetailPages.find((item) => item.pageCode === pageCode);
                if (page) {
                    await openEditor('page', 'edit', page);
                }
            }

            const headerAction = event.target.closest('[data-catalog-action]');
            if (headerAction) {
                if (headerAction.hasAttribute('disabled') || headerAction.classList.contains('disabled')) {
                    event.preventDefault();
                    return;
                }

                const action = headerAction.getAttribute('data-catalog-action');
                try {
                    await runCatalogAction(action);
                } catch (error) {
                    if (!isAuthHandledError(error)) {
                        window.showToast?.(mapBackendMessage(error.message || L.BackendError), 'error');
                    }
                }
            }
        });

        document.getElementById('oc-btn-edit')?.addEventListener('click', async () => {
            const moduleId = document.getElementById('oc-btn-edit')?.dataset.moduleId;
            if (!moduleId) return;
            try {
                await openModuleEditor(moduleId);
            } catch (error) {
                if (!isAuthHandledError(error)) {
                    window.showToast?.(mapBackendMessage(error.message || L.BackendError), 'error');
                }
            }
        });

        document.getElementById('btnCreateModulePage')?.addEventListener('click', async () => {
            if (!currentDetailModuleId) return;
            await openEditor('page', 'create', {
                pageType: 'List',
                isNavigationCandidate: true,
                isActive: true
            });
        });

        document.getElementById('catalogEditorForm')?.addEventListener('change', (event) => {
            if (event.target.name === 'domainLandscapeId' || event.target.name === 'suitePlatformId') {
                refreshEditorDependentSelects();
            }
        });

        document.getElementById('btnCatalogEditorSave')?.addEventListener('click', saveEditor);

        document.getElementById('filterDomain')?.addEventListener('change', syncDependentFilters);
        document.getElementById('filterSuite')?.addEventListener('change', syncDependentFilters);

        document.getElementById('btnFilterApply')?.addEventListener('click', async () => {
            appliedFilters = getStagedFilters();
            await reloadTableData();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
            bootstrap.Collapse.getInstance(document.getElementById(filterCollapseId))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', async (event) => {
            event.preventDefault();
            appliedFilters = defaultViewState
                ? {
                    domainLandscapeId: normalizeString(defaultViewState.domainLandscapeId),
                    suitePlatformId: normalizeString(defaultViewState.suitePlatformId),
                    capabilityGroupId: normalizeString(defaultViewState.capabilityGroupId),
                    status: normalizeString(defaultViewState.status),
                    isTenantAssignable: normalizeString(defaultViewState.isTenantAssignable),
                    isPlatformCore: normalizeString(defaultViewState.isPlatformCore)
                }
                : {
                    domainLandscapeId: '',
                    suitePlatformId: '',
                    capabilityGroupId: '',
                    status: '',
                    isTenantAssignable: '',
                    isPlatformCore: ''
                };
            syncFilterControls(appliedFilters);
            await reloadTableData();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', clearSelection);

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            this.closest('tr')?.classList.toggle('selected', this.checked);
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

        document.getElementById('btnImportSubmit')?.addEventListener('click', async () => {
            const submitButton = document.getElementById('btnImportSubmit');
            const resultHost = document.getElementById('importResultHost');
            submitButton.setAttribute('disabled', 'disabled');
            try {
                const payload = parseImportPayload();
                const result = await requestJson(buildCatalogUrl(endpoints.import), {
                    method: 'POST',
                    body: JSON.stringify(payload)
                });
                renderImportResult(result);
                await bootstrapRefresh();
                window.showToast?.((result.failedCount || 0) > 0 ? L.PartialImport : L.ImportSuccess, (result.failedCount || 0) > 0 ? 'warning' : 'success');
            } catch (error) {
                if (!isAuthHandledError(error)) {
                    resultHost.innerHTML = `<div class="alert alert-danger mb-0">${escapeHtml(error.message || L.BackendError)}</div>`;
                }
            } finally {
                if (!permissionDenied) {
                    submitButton.removeAttribute('disabled');
                }
            }
        });

        importModalEl?.addEventListener('hidden.bs.modal', () => {
            document.getElementById('importResultHost').innerHTML = '';
        });
    };

    return {
        init: async () => {
            syncL10n();
            await initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => {
    ModuleCatalogList.init();
});
