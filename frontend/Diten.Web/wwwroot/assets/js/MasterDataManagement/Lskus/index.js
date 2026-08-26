/* MOD-0290 LSKU Golden Slim — same-origin MVC proxy only. */
const LskusList = (() => {
    'use strict';

    const endpoint = '/MasterDataManagement/Lskus/api';
    const tableEl = document.querySelector('.datatables-lskus');
    const L = window.L10n || {};
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'Lskus' };
    const baseOrder = [[0, 'asc']];
    const dataColumnCount = 5;
    const initialColumnOrder = Array.from({ length: dataColumnCount + 1 }, (_, index) => index);

    let dt = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const valueOf = (value, camelName, pascalName) => value?.[camelName] ?? value?.[pascalName];
    const unwrapData = value => value?.data ?? value?.Data ?? value;
    const escapeHtml = value => String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');

    const emptyFilters = () => ({ search: '' });
    const defaultColVis = () => Array.from({ length: dataColumnCount + 1 }, () => true);
    const normalizeView = view => ({
        filters: { ...emptyFilters(), ...(view?.filters || {}) },
        search: view?.search || '',
        colVis: Array.isArray(view?.colVis) ? view.colVis : defaultColVis(),
        columnOrder: Array.isArray(view?.columnOrder) ? view.columnOrder : [...initialColumnOrder],
        order: Array.isArray(view?.order) && view.order.length ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: dataColumnCount + 1 }, (_, index) => index),
        order: baseOrder
    });
    const serializeView = view => JSON.stringify(normalizeView(view));
    const getCurrentView = api => normalizeView({
        filters: { search: document.getElementById('lskuSearch')?.value || '' },
        search: api.search(),
        colVis: api.columns().visible().toArray(),
        columnOrder: api.colReorder?.order?.() || [...initialColumnOrder],
        order: api.order()
    });
    const isDirtyComparedToDefault = api =>
        serializeView(getCurrentView(api)) !== serializeView(defaultViewState || getResetBaselineState());
    const setSaveFilterVisible = visible => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const applySavedTableState = (api, view) => {
        const normalized = normalizeView(view);
        const searchInput = document.getElementById('lskuSearch');
        if (searchInput) searchInput.value = normalized.filters.search || normalized.search;
        api.search(normalized.search);
        normalized.colVis.forEach((visible, index) => api.column(index).visible(visible, false));
        if (api.colReorder?.order) api.colReorder.order(normalized.columnOrder, true);
        api.order(normalized.order);
    };

    const parseSavedConfiguration = record => {
        const raw = record?.viewDefinition ?? record?.ViewDefinition;
        if (!raw) return null;
        try { return normalizeView(typeof raw === 'string' ? JSON.parse(raw) : raw); }
        catch { return null; }
    };
    const getSavedViewId = record => record?.id || record?.Id || record?._id || null;
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(
                personalizationContext.moduleKey,
                personalizationContext.pageKey);
            const records = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            defaultViewRecord = records.find(item => item?.isDefault || item?.IsDefault) || records[0] || null;
            defaultViewState = parseSavedConfiguration(defaultViewRecord);
            return defaultViewState;
        } catch {
            defaultViewRecord = null;
            defaultViewState = null;
            return null;
        }
    };
    const saveDefaultView = async view => {
        if (!personalizationClient?.saveView) return null;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView || 'Default'),
            isDefault: true,
            viewDefinition: normalized
        };
        const id = getSavedViewId(defaultViewRecord);
        const response = id && personalizationClient.updateView
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = response?.data || response?.Data || response || payload;
        defaultViewState = normalized;
        setSaveFilterVisible(false);
        return defaultViewRecord;
    };

    const getErrorMessage = async response => {
        const payload = await response.json().catch(() => null);
        return payload?.errors?.[0] || payload?.Errors?.[0] || L.ErrorGateway || 'Request failed.';
    };
    const formatDate = value => value
        ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : (L.Unknown || '-');

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const loadCreateOptions = async () => {
        const response = await fetch(`${endpoint}/create-options`, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });
        if (!response.ok) throw new Error(await getErrorMessage(response));
        const options = unwrapData(await response.json());
        const gskus = options?.gskus || options?.Gskus || [];
        const markets = options?.markets || options?.Markets || [];
        const gskuSelect = document.getElementById('gskuId');
        const marketSelect = document.getElementById('marketCode');
        gskuSelect.innerHTML = '<option value=""></option>';
        marketSelect.innerHTML = '<option value=""></option>';
        gskus.forEach(item => gskuSelect.add(new Option(
            `${valueOf(item, 'canonicalCode', 'CanonicalCode')} — ${valueOf(item, 'globalProductName', 'GlobalProductName')}`,
            valueOf(item, 'id', 'Id'))));
        markets.forEach(item => marketSelect.add(new Option(
            `${valueOf(item, 'code', 'Code')} — ${valueOf(item, 'displayText', 'DisplayText')}`,
            valueOf(item, 'code', 'Code'))));
    };

    const openCreate = async () => {
        const form = document.getElementById('formLsku');
        if (!form) return;
        form.reset();
        form.classList.remove('was-validated');
        document.getElementById('requiredProgress').textContent = '0/2';
        await loadCreateOptions();
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };

    const submitCreate = async () => {
        const form = document.getElementById('formLsku');
        const gskuId = document.getElementById('gskuId')?.value || '';
        const marketCode = document.getElementById('marketCode')?.value || '';
        if (!form || !gskuId || !marketCode || !form.checkValidity()) {
            form?.classList.add('was-validated');
            return;
        }
        const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const body = new FormData();
        body.set('GskuId', gskuId);
        body.set('MarketCode', marketCode);
        body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
        body.set('__RequestVerificationToken', antiForgeryToken);
        const response = await fetch(endpoint, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken },
            body
        });
        const payload = await response.json().catch(() => null);
        if (response.status === 202 || payload?.success === false && response.status === 202) {
            window.showToast?.(L.CreateReconciliationPending || L.Pending, 'warning');
            return;
        }
        if (response.status !== 201) throw new Error(payload?.errors?.[0] || L.ErrorGateway);
        const nextToken = payload?.formAttemptToken || payload?.FormAttemptToken;
        if (nextToken) document.getElementById('formAttemptToken').value = nextToken;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
        dt?.ajax.reload(null, false);
        window.showToast?.(L.CreateSuccess || L.Success, 'success');
    };

    const setDetailValue = (id, value) => {
        const element = document.getElementById(id);
        if (element) element.textContent = value ?? L.Unknown ?? '-';
    };
    const populateDetails = async id => {
        const loading = document.getElementById('lskuDetailLoading');
        const error = document.getElementById('lskuDetailError');
        loading?.classList.remove('d-none');
        error?.classList.add('d-none');
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(response.status === 404 ? L.ErrorNotFound : await getErrorMessage(response));
            const detail = unwrapData(await response.json());
            setDetailValue('oc-title', valueOf(detail, 'canonicalCode', 'CanonicalCode'));
            setDetailValue('oc-code', valueOf(detail, 'canonicalCode', 'CanonicalCode'));
            setDetailValue('oc-gsku-code', valueOf(detail, 'gskuCanonicalCode', 'GskuCanonicalCode'));
            setDetailValue('oc-market', valueOf(detail, 'marketCode', 'MarketCode'));
            setDetailValue('oc-status', valueOf(detail, 'lifecycleStatus', 'LifecycleStatus'));
            setDetailValue('oc-version', valueOf(detail, 'version', 'Version'));
            setDetailValue('oc-created-at', formatDate(valueOf(detail, 'createdAt', 'CreatedAt')));
            setDetailValue('oc-updated-at', formatDate(valueOf(detail, 'updatedAt', 'UpdatedAt')));
        } catch (exception) {
            if (error) {
                error.textContent = exception.message || L.ErrorGateway;
                error.classList.remove('d-none');
            }
        } finally {
            loading?.classList.add('d-none');
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        }
    };

    const buildQuery = data => new URLSearchParams({
        pageNumber: String(Math.floor(data.start / data.length) + 1),
        pageSize: String(data.length),
        search: data.search.value || ''
    });

    const initDataTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const savedState = await loadDefaultView();
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                attr: {
                    title: L.Filter,
                    'aria-label': L.Filter,
                    'aria-controls': 'inlineFilterCollapse'
                },
                action: toggleInlineFilter
            },
            saveFilterBtn: {
                text: L.SaveView || 'Save View',
                className: 'btn btn-label-primary dt-save-filter-btn d-none',
                attr: { title: L.SaveView, 'aria-label': L.SaveView },
                action: async (event, api) => {
                    await saveDefaultView(getCurrentView(api || dt));
                    setSaveFilterVisible(false);
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
            colReorder: { columns: ':not(:last-child)' },
            ajax: (data, callback) => fetch(`${endpoint}?${buildQuery(data)}`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            }).then(async response => {
                if (!response.ok) throw new Error(await getErrorMessage(response));
                return response.json();
            }).then(payload => {
                const page = unwrapData(payload);
                const count = page?.totalCount || page?.TotalCount || 0;
                callback({ data: page?.items || page?.Items || [], recordsTotal: count, recordsFiltered: count });
            }).catch(exception => {
                window.showToast?.(exception.message || L.ErrorGateway, 'error');
                callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
            }),
            columns: [
                { data: 'canonicalCode' },
                { data: 'gskuCanonicalCode' },
                { data: 'marketCode' },
                { data: 'lifecycleStatus' },
                { data: 'version' },
                { data: null }
            ],
            columnDefs: [
                { targets: 0, render: data => `<span class="fw-medium">${escapeHtml(data)}</span>` },
                { targets: [1, 2, 3, 4], render: escapeHtml },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => window.DitenDataTable.renderActions([{
                        key: 'details',
                        className: 'js-quick-view',
                        text: L.QuickView,
                        icon: 'bx bx-show',
                        attrs: { 'data-id': valueOf(row, 'id', 'Id'), title: L.ViewDetails }
                    }])
                }
            ],
            buttons: window.DtDefaults.exportButtons(
                document.querySelector('[data-can-create]')?.dataset.canCreate === 'true' ? L.AddNew : null,
                {},
                extraButtons,
                { exportColumns: [0, 1, 2, 3, 4], colvisColumns: [0, 1, 2, 3, 4] }),
            initComplete: function () {
                const api = this.api();
                applySavedTableState(api, savedState || getResetBaselineState());
                document.querySelector('.dt-export-collection-btn')?.setAttribute('title', L.Export);
                document.querySelector('.buttons-colvis')?.setAttribute('title', L.ColumnVisibility);
                document.querySelector('.add-new')?.addEventListener('click', event => {
                    event.preventDefault();
                    openCreate().catch(exception => window.showToast?.(exception.message || L.ErrorGateway, 'error'));
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
        document.getElementById('btnSaveLsku')?.addEventListener('click', () =>
            submitCreate().catch(exception => window.showToast?.(exception.message || L.ErrorGateway, 'error')));
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            if (!dt) return;
            dt.search(document.getElementById('lskuSearch')?.value || '').draw();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', event => {
            event.preventDefault();
            if (!dt) return;
            applySavedTableState(dt, getResetBaselineState());
            dt.draw();
            setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        document.addEventListener('change', event => {
            if (!event.target.matches('#gskuId, #marketCode')) return;
            const completed = ['gskuId', 'marketCode'].filter(id => document.getElementById(id)?.value).length;
            document.getElementById('requiredProgress').textContent = `${completed}/2`;
        });
        document.addEventListener('click', event => {
            const action = event.target.closest('.js-quick-view');
            if (!action || !action.closest('.datatables-lskus')) return;
            event.preventDefault();
            populateDetails(action.dataset.id);
        });
    };

    return { init: async () => { bindEvents(); await initDataTable(); } };
})();

document.addEventListener('DOMContentLoaded', () => LskusList.init());
