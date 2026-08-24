/**
 * MOD-0290 GSKU Register — tenant-shell Golden Slim read/create-only surface.
 * Browser traffic is restricted to the same-origin MVC proxy.
 */
'use strict';

const GskusList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    const endpoint = '/MasterDataManagement/Gskus/api';
    const tableEl = document.querySelector('.datatables-gskus');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'Gskus' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const totalColumnCount = 7;
    const baseOrder = [[1, 'asc']];
    const L = window.L10n || {};
    const canCreate = document.querySelector('[data-can-create]')?.getAttribute('data-can-create') === 'true';
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const emptyFilters = () => ({ search: '' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const normalizeSearch = (value) => typeof value === 'string' ? value.trim().slice(0, 200) : '';
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
        filters: { search: normalizeSearch(view?.filters?.search) },
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
            filters: { search },
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
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView || 'Default').trim(),
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
        const input = document.getElementById('filterGskuSearch');
        if (input) input.value = normalized.search;
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
            503: L.ErrorProviderUnavailable,
            504: L.ErrorProviderTimeout
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
    const bindFilter = () => {
        const collapse = document.getElementById('inlineFilterCollapse');
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            dt?.search(normalizeSearch(document.getElementById('filterGskuSearch')?.value)).draw();
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
    };

    const setText = (elementId, fieldValue) => {
        const element = document.getElementById(elementId);
        if (element) element.textContent = fieldValue === null || fieldValue === undefined || fieldValue === ''
            ? L.NotAvailable
            : String(fieldValue);
    };
    const formatDate = (dateValue) => dateValue
        ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, {
            dateStyle: 'medium', timeStyle: 'short'
        }).format(new Date(dateValue))
        : L.NotAvailable;
    const populateDetails = async (id) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                credentials: 'same-origin', headers: getAuthHeaders()
            });
            if (response.status === 401) handleUnauthorized();
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const detail = unwrapData(await response.json());
            const code = valueOf(detail, 'canonicalCode', 'CanonicalCode');
            const productCode = valueOf(detail, 'globalProductCanonicalCode', 'GlobalProductCanonicalCode');
            const productName = valueOf(detail, 'globalProductName', 'GlobalProductName');
            const quantity = valueOf(detail, 'packQuantity', 'PackQuantity');
            const uom = valueOf(detail, 'packUomCode', 'PackUomCode');
            setText('oc-title', code);
            setText('oc-subtitle', valueOf(detail, 'revisionIdentifier', 'RevisionIdentifier'));
            setText('oc-id', valueOf(detail, 'id', 'Id'));
            setText('oc-code', code);
            setText('oc-global-product', `${productCode || ''}${productCode && productName ? ' — ' : ''}${productName || ''}`);
            setText('oc-revision', valueOf(detail, 'revisionIdentifier', 'RevisionIdentifier'));
            setText('oc-pack', `${quantity ?? ''}${quantity !== null && quantity !== undefined && uom ? ' ' : ''}${uom || ''}`);
            setText('oc-version', valueOf(detail, 'version', 'Version'));
            setText('oc-created-at', formatDate(valueOf(detail, 'createdAt', 'CreatedAt')));
            setText('oc-updated-at', formatDate(valueOf(detail, 'updatedAt', 'UpdatedAt')));
            const statusElement = document.getElementById('oc-status');
            if (statusElement) {
                statusElement.outerHTML = renderLifecycle(valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'))
                    .replace('<span ', '<span id="oc-status" ');
            }
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        } catch (error) {
            if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
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
        form.reset();
        form.classList.remove('was-validated');
        initializeCreateSelects();
        $('#globalProductId, #packUomCode').val(null).trigger('change');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        await loadCreateOptions();
    };
    const validateQuantity = (quantity, selectedUom) => {
        const match = String(quantity).match(/^\d+(?:\.(\d+))?$/);
        if (!match || Number(quantity) <= 0) return L.PackQuantityRequired;
        const precision = Number(selectedUom?.dataset.maximumDecimalPrecision);
        if (!Number.isInteger(precision) || (match[1]?.length || 0) > precision) {
            return (L.PackQuantityPrecision || '').replace('{0}', String(Number.isInteger(precision) ? precision : 0));
        }
        return '';
    };
    const submitCreate = () => {
        const form = document.getElementById('formGsku');
        const globalProductId = $('#globalProductId').val();
        const packUomCode = $('#packUomCode').val();
        const quantity = document.getElementById('packQuantity')?.value || '';
        if (!form || !globalProductId || !packUomCode || !form.checkValidity()) {
            form?.classList.add('was-validated');
            window.showToast?.(!globalProductId ? L.GlobalProductRequired : (!packUomCode ? L.PackUomRequired : L.PackQuantityRequired), 'error');
            return;
        }
        const quantityError = validateQuantity(quantity, document.querySelector('#packUomCode option:checked'));
        if (quantityError) {
            window.showToast?.(quantityError, 'error');
            return;
        }

        const entityName = $('#globalProductId option:selected').text() || String(globalProductId);
        window.showConfirm?.(L.CreateConfirmation, async () => {
            const button = document.getElementById('btnSaveGsku');
            if (button) button.disabled = true;
            try {
                const body = new FormData();
                body.set('GlobalProductId', String(globalProductId));
                body.set('PackQuantity', quantity);
                body.set('PackUomCode', String(packUomCode));
                body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
                const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                body.set('__RequestVerificationToken', antiForgeryToken);
                const response = await fetch(endpoint, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': antiForgeryToken, 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });
                if (response.status === 401) handleUnauthorized();
                if (response.status !== 201 && response.status !== 202) throw new Error(await getErrorMessage(response));
                const payload = await response.json();
                const draft = unwrapData(payload);
                if (response.status === 202 || payload?.success === false) {
                    window.showToast?.(L.CreateReconciliationPending, 'warning');
                    return;
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
                window.showToast?.((L.CreateSuccessWithIdentifiers || '')
                    .replace('{0}', code)
                    .replace('{1}', revision), 'success');
            } catch (error) {
                if (!error?.authHandled) window.showToast?.(error.message || L.ErrorGateway, 'error');
            } finally {
                if (button) button.disabled = false;
            }
        }, { entityName, type: 'primary', confirmButtonText: L.Save });
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
                    render: (data, type, row) => window.DitenDataTable.renderActions([{
                        key: 'details',
                        className: 'js-quick-view',
                        text: L.QuickView,
                        icon: 'bx bx-show',
                        attrs: { 'data-id': row.id || row.Id, title: L.ViewDetails }
                    }])
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
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), 0); }
        });
        dt = new DataTable(tableEl, config);
        $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const bindEvents = () => {
        bindFilter();
        document.getElementById('btnSaveGsku')?.addEventListener('click', submitCreate);
        document.addEventListener('click', (event) => {
            const action = event.target.closest('.js-quick-view');
            if (!action || !action.closest('.datatables-gskus')) return;
            event.preventDefault();
            populateDetails(action.dataset.id);
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
