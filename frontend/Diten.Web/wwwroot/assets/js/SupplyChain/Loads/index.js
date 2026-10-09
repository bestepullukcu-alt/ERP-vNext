/**
 * MOD-0185 Routing & Load Planning — bounded list/create tenant surface (pack §30, scope mvp6-loads-ui-scope-01). Built by
 * R-4b against MODULE-RECIPE.md. Browser traffic is same-origin; the MVC adapter is the only Gateway caller. No transition
 * UI in this slice (ROOT-UI-01), no detail, edit, delete, bulk, lookup or optimisation.
 */
'use strict';

const LoadList = (function () {
    const endpoint = '/SupplyChain/Loads/api';
    const tableElement = document.querySelector('.datatables-loads');
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'SupplyChain', pageKey: 'Loads' };
    const baseOrder = [[1, 'asc']];
    const totalColumnCount = 5;
    const saveViewColumnIndexes = [1, 2, 3, 4];
    const filterCollapseId = 'inlineFilterCollapse';
    const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    let dt;
    let L = window.L10n || {};
    let permissions = { canCreate: false };
    let appliedFilters = { status: '', carrierId: '' };
    // R-2 (SHIPMENT-BUNDLE 3.2.0): a REQUIRED scope on every call, not an optional filter — the service takes
    // exactly one X-Legal-Entity-Id, so there is no 'all companies' answer to ask for.
    let legalEntities = [];
    let legalEntityScope = '';
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    let createIntent = null;

    const syncL10n = () => { L = window.L10n || {}; };
    const createUuid = () => typeof crypto?.randomUUID === 'function'
        ? crypto.randomUUID()
        : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (char) => {
            const value = crypto.getRandomValues(new Uint8Array(1))[0] & 15;
            return (char === 'x' ? value : (value & 3) | 8).toString(16);
        });
    // Reads carry a fresh trace per request.
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': createUuid() });
    const getAntiForgeryToken = (form) => form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const escapeHtml = (value) => String(value ?? '')
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');

    const readPermissions = () => {
        const payload = document.getElementById('load-permissions');
        if (!payload) return;
        try { permissions = Object.assign(permissions, JSON.parse(payload.textContent || '{}')); }
        catch (error) { console.error('[Loads] Permission payload could not be parsed.', error); }
    };

    // Labels are localized; the wire value stays the contract's English name (MODULE-RECIPE 5.2).
    const statusLabels = () => ({
        Draft: L.StatusDraft, Planned: L.StatusPlanned, Tendered: L.StatusTendered, Accepted: L.StatusAccepted,
        Dispatched: L.StatusDispatched, Completed: L.StatusCompleted, Cancelled: L.StatusCancelled
    });
    const getStatusBadge = (status) => {
        const css = status === 'Completed' ? 'bg-label-success' : status === 'Cancelled' ? 'bg-label-secondary'
            : status === 'Draft' ? 'bg-label-info' : 'bg-label-primary';
        return `<span class="badge ${css}">${escapeHtml(statusLabels()[status] || L.NotProvided)}</span>`;
    };
    // Opaque references are shown as given, LTR-isolated; no enrichment is invented (scope: no Carrier/Shipment names).
    const ltr = (value) => `<bdi dir="ltr">${escapeHtml(value)}</bdi>`;
    const notProvided = () => `<span class="text-muted">${escapeHtml(L.NotProvided)}</span>`;

    // MODULE-RECIPE 3.4 (Q374, R-2): the datetime-local field holds the user's LOCAL wall clock; the wire is UTC. A wall-clock
    // time that does not exist (out-of-range parts, a skipped daylight-saving hour) is rejected, not shifted. Same functions
    // as Shipments details.js and Returns index.js, kept module-local: no shared date helper exists yet.
    const pad = (n) => String(n).padStart(2, '0');
    const localInputValue = (when) => `${when.getFullYear()}-${pad(when.getMonth() + 1)}-${pad(when.getDate())}T${pad(when.getHours())}:${pad(when.getMinutes())}`;
    const zoneLabel = (when) => { const offset = -when.getTimezoneOffset(); const sign = offset < 0 ? '-' : '+';
        return `${Intl.DateTimeFormat().resolvedOptions().timeZone || ''} (UTC${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)})`.trim(); };
    const parseLocalInput = (raw) => {
        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(raw);
        if (!match) return null;
        const [, year, month, day, hour, minute, second] = match.map(Number);
        const value = new Date(year, month - 1, day, hour, minute, second || 0);
        const exact = value.getFullYear() === year && value.getMonth() === month - 1 && value.getDate() === day
            && value.getHours() === hour && value.getMinutes() === minute;
        return exact ? value : null;
    };

    // ─── Saved view (shared personalizationClient; no browser storage) ────────
    const normalizeSavedString = (value) => typeof value === 'string' ? value.trim() : '';
    const getSavedViewId = (view) => view?.id || view?.Id || view?._id || '';
    const getSavedViewName = (view) => normalizeSavedString(view?.viewName || view?.ViewName);
    const isSavedViewDefault = (view) => view?.isDefault === true || view?.IsDefault === true;
    const getSavedViewDefinition = (view) => {
        const raw = view?.viewDefinition ?? view?.ViewDefinition ?? {};
        if (raw && typeof raw === 'object') return raw;
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (error) { return {}; } }
        return {};
    };
    const defaultColVis = () => saveViewColumnIndexes.reduce((result, index) => { result[index] = true; return result; }, {});
    const normalizeColVis = (value) => {
        if (!value || typeof value !== 'object') return null;
        const result = {};
        saveViewColumnIndexes.forEach((index) => { if (typeof value[index] === 'boolean') result[index] = value[index]; });
        return Object.keys(result).length ? result : null;
    };
    const normalizeColumnOrder = (value) => {
        if (!Array.isArray(value) || value.length !== totalColumnCount) return null;
        const result = value.map(Number);
        return result.every((index) => Number.isInteger(index) && index >= 0 && index < totalColumnCount)
            && new Set(result).size === totalColumnCount ? result : null;
    };
    const captureColumnVisibility = (api) => saveViewColumnIndexes.reduce((result, index) => {
        result[index] = !!api.column(index).visible(); return result;
    }, {});
    const captureColumnOrder = (api) => { try { return normalizeColumnOrder(api?.colReorder?.order?.()); } catch (error) { return null; } };
    const getCurrentView = (api) => ({
        filters: { status: appliedFilters.status || '', carrierId: appliedFilters.carrierId || '' },
        search: api.search() || '', colVis: captureColumnVisibility(api), columnOrder: captureColumnOrder(api), order: api.order()
    });
    const normalizeView = (view) => ({
        filters: { status: normalizeSavedString(view?.filters?.status), carrierId: normalizeSavedString(view?.filters?.carrierId) },
        search: normalizeSavedString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColumnOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const serializeView = (view) => JSON.stringify(normalizeView(view));
    const getResetBaselineState = () => ({
        filters: { status: '', carrierId: '' }, search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index), order: baseOrder
    });
    const isDirtyComparedToDefault = (api) => serializeView(getCurrentView(api)) !== serializeView(defaultViewState || getResetBaselineState());
    const setSaveFilterVisible = (visible) => {
        const button = document.querySelector('.dt-save-filter-btn');
        if (!button) return;
        button.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const applySavedTableState = (api, state) => {
        const normalized = normalizeView(state);
        appliedFilters = { status: normalized.filters.status, carrierId: normalized.filters.carrierId };
        $('#filterStatus').val(appliedFilters.status).trigger('change');
        const carrierFilter = document.getElementById('filterCarrierId');
        if (carrierFilter) carrierFilter.value = appliedFilters.carrierId;
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
            const result = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const views = Array.isArray(result) ? result : (result?.data || result?.Data || []);
            defaultViewRecord = views.find(isSavedViewDefault) || views[0] || null;
            defaultViewState = defaultViewRecord ? normalizeView(getSavedViewDefinition(defaultViewRecord)) : null;
            if (defaultViewState) appliedFilters = { ...defaultViewState.filters };
        } catch (error) {
            if (!error?.authHandled) console.error('[Loads] Saved view load failed.', error);
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return;
        const normalized = normalizeView(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey, pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalized, isDefault: true, visibility: 'private'
        };
        const id = getSavedViewId(defaultViewRecord);
        const result = id ? await personalizationClient.updateView(id, payload) : await personalizationClient.saveView(payload);
        defaultViewRecord = result?.data || result?.Data || result || payload;
        defaultViewState = normalized;
    };

    // ─── Inline filter: status (single; ShowAll omits it) and carrierId (UUID text, not a lookup) ───
    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const toolbar = document.querySelector('.dt-filter-btn')?.closest('.dt-layout-row');
        if (host && toolbar) { toolbar.insertAdjacentElement('afterend', host); host.classList.add('px-3'); }
    };
    const setupFilters = () => {
        const $status = $('#filterStatus');
        if ($status.hasClass('select2-hidden-accessible')) return;
        $status.select2({ dropdownParent: $(document.body), dropdownCssClass: 'dt-inline-filter-dropdown',
            minimumResultsForSearch: Infinity, selectionCssClass: 'form-select form-select-sm', width: 'element', allowClear: true });
        $status.val(appliedFilters.status).trigger('change');
    };
    const toggleInlineFilter = () => {
        const element = document.getElementById(filterCollapseId);
        if (element) bootstrap.Collapse.getOrCreateInstance(element, { toggle: false }).toggle();
    };
    const bindFilterEvents = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            const carrierId = (document.getElementById('filterCarrierId')?.value || '').trim();
            const invalid = carrierId !== '' && !uuidPattern.test(carrierId);
            document.getElementById('filterCarrierId')?.classList.toggle('is-invalid', invalid);
            if (invalid) return;
            appliedFilters = { status: String($('#filterStatus').val() || ''), carrierId };
            legalEntityScope = String($('#filterLegalEntity').val() || legalEntityScope);
            dt.ajax.reload(() => setSaveFilterVisible(isDirtyComparedToDefault(dt)), true);
            bootstrap.Collapse.getOrCreateInstance(document.getElementById(filterCollapseId), { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            document.getElementById('filterCarrierId')?.classList.remove('is-invalid');
            applySavedTableState(dt, getResetBaselineState());
            dt.ajax.reload(() => setSaveFilterVisible(isDirtyComparedToDefault(dt)), true);
        });
    };
    const buildListUrl = () => {
        const query = new URLSearchParams();
        if (legalEntityScope) query.set('legalEntityId', legalEntityScope);
        if (appliedFilters.status) query.set('status', appliedFilters.status);
        if (appliedFilters.carrierId) query.set('carrierId', appliedFilters.carrierId);
        const text = query.toString();
        return text ? `${endpoint}?${text}` : endpoint;
    };
    const withScope = (url, scope) => {
        const value = scope || legalEntityScope;
        if (!value) return url;
        return `${url}${url.includes('?') ? '&' : '?'}legalEntityId=${encodeURIComponent(value)}`;
    };
    // The create panel's own select scopes a create, not the list scope, or the panel's required changeable
    // field would be decorative.
    const formScope = () => String(document.getElementById('formLegalEntity')?.value || legalEntityScope || '');
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
            if (!response.ok) { window.DtDefaults?.showError?.(L.LegalEntityUnavailable); return false; }
            const payload = await readJson(response);
            const rows = Array.isArray(payload) ? payload : (payload?.data ?? payload?.items ?? []);
            legalEntities = rows.map((row) => ({
                id: String(row.legalEntityId ?? row.LegalEntityId ?? ''),
                name: String(row.displayName ?? row.DisplayName ?? row.legalName ?? row.LegalName ?? row.code ?? '')
            })).filter((row) => row.id);
            // An empty answer is not an outage: the tenant genuinely has none, and the page says so rather
            // than offering an empty required field.
            if (legalEntities.length === 0) { window.DtDefaults?.showError?.(L.LegalEntityNone); return false; }
            legalEntityScope = legalEntities[0].id;
            fillLegalEntitySelect(document.getElementById('filterLegalEntity'), legalEntityScope);
            fillLegalEntitySelect(document.getElementById('formLegalEntity'), legalEntityScope);
            return true;
        } catch (error) {
            window.DtDefaults?.showError?.(L.LegalEntityUnavailable);
            return false;
        }
    };

    // ─── Failures ─────────────────────────────────────────────────────────────
    const handleUnauthorized = () => window.DtDefaults?.handleUnauthorized?.();
    const readJson = async (response) => { try { return await response.json(); } catch (error) { return null; } };
    const referenceOf = (payload, response) => payload?.error?.correlationId || response?.headers?.get('X-Correlation-Id') || '';
    const withReference = (message, reference) => `${message}${reference ? ` ${L.SupportReference}: ${reference}` : ''}`;
    // Every published createLoadPlan code has its own text. An unknown code is never shown as a validation problem unless the
    // status says so (MODULE-RECIPE 3.7).
    const messageForError = (code, status) => ({
        CARRIER_NOT_FOUND: L.ErrCarrierNotFound, CARRIER_MODE_UNSUPPORTED: L.ErrCarrierModeUnsupported,
        SHIPMENT_NOT_FOUND: L.ErrShipmentNotFound, SHIPMENT_NOT_ELIGIBLE: L.ErrShipmentNotEligible,
        SHIPMENT_CARRIER_MISMATCH: L.ErrShipmentCarrierMismatch, SHIPMENT_ALREADY_ASSIGNED: L.ErrShipmentAlreadyAssigned,
        DUPLICATE_SHIPMENT: L.ErrDuplicateShipment, INVALID_LOAD_STOPS: L.ErrInvalidLoadStops,
        IDEMPOTENCY_KEY_REUSED: L.ErrIdempotencyKeyReused, CORRELATION_ROOT_MISMATCH: L.ErrCorrelationRootMismatch,
        DEPENDENCY_UNAVAILABLE: L.ErrDependencyUnavailable, DEPENDENCY_RESPONSE_INVALID: L.ErrDependencyResponseInvalid,
        REFERENCE_STATE_UNAVAILABLE: L.ErrReferenceStateUnavailable, PERSISTENCE_UNAVAILABLE: L.ErrPersistenceUnavailable,
        INTERNAL_ERROR: L.ErrInternalError
    })[code] || (status === 403 ? L.AccessDenied : status === 400 || status === 415 ? L.ValidationError : L.ErrInternalError);

    // ─── List states (MODULE-RECIPE 3.6, 3.11, 4.1) ─────────────────────────────
    // The skeleton is owned by this page (the shared .backbone-skeleton is display:none and DtDefaults fades #skeleton-loader
    // on its own); a failed load is an alert in place of the table with read wording; a 403 is the denied card.
    const tableCard = () => document.getElementById('dt-loads')?.closest('.card-datatable');
    const setSkeleton = (visible) => {
        const skeleton = document.getElementById('loadsSkeleton');
        if (skeleton) skeleton.style.display = visible ? 'block' : 'none';
        tableCard()?.classList.toggle('d-none', visible);
    };
    const showDenied = () => {
        const surface = document.getElementById('loadsListSurface'); const denied = document.getElementById('loadsListDenied');
        if (!surface || !denied) return;
        surface.hidden = true; surface.setAttribute('inert', ''); denied.hidden = false;
    };
    const showLoadFailure = (reference) => {
        const alert = document.getElementById('loadsLoadError'); if (!alert) return;
        alert.textContent = withReference(L.ListUnavailable, reference);
        alert.classList.remove('d-none'); tableCard()?.classList.add('d-none');
    };
    const clearLoadFailure = () => {
        document.getElementById('loadsLoadError')?.classList.add('d-none'); tableCard()?.classList.remove('d-none');
    };
    let firstLoad = true;
    const loadRows = async (_request, callback) => {
        if (firstLoad) setSkeleton(true);
        try {
            const response = await fetch(buildListUrl(), { credentials: 'same-origin', headers: getAuthHeaders() });
            if (!response.ok) {
                if (response.status === 401) { handleUnauthorized(); callback({ data: [] }); return; }
                if (response.status === 403) { showDenied(); callback({ data: [] }); return; }
                showLoadFailure(referenceOf(await readJson(response), response));
                callback({ data: [] });
                return;
            }
            const payload = await response.json();
            // A malformed envelope is an error, never an empty list (scope: items/total/v1 checked).
            if (!Array.isArray(payload?.items) || typeof payload?.total !== 'number' || payload?.contractVersion !== 'v1') {
                showLoadFailure(response.headers.get('X-Correlation-Id'));
                callback({ data: [] });
                return;
            }
            clearLoadFailure();
            callback({ data: payload.items });
        } catch (error) {
            console.error('[Loads] List request failed.', error);
            showLoadFailure('');
            callback({ data: [] });
        } finally {
            if (firstLoad) {
                firstLoad = false; setSkeleton(false);
                if (!document.getElementById('loadsLoadError')?.classList.contains('d-none')) tableCard()?.classList.add('d-none');
            }
        }
    };

    // ─── Create (one intent per opened form: MODULE-RECIPE 3.1/3.2, MOD-0185 §30, Q403) ───
    // The key AND the correlation are minted when the form opens and kept across edits, failures and retries until it closes:
    // the create's X-Correlation-Id is the new Load's root, and a replay under a different root answers 409
    // CORRELATION_ROOT_MISMATCH (LoadRepository.cs:16). After 409 IDEMPOTENCY_KEY_REUSED the intent is spent: the form stops
    // and never mints a key to get past it.
    const newIntent = () => ({ key: createUuid(), correlation: createUuid(), blocked: false });
    const mutationHeaders = (form, intent) => ({
        'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': intent.correlation, 'Content-Type': 'application/json',
        'RequestVerificationToken': getAntiForgeryToken(form), 'Idempotency-Key': intent.key
    });
    const setBusy = (button, busy) => { if (!button) return; button.disabled = busy; button.setAttribute('aria-busy', String(busy)); };
    const showFormError = (message) => {
        const alert = document.getElementById('formLoadAlert');
        alert.textContent = message; alert.classList.remove('d-none');
    };
    const hideFormError = () => document.getElementById('formLoadAlert')?.classList.add('d-none');

    const shipmentList = () => document.getElementById('loadShipmentList');
    const stopList = () => document.getElementById('loadStopList');
    const addShipmentRow = (value = '') => {
        const template = document.getElementById('loadShipmentTemplate');
        const row = template.content.firstElementChild.cloneNode(true);
        const input = row.querySelector('input');
        const index = shipmentList().children.length + 1;
        input.id = `loadShipmentId_${createUuid()}`;
        input.value = value;
        input.setAttribute('aria-label', `${L.ShipmentId} ${index}`);
        row.querySelector('.js-remove-shipment').setAttribute('aria-label', `${L.RemoveShipment} ${index}`);
        shipmentList().appendChild(row);
    };
    const addStopRow = (sequence, action) => {
        const template = document.getElementById('loadStopTemplate');
        const row = template.content.firstElementChild.cloneNode(true);
        const index = stopList().children.length + 1;
        row.querySelectorAll('input, select').forEach((field) => {
            field.id = `${field.dataset.field}_${createUuid()}`;
            field.setAttribute('aria-label', `${L[field.dataset.label]} ${index}`);
        });
        row.querySelector('[data-field="sequence"]').value = String(sequence ?? index);
        row.querySelector('[data-field="action"]').value = action || 'Pickup';
        row.querySelector('.js-remove-stop').setAttribute('aria-label', `${L.RemoveStop} ${index}`);
        stopList().appendChild(row);
    };
    const showCreate = () => {
        if (!permissions.canCreate) return;
        const form = document.getElementById('formLoad');
        form.reset();
        createIntent = newIntent();
        hideFormError();
        form.querySelectorAll('.is-invalid').forEach((field) => field.classList.remove('is-invalid'));
        shipmentList().replaceChildren(); stopList().replaceChildren();
        addShipmentRow();
        addStopRow(1, 'Pickup'); addStopRow(2, 'Delivery');
        const now = new Date();
        document.getElementById('plannedDepartAt').value = localInputValue(now);
        const zone = document.getElementById('plannedDepartAtZone');
        if (zone) zone.textContent = zoneLabel(now);
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };
    // CreateLoadCommand, exactly: carrierId, shipmentIds (order kept), mode, plannedDepartAt (UTC), stops (sequence as a
    // positive integer, locationReferenceId as typed — empty allowed, action). No trim, maxlength or reordering is invented.
    const buildPayload = () => {
        const departAt = parseLocalInput(document.getElementById('plannedDepartAt').value);
        return {
            departAtValid: departAt !== null,
            body: {
                carrierId: document.getElementById('carrierId').value,
                shipmentIds: [...shipmentList().querySelectorAll('input')].map((input) => input.value),
                mode: document.getElementById('mode').value,
                plannedDepartAt: departAt ? departAt.toISOString() : '',
                stops: [...stopList().children].map((row) => ({
                    sequence: Number(row.querySelector('[data-field="sequence"]').value),
                    locationReferenceId: row.querySelector('[data-field="locationReferenceId"]').value,
                    action: row.querySelector('[data-field="action"]').value
                }))
            }
        };
    };
    // Client presence checks only; the backend stays authoritative for every business rule (422 codes).
    const validate = (payload) => {
        const form = document.getElementById('formLoad');
        form.querySelectorAll('.is-invalid').forEach((field) => field.classList.remove('is-invalid'));
        const bad = [];
        if (!uuidPattern.test(payload.body.carrierId)) bad.push(document.getElementById('carrierId'));
        shipmentList().querySelectorAll('input').forEach((input) => { if (!uuidPattern.test(input.value)) bad.push(input); });
        if (!payload.departAtValid) bad.push(document.getElementById('plannedDepartAt'));
        stopList().querySelectorAll('[data-field="sequence"]').forEach((input) => {
            if (!/^[1-9][0-9]*$/.test(input.value)) bad.push(input);
        });
        bad.forEach((field) => { field.classList.add('is-invalid'); field.setAttribute('aria-invalid', 'true'); });
        if (payload.body.shipmentIds.length < 1 || payload.body.stops.length < 2) return L.ValidationError;
        if (!payload.departAtValid) return L.PlannedDepartAtInvalid;
        return bad.length ? L.ValidationError : '';
    };
    const submitCreate = async () => {
        const form = document.getElementById('formLoad');
        const button = document.getElementById('btnSaveLoad');
        if (!createIntent) createIntent = newIntent();
        if (createIntent.blocked) { showFormError(L.ErrIdempotencyKeyReused); return; }
        const payload = buildPayload();
        const problem = validate(payload);
        if (problem) { showFormError(problem); form.querySelector('.is-invalid')?.focus({ preventScroll: true }); return; }
        hideFormError();
        const intent = createIntent;
        setBusy(button, true);
        try {
            const createdScope = formScope();
            if (!createdScope) { showFormError(L.LegalEntityRequired); return; }
            const response = await fetch(withScope(endpoint, createdScope), {
                method: 'POST', credentials: 'same-origin', headers: mutationHeaders(form, intent), body: JSON.stringify(payload.body)
            });
            if (!response.ok) {
                if (response.status === 401) { handleUnauthorized(); return; }
                const failure = await readJson(response);
                const code = failure?.error?.code || '';
                if (code === 'IDEMPOTENCY_KEY_REUSED' || code === 'CORRELATION_ROOT_MISMATCH') intent.blocked = true;
                showFormError(withReference(messageForError(code, response.status), referenceOf(failure, response)));
                return;
            }
            const result = await readJson(response);
            window.showToast?.(result?.idempotentReplay ? L.CreateReplaySuccess : L.CreateSuccess, 'success');
            createIntent = null;
            bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasCreateEdit'))?.hide();
            dt.ajax.reload(null, false);
        } catch (error) {
            // Unknown outcome: the same key, correlation and body are resent on the explicit retry.
            console.error('[Loads] Create request outcome is unknown.', error);
            showFormError(`${L.ErrPersistenceUnavailable} ${L.RetrySameRequest}`);
        } finally {
            setBusy(button, false);
        }
    };

    const bindPageEvents = () => {
        document.getElementById('btnSaveLoad')?.addEventListener('click', submitCreate);
        document.getElementById('btnAddShipment')?.addEventListener('click', () => addShipmentRow());
        document.getElementById('btnAddStop')?.addEventListener('click', () => addStopRow(stopList().children.length + 1, 'Delivery'));
        document.getElementById('offcanvasCreateEdit')?.addEventListener('click', (event) => {
            const removeShipment = event.target.closest('.js-remove-shipment');
            if (removeShipment && shipmentList().children.length > 1) removeShipment.closest('.js-shipment-row').remove();
            const removeStop = event.target.closest('.js-remove-stop');
            if (removeStop && stopList().children.length > 2) removeStop.closest('.js-stop-row').remove();
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
                    try { await saveDefaultView(getCurrentView(api)); setSaveFilterVisible(false); window.showToast?.(L.RecordSaved, 'success'); }
                    catch (error) { if (!error?.authHandled) window.showToast?.(L.ErrorOccurred, 'error'); }
                }
            }
        };
        const features = window.DtDefaults.exportButtons(
            permissions.canCreate ? L.AddNewLoad : '',
            permissions.canCreate ? { title: L.AddNewLoad, 'aria-label': L.AddNewLoad } : {},
            extraButtons,
            { exportColumns: [1, 2, 3, 4], colvisColumns: [1, 2, 3, 4] });
        // This bounded slice has no export operation.
        return features.filter((feature) => !feature.buttons?.some((button) =>
            String(button.className || '').includes('dt-export-collection-btn')));
    };

    const initDataTable = async () => {
        if (!tableElement) return;
        syncL10n();
        readPermissions();
        // Resolved BEFORE the table asks for rows: every list call needs it, so a table drawn first would
        // fire one guaranteed 400.
        if (!await loadLegalEntities()) return;
        await loadDefaultView();
        dt = new DataTable(tableElement, window.DtDefaults.create({
            // DataTables calls .abort() on whatever ajax returns; an async function returns a Promise, so every ajax.reload()
            // would throw after a committed create (R-4a, measured; Returns draft UI-PM-03).
            ajax: (data, callback) => { void loadRows(data, callback); },
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(0)' },
            columns: [
                { data: null, name: 'control' },
                { data: 'loadNumber', name: 'loadNumber' },
                { data: 'carrierId', name: 'carrierId' },
                { data: 'shipmentIds', name: 'shipmentIds' },
                { data: 'status', name: 'status' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => typeof data === 'string' ? `<span class="fw-medium text-heading">${ltr(data)}</span>` : notProvided() },
                { targets: 2, render: (data) => typeof data === 'string' ? ltr(data) : notProvided() },
                {
                    targets: 3, render: (data, type) => {
                        if (!Array.isArray(data)) return type === 'display' ? notProvided() : '';
                        return type === 'display' ? data.map(ltr).join('<br>') : data.join(' ');
                    }
                },
                { targets: 4, render: (data, type) => type === 'display' ? getStatusBadge(data) : (data ?? '') }
            ],
            buttons: createToolbarButtons(),
            language: { emptyTable: L.EmptyList, zeroRecords: L.EmptyList },
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                setupFilters();
                bindFilterEvents();
                if (defaultViewState) applySavedTableState(api, defaultViewState);
                document.querySelector('.add-new')?.addEventListener('click', (event) => { event.preventDefault(); showCreate(); });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), (appliedFilters.status ? 1 : 0) + (appliedFilters.carrierId ? 1 : 0));
            }
        }));
        dt.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
        bindPageEvents();
    };

    return { init: initDataTable, parseLocalInput, localInputValue };
})();

document.addEventListener('DOMContentLoaded', () => {
    window.LoadList = LoadList;
    if (document.querySelector('.datatables-loads')) LoadList.init();
});
