'use strict';

// MOD-0288 Phase 4 — Position Assignment compact two-level create/edit form. Position + User are searchable
// select2 (the User's GUID is the option value, never typed — no raw GUID input). Dates use flatpickr in the
// request culture. There is no backend GetById for assignments, so edit resolves the record from the list.
// Create POSTs to /PositionAssignments/api, edit PUTs to /PositionAssignments/api/{id}. The only 409 returned is
// the one-primary-per-position conflict → shown as a localized message.
(function () {
    const page = document.getElementById('a-form-page');
    if (!page) return;

    const endpoint = '/PositionAssignments/api';
    const entityId = page.dataset.aId || '';
    const isEdit = page.dataset.aMode === 'edit';
    let L = {};

    const REQUIRED = ['aPositionId', 'aUserId', 'aEffectiveFrom'];
    const DATE_FIELDS = ['aEffectiveFrom', 'aEffectiveTo'];

    const byId = (id) => document.getElementById(id);
    const trim = (v) => (typeof v === 'string' ? v.trim() : '');
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const loadL10n = () => {
        const node = byId('assignment-form-l10n');
        if (!node) return;
        try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[Assignment Form] L10n parse failed.', e); }
    };

    const showAlert = (message) => {
        const el = byId('a-form-alert');
        if (!el) return;
        const list = (Array.isArray(message) ? message : [message]).filter(Boolean);
        if (!list.length) { el.classList.add('d-none'); el.innerHTML = ''; return; }
        el.innerHTML = list.map((m) => `<div>${escapeHtml(m)}</div>`).join('');
        el.classList.remove('d-none');
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#a-form-page input[name="__RequestVerificationToken"]')?.value || '';

    // Robust unwrap: plain array, Response<T> ({data:[...]}), paginated ({items:[...]} — the users feed), or nested.
    const unwrapList = (payload) => {
        if (Array.isArray(payload)) return payload;
        const data = payload?.data ?? payload?.Data;
        if (Array.isArray(data)) return data;
        return payload?.items || payload?.Items || data?.items || data?.Items || [];
    };
    const fetchList = (url) => fetch(url, { headers: getAuthHeaders() }).then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);

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

    const positionLabel = (p) => {
        const code = p.code || p.Code || '';
        const name = p.name || p.Name || '';
        return { value: p.id || p.Id, text: code ? `${code} — ${name}` : name };
    };
    const userLabel = (u) => {
        const id = u.id || u.Id || u.userId || u.UserId;
        const name = u.displayName || u.DisplayName || u.fullName || u.FullName
            || [u.firstName || u.FirstName, u.lastName || u.LastName].filter(Boolean).join(' ')
            || u.userName || u.UserName || '';
        const email = u.email || u.Email || '';
        const text = name && email ? `${name} — ${email}` : (name || email || id);
        return { value: id, text };
    };

    let usersLoaded = [];
    const loadLookups = async () => {
        const [positions, users] = await Promise.all([
            fetchList(`${endpoint}/positions`),
            fetchList(`${endpoint}/users`)
        ]);
        usersLoaded = users || [];
        fillSelect('aPositionId', positions, positionLabel);
        fillSelect('aUserId', users, userLabel);
    };

    const initSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        // Pass each select's own empty first-option text as the select2 placeholder so the unselected state renders
        // in the muted placeholder colour (matching the text inputs) instead of the darker option-text colour.
        jq('#aPositionId, #aUserId').each(function () {
            const ph = jq(this).find('option[value=""]').first().text() || '';
            jq(this).select2({ width: '100%', placeholder: ph });
        });
        jq('#aPositionId, #aUserId').on('change', function () { this.classList.remove('is-invalid'); });
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
        const allocRaw = trim(byId('aAllocationPercent')?.value);
        return {
            positionId: valueOrNull('aPositionId'),
            userId: valueOrNull('aUserId'),
            effectiveFrom: dateOrNull('aEffectiveFrom'),
            effectiveTo: dateOrNull('aEffectiveTo'),
            assignmentType: byId('aAssignmentType')?.value || 'Primary',
            allocationPercent: allocRaw.length ? Number(allocRaw) : null,
            reason: byId('aReason')?.value || 'Hire',
            notes: valueOrNull('aNotes'),
            isCancelled: !!byId('aIsCancelled')?.checked
        };
    };

    // ─── Save ────────────────────────────────────────────────────────────────
    const save = async () => {
        if (!validate()) return;
        showAlert(null);
        const url = isEdit ? `${endpoint}/${encodeURIComponent(entityId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';
        const btn = byId('a-submit');
        if (btn) btn.disabled = true;
        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
                body: JSON.stringify(collectPayload())
            });
            if (res.ok) {
                try { sessionStorage.setItem('a-toast', isEdit ? (L.RecordUpdated || '') : (L.RecordCreated || '')); } catch { /* ignore */ }
                window.location.href = '/PositionAssignments';
                return;
            }
            // The only 409 from create/update is the one-primary-per-position conflict → localized message.
            if (res.status === 409) { showAlert([L.OnePrimaryError || 'A primary assignment already covers this period.']); return; }
            let errors = [];
            try { const json = await res.json(); errors = (json.errors || json.Errors || []); } catch { /* non-JSON */ }
            showAlert(errors.length ? errors : [L.ErrorOccurred || 'An error occurred.']);
        } catch (error) {
            console.error('[Assignment Form] Save failed.', error);
            showAlert([L.ErrorOccurred || 'An error occurred.']);
        } finally {
            if (btn) btn.disabled = false;
        }
    };

    // ─── Edit pre-populate (resolve from the list — no GetById endpoint) ──────
    const setSelect = (id, v) => {
        const el = byId(id);
        if (!el || v == null) return;
        el.value = v;
        if (window.jQuery?.fn?.select2) window.jQuery(el).val(String(v)).trigger('change');
    };
    const titleCase = (s) => { const v = String(s || '').toLowerCase(); return v.charAt(0).toUpperCase() + v.slice(1); };

    const populate = (d) => {
        setSelect('aPositionId', d.positionId);
        setSelect('aUserId', d.userId);
        if (d.assignmentType) byId('aAssignmentType').value = titleCase(d.assignmentType);
        setDate('aEffectiveFrom', d.effectiveFrom);
        setDate('aEffectiveTo', d.effectiveTo);
        if (d.allocationPercent != null) byId('aAllocationPercent').value = d.allocationPercent;
        if (d.reason) byId('aReason').value = titleCase(d.reason);
        if (d.notes != null) byId('aNotes').value = d.notes;
        if (byId('aIsCancelled')) byId('aIsCancelled').checked = !!(d.isCancelled ?? d.IsCancelled);
    };

    const loadForEdit = async () => {
        try {
            const all = await fetchList(endpoint);
            const match = (all || []).find((x) => String(x.id || x.Id) === String(entityId));
            if (match) populate(match); else showAlert([L.ErrorOccurred || '']);
        } catch (error) {
            console.error('[Assignment Form] Load for edit failed.', error);
            showAlert([L.ErrorOccurred || '']);
        }
    };

    const bindEvents = () => {
        byId('a-submit')?.addEventListener('click', () => save());
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
