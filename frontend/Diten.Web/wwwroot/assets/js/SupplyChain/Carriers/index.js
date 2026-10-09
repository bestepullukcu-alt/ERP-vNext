/**
 * MOD-0184 Carrier Management — bounded list/create/status tenant surface.
 * Browser traffic is same-origin; the MVC adapter is the only Gateway caller.
 */
'use strict';

const CarrierList = (function () {
    const endpoint = '/SupplyChain/Carriers/api';
    const tableElement = document.querySelector('.datatables-carriers');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'SupplyChain', pageKey: 'Carriers' };
    const baseOrder = [[1, 'asc']];
    const totalColumnCount = 6;
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const filterCollapseId = 'inlineFilterCollapse';
    let dt;
    let L = window.L10n || {};
    let permissions = { canCreate: false, canChangeStatus: false };
    let appliedFilters = { status: '' };
    // R-2 (SHIPMENT-BUNDLE 3.2.0): the company is a REQUIRED scope on every call, not an optional filter —
    // the service takes exactly one X-Legal-Entity-Id, so there is no 'all companies' list to ask for.
    let legalEntities = [];
    let legalEntityScope = '';
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let createIntent = null;
    const statusIntents = new Map();

    const syncL10n = () => { L = window.L10n || {}; };
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': createUuid() });
    const getAntiForgeryToken = (form) =>
        form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const createUuid = () => typeof crypto?.randomUUID === 'function'
        ? crypto.randomUUID()
        : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (char) => {
            const value = crypto.getRandomValues(new Uint8Array(1))[0] & 15;
            return (char === 'x' ? value : (value & 3) | 8).toString(16);
        });
    const escapeHtml = (value) => String(value ?? '')
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');

    const readPermissions = () => {
        const payload = document.getElementById('carrier-permissions');
        if (!payload) return;
        try { permissions = Object.assign(permissions, JSON.parse(payload.textContent || '{}')); }
        catch (error) { console.error('[Carriers] Permission payload could not be parsed.', error); }
    };

    const statusLabels = () => ({
        Active: L.StatusActive,
        Suspended: L.StatusSuspended,
        Retired: L.StatusRetired
    });
    const modeLabels = () => ({
        Road: L.ModeRoad,
        Air: L.ModeAir,
        Sea: L.ModeSea,
        Rail: L.ModeRail,
        Parcel: L.ModeParcel
    });

    const getStatusBadge = (status) => {
        const css = status === 'Active' ? 'bg-label-success'
            : status === 'Suspended' ? 'bg-label-warning'
                : status === 'Retired' ? 'bg-label-secondary' : 'bg-label-primary';
        return `<span class="badge ${css}">${escapeHtml(statusLabels()[status] || L.Unknown)}</span>`;
    };
    const getModeText = (modes) => (Array.isArray(modes) ? modes : [])
        .map((mode) => modeLabels()[mode] || mode).join(', ');

    const normalizeSavedString = (value) => typeof value === 'string' ? value.trim() : '';
    const getSavedViewId = (view) => view?.id || view?.Id || view?._id || '';
    const getSavedViewName = (view) => normalizeSavedString(view?.viewName || view?.ViewName);
    const isSavedViewDefault = (view) => view?.isDefault === true || view?.IsDefault === true;
    const getSavedViewDefinition = (view) => {
        const raw = view?.viewDefinition ?? view?.ViewDefinition ?? {};
        if (raw && typeof raw === 'object') return raw;
        if (typeof raw === 'string') {
            try { return JSON.parse(raw); } catch (error) { return {}; }
        }
        return {};
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((result, index) => {
        result[index] = true;
        return result;
    }, {});
    const normalizeColVis = (value) => {
        if (!value || typeof value !== 'object') return null;
        const result = {};
        saveViewColumnIndexes.forEach((index) => {
            if (typeof value[index] === 'boolean') result[index] = value[index];
        });
        return Object.keys(result).length ? result : null;
    };
    const normalizeColumnOrder = (value) => {
        if (!Array.isArray(value) || value.length !== totalColumnCount) return null;
        const result = value.map(Number);
        return result.every((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount)
            && new Set(result).size === totalColumnCount ? result : null;
    };
    const captureColumnVisibility = (api) => saveViewColumnIndexes.reduce((result, index) => {
        result[index] = !!api.column(index).visible();
        return result;
    }, {});
    const captureColumnOrder = (api) => {
        try { return normalizeColumnOrder(api?.colReorder?.order?.()); }
        catch (error) { return null; }
    };
    const getCurrentView = (api) => ({
        filters: { status: appliedFilters.status || '' },
        search: api.search() || '',
        colVis: captureColumnVisibility(api),
        columnOrder: captureColumnOrder(api),
        order: api.order()
    });
    const normalizeView = (view) => ({
        filters: { status: normalizeSavedString(view?.filters?.status ?? view?.status) },
        search: normalizeSavedString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColumnOrder(view?.columnOrder)
            || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const serializeView = (view) => JSON.stringify(normalizeView(view));
    const getResetBaselineState = () => {
        const emptyFilters = () => ({ status: '' });
        const defaultColVis = () => saveViewColumnIndexes.reduce((result, index) => {
            result[index] = true;
            return result;
        }, {});
        return {
            filters: emptyFilters(),
            search: '',
            colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index),
            order: baseOrder
        };
    };
    const isDirtyComparedToDefault = (api) => serializeView(getCurrentView(api)) !== serializeView(
        defaultViewState || getResetBaselineState());
    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const applySavedTableState = (api, state) => {
        const normalized = normalizeView(state);
        appliedFilters = { status: normalized.filters.status };
        $('#filterStatus').val(appliedFilters.status).trigger('change');
        api.search(normalized.search);
        const searchInput = api.table().container()?.querySelector('.dt-search input');
        if (searchInput) searchInput.value = normalized.search;
        saveViewColumnIndexes.forEach((index) => api.column(index).visible(normalized.colVis[index], false));
        if (typeof api?.colReorder?.order === 'function') api.colReorder.order(normalized.columnOrder, true);
        api.order(normalized.order);
        api.draw(false);
    };
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return;
        try {
            const result = await personalizationClient.getViews(
                personalizationContext.moduleKey, personalizationContext.pageKey);
            const views = Array.isArray(result) ? result : (result?.data || result?.Data || []);
            defaultViewRecord = views.find(isSavedViewDefault) || views[0] || null;
            defaultViewState = defaultViewRecord
                ? normalizeView(getSavedViewDefinition(defaultViewRecord)) : null;
            if (defaultViewState) appliedFilters = { status: defaultViewState.filters.status };
        } catch (error) {
            if (!error?.authHandled) console.error('[Carriers] Saved view load failed.', error);
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalized,
            isDefault: true,
            visibility: 'private'
        };
        const id = getSavedViewId(defaultViewRecord);
        const result = id
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = result?.data || result?.Data || result || payload;
        defaultViewState = normalized;
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const toolbar = document.querySelector('.dt-filter-btn')?.closest('.dt-layout-row');
        if (host && toolbar) {
            toolbar.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
    };
    const setupFilters = () => {
        const $status = $('#filterStatus');
        if ($status.hasClass('select2-hidden-accessible')) return;
        $status.select2({
            dropdownParent: $(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            minimumResultsForSearch: Infinity,
            selectionCssClass: 'form-select form-select-sm',
            width: 'element',
            allowClear: true
        });
        $status.val(appliedFilters.status).trigger('change');
    };
    const toggleInlineFilter = () => {
        const element = document.getElementById(filterCollapseId);
        if (element) bootstrap.Collapse.getOrCreateInstance(element, { toggle: false }).toggle();
    };
    const bindFilterEvents = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { status: String($('#filterStatus').val() || '') };
            legalEntityScope = String($('#filterLegalEntity').val() || legalEntityScope);
            dt.ajax.reload(() => setSaveFilterVisible(isDirtyComparedToDefault(dt)), true);
            bootstrap.Collapse.getOrCreateInstance(document.getElementById(filterCollapseId), { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            const api = dt;
            applySavedTableState(api, getResetBaselineState());
            api.ajax.reload(() => setSaveFilterVisible(isDirtyComparedToDefault(api)), true);
        });
    };

    // Every call carries the company. Without it the adapter answers 400, so this is appended for list,
    // create and status change alike rather than only where a filter would go.
    const withScope = (url) => {
        if (!legalEntityScope) return url;
        return `${url}${url.includes('?') ? '&' : '?'}legalEntityId=${encodeURIComponent(legalEntityScope)}`;
    };
    const buildListUrl = () => withScope(appliedFilters.status
        ? `${endpoint}?status=${encodeURIComponent(appliedFilters.status)}` : endpoint);
    const fillLegalEntitySelect = (element, selected) => {
        if (!element) return;
        element.innerHTML = '';
        legalEntities.forEach((item) => {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = item.name;
            if (item.id === selected) option.selected = true;
            element.appendChild(option);
        });
    };
    const loadLegalEntities = async () => {
        try {
            const response = await fetch('/SupplyChain/api/legal-entities',
                { credentials: 'same-origin', headers: getAuthHeaders() });
            if (response.status === 401) { handleUnauthorized(); return false; }
            if (!response.ok) { showLegalEntityOutage(); return false; }
            const payload = await readJson(response);
            const rows = Array.isArray(payload) ? payload : (payload?.data ?? payload?.items ?? []);
            legalEntities = rows.map((row) => ({
                id: String(row.legalEntityId ?? row.LegalEntityId ?? ''),
                name: String(row.displayName ?? row.DisplayName ?? row.legalName ?? row.LegalName ?? row.code ?? '')
            })).filter((row) => row.id);
            // An empty answer is not an outage: the tenant genuinely has no referenceable company, and the
            // page says so instead of offering an empty required field.
            if (legalEntities.length === 0) { showLegalEntityEmpty(); return false; }
            // Exactly one company auto-selects, as R-2 requires. With several, the first is the opening scope
            // rather than a blank: the service has no 'all companies' answer, so a blank would guarantee a 400.
            legalEntityScope = legalEntities[0].id;
            fillLegalEntitySelect(document.getElementById('filterLegalEntity'), legalEntityScope);
            fillLegalEntitySelect(document.getElementById('formLegalEntity'), legalEntityScope);
            return true;
        } catch (error) {
            showLegalEntityOutage();
            return false;
        }
    };
    const showLegalEntityOutage = () => window.DtDefaults?.showError?.(L.LegalEntityUnavailable
        || 'Company list is unavailable.');
    const showLegalEntityEmpty = () => window.DtDefaults?.showError?.(L.LegalEntityNone
        || 'No referenceable company is assigned to this tenant.');
    const handleUnauthorized = () => window.DtDefaults?.handleUnauthorized?.();
    const readJson = async (response) => {
        try { return await response.json(); } catch (error) { return null; }
    };
    const correlationSuffix = (response) => {
        const correlation = response?.headers?.get('X-Correlation-Id');
        return correlation ? ` ${L.SupportReference}: ${correlation}` : '';
    };
    const messageForError = (code, status) => ({
        CARRIER_NOT_FOUND: L.CarrierNotFound,
        CARRIER_CODE_CONFLICT: L.CarrierCodeConflict,
        IDEMPOTENCY_KEY_REUSED: L.IdempotencyKeyReused,
        INVALID_CARRIER_TRANSITION: L.InvalidCarrierTransition,
        PERSISTENCE_UNAVAILABLE: L.PersistenceUnavailable,
        INTERNAL_ERROR: L.InternalError
    })[code] || (status === 403 ? L.AccessDenied : status === 400 || status === 415
        ? L.ValidationError : L.ErrorOccurred);
    const notifyFailure = async (response, options) => {
        if (response.status === 401) {
            handleUnauthorized();
            return { code: 'UNAUTHORIZED', payload: null };
        }
        const payload = await readJson(response);
        const code = payload?.error?.code || payload?.Error?.Code || '';
        const message = `${messageForError(code, response.status)}${correlationSuffix(response)}`;
        window.showToast?.(message, response.status >= 500 ? 'error' : 'warning');
        if (code === 'INVALID_CARRIER_TRANSITION' || code === 'CARRIER_NOT_FOUND')
            dt?.ajax?.reload(null, false);
        if (response.status === 403 && options?.offcanvas)
            bootstrap.Offcanvas.getInstance(options.offcanvas)?.hide();
        return { code, payload };
    };
    // R-4a (MODULE-RECIPE 3.6, 3.11, 4.1): skeleton until the first load ends (owned by this page: the shared
    // `.backbone-skeleton` is display:none and DtDefaults fades #skeleton-loader on its own); a failed load is an alert in
    // place of the table, worded as a read failure, never an empty table under a toast; a 403 is the denied card.
    const tableCard = () => document.getElementById('dt-carriers')?.closest('.card-datatable');
    const setSkeleton = (visible) => {
        const skeleton = document.getElementById('carriersSkeleton');
        if (skeleton) skeleton.style.display = visible ? 'block' : 'none';
        const card = tableCard();
        if (card) card.classList.toggle('d-none', visible);
    };
    const showDenied = () => {
        const surface = document.getElementById('carriersListSurface'); const denied = document.getElementById('carriersListDenied');
        if (!surface || !denied) return;
        surface.hidden = true; surface.setAttribute('inert', ''); denied.hidden = false;
    };
    const showLoadFailure = (correlation) => {
        const alert = document.getElementById('carriersLoadError'); if (!alert) return;
        alert.textContent = `${L.ListUnavailable}${correlation ? ` ${L.SupportReference}: ${correlation}` : ''}`;
        alert.classList.remove('d-none'); tableCard()?.classList.add('d-none');
    };
    const clearLoadFailure = () => {
        document.getElementById('carriersLoadError')?.classList.add('d-none'); tableCard()?.classList.remove('d-none');
    };
    let firstLoad = true;
    const loadRows = async (_request, callback) => {
        if (firstLoad) setSkeleton(true);
        try {
            const response = await fetch(buildListUrl(), { credentials: 'same-origin', headers: getAuthHeaders() });
            if (!response.ok) {
                if (response.status === 401) { handleUnauthorized(); callback({ data: [] }); return; }
                if (response.status === 403) { showDenied(); callback({ data: [] }); return; }
                const payload = await readJson(response);
                showLoadFailure(payload?.error?.correlationId || response.headers.get('X-Correlation-Id'));
                callback({ data: [] });
                return;
            }
            const payload = await response.json();
            clearLoadFailure();
            callback({ data: Array.isArray(payload?.items) ? payload.items : [] });
        } catch (error) {
            console.error('[Carriers] List request failed.', error);
            showLoadFailure(null);
            callback({ data: [] });
        } finally {
            if (firstLoad) { firstLoad = false; setSkeleton(false); if (!document.getElementById('carriersLoadError')?.classList.contains('d-none')) tableCard()?.classList.add('d-none'); }
        }
    };

    const getRowData = (button) => {
        let row = dt.row(button.closest('tr'));
        if (!row.data()) row = dt.row(button.closest('tr')?.previousElementSibling);
        return row.data();
    };
    const openQuickView = (row) => {
        if (!row) return;
        document.getElementById('carrierQuickViewCode').textContent = row.carrierCode || '';
        document.getElementById('carrierQuickViewName').textContent = row.displayName || '';
        document.getElementById('carrierQuickViewModes').textContent = getModeText(row.supportedModes);
        const statusHost = document.getElementById('carrierQuickViewStatus');
        statusHost.textContent = statusLabels()[row.status] || L.Unknown;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
    };
    const availableStatusTargets = (status) => status === 'Active'
        ? ['Suspended', 'Retired'] : status === 'Suspended' ? ['Active', 'Retired'] : [];
    const openStatus = (row) => {
        if (!row || !permissions.canChangeStatus) return;
        statusIntents.set(row.carrierId, newIntent());
        const select = document.getElementById('targetStatus');
        select.replaceChildren(...availableStatusTargets(row.status).map((status) =>
            new Option(statusLabels()[status] || status, status)));
        document.getElementById('statusCarrierId').value = row.carrierId;
        document.getElementById('reasonCode').value = '';
        document.getElementById('statusCarrierAlert').classList.add('d-none');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasChangeStatus')).show();
    };
    const showCreate = () => {
        if (!permissions.canCreate) return;
        const form = document.getElementById('formCarrier');
        form.reset();
        createIntent = newIntent();
        $('#supportedModes').val([]).trigger('change');
        document.getElementById('formCarrierAlert').classList.add('d-none');
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };
    // R-4a (MODULE-RECIPE 3.1; MOD-0184 §32.3 as applied, Q403): an intent is one opened create form or status panel, not
    // one payload. The key is minted when the form or panel opens and kept across edits, failures and retries until it
    // closes. After 409 IDEMPOTENCY_KEY_REUSED the intent is spent: the form stops and never mints a key to get past it.
    const newIntent = () => ({ key: createUuid(), blocked: false });
    const requestHeaders = (form, intent) => Object.assign(getAuthHeaders(), {
        'Content-Type': 'application/json',
        'RequestVerificationToken': getAntiForgeryToken(form),
        'Idempotency-Key': intent.key
    });
    const setBusy = (button, busy) => {
        if (!button) return;
        button.disabled = busy;
        button.setAttribute('aria-busy', String(busy));
    };
    const showFormError = (id, message) => {
        const alert = document.getElementById(id);
        alert.textContent = message;
        alert.classList.remove('d-none');
    };

    const submitCreate = async () => {
        const form = document.getElementById('formCarrier');
        const button = document.getElementById('btnSaveCarrier');
        const modes = $('#supportedModes').val() || [];
        const payload = {
            carrierCode: document.getElementById('carrierCode').value,
            displayName: document.getElementById('displayName').value,
            supportedModes: Array.from(modes),
            externalReference: document.getElementById('externalReference').value
        };
        if (payload.carrierCode.length < 1 || payload.displayName.length < 1 || payload.supportedModes.length < 1) {
            showFormError('formCarrierAlert', L.ValidationError);
            return;
        }
        const signature = JSON.stringify(payload);
        if (!createIntent) createIntent = newIntent();
        if (createIntent.blocked) {
            showFormError('formCarrierAlert', L.IdempotencyKeyReused);
            return;
        }
        setBusy(button, true);
        try {
            const response = await fetch(withScope(endpoint), {
                method: 'POST', credentials: 'same-origin', headers: requestHeaders(form, createIntent),
                body: signature
            });
            if (!response.ok) {
                const failure = await notifyFailure(response, { offcanvas: document.getElementById('offcanvasCreateEdit') });
                if (failure.code === 'IDEMPOTENCY_KEY_REUSED') createIntent.blocked = true;
                if (response.status === 400 || response.status === 415 || response.status === 409)
                    showFormError('formCarrierAlert', messageForError(failure.code, response.status));
                return;
            }
            const result = await response.json();
            window.showToast?.(result.idempotentReplay ? L.CreateReplaySuccess : L.CreateSuccess, 'success');
            createIntent = null;
            bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasCreateEdit'))?.hide();
            dt.ajax.reload(null, false);
        } catch (error) {
            console.error('[Carriers] Create request outcome is unknown.', error);
            window.showToast?.(`${L.PersistenceUnavailable} ${L.RetrySameRequest}`, 'error');
        } finally {
            setBusy(button, false);
        }
    };

    const submitStatus = async () => {
        const form = document.getElementById('formCarrierStatus');
        const button = document.getElementById('btnConfirmStatus');
        const carrierId = document.getElementById('statusCarrierId').value;
        const payload = {
            targetStatus: document.getElementById('targetStatus').value,
            reasonCode: document.getElementById('reasonCode').value
        };
        if (!carrierId || !payload.targetStatus) {
            showFormError('statusCarrierAlert', L.ValidationError);
            return;
        }
        const signature = JSON.stringify(payload);
        const intent = statusIntents.get(carrierId) || newIntent();
        statusIntents.set(carrierId, intent);
        if (intent.blocked) {
            showFormError('statusCarrierAlert', L.IdempotencyKeyReused);
            return;
        }
        const run = async () => {
            setBusy(button, true);
            try {
                const response = await fetch(withScope(`${endpoint}/${encodeURIComponent(carrierId)}/status`), {
                    method: 'POST', credentials: 'same-origin', headers: requestHeaders(form, intent),
                    body: signature
                });
                if (!response.ok) {
                    const failure = await notifyFailure(response, { offcanvas: document.getElementById('offcanvasChangeStatus') });
                    if (failure.code === 'IDEMPOTENCY_KEY_REUSED') intent.blocked = true;
                    return;
                }
                const result = await response.json();
                window.showToast?.(result.idempotentReplay ? L.StatusReplaySuccess : L.StatusSuccess, 'success');
                statusIntents.delete(carrierId);
                bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasChangeStatus'))?.hide();
                dt.ajax.reload(null, false);
            } catch (error) {
                console.error('[Carriers] Status request outcome is unknown.', error);
                window.showToast?.(`${L.PersistenceUnavailable} ${L.RetrySameRequest}`, 'error');
            } finally {
                setBusy(button, false);
            }
        };
        window.showConfirm?.(L.StatusChangeConfirm, run, {
            type: 'warning', confirmButtonText: L.ConfirmStatusChange
        });
    };

    const bindPageEvents = () => {
        tableElement?.addEventListener('click', (event) => {
            const quickView = event.target.closest('.js-quick-view');
            if (quickView) { openQuickView(getRowData(quickView)); return; }
            const status = event.target.closest('.js-change-status');
            if (status) openStatus(getRowData(status));
        });
        document.getElementById('btnSaveCarrier')?.addEventListener('click', submitCreate);
        document.getElementById('btnConfirmStatus')?.addEventListener('click', submitStatus);
        document.getElementById('offcanvasCreateEdit')?.addEventListener('show.bs.offcanvas', () => {
            const $modes = $('#supportedModes');
            if (!$modes.hasClass('select2-hidden-accessible')) {
                $modes.select2({ dropdownParent: $('#offcanvasCreateEdit'), width: '100%', closeOnSelect: false });
            }
        });
    };

    const createToolbarButtons = () => {
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-label': L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false' },
                action: toggleInlineFilter
            },
            saveFilterBtn: {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView)}</span>`,
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'aria-label': L.SaveView },
                action: async (_event, api) => {
                    try {
                        await saveDefaultView(getCurrentView(api));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved, 'success');
                    } catch (error) {
                        if (!error?.authHandled) window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };
        const features = window.DtDefaults.exportButtons(
            permissions.canCreate ? L.AddNewCarrier : '',
            permissions.canCreate ? { title: L.AddNewCarrier, 'aria-label': L.AddNewCarrier } : {},
            extraButtons,
            { exportColumns: [1, 2, 3, 4], colvisColumns: [1, 2, 3, 4] });
        // The shared helper also returns a generic export collection; this bounded Carrier slice has no export operation.
        return features.filter((feature) => !feature.buttons?.some((button) =>
            String(button.className || '').includes('dt-export-collection-btn')));
    };

    const initDataTable = async () => {
        if (!tableElement) return;
        syncL10n();
        readPermissions();
        // The company scope is resolved BEFORE the table asks for rows: every list call needs it, so a table
        // drawn first would fire one guaranteed 400.
        if (!await loadLegalEntities()) return;
        await loadDefaultView();
        dt = new DataTable(tableElement, window.DtDefaults.create({
            // R-4a (Returns draft UI-PM-03): DataTables calls .abort() on whatever ajax returns; an async function returns a
            // Promise, so every ajax.reload() threw after a committed create and the page reported "outcome unavailable".
            ajax: (data, callback) => { void loadRows(data, callback); },
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            columns: [
                { data: null, name: 'control' },
                { data: 'carrierCode', name: 'carrierCode' },
                { data: 'displayName', name: 'displayName' },
                { data: 'supportedModes', name: 'supportedModes' },
                { data: 'status', name: 'status' },
                { data: 'carrierId', name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 3, render: (data) => escapeHtml(getModeText(data)) },
                { targets: 4, render: (data, type) => type === 'display' ? getStatusBadge(data) : data },
                {
                    targets: -1, searchable: false, orderable: false, className: 'cell-fit all text-end',
                    render: (_data, _type, row) => {
                        const quick = `<button type="button" class="btn btn-sm btn-icon btn-text-secondary js-quick-view" title="${escapeHtml(L.QuickView)}" aria-label="${escapeHtml(L.QuickView)}"><i class="bx bx-show"></i></button>`;
                        const status = permissions.canChangeStatus && row.status !== 'Retired'
                            ? `<button type="button" class="btn btn-sm btn-icon btn-text-primary js-change-status" title="${escapeHtml(L.ChangeStatus)}" aria-label="${escapeHtml(L.ChangeStatus)}"><i class="bx bx-transfer-alt"></i></button>` : '';
                        return `<div class="d-flex justify-content-end gap-1">${quick}${status}</div>`;
                    }
                }
            ],
            buttons: createToolbarButtons(),
            language: { emptyTable: L.EmptyList, zeroRecords: L.EmptyList },
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                setupFilters();
                bindFilterEvents();
                if (defaultViewState) applySavedTableState(api, defaultViewState);
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    showCreate();
                });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), appliedFilters.status ? 1 : 0);
            }
        }));
        dt.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        bindPageEvents();
    };

    return { init: initDataTable };
})();

document.addEventListener('DOMContentLoaded', () => CarrierList.init());
