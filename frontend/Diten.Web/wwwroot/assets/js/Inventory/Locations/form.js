/**
 * MOD-0173-FU02 — the location screens' shared code (LOC2): ONE form (L-3), ONE status-change dialog, ONE vocabulary
 * of badges — loaded by both L-1 (/LocationTree) and L-2 (/Locations), so the two screens cannot drift apart.
 *
 *   window.LocationUi   — formatters (type / status / mode text, badges, escaping, the reference disclosure).
 *   window.LocationForm — open({ mode: 'create'|'child'|'edit', parent, id, onSaved }), changeStatus(node, target, { onDone }).
 *
 * THE BROWSER TALKS ONLY TO THE SAME-ORIGIN MVC PROXIES (/Locations/…, pack §8): never to 5066, never to
 * /api/locations, never to the Gateway. Writes carry the page's anti-forgery token.
 *
 * FORM (pack §4/§12, Slim offcanvas, 8 fields): the company is NOT a field (K-03/K-14). Type is chosen at create only
 * (LOC-04) and limited by the parent (K-01 matrix); a Site has no parent; "Can hold stock" only for Bin /
 * StorageLocation (LOC1-04); operating mode only for Site / Warehouse (DEC-INV-19). The org-unit list is the
 * company's active Platform units — when it cannot be read the field says so and the form still saves. Edit sends
 * only what changed, guarded by If-Match (stale → LOCATION_CONFLICT: "reload", never a silent retry). One
 * Idempotency-Key per opened create form: a double submit is a replay, never a second location.
 *
 * STATUS (K-04…K-06): Activate / Deactivate / Archive through the shared window.showConfirm (MOD-0013, one dialog
 * implementation). Archive is the danger variant and says it cannot be undone. A refusal (LOCATION_HAS_STOCK,
 * LOCATION_HAS_ACTIVE_CHILDREN, LOCATION_PARENT_INACTIVE …) stays IN the dialog through its didOpen seam — the
 * FU01 pattern — with the reference id; nothing is changed.
 */
'use strict';

window.LocationUi = (function () {
    const L = () => window.L10n || {};
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    const typeIcon = {
        Site: 'bx bx-buildings',
        Warehouse: 'bx bx-store-alt',
        StorageLocation: 'bx bx-cabinet',
        Zone: 'bx bx-grid-alt',
        Bin: 'bx bx-box'
    };
    const statusClass = { Active: 'bg-label-success', Inactive: 'bg-label-warning', Archived: 'bg-label-secondary' };

    // K-01 (pack §12): what may sit under what. The service stays the authority; the form only offers the valid.
    const allowedChildren = {
        Site: ['Warehouse'],
        Warehouse: ['StorageLocation'],
        StorageLocation: ['Zone', 'Bin'],
        Zone: ['Bin'],
        Bin: []
    };
    const allowedParents = {
        Site: [],
        Warehouse: ['Site'],
        StorageLocation: ['Warehouse'],
        Zone: ['StorageLocation'],
        Bin: ['Zone', 'StorageLocation']
    };

    const typeText = (type) => (type && L()['Type' + type]) || type || '';
    const statusText = (status) => (status && L()['Status' + status]) || status || '';
    const modeText = (mode) => mode === 'external-integrated' ? (L().ModeExternal || mode) : mode === 'internal' ? (L().ModeInternal || mode) : '';
    const statusBadge = (status) => status
        ? `<span class="badge ${statusClass[status] || 'bg-label-secondary'}">${esc(statusText(status))}</span>`
        : '';
    const typeBadge = (type) => type
        ? `<span class="badge bg-label-info"><i class="${typeIcon[type] || 'bx bx-map'} me-1"></i>${esc(typeText(type))}</span>`
        : '';

    // The reference id of a refusal goes into a disclosure (pack §13): support can trace it, the reader is not
    // handed a technical code as the message.
    const withReference = (message, correlationId) => {
        let html = `<div>${esc(message || L().ErrServiceUnavailable || '')}</div>`;
        if (correlationId) {
            html += `<details class="small text-muted mt-1"><summary>${esc(L().ErrorDetails || '')}</summary>`
                + `${esc(L().ReferenceLabel || '')}: <span class="font-monospace">${esc(correlationId)}</span></details>`;
        }
        return html;
    };

    // "View in stock list" (K-10): FU01 S-1 filtered by this location — shown only to a reader of the stock list.
    const stockListUrl = (id) => `/StockLedger?${new URLSearchParams({ locationId: id }).toString()}`;

    const newKey = () => (window.crypto?.randomUUID
        ? window.crypto.randomUUID()
        : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
            const r = window.crypto.getRandomValues(new Uint8Array(1))[0] % 16;
            return (c === 'x' ? r : ((r & 0x3) | 0x8)).toString(16);
        }));

    const can = (key) => window.Permissions?.has?.(key) === true;

    const antiforgeryToken = () =>
        document.querySelector('#formLocation input[name="__RequestVerificationToken"]')?.value || '';

    // Same-origin JSON call; every failure becomes { ok: false, status, code, message, correlationId }.
    const call = async (url, options) => {
        try {
            const init = Object.assign({ credentials: 'same-origin' }, options || {});
            if (init.body !== undefined) {
                init.method = init.method || 'POST';
                init.headers = Object.assign({ 'Content-Type': 'application/json', 'RequestVerificationToken': antiforgeryToken() }, init.headers || {});
                init.body = JSON.stringify(init.body);
            }
            const res = await fetch(url, init);
            const json = await res.json().catch(() => null);
            if (res.ok) return Object.assign({ ok: true, status: res.status }, json || {});
            return {
                ok: false,
                status: res.status,
                code: json?.error?.code || null,
                message: json?.error?.message || L().ErrServiceUnavailable || '',
                correlationId: json?.error?.correlationId || null
            };
        } catch (error) {
            return { ok: false, status: 0, code: null, message: L().ErrServiceUnavailable || '', correlationId: null };
        }
    };

    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value), String(template || ''));

    return {
        L, esc, typeIcon, typeText, statusText, modeText, statusBadge, typeBadge, allowedChildren, allowedParents,
        withReference, stockListUrl, newKey, can, call, format
    };
})();

window.LocationForm = (function () {
    const ui = window.LocationUi;
    const L = ui.L;
    const byId = (id) => document.getElementById(id);

    const CODE = /^[A-Za-z0-9._/-]{1,40}$/;
    const COST_CENTER = /^[A-Za-z0-9._-]{1,40}$/;

    // Which contract error lands on which field (pack §12/§13); anything else goes to the form's alert.
    const fieldOfCode = {
        LOCATION_CODE_TAKEN: 'code',
        LOCATION_LEVEL_INVALID: 'parentId',
        LOCATION_PARENT_INACTIVE: 'parentId',
        LOCATION_MOVE_INTO_SUBTREE: 'parentId',
        UNKNOWN_LOCATION: 'parentId',
        CROSS_LE_FORBIDDEN: 'parentId',
        LOCATION_ARCHIVED: 'parentId',
        ORG_UNIT_INVALID: 'orgUnitRef',
        ORG_UNIT_DIRECTORY_UNAVAILABLE: 'orgUnitRef'
    };

    let state = null;
    let orgUnits = null; // { available, items, message } — read once per page

    const offcanvas = () => {
        const el = byId('offcanvasLocationForm');
        return el && window.bootstrap?.Offcanvas ? window.bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const typeSelect = () => byId('locType');
    const show = (id, visible) => byId(id)?.classList.toggle('d-none', !visible);

    const clearErrors = () => {
        const form = byId('formLocation');
        form?.classList.remove('was-validated');
        form?.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        form?.querySelectorAll('[data-field-error]').forEach((el) => { el.innerHTML = ''; });
        const alert = byId('locationFormAlert');
        if (alert) { alert.innerHTML = ''; alert.classList.add('d-none'); }
    };

    const controlOf = (field) => ({
        code: byId('locCode'),
        name: byId('locName'),
        type: byId('locType'),
        parentId: byId('locParent'),
        isStockable: byId('locStockable'),
        operatingMode: byId('locMode'),
        orgUnitRef: byId('locOrgUnit'),
        costCenterCode: byId('locCostCenter')
    })[field];

    const fieldError = (field, html) => {
        const slot = document.querySelector(`#formLocation [data-field-error="${field}"]`);
        const control = controlOf(field);
        if (!slot) return false;
        slot.innerHTML = html;
        slot.classList.add('d-block');
        // A select2 field keeps its box (the theme's invalid styling re-pads it over the field icon); its sentence
        // sits right under it. Plain inputs get the red border.
        if (control && !control.classList.contains('select2-hidden-accessible')) control.classList.add('is-invalid');
        return true;
    };

    const formAlert = (html) => {
        const alert = byId('locationFormAlert');
        if (!alert) return;
        alert.innerHTML = html;
        alert.classList.remove('d-none');
    };

    // ── select2 pickers inside the offcanvas ────────────────────────────────────────────────────────────────
    const initParentPicker = (types) => {
        const el = byId('locParent');
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = $(el);
        if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
        $el.select2({
            dropdownParent: $('#offcanvasLocationForm'),
            placeholder: $el.data('placeholder') || '',
            allowClear: false,
            width: '100%',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url: '/Locations/parents',
                dataType: 'json',
                delay: 250,
                traditional: true,
                // A new child or a move needs an ACTIVE parent (K-06).
                data: (params) => ({ q: params.term || '', type: types, activeOnly: true }),
                processResults: (data) => ({ results: (data?.items || []).map((item) => ({ id: item.value, text: item.text, type: item.type })) })
            }
        });
        $el.off('select2:select.locform').on('select2:select.locform', (e) => {
            if (state) state.parentType = e.params?.data?.type || state.parentType;
        });
    };

    const setParentValue = (id, text) => {
        const el = byId('locParent');
        if (!el) return;
        el.innerHTML = '<option value=""></option>';
        if (id) el.add(new Option(text || id, id, true, true));
        if (window.jQuery) $(el).trigger('change');
    };

    const loadOrgUnits = async () => {
        if (orgUnits) return orgUnits;
        const reply = await ui.call('/Locations/orgunits');
        orgUnits = reply.ok
            ? { available: reply.available !== false, items: reply.items || [], message: reply.message || '' }
            : { available: false, items: [], message: L().OrgUnitsUnavailable || '' };
        return orgUnits;
    };

    const fillOrgUnits = async (selected) => {
        const el = byId('locOrgUnit');
        if (!el) return;
        const list = await loadOrgUnits();
        el.innerHTML = '<option value=""></option>';
        list.items.forEach((item) => el.add(new Option(item.text, item.value, false, item.value === selected)));
        // A unit no longer in the company's active list keeps showing (soft link, LOC-11) — raw, as it is stored.
        if (selected && !list.items.some((item) => item.value === selected)) el.add(new Option(selected, selected, true, true));
        const note = byId('locOrgUnitUnavailable');
        if (note) {
            note.textContent = list.available ? '' : (list.message || L().OrgUnitsUnavailable || '');
            note.classList.toggle('d-none', list.available);
        }
        if (window.jQuery?.fn?.select2) {
            const $el = $(el);
            if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
            $el.select2({ dropdownParent: $('#offcanvasLocationForm'), placeholder: $el.data('placeholder') || '', allowClear: true, width: '100%' });
            $el.val(selected || '').trigger('change');
        }
    };

    // ── what the chosen type allows ─────────────────────────────────────────────────────────────────────────
    const applyType = (type, { keepValues } = {}) => {
        const isSite = type === 'Site';
        const parents = ui.allowedParents[type] || [];
        show('locParentGroup', !!type && !isSite);
        show('locSiteNote', isSite);
        const parent = byId('locParent');
        if (parent) parent.required = !!type && !isSite;

        const stockable = type === 'Bin' || type === 'StorageLocation';
        show('locStockableGroup', stockable);
        if (!keepValues && byId('locStockable')) byId('locStockable').checked = type === 'Bin'; // K-08 defaults

        const hasMode = type === 'Site' || type === 'Warehouse';
        show('locModeGroup', hasMode);
        if (!keepValues && byId('locMode')) setStatic('locMode', 'internal'); // DEC-INV-19 default

        const hint = byId('locParentHint');
        if (hint) hint.textContent = type === 'Bin' ? (L().HintZoneOptional || '') : '';

        if (!state?.lockedParent && parents.length) initParentPicker(parents);
    };

    // The two fixed-list fields (type, operating mode) are select2 like every offcanvas select (Golden Slim): the
    // shared .diten-field rules inset select2's box, not a native <select>. Their options are the contract's enums.
    const staticOptions = {};
    const syncStatic = (id) => {
        const el = byId(id);
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = $(el);
        if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
        $el.select2({
            dropdownParent: $('#offcanvasLocationForm'),
            placeholder: $el.find('option[value=""]').text() || '',
            minimumResultsForSearch: Infinity,
            width: '100%'
        });
    };
    // Sets a value without firing the form's own change handler (select2 only redraws).
    const setStatic = (id, value) => {
        const el = byId(id);
        if (!el) return;
        el.value = value;
        if (window.jQuery) $(el).trigger('change.select2');
    };

    // Only the types the parent allows are OFFERED — the list is rebuilt, nothing is left greyed out
    // (frontend-form-template: dependent selects rebuild their options).
    const restrictTypes = (types) => {
        const select = typeSelect();
        if (!select) return;
        staticOptions.locType = staticOptions.locType || Array.from(select.options).map((o) => ({ value: o.value, text: o.textContent }));
        select.innerHTML = '';
        staticOptions.locType
            .filter((o) => !o.value || types.includes(o.value))
            .forEach((o) => select.add(new Option(o.text, o.value)));
        syncStatic('locType');
    };

    const reset = () => {
        clearErrors();
        ['locCode', 'locName', 'locCostCenter'].forEach((id) => { if (byId(id)) byId(id).value = ''; });
        const select = typeSelect();
        if (select) select.disabled = false;
        show('locTypeFixed', false);
        restrictTypes(['Site', 'Warehouse', 'StorageLocation', 'Zone', 'Bin']);
        setStatic('locType', '');
        syncStatic('locMode');
        setParentValue(null);
        const parent = byId('locParent');
        if (parent) parent.disabled = false;
        show('locParentGroup', false);
        show('locSiteNote', false);
        show('locStockableGroup', false);
        show('locModeGroup', false);
    };

    // ── open ────────────────────────────────────────────────────────────────────────────────────────────────
    const open = async (options) => {
        if (!byId('formLocation')) return; // a read-only user has no form on the page (UAS-001: absent)
        const opts = options || {};
        state = {
            mode: opts.mode || 'create',
            id: opts.id || null,
            key: ui.newKey(), // ONE key per opened form (double submit = replay)
            etag: null,
            original: null,
            lockedParent: false,
            parentType: null,
            onSaved: opts.onSaved || null,
            busy: false
        };
        reset();

        const title = byId('offcanvasLocationFormLabel');
        if (state.mode === 'edit') {
            const reply = await ui.call(`/Locations/node/${encodeURIComponent(state.id)}`);
            if (!reply.ok || !reply.location) {
                window.showToast?.(reply.message || L().ErrServiceUnavailable || '', 'error');
                return;
            }
            const location = reply.location;
            state.etag = reply.etag;
            state.type = location.type;
            state.original = {
                code: location.code || '',
                name: location.name || '',
                parentId: location.parentId || null,
                isStockable: !!location.isStockable,
                operatingMode: location.operatingMode || null,
                orgUnitRef: location.orgUnitRef || null,
                costCenterCode: location.costCenterCode || null
            };
            if (title) title.textContent = L().FormTitleEdit || '';
            byId('locCode').value = state.original.code;
            byId('locName').value = state.original.name;
            byId('locCostCenter').value = state.original.costCenterCode || '';
            restrictTypes([location.type]);
            typeSelect().disabled = true; // LOC-04: type is fixed after create
            syncStatic('locType');
            setStatic('locType', location.type);
            show('locTypeFixed', true);
            applyType(location.type, { keepValues: true });
            byId('locStockable').checked = state.original.isStockable;
            if (state.original.operatingMode) setStatic('locMode', state.original.operatingMode);
            if (state.original.parentId) {
                const steps = reply.path || [];
                const parentStep = steps.length > 1 ? steps[steps.length - 2] : null;
                setParentValue(state.original.parentId, parentStep?.code || state.original.parentId);
            }
            await fillOrgUnits(state.original.orgUnitRef);
        } else if (state.mode === 'child' && opts.parent) {
            const types = ui.allowedChildren[opts.parent.type] || [];
            if (!types.length) return; // a Bin has no children (K-01)
            state.lockedParent = true;
            state.parentType = opts.parent.type;
            if (title) title.textContent = ui.format(L().FormTitleChild, opts.parent.code || '');
            restrictTypes(types);
            setParentValue(opts.parent.id, `${opts.parent.code || ''} · ${opts.parent.name || ''}`);
            // The parent is the node the reader started from: shown, not choosable (select2 box like every field).
            byId('locParent').disabled = true;
            syncStatic('locParent');
            if (types.length === 1) {
                setStatic('locType', types[0]);
                applyType(types[0]);
            }
            await fillOrgUnits(null);
        } else {
            if (title) title.textContent = L().FormTitleCreate || '';
            if (opts.siteOnly) {
                restrictTypes(['Site']);
                setStatic('locType', 'Site');
                applyType('Site');
            }
            await fillOrgUnits(null);
        }

        offcanvas()?.show();
        window.setTimeout(() => byId('locCode')?.focus(), 250);
    };

    // ── validate + save ─────────────────────────────────────────────────────────────────────────────────────
    const readValues = () => {
        const type = state.mode === 'edit' ? state.type : typeSelect().value;
        const hasStockable = type === 'Bin' || type === 'StorageLocation';
        const hasMode = type === 'Site' || type === 'Warehouse';
        const orgUnit = byId('locOrgUnit')?.value || '';
        const costCenter = (byId('locCostCenter')?.value || '').trim();
        return {
            code: (byId('locCode').value || '').trim(),
            name: (byId('locName').value || '').trim(),
            type,
            parentId: type && type !== 'Site' ? (byId('locParent')?.value || null) : null,
            isStockable: hasStockable ? !!byId('locStockable')?.checked : undefined,
            operatingMode: hasMode ? (byId('locMode')?.value || 'internal') : undefined,
            orgUnitRef: orgUnit || null,
            costCenterCode: costCenter || null
        };
    };

    // Client checks mirror the contract's shape only; the service stays the authority (pack §12).
    const validate = (values) => {
        let valid = true;
        const fail = (field, key) => { fieldError(field, ui.esc(L()[key] || '')); valid = false; };
        if (!values.code) fail('code', 'ValidationRequired');
        else if (!CODE.test(values.code)) fail('code', 'ValidationCodeFormat');
        if (!values.name) fail('name', 'ValidationRequired');
        else if (values.name.length > 120) fail('name', 'ValidationNameLength');
        if (state.mode !== 'edit' && !values.type) fail('type', 'ValidationRequired');
        if (values.type && values.type !== 'Site' && !values.parentId) fail('parentId', 'ValidationRequired');
        if (values.costCenterCode && !COST_CENTER.test(values.costCenterCode)) fail('costCenterCode', 'ValidationCostCenterFormat');
        return valid;
    };

    // Edit: only the members that changed (absent = unchanged, null = cleared — LOCATION v1.1 PATCH).
    const changesOf = (values) => {
        const o = state.original;
        const changes = {};
        if (values.code !== o.code) changes.code = values.code;
        if (values.name !== o.name) changes.name = values.name;
        if (values.type !== 'Site' && values.parentId && values.parentId !== o.parentId) changes.parentId = values.parentId;
        if (values.isStockable !== undefined && values.isStockable !== o.isStockable) changes.isStockable = values.isStockable;
        if (values.operatingMode !== undefined && values.operatingMode !== (o.operatingMode || 'internal')) changes.operatingMode = values.operatingMode;
        if ((values.orgUnitRef || null) !== (o.orgUnitRef || null)) changes.orgUnitRef = values.orgUnitRef;
        if ((values.costCenterCode || null) !== (o.costCenterCode || null)) changes.costCenterCode = values.costCenterCode;
        return changes;
    };

    const showRefusal = (reply, changes) => {
        const html = ui.withReference(reply.message, reply.correlationId);
        let field = fieldOfCode[reply.code];
        // LOCATION_HAS_STOCK names the field that was changed (move / stockable / operating mode — K-07/K-08/K-09).
        if (reply.code === 'LOCATION_HAS_STOCK' && changes) {
            field = 'parentId' in changes ? 'parentId' : 'isStockable' in changes ? 'isStockable' : 'operatingMode' in changes ? 'operatingMode' : null;
        }
        if (reply.code === 'LOCATION_CONFLICT') {
            // Another user saved first: say so and offer the reload — never a silent retry (pack §13).
            formAlert(`${html}<button type="button" class="btn btn-sm btn-label-primary mt-2" id="btnLocationReload">${ui.esc(L().ReloadLabel || '')}</button>`);
            byId('btnLocationReload')?.addEventListener('click', () => open({ mode: 'edit', id: state.id, onSaved: state.onSaved }));
            return;
        }
        if (!field || !fieldError(field, html)) formAlert(html);
    };

    const save = async () => {
        if (!state || state.busy) return;
        clearErrors();
        const values = readValues();
        // Errors are marked per field (is-invalid + the field's own sentence). No `was-validated` on the form: the
        // theme's :valid / :invalid styling would also paint every untouched optional field and re-pad select2.
        if (!validate(values)) return;

        let url;
        let body;
        let changes = null;
        if (state.mode === 'edit') {
            changes = changesOf(values);
            if (!Object.keys(changes).length) {
                offcanvas()?.hide();
                window.showToast?.(L().NoChanges || '', 'info');
                return;
            }
            url = `/Locations/edit/${encodeURIComponent(state.id)}`;
            body = { ifMatch: state.etag, changes };
        } else {
            url = '/Locations/create';
            body = { idempotencyKey: state.key, code: values.code, name: values.name, type: values.type };
            if (values.parentId) body.parentId = values.parentId;
            if (values.isStockable !== undefined) body.isStockable = values.isStockable;
            if (values.operatingMode !== undefined) body.operatingMode = values.operatingMode;
            if (values.orgUnitRef) body.orgUnitRef = values.orgUnitRef;
            if (values.costCenterCode) body.costCenterCode = values.costCenterCode;
        }

        const button = byId('btnSaveLocation');
        state.busy = true;
        if (button) button.disabled = true;
        try {
            const reply = await ui.call(url, { body });
            if (!reply.ok) {
                showRefusal(reply, changes);
                return;
            }
            offcanvas()?.hide();
            window.showToast?.(reply.message || L().RecordSaved || '', 'success');
            if (typeof state.onSaved === 'function') state.onSaved(reply.location || null);
        } finally {
            state.busy = false;
            if (button) button.disabled = false;
        }
    };

    // ── status change (Activate / Deactivate / Archive) ─────────────────────────────────────────────────────
    const statusWords = {
        Active: { title: 'ConfirmActivateTitle', text: 'ConfirmActivateText', button: 'ActionActivate', type: 'info' },
        Inactive: { title: 'ConfirmDeactivateTitle', text: 'ConfirmDeactivateText', button: 'ActionDeactivate', type: 'warning' },
        Archived: { title: 'ConfirmArchiveTitle', text: 'ConfirmArchiveText', button: 'ActionArchive', type: 'danger' }
    };

    const changeStatus = async (node, target, options) => {
        const words = statusWords[target];
        if (!node?.id || !words || !document.querySelector('#formLocation')) return;
        // A fresh read gives the ETag the change is guarded by (If-Match).
        const current = await ui.call(`/Locations/node/${encodeURIComponent(node.id)}`);
        if (!current.ok || !current.location) {
            window.showToast?.(current.message || L().ErrServiceUnavailable || '', 'error');
            return;
        }
        const location = current.location;
        let etag = current.etag;
        let busy = false;
        const label = `${location.code || ''} · ${location.name || ''}`;

        window.showConfirm?.(L()[words.title] || '', null, {
            type: words.type,
            subtext: ui.esc(ui.format(L()[words.text], location.code || '')),
            entityName: ui.esc(label),
            confirmButtonText: L()[words.button] || '',
            cancelButtonText: L().Cancel || '',
            didOpen: (popup, swal) => {
                const confirm = popup.querySelector('.swal2-confirm');
                confirm?.addEventListener('click', async (event) => {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    if (busy) return; // a double click submits once
                    busy = true;
                    swal.resetValidationMessage();
                    swal.showLoading(confirm);
                    const reply = await ui.call(`/Locations/status/${encodeURIComponent(location.locationId)}`, { body: { status: target, ifMatch: etag } });
                    if (reply.ok) {
                        swal.close();
                        window.showToast?.(reply.message || '', 'success');
                        if (typeof options?.onDone === 'function') options.onDone(reply.location || null);
                        return;
                    }
                    swal.hideLoading();
                    let html = ui.withReference(reply.message, reply.correlationId);
                    // Has stock → where the stock is (pack §13: "+ view in stock list"), for a reader of the stock list.
                    if (reply.code === 'LOCATION_HAS_STOCK' && ui.can('inventory.stock.read')) {
                        html += `<a class="d-block mt-1" href="${ui.esc(ui.stockListUrl(location.locationId))}">${ui.esc(L().ActionViewStock || '')}</a>`;
                    }
                    if (reply.code === 'LOCATION_CONFLICT') {
                        const fresh = await ui.call(`/Locations/node/${encodeURIComponent(location.locationId)}`);
                        if (fresh.ok) etag = fresh.etag;
                    }
                    // One block: the dialog's message area lays its children out in a row.
                    swal.showValidationMessage(`<div class="text-start">${html}</div>`);
                    busy = false;
                }, { capture: true });
            }
        });
    };

    const bind = () => {
        byId('btnSaveLocation')?.addEventListener('click', save);
        // select2 raises its change through jQuery; a jQuery handler also sees a native change.
        const onTypeChange = () => {
            if (!state || state.mode === 'edit') return;
            clearErrors();
            if (!state.lockedParent) setParentValue(null);
            applyType(typeSelect().value);
        };
        if (window.jQuery && typeSelect()) $(typeSelect()).on('change.locform', onTypeChange);
        else typeSelect()?.addEventListener('change', onTypeChange);
        syncStatic('locType');
        syncStatic('locMode');
        // Enter in a text field saves, like every Slim form; the offcanvas has no submit button inside the form.
        byId('formLocation')?.addEventListener('submit', (e) => { e.preventDefault(); save(); });
    };

    document.addEventListener('DOMContentLoaded', bind);

    return { open, changeStatus };
})();
