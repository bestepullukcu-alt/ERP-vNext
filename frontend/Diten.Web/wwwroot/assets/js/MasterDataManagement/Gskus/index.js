/**
 * MOD-0290 GSKU Register — tenant-shell Golden Slim lifecycle surface.
 * Browser traffic is restricted to the same-origin MVC proxy.
 */
'use strict';

const GskusList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let appliedFilters = { lifecycleStatus: '' };
    let editorId = '';
    let editorGskuVersion = null;
    let editorRevisionVersion = null;
    let editorMode = 'create';
    const lifecycleRequests = new Set();
    const endpoint = '/MasterDataManagement/Gskus/api';
    const tableEl = document.querySelector('.datatables-gskus');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'Gskus' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const totalColumnCount = 7;
    const baseOrder = [[1, 'asc']];
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT'];
    const supportedActions = new Set(['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT']);
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const emptyFilters = () => ({ lifecycleStatus: '' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeSearch = (value) => typeof value === 'string' ? value.trim().slice(0, 200) : '';
    const normalizeFilters = (filters) => ({
        lifecycleStatus: ['Draft', 'PendingIdentityApproval', 'IdentityApproved', 'Retired']
            .includes(filters?.lifecycleStatus) ? filters.lifecycleStatus : ''
    });
    const unwrapData = (payload) => payload?.data || payload?.Data || {};
    const valueOf = (source, camel, pascal) => source?.[camel] ?? source?.[pascal];

    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const normalized = order.map(Number).filter((index) =>
            Number.isInteger(index) && index >= 0 && index < totalColumnCount);
        return normalized.length === totalColumnCount && new Set(normalized).size === totalColumnCount
            ? normalized
            : null;
    };
    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const normalized = {};
        saveViewColumnIndexes.forEach((index, position) => {
            const value = Array.isArray(colVis) ? (colVis[index] ?? colVis[position]) : colVis[index];
            if (typeof value === 'boolean') normalized[index] = value;
        });
        return Object.keys(normalized).length ? normalized : null;
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((state, index) => {
        state[index] = true;
        return state;
    }, {});
    const normalizeView = (view) => ({
        filters: normalizeFilters(view?.filters),
        search: normalizeSearch(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder)
            || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const captureColVis = (api) => saveViewColumnIndexes.reduce((state, index) => {
        state[index] = !!api.column(index).visible();
        return state;
    }, {});
    const captureColOrder = (api) => {
        try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (error) { return null; }
    };
    const getSearchValue = (api) => {
        try { return normalizeSearch(api.search()); } catch (error) { return ''; }
    };
    const getCurrentView = (api) => {
        const search = getSearchValue(api);
        return normalizeView({
            filters: normalizeFilters(appliedFilters),
            search,
            colVis: captureColVis(api),
            columnOrder: captureColOrder(api),
            order: api.order()
        });
    };
    const serializeView = (view) => JSON.stringify(normalizeView(view));
    const getSavedViewDefinition = (view) => {
        const definition = view?.viewDefinition ?? view?.ViewDefinition ?? {};
        if (typeof definition !== 'string') return definition;
        try { return JSON.parse(definition); } catch (error) { return {}; }
    };
    const getSavedViewId = (view) => view?.id || view?.Id || view?._id || null;
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(
                personalizationContext.moduleKey,
                personalizationContext.pageKey);
            const items = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            defaultViewRecord = items.find((item) => item?.isDefault === true || item?.IsDefault === true)
                || items[0]
                || null;
            defaultViewState = defaultViewRecord ? normalizeView(getSavedViewDefinition(defaultViewRecord)) : null;
            return defaultViewState;
        } catch (error) {
            if (!error?.authHandled) console.error('[Gskus SaveView] Load failed.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView).trim(),
            viewDefinition: normalized,
            isDefault: true,
            visibility: 'private'
        };
        const id = getSavedViewId(defaultViewRecord);
        const response = id
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = response?.data || response?.Data || response || payload;
        defaultViewState = normalized;
        return normalized;
    };
    const setSaveFilterVisible = (visible) => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
        order: baseOrder
    });
    const isDirtyComparedToDefault = (api) =>
        serializeView(getCurrentView(api)) !== serializeView(defaultViewState || getResetBaselineState());
    const applySavedTableState = (api, view) => {
        const normalized = normalizeView(view || {});
        if (typeof api?.colReorder?.order === 'function') api.colReorder.order(normalized.columnOrder, true);
        saveViewColumnIndexes.forEach((index) => api.column(index).visible(normalized.colVis[index], false));
        api.search(normalized.search);
        api.order(normalized.order);
        appliedFilters = normalizeFilters(normalized.filters);
        const lifecycle = document.getElementById('filterLifecycleStatus');
        if (lifecycle) $(lifecycle).val(appliedFilters.lifecycleStatus || null).trigger('change');
        syncSingleFilterState(lifecycle);
    };

    const lifecycleMap = () => ({
        Draft: { title: L.LifecycleDraft, class: 'bg-label-secondary' },
        1: { title: L.LifecycleDraft, class: 'bg-label-secondary' },
        PendingIdentityApproval: { title: L.LifecyclePendingIdentityApproval, class: 'bg-label-warning' },
        2: { title: L.LifecyclePendingIdentityApproval, class: 'bg-label-warning' },
        IdentityApproved: { title: L.LifecycleIdentityApproved, class: 'bg-label-success' },
        3: { title: L.LifecycleIdentityApproved, class: 'bg-label-success' },
        Retired: { title: L.LifecycleRetired, class: 'bg-label-danger' },
        4: { title: L.LifecycleRetired, class: 'bg-label-danger' }
    });
    const renderLifecycle = (value) => {
        const item = lifecycleMap()[value] || { title: value || L.Unknown, class: 'bg-label-secondary' };
        return `<span class="badge ${item.class}">${escapeHtml(item.title)}</span>`;
    };
    const lifecycleCode = (value) => ({
        Draft: 1, 1: 1,
        PendingIdentityApproval: 2, 2: 2,
        IdentityApproved: 3, 3: 3,
        Retired: 4, 4: 4
    })[value] || 0;
    const handleUnauthorized = () => {
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error('auth-refresh-in-progress');
        error.authHandled = true;
        throw error;
    };
    const getErrorMessage = async (response) => {
        let payload = {};
        try { payload = await response.json(); } catch (error) { }
        const errors = payload?.errors || payload?.Errors || [];
        if (Array.isArray(errors) && errors.find(Boolean)) return errors.find(Boolean);
        return ({
            400: L.ErrorValidation,
            401: L.ErrorUnauthorized,
            403: L.ErrorForbidden,
            404: L.ErrorNotFound,
            409: L.ErrorConflict,
            503: L.ErrorServiceUnavailable,
            504: L.ErrorTimeout
        })[response.status] || L.ErrorGateway;
    };
    const buildQuery = (data) => {
        const pageSize = Math.max(1, Math.min(Number(data.length) || 20, 100));
        const pageNumber = Math.max(1, Math.min(
            Math.floor((Number(data.start) || 0) / pageSize) + 1,
            1000000));
        const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
        const search = normalizeSearch(data.search?.value);
        if (search) query.set('search', search);
        if (appliedFilters.lifecycleStatus) {
            query.set('lifecycleStatus', appliedFilters.lifecycleStatus);
        }
        return query.toString();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const button = document.querySelector('.dt-filter-btn');
        const toolbar = button?.closest('.dt-layout-row') || button?.closest('.row');
        if (host && toolbar) {
            toolbar.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
    };
    const toggleFilter = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        if (collapse) bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).toggle();
    };
    const syncSingleFilterState = (select) => {
        if (!select) return;
        const selected = !!select.value;
        const container = $(select).next('.select2-container');
        container.toggleClass('filter-selected', selected);
        container.find('.select2-selection').toggleClass('border-primary text-primary', selected);
    };
    const getAppliedFilterCount = () => appliedFilters.lifecycleStatus ? 1 : 0;
    const bindFilter = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        const lifecycle = document.getElementById('filterLifecycleStatus');
        const applyButton = document.getElementById('btnFilterApply');
        if (lifecycle && !$(lifecycle).hasClass('select2-hidden-accessible')) {
            $(lifecycle).select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                width: 'element',
                allowClear: true,
                minimumResultsForSearch: Infinity
            });
        }
        $(lifecycle).on('change', () => syncSingleFilterState(lifecycle));
        applyButton?.addEventListener('click', () => {
            appliedFilters = normalizeFilters({ lifecycleStatus: lifecycle?.value || '' });
            syncSingleFilterState(lifecycle);
            dt?.draw();
            if (saveFilterArmed && dt) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            if (collapse) bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            if (!dt) return;
            applySavedTableState(dt, getResetBaselineState());
            dt.draw();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        syncSingleFilterState(lifecycle);
    };

    const setText = (elementId, fieldValue) => {
        const element = document.getElementById(elementId);
        if (!element) throw new Error(L.ErrorGateway);
        element.textContent = fieldValue === null || fieldValue === undefined || fieldValue === ''
            ? L.NotAvailable
            : String(fieldValue);
    };
    const formatDate = (dateValue) => dateValue
        ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, {
            dateStyle: 'medium', timeStyle: 'short'
        }).format(new Date(dateValue))
        : L.NotAvailable;
    const fetchDetail = async (id) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (response.status === 401) handleUnauthorized();
        if (!response.ok) throw new Error(await getErrorMessage(response));
        return unwrapData(await response.json());
    };
    const renderDetail = (detail, expectedId) => {
        const detailId = valueOf(detail, 'id', 'Id');
        const detailVersion = Number(valueOf(detail, 'gskuVersion', 'GskuVersion'));
        const revisionVersion = Number(valueOf(detail, 'revisionVersion', 'RevisionVersion'));
        const detailState = lifecycleCode(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'));
        const offcanvas = document.getElementById('offcanvasDetailsPreview');
        if (!detailId || String(detailId).toLowerCase() !== String(expectedId).toLowerCase()
            || !Number.isInteger(detailVersion) || detailVersion < 0
            || !Number.isInteger(revisionVersion) || revisionVersion < 0
            || detailState === 0 || !offcanvas) {
            throw new Error(L.ErrorGateway);
        }
        const code = valueOf(detail, 'canonicalCode', 'CanonicalCode');
        const productCode = valueOf(detail, 'globalProductCanonicalCode', 'GlobalProductCanonicalCode');
        const productName = valueOf(detail, 'globalProductName', 'GlobalProductName');
        const quantity = valueOf(detail, 'packQuantity', 'PackQuantity');
        const uom = valueOf(detail, 'packUomCode', 'PackUomCode');
        setText('oc-title', code);
        setText('oc-subtitle', valueOf(detail, 'revisionIdentifier', 'RevisionIdentifier'));
        setText('oc-id', detailId);
        setText('oc-code', code);
        setText('oc-global-product', `${productCode || ''}${productCode && productName ? ' — ' : ''}${productName || ''}`);
        setText('oc-revision', valueOf(detail, 'revisionIdentifier', 'RevisionIdentifier'));
        setText('oc-pack', `${quantity ?? ''}${quantity !== null && quantity !== undefined && uom ? ' ' : ''}${uom || ''}`);
        setText('oc-version', detailVersion);
        setText('oc-created-at', formatDate(valueOf(detail, 'createdAt', 'CreatedAt')));
        setText('oc-updated-at', formatDate(valueOf(detail, 'updatedAt', 'UpdatedAt')));
        const statusElement = document.getElementById('oc-status');
        if (!statusElement) throw new Error(L.ErrorGateway);
        statusElement.outerHTML = renderLifecycle(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'))
            .replace('<span ', '<span id="oc-status" ');
        bootstrap.Offcanvas.getOrCreateInstance(offcanvas).show();
        return detailState;
    };
    const populateDetails = async (id) => {
        try {
            const detail = await fetchDetail(id);
            renderDetail(detail, id);
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        }
    };

    const readAvailableActions = (detail) => {
        const raw = detail?.availableActions ?? detail?.AvailableActions;
        if (!Array.isArray(raw) || !raw.length) throw new Error(L.ErrorGateway);
        const actions = raw.map((value) => typeof value === 'string' ? value.trim().toUpperCase() : '');
        if (actions.some((value) => !actionOrder.includes(value))
            || new Set(actions).size !== actions.length
            || actions[0] !== 'DETAILS'
            || actions.some((value, index) => index > 0
                && actionOrder.indexOf(value) <= actionOrder.indexOf(actions[index - 1]))) {
            throw new Error(L.ErrorGateway);
        }
        return actions;
    };
    const renderActions = (row) => {
        const id = valueOf(row, 'id', 'Id');
        return `<div class="dropdown"><button type="button" class="btn btn-icon dropdown-toggle hide-arrow js-gsku-actions-toggle" data-id="${escapeHtml(id)}" aria-expanded="false" title="${escapeHtml(L.Actions)}" aria-label="${escapeHtml(L.Actions)}"><i class="bx bx-dots-vertical-rounded icon-md"></i></button><div class="dropdown-menu dropdown-menu-end m-0 js-gsku-actions-menu"></div></div>`;
    };
    const actionPresentation = {
        DETAILS: ['js-quick-view', 'bx bx-show', () => L.ViewDetails],
        EDIT: ['js-edit-draft', 'bx bx-edit', () => L.EditDraft],
        SUBMIT: ['js-lifecycle-action', 'bx bx-send', () => L.SubmitIdentity],
        WITHDRAW_APPROVAL: ['js-lifecycle-action', 'bx bx-undo', () => L.WithdrawApproval],
        REQUEST_CORRECTION: ['js-request-correction', 'bx bx-revision', () => L.RequestCorrection],
        REQUEST_RETIREMENT: ['js-request-retirement', 'bx bx-archive', () => L.RequestRetirement]
    };
    const renderFreshActionMenu = (menu, id, actions) => {
        menu.innerHTML = actions.filter((action) => supportedActions.has(action)).map((action) => {
            const [className, icon, label] = actionPresentation[action];
            return `<a href="javascript:void(0);" class="dropdown-item dt-action-item ${className}" data-id="${escapeHtml(id)}" data-action="${action}"><i class="${icon} dt-action-icon"></i>${escapeHtml(label())}</a>`;
        }).join('');
    };
    const loadActionMenu = async (toggle) => {
        const id = toggle?.dataset.id;
        const menu = toggle?.parentElement?.querySelector('.js-gsku-actions-menu');
        if (!id || !menu || toggle.classList.contains('disabled')) return;
        toggle.classList.add('disabled');
        try {
            const actions = readAvailableActions(await fetchDetail(id));
            renderFreshActionMenu(menu, id, actions);
            if (!menu.children.length) throw new Error(L.ErrorGateway);
            bootstrap.Dropdown.getOrCreateInstance(toggle).show();
        } catch (error) {
            menu.replaceChildren();
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        } finally {
            toggle.classList.remove('disabled');
        }
    };

    const initializeCreateSelects = () => {
        ['#globalProductId', '#packUomCode'].forEach((selector) => {
            const $select = $(selector);
            if ($select.length && !$select.hasClass('select2-hidden-accessible')) {
                $select.select2({
                    dropdownParent: $('#offcanvasCreateEdit'),
                    width: '100%',
                    allowClear: true
                });
            }
        });
    };
    const replaceOptions = (select, options) => {
        select.replaceChildren(new Option('', ''));
        options.forEach((option) => select.add(option));
        $(select).val(null).trigger('change');
    };
    const loadCreateOptions = async () => {
        const button = document.getElementById('btnSaveGsku');
        const globalProduct = document.getElementById('globalProductId');
        const uom = document.getElementById('packUomCode');
        if (!globalProduct || !uom) return false;
        if (button) button.disabled = true;
        globalProduct.disabled = true;
        uom.disabled = true;
        try {
            const response = await fetch(`${endpoint}/create-options`, {
                credentials: 'same-origin', headers: getAuthHeaders()
            });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const data = unwrapData(await response.json());
            const products = data.globalProducts || data.GlobalProducts || [];
            const uoms = data.uoms || data.Uoms || [];
            if (!products.length || !uoms.length) throw new Error(L.OptionsEmpty);
            replaceOptions(globalProduct, products.map((item) => new Option(
                `${valueOf(item, 'canonicalCode', 'CanonicalCode')} — ${valueOf(item, 'globalProductName', 'GlobalProductName')}`,
                valueOf(item, 'id', 'Id'))));
            replaceOptions(uom, [...uoms]
                .sort((left, right) => Number(valueOf(left, 'sortOrder', 'SortOrder')) - Number(valueOf(right, 'sortOrder', 'SortOrder')))
                .map((item) => {
                    const option = new Option(
                        valueOf(item, 'displayText', 'DisplayText'),
                        valueOf(item, 'code', 'Code'));
                    option.dataset.maximumDecimalPrecision = String(
                        valueOf(item, 'maximumDecimalPrecision', 'MaximumDecimalPrecision'));
                    return option;
                }));
            globalProduct.disabled = false;
            uom.disabled = false;
            if (button) button.disabled = false;
            return true;
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            return false;
        }
    };
    const openCreate = async () => {
        const form = document.getElementById('formGsku');
        if (!form) return;
        editorId = '';
        editorGskuVersion = null;
        editorRevisionVersion = null;
        editorMode = 'create';
        form.reset();
        form.classList.remove('was-validated');
        document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleCreate;
        document.getElementById('btnSaveGskuText').textContent = L.Save;
        initializeCreateSelects();
        document.getElementById('globalProductId').disabled = false;
        $('#globalProductId, #packUomCode').val(null).trigger('change');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        await loadCreateOptions();
    };
    const setBusy = (button, busy) => {
        if (button) button.disabled = busy;
        button?.classList.toggle('disabled', busy);
        button?.setAttribute('aria-disabled', busy ? 'true' : 'false');
    };
    const openExistingEditor = async (id, actionButton, mode) => {
        setBusy(actionButton, true);
        try {
            const detail = await fetchDetail(id);
            const requiredAction = mode === 'correction' ? 'REQUEST_CORRECTION' : 'EDIT';
            if (!readAvailableActions(detail).includes(requiredAction)) throw new Error(L.LifecycleStateChanged);
            const detailId = valueOf(detail, 'id', 'Id');
            const gskuVersion = Number(valueOf(detail, 'gskuVersion', 'GskuVersion'));
            const revisionVersion = Number(valueOf(detail, 'revisionVersion', 'RevisionVersion'));
            if (!detailId || String(detailId).toLowerCase() !== String(id).toLowerCase()
                || !Number.isInteger(gskuVersion) || gskuVersion < 0
                || !Number.isInteger(revisionVersion) || revisionVersion < 0) {
                throw new Error(L.ErrorConflict);
            }

            editorId = String(id);
            editorGskuVersion = gskuVersion;
            editorRevisionVersion = revisionVersion;
            editorMode = mode;
            const form = document.getElementById('formGsku');
            form?.classList.remove('was-validated');
            initializeCreateSelects();
            const globalProduct = document.getElementById('globalProductId');
            const uom = document.getElementById('packUomCode');
            if (!form || !globalProduct || !uom) throw new Error(L.ErrorGateway);
            const productId = valueOf(detail, 'globalProductId', 'GlobalProductId');
            const productCode = valueOf(detail, 'globalProductCanonicalCode', 'GlobalProductCanonicalCode');
            const productName = valueOf(detail, 'globalProductName', 'GlobalProductName');
            const uomCode = valueOf(detail, 'packUomCode', 'PackUomCode');
            document.getElementById('packQuantity').value = String(valueOf(detail, 'packQuantity', 'PackQuantity'));
            document.getElementById('offcanvasCreateEditLabel').textContent = mode === 'correction'
                ? L.FormTitleCorrection : L.FormTitleEdit;
            document.getElementById('btnSaveGskuText').textContent = mode === 'correction'
                ? L.RequestCorrection : L.UpdateDraft;
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
            if (!await loadCreateOptions()) {
                editorId = '';
                editorGskuVersion = null;
                editorRevisionVersion = null;
                editorMode = 'create';
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                return;
            }
            if (![...globalProduct.options].some((option) => option.value === String(productId))) {
                globalProduct.add(new Option(
                    `${productCode || ''}${productCode && productName ? ' — ' : ''}${productName || ''}`,
                    productId));
            }
            if (![...uom.options].some((option) => option.value === String(uomCode))) {
                throw new Error(L.ErrorConflict);
            }
            $(globalProduct).val(String(productId)).trigger('change');
            globalProduct.disabled = true;
            $(uom).val(String(uomCode)).trigger('change');
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
        } finally {
            setBusy(actionButton, false);
        }
    };
    const openEdit = (id, actionButton) => openExistingEditor(id, actionButton, 'edit');
    const openCorrection = (id, actionButton) => openExistingEditor(id, actionButton, 'correction');
    const validateQuantity = (quantity, selectedUom) => {
        const match = String(quantity).match(/^\d+(?:\.(\d+))?$/);
        if (!match || Number(quantity) <= 0) return L.PackQuantityRequired;
        const precision = Number(selectedUom?.dataset.maximumDecimalPrecision);
        if (!Number.isInteger(precision) || (match[1]?.length || 0) > precision) {
            return L.PackQuantityPrecision.replace('{0}', String(Number.isInteger(precision) ? precision : 0));
        }
        return '';
    };
    const submitEditor = () => {
        const form = document.getElementById('formGsku');
        const globalProductId = $('#globalProductId').val();
        const packUomCode = $('#packUomCode').val();
        const quantity = document.getElementById('packQuantity')?.value || '';
        if (!form || !globalProductId || !packUomCode || !form.checkValidity()) {
            form?.classList.add('was-validated');
            window.showToast?.(!globalProductId ? L.GlobalProductRequired : (!packUomCode ? L.PackUomRequired : L.PackQuantityRequired), 'error');
            return;
        }
        const quantityError = editorId
            ? (!/^\d+(?:\.\d+)?$/.test(quantity) || Number(quantity) <= 0 ? L.PackQuantityRequired : '')
            : validateQuantity(quantity, document.querySelector('#packUomCode option:checked'));
        if (quantityError) {
            window.showToast?.(quantityError, 'error');
            return;
        }

        const entityName = $('#globalProductId option:selected').text() || String(globalProductId);
        const confirmation = editorMode === 'correction' ? L.CorrectionConfirmation
            : editorId ? L.UpdateConfirmation : L.CreateConfirmation;
        const confirmButtonText = editorMode === 'correction' ? L.RequestCorrection
            : editorId ? L.UpdateDraft : L.Save;
        window.showConfirm?.(confirmation, async () => {
            const button = document.getElementById('btnSaveGsku');
            if (button) button.disabled = true;
            try {
                if (editorMode === 'correction') {
                    const fresh = await fetchDetail(editorId);
                    const freshGskuVersion = Number(valueOf(fresh, 'gskuVersion', 'GskuVersion'));
                    const freshRevisionVersion = Number(valueOf(fresh, 'revisionVersion', 'RevisionVersion'));
                    if (!readAvailableActions(fresh).includes('REQUEST_CORRECTION')
                        || freshGskuVersion !== editorGskuVersion
                        || freshRevisionVersion !== editorRevisionVersion) {
                        throw new Error(L.LifecycleStateChanged);
                    }
                }
                const body = new FormData();
                body.set('PackQuantity', quantity);
                body.set('PackUomCode', String(packUomCode));
                if (editorMode === 'correction') body.set('ExpectedGskuVersion', String(editorGskuVersion));
                else if (editorId) body.set('ExpectedVersion', String(editorGskuVersion));
                else {
                    body.set('GlobalProductId', String(globalProductId));
                    body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
                }
                const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                body.set('__RequestVerificationToken', antiForgeryToken);
                const target = editorMode === 'correction'
                    ? `${endpoint}/${encodeURIComponent(editorId)}/correction-requests`
                    : editorId ? `${endpoint}/${encodeURIComponent(editorId)}` : endpoint;
                const response = await fetch(target, {
                    method: editorMode === 'edit' ? 'PUT' : 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': antiForgeryToken, 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });
                if (response.status === 401) handleUnauthorized();
                if (editorMode === 'correction' && response.status !== 200 && response.status !== 202
                    || editorMode === 'edit' && response.status !== 200
                    || !editorId && response.status !== 201 && response.status !== 202) {
                    throw new Error(await getErrorMessage(response));
                }
                const payload = await response.json();
                const draft = unwrapData(payload);
                if (!editorId && (response.status === 202 || payload?.success === false)) {
                    window.showToast?.(L.CreateReconciliationPending, 'warning');
                    return;
                }
                if (editorMode === 'correction') {
                    const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
                    const responseGskuId = valueOf(draft, 'gskuId', 'GskuId');
                    const checkpoint = valueOf(draft, 'checkpoint', 'Checkpoint');
                    const responseVersion = Number(valueOf(draft, 'gskuVersion', 'GskuVersion'));
                    const operationId = valueOf(draft, 'operationId', 'OperationId');
                    if (successful !== true
                        || String(responseGskuId).toLowerCase() !== editorId.toLowerCase()
                        || !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(operationId))
                        || !Number.isInteger(responseVersion) || responseVersion < editorGskuVersion
                        || response.status === 202 && checkpoint !== 'AwaitingDecision'
                        || response.status === 200 && checkpoint !== 'Completed') {
                        throw new Error(L.ErrorGateway);
                    }
                    const refreshed = await fetchDetail(editorId);
                    const refreshedVersion = Number(valueOf(refreshed, 'gskuVersion', 'GskuVersion'));
                    const refreshedRevisionVersion = Number(valueOf(refreshed, 'revisionVersion', 'RevisionVersion'));
                    const refreshedActions = readAvailableActions(refreshed);
                    if (!Number.isInteger(refreshedRevisionVersion)
                        || response.status === 202 && (refreshedVersion !== editorGskuVersion
                            || refreshedRevisionVersion !== editorRevisionVersion
                            || refreshedActions.includes('REQUEST_CORRECTION'))
                        || response.status === 200 && (refreshedVersion <= editorGskuVersion
                            || String(valueOf(refreshed, 'packUomCode', 'PackUomCode')) !== String(packUomCode)
                            || Number(valueOf(refreshed, 'packQuantity', 'PackQuantity')) !== Number(quantity))) {
                        throw new Error(L.LifecycleStateChanged);
                    }
                } else if (editorId) {
                    const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
                    const responseGskuId = valueOf(draft, 'gskuId', 'GskuId');
                    const responseVersion = Number(valueOf(draft, 'version', 'Version'));
                    if (successful !== true
                        || String(responseGskuId).toLowerCase() !== editorId.toLowerCase()
                        || !Number.isInteger(responseVersion)
                        || responseVersion <= editorGskuVersion) {
                        throw new Error(L.ErrorGateway);
                    }
                }

                const nextToken = payload?.formAttemptToken || payload?.FormAttemptToken;
                const tokenInput = document.getElementById('formAttemptToken');
                if (tokenInput && nextToken) tokenInput.value = nextToken;
                const code = valueOf(draft, 'canonicalCode', 'CanonicalCode') || L.NotAvailable;
                const revision = valueOf(draft, 'revisionIdentifier', 'RevisionIdentifier') || L.NotAvailable;
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
                form.reset();
                $('#globalProductId, #packUomCode').val(null).trigger('change');
                dt?.ajax.reload(null, false);
                window.showToast?.(editorMode === 'correction'
                    ? response.status === 202 ? L.CorrectionPending : L.CorrectionCompleted
                    : editorId ? L.UpdateSuccess : L.CreateSuccessWithIdentifiers
                    .replace('{0}', code)
                    .replace('{1}', revision),
                    editorMode === 'correction' && response.status === 202 ? 'warning' : 'success');
                editorId = '';
                editorGskuVersion = null;
                editorRevisionVersion = null;
                editorMode = 'create';
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                if (button) button.disabled = false;
            }
        }, { entityName, type: 'primary', confirmButtonText });
    };

    const requestLifecycle = (id, action, actionButton) => {
        if (!id || !['SUBMIT', 'WITHDRAW_APPROVAL'].includes(action)) return;
        const confirmation = action === 'SUBMIT' ? L.SubmitConfirmation : L.WithdrawConfirmation;
        const confirmButtonText = action === 'SUBMIT' ? L.SubmitIdentity : L.WithdrawApproval;
        window.showConfirm?.(confirmation, async () => {
            const requestKey = `${id}:${action}`;
            if (lifecycleRequests.has(requestKey)) return;
            lifecycleRequests.add(requestKey);
            setBusy(actionButton, true);
            try {
                const detail = await fetchDetail(id);
                if (!readAvailableActions(detail).includes(action)) throw new Error(L.LifecycleStateChanged);
                const gskuVersion = Number(valueOf(detail, 'gskuVersion', 'GskuVersion'));
                if (!Number.isInteger(gskuVersion) || gskuVersion < 0) throw new Error(L.ErrorConflict);
                const body = new FormData();
                const suffix = action === 'SUBMIT' ? 'submit' : 'identity-approval/withdraw';
                if (action === 'SUBMIT') body.set('ExpectedVersion', String(gskuVersion));
                else {
                    body.set('ExpectedGskuVersion', String(gskuVersion));
                    body.set('ReasonCode', 'REQUESTER_WITHDRAWAL');
                    body.set('Comment', '');
                }
                const token = document.querySelector('#gskuLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
                body.set('__RequestVerificationToken', token);
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${suffix}`, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }, body
                });
                if (response.status === 401) handleUnauthorized();
                if (!response.ok) throw new Error(await getErrorMessage(response));
                const payload = await response.json();
                const successful = payload?.isSuccessful ?? payload?.IsSuccessful ?? payload?.success;
                const statusCode = Number(payload?.statusCode ?? payload?.StatusCode ?? response.status);
                if (successful !== true || statusCode !== response.status) throw new Error(L.ErrorGateway);
                const refreshed = await fetchDetail(id);
                const refreshedState = lifecycleCode(valueOf(refreshed, 'lifecycleStatus', 'LifecycleStatus'));
                const refreshedActions = readAvailableActions(refreshed);
                if (action === 'SUBMIT' && (refreshedState !== 2 || refreshedActions.includes('SUBMIT'))
                    || action === 'WITHDRAW_APPROVAL' && response.status === 200
                        && (refreshedState !== 1 || refreshedActions.includes('WITHDRAW_APPROVAL'))) {
                    throw new Error(L.LifecycleStateChanged);
                }
                dt?.ajax.reload(null, false);
                window.showToast?.(
                    action === 'SUBMIT' ? L.SubmitPendingSuccess
                        : response.status === 202 ? L.WithdrawPending : L.WithdrawSuccess,
                    response.status === 202 && action === 'WITHDRAW_APPROVAL' ? 'warning' : 'success');
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                lifecycleRequests.delete(requestKey);
                setBusy(actionButton, false);
            }
        }, { type: 'warning', confirmButtonText });
    };

    const requestRetirement = (id, actionButton) => {
        if (!id) return;
        window.showConfirm?.(L.RetirementRequestConfirmation, async (input) => {
            const requestReason = String(input ?? '').trim();
            if (!requestReason || Array.from(requestReason).length > 128) {
                window.showToast?.(L.RetirementRequestReasonRequired, 'error');
                return;
            }
            if (/[\u0000-\u001F\u007F-\u009F]/u.test(requestReason)) {
                window.showToast?.(L.RetirementRequestReasonInvalid, 'error');
                return;
            }
            const requestKey = `${id}:REQUEST_RETIREMENT`;
            if (lifecycleRequests.has(requestKey)) return;
            lifecycleRequests.add(requestKey);
            setBusy(actionButton, true);
            try {
                const detail = await fetchDetail(id);
                if (!readAvailableActions(detail).includes('REQUEST_RETIREMENT')) {
                    throw new Error(L.LifecycleStateChanged);
                }
                const gskuVersion = Number(valueOf(detail, 'gskuVersion', 'GskuVersion'));
                const revisionVersion = Number(valueOf(detail, 'revisionVersion', 'RevisionVersion'));
                if (!Number.isInteger(gskuVersion) || gskuVersion < 0
                    || !Number.isInteger(revisionVersion) || revisionVersion < 0) {
                    throw new Error(L.ErrorConflict);
                }
                const token = document.querySelector(
                    '#gskuLifecycleToken input[name="__RequestVerificationToken"]')?.value || '';
                const body = new FormData();
                body.set('ExpectedGskuVersion', String(gskuVersion));
                body.set('RequestReason', requestReason);
                body.set('__RequestVerificationToken', token);
                const response = await fetch(
                    `${endpoint}/${encodeURIComponent(id)}/retirement-requests`, {
                        method: 'POST', credentials: 'same-origin',
                        headers: {
                            'RequestVerificationToken': token,
                            'X-Requested-With': 'XMLHttpRequest'
                        },
                        body
                    });
                if (response.status === 401) handleUnauthorized();
                if (response.status !== 200 && response.status !== 202) {
                    throw new Error(await getErrorMessage(response));
                }
                const payload = await response.json();
                const data = unwrapData(payload);
                const successful = payload?.isSuccessful ?? payload?.IsSuccessful;
                const statusCode = Number(payload?.statusCode ?? payload?.StatusCode);
                const responseId = valueOf(data, 'gskuId', 'GskuId');
                const responseVersion = Number(valueOf(data, 'gskuVersion', 'GskuVersion'));
                const operationId = valueOf(data, 'operationId', 'OperationId');
                const checkpoint = valueOf(data, 'checkpoint', 'Checkpoint');
                if (successful !== true || statusCode !== response.status
                    || String(responseId).toLowerCase() !== String(id).toLowerCase()
                    || !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(operationId))
                    || !Number.isInteger(responseVersion) || responseVersion <= gskuVersion
                    || response.status === 202 && checkpoint !== 'AwaitingDecision'
                    || response.status === 200 && checkpoint !== 'Completed') {
                    throw new Error(L.ErrorGateway);
                }
                const refreshed = await fetchDetail(id);
                const refreshedState = lifecycleCode(valueOf(refreshed, 'lifecycleStatus', 'LifecycleStatus'));
                const refreshedGskuVersion = Number(valueOf(refreshed, 'gskuVersion', 'GskuVersion'));
                const refreshedRevisionVersion = Number(valueOf(refreshed, 'revisionVersion', 'RevisionVersion'));
                const refreshedActions = readAvailableActions(refreshed);
                if (refreshedGskuVersion !== responseVersion
                    || response.status === 202 && (refreshedState !== 3
                        || refreshedRevisionVersion !== revisionVersion
                        || refreshedActions.includes('REQUEST_CORRECTION')
                        || refreshedActions.includes('REQUEST_RETIREMENT'))
                    || response.status === 200 && (refreshedState !== 4
                        || refreshedRevisionVersion <= revisionVersion)) {
                    throw new Error(L.LifecycleStateChanged);
                }
                dt?.ajax.reload(null, false);
                window.showToast?.(L.RetirementRequestedSuccess,
                    response.status === 202 ? 'warning' : 'success');
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                lifecycleRequests.delete(requestKey);
                setBusy(actionButton, false);
            }
        }, {
            type: 'warning', showInput: true, inputType: 'text', inputRequired: true,
            inputLabel: L.RetirementRequestReasonLabel,
            inputAttributes: { maxlength: 128 }, confirmButtonText: L.RequestRetirement
        });
    };

    const initDataTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                attr: { title: L.Filter, 'aria-label': L.Filter, 'aria-controls': 'inlineFilterCollapse' },
                action: toggleFilter
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView)}</span>`,
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { title: L.SaveView, 'aria-label': L.SaveView },
                action: async (event, api) => {
                    try {
                        await saveDefaultView(getCurrentView(api || dt));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved, 'success');
                    } catch (error) {
                        if (!error?.authHandled) window.showToast?.(L.ErrorGateway, 'error');
                    }
                }
            }
        };
        const config = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            pageLength: 20,
            order: savedState?.order || baseOrder,
            search: { search: savedState?.search || '' },
            colReorder: { columns: ':gt(0):not(:last-child)' },
            ajax: (data, callback) => {
                fetch(`${endpoint}?${buildQuery(data)}`, { credentials: 'same-origin', headers: getAuthHeaders() })
                    .then(async (response) => {
                        if (response.status === 401) handleUnauthorized();
                        if (!response.ok) throw new Error(await getErrorMessage(response));
                        return response.json();
                    })
                    .then((payload) => {
                        const page = unwrapData(payload);
                        const count = page.totalCount || page.TotalCount || 0;
                        callback({ data: page.items || page.Items || [], recordsTotal: count, recordsFiltered: count });
                    })
                    .catch((error) => {
                        if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'globalProductName', name: 'globalProductName' },
                { data: 'revisionIdentifier', name: 'revisionIdentifier' },
                { data: 'packQuantity', name: 'packQuantity' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                {
                    targets: 2,
                    render: (data, type, row) => {
                        const code = row.globalProductCanonicalCode || row.GlobalProductCanonicalCode || '';
                        const name = data || row.GlobalProductName || '';
                        return escapeHtml(`${code}${code && name ? ' — ' : ''}${name}`);
                    }
                },
                { targets: 3, render: escapeHtml },
                {
                    targets: 4,
                    render: (data, type, row) => escapeHtml(`${data} ${row.packUomCode || row.PackUomCode || ''}`.trim())
                },
                { targets: 5, render: renderLifecycle },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderActions(row)
                }
            ],
            buttons: window.DtDefaults.exportButtons(canCreate ? L.AddNew : null, {}, extraButtons, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                applySavedTableState(api, savedState || {});
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    openCreate();
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount()); }
        });
        dt = new DataTable(tableEl, config);
        $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        bindFilter();
        document.getElementById('btnSaveGsku')?.addEventListener('click', submitEditor);
        document.addEventListener('click', (event) => {
            const menuToggle = event.target.closest('.js-gsku-actions-toggle');
            if (menuToggle && menuToggle.closest('.datatables-gskus')) {
                event.preventDefault();
                loadActionMenu(menuToggle);
                return;
            }
            const quickViewAction = event.target.closest('.js-quick-view');
            const action = quickViewAction || event.target.closest('.js-edit-draft, .js-request-correction, .js-request-retirement, .js-lifecycle-action');
            if (!action || !action.closest('.datatables-gskus') || action.classList.contains('disabled')) return;
            event.preventDefault();
            const id = action.dataset.id;
            const code = action.dataset.action;
            if (code === 'EDIT') openEdit(id, action);
            else if (code === 'REQUEST_CORRECTION') openCorrection(id, action);
            else if (code === 'REQUEST_RETIREMENT') requestRetirement(id, action);
            else if (code === 'SUBMIT' || code === 'WITHDRAW_APPROVAL') requestLifecycle(id, code, action);
            else populateDetails(id);
        });
    };

    return {
        init: async () => {
            bindEvents();
            await initDataTable();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => GskusList.init());
