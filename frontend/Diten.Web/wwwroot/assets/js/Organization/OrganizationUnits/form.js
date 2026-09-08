'use strict';

// MOD-0288 Phase 2 — Org Unit compact two-level create/edit form. Legal Entity / Parent / Manager Position are
// searchable select2 (fed via the Diten.Web proxy so the HttpOnly token is attached server-side). Dates use
// flatpickr in the request culture's format; stored/sent value stays ISO. Create POSTs to /OrganizationUnits/api,
// edit PUTs to /OrganizationUnits/api/{id}; on success it returns to the list with a toast.
//
// MOD-0288-FU03 adds the SECOND reporting line and custom field values. Three rules run through everything
// below and each of them is a decision, not an implementation detail:
//
//   1. AN EMPTY ADMINISTRATIVE LINE IS EMPTY. It never falls back to, and is never pre-filled from, the
//      functional parent (FU02 §8 decision 2). Two lines that collapse into one are not two lines.
//   2. THE TWO LINES MAY LEGITIMATELY HOLD THE SAME UNIT — the administrator is allowed to say so, and the
//      server accepts it (FU02 §12). What is forbidden is the SYSTEM producing it: no default, no copy
//      button, no auto-fill. Every value in either select was typed by a person.
//   3. CHANGING A LINE IS ITS OWN PERMISSION. The backend PUT on the unit answers 403 to a changed line, so a
//      line change goes through the reporting-lines endpoint. Without that grant both selects are read-only
//      and the rest of the form still saves — hiding the form, or letting a doomed save reach the server, are
//      both wrong (§9).
(function () {
    const page = document.getElementById('ou-form-page');
    if (!page) return;

    const endpoint = '/OrganizationUnits/api';
    const entityId = page.dataset.ouId || '';
    const parentSeedId = page.dataset.ouParentId || '';
    const isEdit = page.dataset.ouMode === 'edit';
    let L = {};
    let parentSeedLegalEntityId = '';

    const REQUIRED = ['ouCode', 'ouName', 'ouLegalEntityId'];
    const DATE_FIELDS = ['ouEffectiveFrom', 'ouEffectiveTo'];

    // FU03 — permission keys are FU02 §14 verbatim; they are not re-invented here.
    const PERM_REPORTING_LINE = 'platform.organization-units.reporting-line.update';
    const PERM_FIELDS_READ = 'platform.organization-units.custom-fields.read';
    const PERM_FIELDS_WRITE = 'platform.organization-units.custom-fields.write-value';

    const cf = () => window.OrgUnitCustomFields;
    const may = (key) => (cf() ? cf().hasPermission(key) : false);

    // The lines as they were LOADED. The edit PUT round-trips these unless the user changed them, which is what
    // keeps an ordinary editor working under `…update` alone (the handler treats an unchanged line as no change).
    let loadedLines = { functional: '', administrative: '' };
    let customFieldDefinitions = [];
    let loadedCustomValues = [];
    let customFieldReferences = {};

    const byId = (id) => document.getElementById(id);
    const trim = (v) => (typeof v === 'string' ? v.trim() : '');
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const loadL10n = () => {
        const node = byId('org-unit-form-l10n');
        if (!node) return;
        try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[OU Form] L10n parse failed.', e); }
    };

    const showAlert = (message) => {
        const el = byId('ou-form-alert');
        if (!el) return;
        const list = (Array.isArray(message) ? message : [message]).filter(Boolean);
        if (!list.length) { el.classList.add('d-none'); el.innerHTML = ''; return; }
        el.innerHTML = list.map((m) => `<div>${escapeHtml(m)}</div>`).join('');
        el.classList.remove('d-none');
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#ou-form-page input[name="__RequestVerificationToken"]')?.value || '';

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    const fetchJson = (url) => fetch(url, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r));

    // ─── Lookups + select2 ───────────────────────────────────────────────────
    const fillSelect = (selectId, items, mapItem) => {
        const select = byId(selectId);
        if (!select) return;
        const first = select.querySelector('option'); // keep placeholder
        select.innerHTML = '';
        if (first) select.appendChild(first);
        (items || []).forEach((it) => {
            const { value, text } = mapItem(it);
            if (!value) return;
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = text;
            select.appendChild(opt);
        });
    };

    const codeName = (code, name) => (code ? `${code} — ${name || ''}` : (name || ''));

    const loadLookups = async () => {
        const [legalEntities, orgUnits, positions] = await Promise.all([
            fetchJson(`${endpoint}/legal-entities`).then(unwrapList).catch(() => []),
            fetchJson(endpoint).then(unwrapList).catch(() => []),
            fetchJson(`${endpoint}/positions`).then(unwrapList).catch(() => [])
        ]);

        fillSelect('ouLegalEntityId', legalEntities, (e) => ({
            value: e.legalEntityId || e.LegalEntityId || e.id || e.Id,
            text: codeName(e.code || e.Code, e.displayName || e.DisplayName || e.legalName || e.LegalName)
        }));

        // Parent options exclude self (an org unit cannot be its own parent) — for BOTH lines.
        const parents = (orgUnits || []).filter((u) => !(isEdit && String(u.id || u.Id) === String(entityId)));
        const parentOption = (u) => ({
            value: u.id || u.Id,
            text: codeName(u.code || u.Code, u.name || u.Name)
        });
        fillSelect('ouParentId', parents, parentOption);
        /*
         * ⚠ THE SAME OPTIONS, AND NOTHING ELSE SHARED. The administrative select is filled from the same list
         * because it points at the same kind of thing — not because it mirrors the functional one. Nothing here
         * reads #ouParentId to decide what #ouAdministrativeParentId shows or holds; the user may pick the same
         * unit in both, and that has to be their choice rather than ours.
         */
        fillSelect('ouAdministrativeParentId', parents, parentOption);

        // Reference-typed custom fields point at an Organization Unit or a Position in this tenant, so the
        // lookups the form already loaded are exactly the option sources they need.
        customFieldReferences = {
            OrganizationUnit: (orgUnits || []).map(parentOption).filter((o) => o.value),
            Position: (positions || []).map((p) => ({
                value: p.id || p.Id,
                text: codeName(p.code || p.Code, p.name || p.Name)
            })).filter((o) => o.value)
        };

        // "Add sub-unit" seed: pre-select the parent and inherit its Legal Entity (backend requires
        // a child to share its parent's Legal Entity), so the common case saves without extra input.
        if (!isEdit && parentSeedId) {
            const seed = (orgUnits || []).find((u) => String(u.id || u.Id) === String(parentSeedId));
            if (seed) parentSeedLegalEntityId = seed.legalEntityId || seed.LegalEntityId || '';
        }

        fillSelect('ouManagerPositionId', positions, (p) => ({
            value: p.id || p.Id,
            text: codeName(p.code || p.Code, p.name || p.Name)
        }));
    };

    const initSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        // Pass each select's own empty first-option text as the select2 placeholder so the unselected state renders
        // in the muted placeholder colour (matching the text inputs) instead of the darker option-text colour.
        jq('#ouLegalEntityId, #ouParentId, #ouAdministrativeParentId, #ouManagerPositionId').each(function () {
            const ph = jq(this).find('option[value=""]').first().text() || '';
            jq(this).select2({ width: '100%', placeholder: ph });
        });
        jq('#ouLegalEntityId').on('change', function () { this.classList.remove('is-invalid'); updateSaveState(); });
    };

    // ─── Culture-aware date pickers ──────────────────────────────────────────
    const initDatePickers = () => {
        if (typeof window.flatpickr !== 'function') return;
        DATE_FIELDS.forEach((id) => {
            const el = byId(id);
            if (el) window.flatpickr(el, { dateFormat: 'Y-m-d', altInput: true, altFormat: L.DateFormat || 'Y-m-d', allowInput: true });
        });
    };
    const dateOrNull = (id) => { const v = trim(byId(id)?.value); return v.length ? `${v}T00:00:00Z` : null; };
    const setDate = (id, iso) => {
        const el = byId(id);
        if (!el || !iso) return;
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return;
        const ymd = d.toISOString().slice(0, 10);
        if (el._flatpickr) el._flatpickr.setDate(ymd, true); else el.value = ymd;
    };

    // ─── Validation + payload ────────────────────────────────────────────────
    const isFilled = (id) => trim(byId(id)?.value).length > 0;
    const updateSaveState = () => { /* placeholder hook for future required-counter; kept for symmetry */ };

    const validate = () => {
        // Tracked separately from the custom-field result: the generic "required fields are missing" line
        // belongs to the FIXED fields. Emitting it alongside a per-field custom message printed the same
        // sentence twice in the alert for a single empty field.
        let fixedOk = true;
        REQUIRED.forEach((id) => {
            const el = byId(id);
            if (!isFilled(id)) { fixedOk = false; el?.classList.add('is-invalid'); }
            else el?.classList.remove('is-invalid');
        });
        let ok = fixedOk;

        /*
         * Custom values are checked here for a better message a moment sooner. ⚠ THE SERVER REMAINS THE
         * AUTHORITY: passing this check is not permission to save, and a server rejection wins over anything
         * decided here (§8). A definition that rendered no control cannot be demanded — it is not in
         * `customFieldDefinitions`, which holds only what was actually put on the page.
         */
        const messages = [];
        if (customFieldDefinitions.length && cf()) {
            const values = cf().readCustomFieldValues(customFieldsHost(), customFieldDefinitions);
            const check = cf().validateCustomFields(customFieldDefinitions, values, L);
            cf().showCustomFieldErrors(customFieldsHost(), check.errors);
            if (!check.valid) {
                ok = false;
                check.errors.forEach((e) => { if (!messages.includes(e.message)) messages.push(e.message); });
            }
        }

        if (!ok) {
            showAlert((fixedOk ? [] : [L.RequiredField || 'Required fields are missing.']).concat(messages));
        }
        return ok;
    };

    const valueOrNull = (id) => { const v = trim(byId(id)?.value); return v.length ? v : null; };

    const collectPayload = () => ({
        code: trim(byId('ouCode')?.value),
        name: trim(byId('ouName')?.value),
        legalEntityId: valueOrNull('ouLegalEntityId'),
        parentOrganizationUnitId: valueOrNull('ouParentId'),
        // Sent EXPLICITLY, including when it is null: the update is a full replace, so an omitted value clears
        // the line. Null here means "no administrative line", never "keep whatever is stored".
        administrativeParentOrganizationUnitId: valueOrNull('ouAdministrativeParentId'),
        orgUnitType: byId('ouOrgUnitType')?.value || 'Department',
        managerPositionId: valueOrNull('ouManagerPositionId'),
        description: valueOrNull('ouDescription'),
        status: byId('ouStatus')?.value || 'Active',
        effectiveFrom: dateOrNull('ouEffectiveFrom'),
        effectiveTo: dateOrNull('ouEffectiveTo')
    });

    // ─── Save ────────────────────────────────────────────────────────────────
    const send = (url, method, body) => fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
        body: body === undefined ? undefined : JSON.stringify(body)
    });

    const readErrors = async (res) => {
        try {
            const json = await res.json();
            const errors = json.errors || json.Errors || [];
            return errors.length ? errors : [L.ErrorOccurred || 'An error occurred.'];
        } catch {
            return [L.ErrorOccurred || 'An error occurred.'];
        }
    };

    const lineChanged = () => {
        const payload = collectPayload();
        return String(payload.parentOrganizationUnitId || '') !== loadedLines.functional
            || String(payload.administrativeParentOrganizationUnitId || '') !== loadedLines.administrative;
    };

    /*
     * Write the custom values that ACTUALLY changed, one PUT per datum (the backend contract is one
     * definition + one value per call). Untouched fields produce no request at all, so a save under
     * `…update` alone does not need `…custom-fields.write-value` just because definitions exist.
     */
    const saveCustomValues = async (unitId) => {
        if (!customFieldDefinitions.length || !cf() || !may(PERM_FIELDS_WRITE)) return null;
        const previous = {};
        (loadedCustomValues || []).forEach((v) => {
            const id = v.definitionId || v.DefinitionId;
            if (id && !(v.redacted === true || v.Redacted === true)) {
                previous[String(id).toLowerCase()] = { value: v.value ?? v.Value ?? '', version: v.version ?? v.Version ?? null };
            }
        });

        const values = cf().readCustomFieldValues(customFieldsHost(), customFieldDefinitions);
        for (const entry of values) {
            const before = previous[String(entry.definitionId).toLowerCase()];
            if (String(before ? (before.value ?? '') : '') === entry.value) continue;
            const res = await send(`${endpoint}/${encodeURIComponent(unitId)}/field-values`, 'PUT', {
                definitionId: entry.definitionId,
                value: entry.value.length ? entry.value : null,
                expectedVersion: before ? before.version : null
            });
            if (!res.ok) return await readErrors(res);
        }
        return null;
    };

    const save = async () => {
        if (!validate()) return;
        showAlert(null);
        const btn = byId('ou-submit');
        if (btn) btn.disabled = true;
        try {
            if (!isEdit) {
                // Create carries both lines in one body; the backend accepts them under `…create`.
                const res = await send(endpoint, 'POST', collectPayload());
                if (!res.ok) { showAlert(await readErrors(res)); return; }
                let newId = '';
                try { const json = await res.json(); newId = json.data || json.Data || ''; } catch { /* ignore */ }
                if (newId) {
                    const failures = await saveCustomValues(newId);
                    if (failures) { showAlert(failures); return; }
                }
            } else {
                /*
                 * ⚠ THE LINE CHANGE GOES FIRST, THROUGH ITS OWN ENDPOINT. The unit PUT answers 403 to a changed
                 * line, so sending everything in one call would fail the whole save for an actor who is allowed
                 * to rename. Doing the line first also means its refusal (403, a cycle, a stale structure 409)
                 * stops the save BEFORE anything is written, rather than after.
                 *
                 * Once it lands, the stored lines equal what the form holds, so the unit PUT below reports no
                 * line change and passes under `…update` alone — which is also the path taken when the user
                 * changed no line at all, or holds no grant to change one.
                 */
                if (lineChanged()) {
                    const payload = collectPayload();
                    const lineRes = await send(`${endpoint}/${encodeURIComponent(entityId)}/reporting-lines`, 'PUT', {
                        parentOrganizationUnitId: payload.parentOrganizationUnitId,
                        administrativeParentOrganizationUnitId: payload.administrativeParentOrganizationUnitId
                    });
                    if (!lineRes.ok) { showAlert(await readErrors(lineRes)); return; }
                    loadedLines = {
                        functional: String(payload.parentOrganizationUnitId || ''),
                        administrative: String(payload.administrativeParentOrganizationUnitId || '')
                    };
                }

                const res = await send(`${endpoint}/${encodeURIComponent(entityId)}`, 'PUT', collectPayload());
                if (!res.ok) { showAlert(await readErrors(res)); return; }

                const failures = await saveCustomValues(entityId);
                if (failures) { showAlert(failures); return; }
            }

            try { sessionStorage.setItem('ou-toast', isEdit ? (L.RecordUpdated || '') : (L.RecordCreated || '')); } catch { /* ignore */ }
            window.location.href = '/OrganizationUnits';
        } catch (error) {
            console.error('[OU Form] Save failed.', error);
            showAlert([L.ErrorOccurred || 'An error occurred.']);
        } finally {
            if (btn) btn.disabled = false;
        }
    };

    // ─── Edit pre-populate ───────────────────────────────────────────────────
    const setVal = (id, v) => { const el = byId(id); if (el && v != null) el.value = v; };
    const setSelect = (id, v) => {
        const el = byId(id);
        if (!el || v == null) return;
        el.value = v;
        if (window.jQuery?.fn?.select2) window.jQuery(el).val(String(v)).trigger('change');
    };
    /*
     * Match a value the API sent against the option values this select actually offers, case-insensitively.
     *
     * ⚠ THIS REPLACES A `titleCase()` THAT ONLY WORKED FOR ONE-WORD ENUMS, and MOD-0288-FU03 is where that ran
     * out: `GroupFunction` lower-cased and re-capitalised is "Groupfunction", which matches no option, so the
     * select fell back to empty and the next save silently wrote `Department` — the edit form quietly changing
     * a unit's type. (`HQ` had already needed its own special case for the same reason; a second exception is
     * the signal to stop guessing at the string and ask the select what it has.)
     */
    const optionValueFor = (id, raw) => {
        const el = byId(id);
        const value = String(raw ?? '').trim();
        if (!el || !value.length) return null;
        const match = Array.from(el.options).find((o) => o.value.toLowerCase() === value.toLowerCase());
        return match ? match.value : null;
    };

    const populate = (d) => {
        setVal('ouCode', d.code);
        setVal('ouName', d.name);
        setSelect('ouLegalEntityId', d.legalEntityId);
        setSelect('ouParentId', d.parentOrganizationUnitId);
        // ⚠ NO `|| d.parentOrganizationUnitId` HERE, EVER. A missing administrative line stays missing: the
        // select shows its own "(no administrative line)" option, which is what the record actually says.
        setSelect('ouAdministrativeParentId', d.administrativeParentOrganizationUnitId);
        loadedLines = {
            functional: String(d.parentOrganizationUnitId || ''),
            administrative: String(d.administrativeParentOrganizationUnitId || '')
        };
        const orgUnitType = optionValueFor('ouOrgUnitType', d.orgUnitType);
        if (orgUnitType) byId('ouOrgUnitType').value = orgUnitType;
        else if (d.orgUnitType) console.error(`[OU Form] Unknown org unit type "${d.orgUnitType}" — leaving the select untouched rather than resetting it.`);
        setSelect('ouManagerPositionId', d.managerPositionId);
        setVal('ouDescription', d.description);
        const status = optionValueFor('ouStatus', d.status);
        if (status) byId('ouStatus').value = status;
        setDate('ouEffectiveFrom', d.effectiveFrom);
        setDate('ouEffectiveTo', d.effectiveTo);
    };

    const loadForEdit = async () => {
        try {
            const payload = await fetchJson(`${endpoint}/${encodeURIComponent(entityId)}`);
            populate(payload.data || payload.Data || {});
        } catch (error) {
            console.error('[OU Form] Load for edit failed.', error);
            showAlert([L.ErrorOccurred || '']);
        }
    };

    // ─── FU03: reporting-line permission + custom field values ───────────────
    const customFieldsHost = () => byId('ou-custom-fields');

    /*
     * ⚠ READ-ONLY, NOT HIDDEN, AND NOT EDITABLE-THEN-REFUSED (§9). An actor with `…update` but not
     * `…reporting-line.update` keeps the rest of the form: they may rename this unit, they simply may not
     * re-hang it. Hiding the two selects would also hide WHERE THE UNIT SITS from someone allowed to read it.
     *
     * On CREATE both stay editable: there is no line to change yet, and the backend accepts both lines under
     * `…create`. Disabling them here would refuse in the UI what the server permits.
     */
    const applyReportingLinePermission = () => {
        if (!isEdit || may(PERM_REPORTING_LINE)) return;
        ['ouParentId', 'ouAdministrativeParentId'].forEach((id) => {
            const el = byId(id);
            if (!el) return;
            el.disabled = true;
            if (window.jQuery?.fn?.select2) window.jQuery(el).prop('disabled', true).trigger('change.select2');
        });
    };

    /*
     * Render whatever definitions the tenant authored. A tenant with none — or an actor without
     * `…custom-fields.read` — sees NO section: not an empty card with a heading (§10.6).
     */
    const bootCustomFields = async () => {
        const section = byId('ou-custom-fields-section');
        const host = customFieldsHost();
        if (!section || !host || !cf() || !may(PERM_FIELDS_READ)) return;

        let definitions = [];
        try {
            definitions = unwrapList(await fetchJson(`${endpoint}/field-definitions`));
        } catch (error) {
            // Not silent, and not fatal: the fixed fields still save. A failed definition load must not take
            // the form down with it.
            console.error('[OU Form] Custom field definitions could not be loaded.', error);
            return;
        }

        if (isEdit) {
            try {
                loadedCustomValues = unwrapList(await fetchJson(`${endpoint}/${encodeURIComponent(entityId)}/field-values`));
            } catch (error) {
                console.error('[OU Form] Custom field values could not be loaded.', error);
                loadedCustomValues = [];
            }
        }

        customFieldDefinitions = cf().renderCustomFields(host, definitions, {
            readOnly: !may(PERM_FIELDS_WRITE),
            references: customFieldReferences,
            booleanYes: L.Yes || 'Yes',
            booleanNo: L.No || 'No'
        });

        if (!customFieldDefinitions.length) return;
        section.classList.remove('d-none');
        if (isEdit) cf().writeCustomFieldValues(host, customFieldDefinitions, loadedCustomValues);
    };

    const bindEvents = () => {
        byId('ou-submit')?.addEventListener('click', () => save());
        REQUIRED.forEach((id) => {
            const el = byId(id);
            if (el) ['input', 'change'].forEach((ev) => el.addEventListener(ev, () => el.classList.remove('is-invalid')));
        });
    };

    const init = async () => {
        loadL10n();
        initDatePickers();
        bindEvents();
        await loadLookups();
        if (isEdit) await loadForEdit();
        await bootCustomFields();
        initSelect2();
        applyReportingLinePermission();
        if (!isEdit && parentSeedId) {
            setSelect('ouParentId', parentSeedId);
            if (parentSeedLegalEntityId) setSelect('ouLegalEntityId', parentSeedLegalEntityId);
        }
        updateSaveState();
    };

    init();
})();
