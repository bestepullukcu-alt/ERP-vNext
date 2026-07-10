'use strict';

// MOD-0288 Phase 2 — Org Unit compact two-level create/edit form. Legal Entity / Parent / Manager Position are
// searchable select2 (fed via the Diten.Web proxy so the HttpOnly token is attached server-side). Dates use
// flatpickr in the request culture's format; stored/sent value stays ISO. Create POSTs to /OrganizationUnits/api,
// edit PUTs to /OrganizationUnits/api/{id}; on success it returns to the list with a toast.
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

        // Parent options exclude self (an org unit cannot be its own parent).
        const parents = (orgUnits || []).filter((u) => !(isEdit && String(u.id || u.Id) === String(entityId)));
        fillSelect('ouParentId', parents, (u) => ({
            value: u.id || u.Id,
            text: codeName(u.code || u.Code, u.name || u.Name)
        }));

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
        jq('#ouLegalEntityId, #ouParentId, #ouManagerPositionId').each(function () {
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
        let ok = true;
        REQUIRED.forEach((id) => {
            const el = byId(id);
            if (!isFilled(id)) { ok = false; el?.classList.add('is-invalid'); }
            else el?.classList.remove('is-invalid');
        });
        if (!ok) showAlert([L.RequiredField || 'Required fields are missing.']);
        return ok;
    };

    const valueOrNull = (id) => { const v = trim(byId(id)?.value); return v.length ? v : null; };

    const collectPayload = () => ({
        code: trim(byId('ouCode')?.value),
        name: trim(byId('ouName')?.value),
        legalEntityId: valueOrNull('ouLegalEntityId'),
        parentOrganizationUnitId: valueOrNull('ouParentId'),
        orgUnitType: byId('ouOrgUnitType')?.value || 'Department',
        managerPositionId: valueOrNull('ouManagerPositionId'),
        description: valueOrNull('ouDescription'),
        status: byId('ouStatus')?.value || 'Active',
        effectiveFrom: dateOrNull('ouEffectiveFrom'),
        effectiveTo: dateOrNull('ouEffectiveTo')
    });

    // ─── Save ────────────────────────────────────────────────────────────────
    const save = async () => {
        if (!validate()) return;
        showAlert(null);
        const url = isEdit ? `${endpoint}/${encodeURIComponent(entityId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';
        const btn = byId('ou-submit');
        if (btn) btn.disabled = true;
        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
                body: JSON.stringify(collectPayload())
            });
            if (res.ok) {
                try { sessionStorage.setItem('ou-toast', isEdit ? (L.RecordUpdated || '') : (L.RecordCreated || '')); } catch { /* ignore */ }
                window.location.href = '/OrganizationUnits';
                return;
            }
            let errors = [];
            try { const json = await res.json(); errors = (json.errors || json.Errors || []); } catch { /* non-JSON */ }
            showAlert(errors.length ? errors : [L.ErrorOccurred || 'An error occurred.']);
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
    const titleCase = (s) => { const v = String(s || '').toLowerCase(); return v.charAt(0).toUpperCase() + v.slice(1); };

    const populate = (d) => {
        setVal('ouCode', d.code);
        setVal('ouName', d.name);
        setSelect('ouLegalEntityId', d.legalEntityId);
        setSelect('ouParentId', d.parentOrganizationUnitId);
        if (d.orgUnitType) byId('ouOrgUnitType').value = titleCase(d.orgUnitType) === 'Hq' ? 'HQ' : titleCase(d.orgUnitType);
        setSelect('ouManagerPositionId', d.managerPositionId);
        setVal('ouDescription', d.description);
        if (d.status) byId('ouStatus').value = titleCase(d.status);
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
        initSelect2();
        if (!isEdit && parentSeedId) {
            setSelect('ouParentId', parentSeedId);
            if (parentSeedLegalEntityId) setSelect('ouLegalEntityId', parentSeedLegalEntityId);
        }
        updateSaveState();
    };

    init();
})();
