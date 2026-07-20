'use strict';

// MOD-0288 Phase 3 — Position compact two-level create/edit form. Organization Unit + Reports-To Position are
// searchable select2 (fed via the Diten.Web proxy). Dates use flatpickr in the request culture; stored/sent value
// stays ISO. Create POSTs to /Positions/api, edit PUTs to /Positions/api/{id}; on success returns to the list.
(function () {
    const page = document.getElementById('p-form-page');
    if (!page) return;

    const endpoint = '/Positions/api';
    const entityId = page.dataset.pId || '';
    const isEdit = page.dataset.pMode === 'edit';
    let L = {};

    const REQUIRED = ['pCode', 'pName', 'pOrgUnitId'];
    const DATE_FIELDS = ['pEffectiveFrom', 'pEffectiveTo'];

    const byId = (id) => document.getElementById(id);
    const trim = (v) => (typeof v === 'string' ? v.trim() : '');
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const loadL10n = () => {
        const node = byId('position-form-l10n');
        if (!node) return;
        try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[Position Form] L10n parse failed.', e); }
    };

    const showAlert = (message) => {
        const el = byId('p-form-alert');
        if (!el) return;
        const list = (Array.isArray(message) ? message : [message]).filter(Boolean);
        if (!list.length) { el.classList.add('d-none'); el.innerHTML = ''; return; }
        el.innerHTML = list.map((m) => `<div>${escapeHtml(m)}</div>`).join('');
        el.classList.remove('d-none');
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#p-form-page input[name="__RequestVerificationToken"]')?.value || '';

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };
    const fetchJson = (url) => fetch(url, { headers: getAuthHeaders() }).then((r) => r.ok ? r.json() : Promise.reject(r));

    // ─── Lookups + select2 ───────────────────────────────────────────────────
    const fillSelect = (selectId, items, mapItem) => {
        const select = byId(selectId);
        if (!select) return;
        const first = select.querySelector('option');
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
        const [orgUnits, positions] = await Promise.all([
            fetchJson(`${endpoint}/org-units`).then(unwrapList).catch(() => []),
            fetchJson(endpoint).then(unwrapList).catch(() => [])
        ]);
        fillSelect('pOrgUnitId', orgUnits, (u) => ({ value: u.id || u.Id, text: codeName(u.code || u.Code, u.name || u.Name) }));
        // Reports-To excludes self (a position cannot report to itself).
        const reports = (positions || []).filter((p) => !(isEdit && String(p.id || p.Id) === String(entityId)));
        fillSelect('pReportsToId', reports, (p) => ({ value: p.id || p.Id, text: codeName(p.code || p.Code, p.name || p.Name) }));
    };

    const initSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        // Pass each select's own empty first-option text as the select2 placeholder so the unselected state renders
        // in the muted placeholder colour (matching the text inputs) instead of the darker option-text colour.
        jq('#pOrgUnitId, #pReportsToId').each(function () {
            const ph = jq(this).find('option[value=""]').first().text() || '';
            jq(this).select2({ width: '100%', placeholder: ph });
        });
        jq('#pOrgUnitId').on('change', function () { this.classList.remove('is-invalid'); });
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

    const collectPayload = () => {
        const fteRaw = trim(byId('pFte')?.value);
        return {
            code: trim(byId('pCode')?.value),
            name: trim(byId('pName')?.value),
            organizationUnitId: valueOrNull('pOrgUnitId'),
            reportsToPositionId: valueOrNull('pReportsToId'),
            jobTitle: valueOrNull('pJobTitle'),
            positionType: byId('pPositionType')?.value || 'Permanent',
            fte: fteRaw.length ? Number(fteRaw) : null,
            status: byId('pStatus')?.value || 'Draft',
            effectiveFrom: dateOrNull('pEffectiveFrom'),
            effectiveTo: dateOrNull('pEffectiveTo')
        };
    };

    // ─── Save ────────────────────────────────────────────────────────────────
    const save = async () => {
        if (!validate()) return;
        showAlert(null);
        const url = isEdit ? `${endpoint}/${encodeURIComponent(entityId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';
        const btn = byId('p-submit');
        if (btn) btn.disabled = true;
        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
                body: JSON.stringify(collectPayload())
            });
            if (res.ok) {
                try { sessionStorage.setItem('p-toast', isEdit ? (L.RecordUpdated || '') : (L.RecordCreated || '')); } catch { /* ignore */ }
                window.location.href = '/Positions';
                return;
            }
            let errors = [];
            try { const json = await res.json(); errors = (json.errors || json.Errors || []); } catch { /* non-JSON */ }
            showAlert(errors.length ? errors : [L.ErrorOccurred || 'An error occurred.']);
        } catch (error) {
            console.error('[Position Form] Save failed.', error);
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
        setVal('pCode', d.code);
        setVal('pName', d.name);
        setSelect('pOrgUnitId', d.organizationUnitId);
        setSelect('pReportsToId', d.reportsToPositionId);
        setVal('pJobTitle', d.jobTitle);
        if (d.positionType) byId('pPositionType').value = titleCase(d.positionType);
        if (d.fte != null) setVal('pFte', d.fte);
        if (d.status) byId('pStatus').value = titleCase(d.status);
        setDate('pEffectiveFrom', d.effectiveFrom);
        setDate('pEffectiveTo', d.effectiveTo);
    };

    const loadForEdit = async () => {
        try {
            const payload = await fetchJson(`${endpoint}/${encodeURIComponent(entityId)}`);
            populate(payload.data || payload.Data || {});
        } catch (error) {
            console.error('[Position Form] Load for edit failed.', error);
            showAlert([L.ErrorOccurred || '']);
        }
    };

    const bindEvents = () => {
        byId('p-submit')?.addEventListener('click', () => save());
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
    };

    init();
})();
