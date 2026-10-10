/**
 * MOD-0290-FU04 S5 — Items ("Kalemler"), the compact item master screen: the list (Index), the form (Create / Edit)
 * and the record (Details). Browser traffic goes only to the same-origin MVC proxy (ItemsController): no token, no
 * tenant and no cookie is handled here — the proxy derives them from the signed-in principal.
 *
 * THE LIST IS THE COMPONENT (BL-440, server mode): DitenDataTable.createList owns the table, the inline filter, Save
 * View, the toolbar and the server wire (start/length/search/materialType/status → the proxy → MDM's page). This file
 * writes only what is the item screen's own: renderers, the server-owned row actions, the lifecycle requests, the form
 * and the record.
 *
 * ACTIONS ARE THE SERVER'S: a row or a record shows exactly the actions MDM returned for it (availableActions, in
 * MDM's order) — an action this screen does not know is never drawn, and a lifecycle request re-reads the item first.
 */
'use strict';

const ItemsPage = (function () {
    let list = null;
    let dt;
    const endpoint = '/MasterDataManagement/Items/api';
    const pagesBase = '/MasterDataManagement/Items';
    const tableEl = document.querySelector('.datatables-items');
    const L = window.L10n || {};
    const permissionHost = document.querySelector('[data-can-create]');
    const canCreate = permissionHost?.getAttribute('data-can-create') === 'true';
    const lifecycleRequests = new Set();
    const columnIndexes = [1, 2, 3, 4, 5, 6];

    // MDM's action vocabulary, in MDM's order (ItemActions.For). WITHDRAW_APPROVAL arrives with S2.
    const actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'DEACTIVATE', 'REACTIVATE', 'RETIRE'];
    // The lifecycle requests: the proxy path, whether a reason is asked, and the reader's words.
    const lifecycleActions = {
        SUBMIT: { suffix: 'submit', reason: false, label: () => L.Submit, confirm: () => L.SubmitConfirmation, success: () => L.SubmitSuccess },
        DEACTIVATE: { suffix: 'deactivate', reason: true, label: () => L.Deactivate, confirm: () => L.DeactivateConfirmation, success: () => L.DeactivateSuccess },
        REACTIVATE: { suffix: 'reactivation-requests', reason: true, label: () => L.Reactivate, confirm: () => L.ReactivateConfirmation, success: () => L.ReactivateSuccess },
        RETIRE: { suffix: 'retire', reason: true, label: () => L.Retire, confirm: () => L.RetireConfirmation, success: () => L.RetireSuccess }
    };
    const maxReasonLength = 500;

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[character]));
    const valueOf = (source, camel) => source?.[camel] ?? source?.[camel.charAt(0).toUpperCase() + camel.slice(1)];
    const unwrapData = (payload) => payload?.data ?? payload?.Data ?? null;
    const readVersion = (source) => {
        const value = valueOf(source, 'version');
        return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0 ? value : NaN;
    };
    const antiForgeryToken = (selector) => document.querySelector(`${selector} input[name="__RequestVerificationToken"]`)?.value || '';

    // ── Labels by stable code (O-22) ─────────────────────────────────────────────────────────────────────────────
    const lifecycleStates = {
        1: { key: 'LifecycleDraft', class: 'bg-label-secondary' },
        2: { key: 'LifecyclePendingActivation', class: 'bg-label-warning' },
        3: { key: 'LifecycleActive', class: 'bg-label-success' },
        4: { key: 'LifecycleDeactivated', class: 'bg-label-dark' },
        5: { key: 'LifecycleRetired', class: 'bg-label-danger' }
    };
    const statusActive = 3;
    const renderLifecycle = (value) => {
        const state = lifecycleStates[value];
        return `<span class="badge ${state ? state.class : 'bg-label-secondary'}">${escapeHtml(state ? L[state.key] : L.Unknown)}</span>`;
    };
    const materialTypeLabel = (code) => (code && L[`MaterialType_${code}`]) || code || '';
    const yesNo = (value) => value === true ? L.Yes : value === false ? L.No : '';

    // The server's actions for this item: known ones only, once each, in MDM's order. Unknown ones are never drawn.
    const readAvailableActions = (item) => {
        const raw = valueOf(item, 'availableActions');
        if (!Array.isArray(raw)) return [];
        const unknown = raw.filter((action) => !actionOrder.includes(action));
        if (unknown.length) console.warn('[Items] Actions this screen does not know were not drawn.', unknown);
        return actionOrder.filter((action) => raw.includes(action));
    };

    // ── Answers ─────────────────────────────────────────────────────────────────────────────────────────────────
    // The proxy says every refusal as a sentence; a code it could not map is flagged (unmapped) and said generally.
    const getErrorMessage = async (response) => {
        if (response.status === 401) return L.ErrorUnauthorized;
        try {
            const payload = await response.json();
            if (payload?.unmapped === true) console.warn('[Items] The service answered a refusal without a sentence.', response.status);
            const errors = Array.isArray(payload?.errors) ? payload.errors.filter((text) => typeof text === 'string' && text) : [];
            if (errors.length) return errors.join(' ');
        } catch (error) {
            // Not a readable answer: the status sentence below.
        }
        return response.status === 403 ? L.ErrorForbidden
            : response.status === 404 ? L.ErrorNotFound
            : response.status === 409 ? L.ErrorConflict
            : response.status === 503 ? L.ErrorProviderUnavailable
            : L.ErrorGateway;
    };

    const fetchItem = async (id) => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin', headers: getAuthHeaders()
        });
        if (!response.ok) throw new Error(await getErrorMessage(response));
        const item = unwrapData(await response.json());
        if (!item || String(valueOf(item, 'id')).toLowerCase() !== String(id).toLowerCase()) throw new Error(L.ErrorGateway);
        return item;
    };

    // ── Lifecycle (list rows and the record alike) ──────────────────────────────────────────────────────────────
    // Confirm (with a reason where MDM asks one) → read the item again (the action must still be offered) → POST with
    // its version → read it back (the answer and the record agree) → done(). A second click while one runs is ignored.
    const requestLifecycle = (id, action, done) => {
        const spec = lifecycleActions[action];
        if (!id || !spec) return;
        window.showConfirm?.(spec.confirm(), async (input) => {
            const reason = String(input ?? '').trim();
            if (spec.reason && (!reason || reason.length > maxReasonLength || /[\u0000-\u001F\u007F-\u009F]/u.test(reason))) {
                window.showToast?.(L.ReasonRequired, 'error');
                return;
            }
            const requestKey = `${id}:${action}`;
            if (lifecycleRequests.has(requestKey)) return;
            lifecycleRequests.add(requestKey);
            try {
                const current = await fetchItem(id);
                if (!readAvailableActions(current).includes(action)) throw new Error(L.LifecycleStateChanged);
                const version = readVersion(current);
                if (!Number.isInteger(version)) throw new Error(L.ErrorGateway);
                const token = antiForgeryToken('#itemLifecycleToken');
                const body = new FormData();
                body.set('ExpectedVersion', String(version));
                if (spec.reason) body.set('Reason', reason);
                body.set('__RequestVerificationToken', token);
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${spec.suffix}`, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }, body
                });
                if (!response.ok) throw new Error(await getErrorMessage(response));
                const payload = await response.json();
                const result = unwrapData(payload);
                if ((payload?.isSuccessful ?? payload?.IsSuccessful) !== true || !result
                    || String(valueOf(result, 'id')).toLowerCase() !== String(id).toLowerCase()) {
                    throw new Error(L.ErrorGateway);
                }
                const refreshed = await fetchItem(id);
                if (readVersion(refreshed) !== readVersion(result)
                    || valueOf(refreshed, 'lifecycleStatus') !== valueOf(result, 'lifecycleStatus')) {
                    throw new Error(L.LifecycleStateChanged);
                }
                window.showToast?.(spec.success(), 'success');
            } catch (error) {
                window.showToast?.(error?.message || L.ErrorGateway, 'error');
            } finally {
                lifecycleRequests.delete(requestKey);
                done?.();
            }
        }, spec.reason
            ? {
                type: 'warning', showInput: true, inputType: 'text', inputRequired: true, inputLabel: L.ReasonLabel,
                inputAttributes: { maxlength: maxReasonLength }, confirmButtonText: spec.label()
            }
            : { type: 'warning', confirmButtonText: spec.label() });
    };

    // ── List ────────────────────────────────────────────────────────────────────────────────────────────────────
    const rowActionPresentation = {
        DETAILS: { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', text: () => L.ViewDetails },
        EDIT: { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: () => L.Edit },
        SUBMIT: { key: 'submit', className: 'js-item-lifecycle', icon: 'bx bx-send', text: () => L.Submit },
        DEACTIVATE: { key: 'deactivate', className: 'js-item-lifecycle', icon: 'bx bx-block', text: () => L.Deactivate },
        REACTIVATE: { key: 'reactivate', className: 'js-item-lifecycle', icon: 'bx bx-revision', text: () => L.Reactivate },
        RETIRE: { key: 'retire', className: 'js-item-lifecycle text-danger', icon: 'bx bx-archive', text: () => L.Retire }
    };
    const renderRowActions = (row) => {
        const id = valueOf(row, 'id');
        return window.DitenDataTable.renderActions(readAvailableActions(row).map((action) => {
            const { key, className, icon, text } = rowActionPresentation[action];
            return { key, className, icon, text: text(), attrs: { 'data-id': id, 'data-action': action, 'title': text() } };
        }));
    };
    const reloadList = () => dt?.ajax.reload(null, false);
    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `${pagesBase}/Details/${encodeURIComponent(id)}`; },
        edit: ({ id }) => { if (id) window.location.href = `${pagesBase}/Edit/${encodeURIComponent(id)}`; },
        submit: ({ id }) => requestLifecycle(id, 'SUBMIT', reloadList),
        deactivate: ({ id }) => requestLifecycle(id, 'DEACTIVATE', reloadList),
        reactivate: ({ id }) => requestLifecycle(id, 'REACTIVATE', reloadList),
        retire: ({ id }) => requestLifecycle(id, 'RETIRE', reloadList)
    };

    // The createList options of the list — one place, so the wire and the saved-view keys are measurable.
    const buildListOptions = () => ({
        tableEl,
        dataMode: 'server',
        ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders() },
        // BL-452: CSV/Excel = every matching row (the proxy pages MDM), never the page the table holds.
        export: { mode: 'server', url: `${endpoint}/export`, fileName: 'items' },
        actions: { onRowAction: rowActionHandlers },
        toolbar: {
            addNewText: canCreate ? L.AddNew : '',
            addNewAttr: canCreate ? { href: `${pagesBase}/Create` } : undefined,
            onAddNew: () => { if (canCreate) window.location.href = `${pagesBase}/Create`; },
            exportColumns: columnIndexes,
            colvisColumns: columnIndexes
        },
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: 'inlineFilterCollapse',
            applyBtn: 'btnFilterApply',
            resetBtn: 'btnFilterReset',
            fields: [
                { id: 'filterMaterialType', key: 'materialType', kind: 'single' },
                { id: 'filterStatus', key: 'status', kind: 'single' }
            ]
        },
        savedView: {
            moduleKey: 'MasterDataManagement',
            pageKey: 'Items',
            saveViewColumnIndexes: columnIndexes,
            defaultVisibleColumnIndexes: columnIndexes,
            baseOrder: [[1, 'asc']]
        },
        config: {
            pageLength: 20,
            // MDM sorts by code only: no other column is offered as a sort.
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'name', name: 'name' },
                { data: 'materialType', name: 'materialType' },
                { data: 'baseUomCode', name: 'baseUomCode' },
                { data: 'stockable', name: 'stockable' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, orderable: false, render: escapeHtml },
                { targets: 3, orderable: false, render: (data) => escapeHtml(materialTypeLabel(data)) },
                { targets: 4, orderable: false, render: escapeHtml },
                { targets: 5, orderable: false, render: (data) => escapeHtml(yesNo(data)) },
                { targets: 6, orderable: false, render: (data, type) => type === 'display' ? renderLifecycle(data) : (L[lifecycleStates[data]?.key] || '') },
                {
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit all text-end pe-3',
                    render: (data, type, row) => renderRowActions(row)
                }
            ]
        }
    });

    const initList = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        list = await window.DitenDataTable.createList(buildListOptions());
        dt = list.dt;
    };

    // ── Form (Create / Edit) ────────────────────────────────────────────────────────────────────────────────────
    const form = document.getElementById('itemForm');
    const formState = { version: null, status: null };
    const field = (name) => form?.querySelector(`[name="${name}"]:not([type="hidden"])`);
    const showFormAlert = (text) => {
        const alert = document.getElementById('itemFormAlert');
        if (!alert) return;
        alert.textContent = text || '';
        alert.classList.toggle('d-none', !text);
    };
    const conversionRows = () => Array.from(document.querySelectorAll('#itemConversionRows .js-conversion-row'));
    const syncConversionEmpty = () => document.getElementById('itemConversionEmpty')
        ?.classList.toggle('d-none', conversionRows().length > 0);
    const addConversionRow = (conversion) => {
        const template = document.getElementById('itemConversionRowTemplate');
        const body = document.getElementById('itemConversionRows');
        if (!template || !body) return null;
        const row = template.content.firstElementChild.cloneNode(true);
        row.querySelector('.js-conversion-from').value = valueOf(conversion, 'fromUom') ?? '';
        row.querySelector('.js-conversion-to').value = valueOf(conversion, 'toUom') ?? '';
        row.querySelector('.js-conversion-numerator').value = valueOf(conversion, 'numerator') ?? '';
        row.querySelector('.js-conversion-denominator').value = valueOf(conversion, 'denominator') ?? '';
        body.appendChild(row);
        syncConversionEmpty();
        return row;
    };
    const readDays = (name) => {
        const text = String(field(name)?.value ?? '').trim();
        if (!text) return null;
        const value = Number(text);
        return Number.isInteger(value) ? value : NaN;
    };
    const splitCodes = (text) => String(text ?? '').split(/[\s,;]+/u).map((code) => code.trim().toUpperCase()).filter(Boolean);

    // What the form sends: MDM's eleven fields (the proxy forwards exactly these). A fully empty conversion row is no row.
    const readForm = () => ({
        name: String(field('Name')?.value ?? '').trim(),
        description: String(field('Description')?.value ?? '').trim() || null,
        materialType: String(field('MaterialType')?.value ?? '').trim(),
        baseUomCode: String(field('BaseUomCode')?.value ?? '').trim().toUpperCase(),
        storageConditionCodes: splitCodes(field('StorageConditionCodes')?.value),
        lotControlled: field('LotControlled')?.checked === true,
        stockable: field('Stockable')?.checked === true,
        shelfLifeDays: readDays('ShelfLifeDays'),
        retestDays: readDays('RetestDays'),
        minRemainingShelfLifeDays: readDays('MinRemainingShelfLifeDays'),
        uomConversions: conversionRows().map((row) => ({
            fromUom: row.querySelector('.js-conversion-from').value.trim().toUpperCase(),
            toUom: row.querySelector('.js-conversion-to').value.trim().toUpperCase(),
            numerator: row.querySelector('.js-conversion-numerator').value.trim(),
            denominator: row.querySelector('.js-conversion-denominator').value.trim()
        })).filter((conversion) => conversion.fromUom || conversion.toUom || conversion.numerator || conversion.denominator)
    });

    const fillForm = (item) => {
        field('Name').value = valueOf(item, 'name') ?? '';
        field('Description').value = valueOf(item, 'description') ?? '';
        field('MaterialType').value = valueOf(item, 'materialType') ?? '';
        field('BaseUomCode').value = valueOf(item, 'baseUomCode') ?? '';
        field('StorageConditionCodes').value = (valueOf(item, 'storageConditionCodes') || []).join(', ');
        field('LotControlled').checked = valueOf(item, 'lotControlled') === true;
        field('Stockable').checked = valueOf(item, 'stockable') === true;
        ['ShelfLifeDays', 'RetestDays', 'MinRemainingShelfLifeDays'].forEach((name) => {
            const value = valueOf(item, name.charAt(0).toLowerCase() + name.slice(1));
            field(name).value = value ?? '';
        });
        document.getElementById('itemConversionRows')?.replaceChildren();
        (valueOf(item, 'uomConversions') || []).forEach(addConversionRow);
        syncConversionEmpty();
    };

    // An Active item (ItemRules.LockedFieldChanged): the material type, the base unit and both flags never change; a
    // filled stock field, the storage conditions and the conversions change only through an approved correction —
    // an empty one may still be filled once (O-8). Those fields are read-only, and the note says why.
    const applyActiveLocks = (item) => {
        if (valueOf(item, 'lifecycleStatus') !== statusActive) return;
        form.querySelectorAll('[data-item-locked="always"]').forEach((input) => { input.disabled = true; });
        form.querySelectorAll('[data-item-locked="filled"]').forEach((input) => { if (String(input.value).trim()) input.disabled = true; });
        if ((valueOf(item, 'uomConversions') || []).length) {
            conversionRows().forEach((row) => row.querySelectorAll('input, button').forEach((input) => { input.disabled = true; }));
            const add = document.getElementById('btnAddConversion');
            if (add) add.disabled = true;
        }
        document.getElementById('itemLockedNote')?.classList.remove('d-none');
    };

    const loadEditForm = async (id) => {
        const save = document.getElementById('btnSaveItem');
        try {
            const item = await fetchItem(id);
            fillForm(item);
            formState.version = readVersion(item);
            formState.status = valueOf(item, 'lifecycleStatus');
            if (!readAvailableActions(item).includes('EDIT')) {
                showFormAlert(L.EditNotAvailable);
                form.querySelectorAll('input, select, textarea, button').forEach((input) => { input.disabled = true; });
                if (save) save.disabled = true;
                return;
            }
            applyActiveLocks(item);
        } catch (error) {
            showFormAlert(error?.message || L.ErrorGateway);
            if (save) save.disabled = true;
        }
    };

    const submitForm = async (event) => {
        event?.preventDefault();
        const save = document.getElementById('btnSaveItem');
        const mode = form.dataset.formMode;
        const id = form.dataset.itemId;
        const values = readForm();
        form.classList.add('was-validated');
        if (!values.name || !values.materialType || !values.baseUomCode
            || [values.shelfLifeDays, values.retestDays, values.minRemainingShelfLifeDays].some(Number.isNaN)) {
            showFormAlert(L.ErrorValidation);
            return;
        }
        if (save?.disabled) return;
        if (save) save.disabled = true;
        showFormAlert('');
        try {
            const token = antiForgeryToken('#itemForm');
            const isEdit = mode === 'edit';
            if (isEdit && !Number.isInteger(formState.version)) throw new Error(L.ErrorConflict);
            const payload = isEdit
                ? { ...values, expectedVersion: formState.version }
                : { ...values, formAttemptToken: form.dataset.formAttempt || '' };
            const response = await fetch(isEdit ? `${endpoint}/${encodeURIComponent(id)}` : endpoint, {
                method: isEdit ? 'PUT' : 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(payload)
            });
            if (!isEdit && response.status === 202) {
                // Saved, not yet completed: the SAME attempt, sent again, finishes it.
                showFormAlert(await getErrorMessage(response));
                return;
            }
            if (!response.ok) throw new Error(await getErrorMessage(response));
            const answer = await response.json();
            const savedId = isEdit ? id : valueOf(unwrapData(answer), 'id');
            if (!savedId) throw new Error(L.ErrorGateway);
            if (!isEdit && typeof answer?.formAttemptToken === 'string') form.dataset.formAttempt = answer.formAttemptToken;
            window.showToast?.(isEdit ? L.UpdateSuccess : L.CreateSuccess, 'success');
            window.location.href = `${pagesBase}/Details/${encodeURIComponent(savedId)}`;
        } catch (error) {
            showFormAlert(error?.message || L.ErrorGateway);
        } finally {
            if (save) save.disabled = false;
        }
    };

    const initForm = async () => {
        if (!form) return;
        form.addEventListener('submit', submitForm);
        document.getElementById('btnAddConversion')?.addEventListener('click', () => addConversionRow(null)?.querySelector('input')?.focus());
        document.getElementById('itemConversionRows')?.addEventListener('click', (event) => {
            const remove = event.target.closest('.js-conversion-remove');
            if (!remove || remove.disabled) return;
            remove.closest('.js-conversion-row')?.remove();
            syncConversionEmpty();
        });
        syncConversionEmpty();
        if (form.dataset.formMode === 'edit' && form.dataset.itemId) await loadEditForm(form.dataset.itemId);
    };

    // ── Record (Details) ────────────────────────────────────────────────────────────────────────────────────────
    const details = document.getElementById('itemDetails');
    const setField = (name, text) => {
        const slot = details?.querySelector(`[data-item-field="${name}"]`);
        if (slot) slot.textContent = text === null || text === undefined || text === '' ? '-' : String(text);
    };
    const formatDate = (value) => {
        const date = value ? new Date(value) : null;
        return date && !Number.isNaN(date.getTime()) ? date.toLocaleString(document.documentElement.lang || undefined) : '';
    };
    const detailButton = (action, text, className, icon) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `btn ${className}`;
        button.dataset.detailAction = action;
        const glyph = document.createElement('i');
        glyph.className = `${icon} me-1`;
        button.append(glyph, document.createTextNode(text || ''));
        return button;
    };

    const renderRecord = (item) => {
        setField('name', valueOf(item, 'name'));
        setField('canonicalCode', valueOf(item, 'canonicalCode'));
        setField('materialType', materialTypeLabel(valueOf(item, 'materialType')));
        setField('description', valueOf(item, 'description'));
        setField('version', valueOf(item, 'version'));
        setField('createdAt', formatDate(valueOf(item, 'createdAt')));
        setField('updatedAt', formatDate(valueOf(item, 'updatedAt')));
        setField('baseUomCode', valueOf(item, 'baseUomCode'));
        setField('storageConditionCodes', (valueOf(item, 'storageConditionCodes') || []).join(', '));
        setField('shelfLifeDays', valueOf(item, 'shelfLifeDays'));
        setField('retestDays', valueOf(item, 'retestDays'));
        setField('minRemainingShelfLifeDays', valueOf(item, 'minRemainingShelfLifeDays'));
        setField('lotControlled', yesNo(valueOf(item, 'lotControlled')));
        setField('stockable', yesNo(valueOf(item, 'stockable')));
        const status = details.querySelector('[data-item-field="lifecycleStatus"]');
        if (status) status.innerHTML = renderLifecycle(valueOf(item, 'lifecycleStatus'));

        const conversions = valueOf(item, 'uomConversions') || [];
        const body = document.getElementById('itemDetailConversions');
        body?.replaceChildren(...conversions.map((conversion) => {
            const row = document.createElement('tr');
            ['fromUom', 'toUom', 'numerator', 'denominator'].forEach((key) => {
                const cell = document.createElement('td');
                cell.textContent = valueOf(conversion, key) ?? '';
                row.appendChild(cell);
            });
            return row;
        }));
        document.getElementById('itemDetailConversionsEmpty')?.classList.toggle('d-none', conversions.length > 0);

        // Only the server's actions (DETAILS is this page itself).
        const host = document.getElementById('itemDetailActions');
        if (!host) return;
        const id = valueOf(item, 'id');
        host.replaceChildren(...readAvailableActions(item).filter((action) => action !== 'DETAILS').map((action) => {
            if (action === 'EDIT') {
                const link = document.createElement('a');
                link.className = 'btn btn-primary';
                link.href = `${pagesBase}/Edit/${encodeURIComponent(id)}`;
                link.textContent = L.Edit || '';
                return link;
            }
            const presentation = rowActionPresentation[action];
            return detailButton(action, presentation.text(),
                action === 'RETIRE' ? 'btn-label-danger' : 'btn-label-primary', presentation.icon);
        }));
    };

    const loadRecord = async () => {
        const id = details?.dataset.itemId;
        if (!id) return;
        const alert = document.getElementById('itemDetailsAlert');
        try {
            renderRecord(await fetchItem(id));
            alert?.classList.add('d-none');
        } catch (error) {
            if (alert) {
                alert.textContent = error?.message || L.ErrorGateway;
                alert.classList.remove('d-none');
            }
        }
    };

    const initDetails = async () => {
        if (!details) return;
        document.getElementById('itemDetailActions')?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-detail-action]');
            if (!button) return;
            requestLifecycle(details.dataset.itemId, button.dataset.detailAction, loadRecord);
        });
        await loadRecord();
    };

    return {
        init: async () => {
            await initList();
            await initForm();
            await initDetails();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ItemsPage.init());
