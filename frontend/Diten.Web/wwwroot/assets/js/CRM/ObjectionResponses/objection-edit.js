/**
 * WP-VW-W3 (W3-WEB-c) — A2 · the editor of a new answer / a draft.
 *  - Product (MDM), category (BRD codes + labels), question ≤ 500, answer ≤ 4000 with live counters.
 *  - Supporting claims: ONLY the selected product's approved, live claims (ObjectionCore.claimOptions), at most 10; a
 *    product change drops the claims of the old product.
 *  - "Taslağı kaydet" saves a half-written draft (K-A3; the product is the one required field). "Onaya gönder" saves and
 *    submits — CRM checks completeness (objection_response_incomplete) and the claims (objection_claim_not_approved /
 *    objection_claim_product_mismatch). No one-click "Onayla" here (K-A2).
 *  - The update carries expectedVersion: someone else's save answers 409 (Err_version_conflict).
 */
(function (window, document) {
    'use strict';
    const C = window.ObjectionCore;
    const P = window.ObjectionPage;
    const root = document.getElementById('orEditRoot');
    if (!C || !P || !root) return;
    const L = P.L;
    const api = P.api(root.dataset.apiBase);
    const canSubmit = root.dataset.canSubmit === 'true';
    const canReadClaims = root.dataset.canReadClaims === 'true';
    const canReadProducts = root.dataset.canReadProducts === 'true';
    const $ = id => document.getElementById(id);

    const state = {
        id: root.dataset.id || null,
        version: null,
        dto: null,
        productId: root.dataset.productId || '',
        claims: [],
        selected: [],
        categories: [],
        dirty: false,
        busy: false
    };

    const markDirty = () => { state.dirty = true; };
    window.addEventListener('beforeunload', e => {
        if (!state.dirty) return;
        e.preventDefault();
        e.returnValue = L.UnsavedChanges || '';
    });

    // ---------------- render ----------------

    const renderCounters = () => {
        const q = C.counter($('orQuestion').value, C.MAX_QUESTION);
        const a = C.counter($('orAnswer').value, C.MAX_ANSWER);
        $('orQuestionCount').textContent = q.text;
        $('orQuestionCount').classList.toggle('text-danger', q.over);
        $('orAnswerCount').textContent = a.text;
        $('orAnswerCount').classList.toggle('text-danger', a.over);
        $('orClaimsCount').textContent = C.fmt(L.ClaimsSelected || '{0}', state.selected.length, C.MAX_CLAIMS);
    };

    const renderPreview = () => {
        const refs = state.selected.map(id => state.claims.find(c => c.id === id)).filter(Boolean).map(P.claimRef);
        P.renderPreview($('orQuestion').value.trim(), $('orAnswer').value.trim(), refs);
    };

    const renderClaims = () => {
        const host = $('orClaims');
        if (!canReadClaims) { host.innerHTML = `<div class="text-muted small">${P.esc(L.ClaimsUnavailable || '')}</div>`; return; }
        if (!state.productId) { host.innerHTML = `<div class="text-muted small">${P.esc(L.ClaimsPickProduct || '')}</div>`; return; }
        const options = C.claimOptions(state.claims, state.productId);
        if (options.length === 0) { host.innerHTML = `<div class="text-muted small">${P.esc(L.ClaimsNone || '')}</div>`; return; }
        const atLimit = state.selected.length >= C.MAX_CLAIMS;
        host.innerHTML = options.map(c => {
            const checked = state.selected.indexOf(c.id) > -1;
            const disabled = !checked && atLimit;
            return `<label class="border rounded p-3 d-flex gap-3 align-items-start or-claim ${checked ? 'is-selected' : ''} ${disabled ? 'opacity-50' : ''}">
                <input type="checkbox" class="form-check-input mt-1 js-claim" value="${P.esc(c.id)}" ${checked ? 'checked' : ''} ${disabled ? 'disabled' : ''}>
                <span class="min-w-0">
                    <span class="d-flex flex-wrap gap-2 align-items-center"><code class="small">${P.esc(c.code || '')}</code><span class="badge bg-label-success">${P.esc(L.Status_approved || '')}</span></span>
                    <span class="d-block text-heading text-break">${P.esc(c.text || '')}</span>
                    ${c.name ? `<small class="text-muted d-block">${P.esc(c.name)}</small>` : ''}
                </span>
            </label>`;
        }).join('') + (atLimit ? `<small class="text-warning">${P.esc(C.fmt(L.ClaimsMax || '', C.MAX_CLAIMS))}</small>` : '');
        host.querySelectorAll('.js-claim').forEach(box => box.addEventListener('change', () => {
            const next = C.toggleClaim(state.selected, box.value);
            state.selected = next.selected;
            markDirty();
            renderClaims();
            renderCounters();
            renderPreview();
        }));
    };

    const renderMeta = () => {
        const d = state.dto;
        const names = (d && d.displayNames) || {};
        $('orMetaCreated').textContent = d ? C.personName(d.createdBy, names, L.UnknownUser) || '—' : '—';
        $('orMetaSaved').textContent = d ? P.dateTime(d.updatedAt || d.createdAt) || '—' : '—';
        const badge = $('orStatusBadge');
        badge.textContent = P.statusLabel(d ? d.status : 'draft');
        const product = $('orProduct');
        $('orCrumbProduct').textContent = (d && d.productName) || (product.selectedOptions[0] && product.value ? product.selectedOptions[0].textContent : '');
    };

    const fillSelect = (select, rows, placeholder, value) => {
        select.innerHTML = `<option value="">${P.esc(placeholder || '')}</option>`
            + rows.map(r => `<option value="${P.esc(r.value)}" ${r.value === value ? 'selected' : ''}>${P.esc(r.text)}</option>`).join('');
    };

    // ---------------- loading ----------------

    const loadClaims = async () => {
        state.claims = [];
        if (!canReadClaims || !state.productId) return;
        try {
            state.claims = (await api.get(`/lookups/claims?productId=${encodeURIComponent(state.productId)}&scope=approved`)) || [];
        } catch (error) {
            P.showAlert($('orAlert'), error);
        }
    };

    const loadProducts = async () => {
        const select = $('orProduct');
        let rows = [];
        if (canReadProducts) {
            try { rows = ((await api.get('/lookups/products')) || []).map(p => ({ value: p.id, text: p.name || p.code || p.id })); }
            catch (error) { P.showAlert($('orAlert'), L.ProductsUnavailable || error); }
        }
        // The record's own product stays selectable even when the catalogue cannot be read.
        if (state.productId && !rows.some(r => r.value === state.productId)) {
            rows.unshift({ value: state.productId, text: (state.dto && state.dto.productName) || state.productId });
        }
        fillSelect(select, rows, L.SelectProduct, state.productId);
        select.disabled = rows.length === 0;
    };

    const loadCategories = async () => {
        const select = $('orCategory');
        try { state.categories = await P.loadCategories(root.dataset.apiBase); }
        catch (error) { state.categories = []; P.showAlert($('orAlert'), L.CategoriesUnavailable || error); }
        fillSelect(select, state.categories.map(c => ({ value: c.code, text: c.label })), L.SelectCategory, state.dto ? state.dto.categoryCode : '');
    };

    const load = async () => {
        P.hideAlert($('orAlert'));
        if (state.id) {
            try {
                state.dto = await api.get(`/objection-responses/${state.id}`);
            } catch (error) {
                P.showAlert($('orAlert'), error.status === 404 ? L.NotFound : error);
                return;
            }
            if (!state.dto.abilities || !state.dto.abilities.canEdit) {
                window.location.href = `/CRM/ObjectionResponses/${encodeURIComponent(state.id)}`;
                return;
            }
            state.version = state.dto.version;
            state.productId = state.dto.productId;
            state.selected = (state.dto.claimIds || []).slice();
            $('orQuestion').value = state.dto.question || '';
            $('orAnswer').value = state.dto.answer || '';
        }
        await Promise.all([loadProducts(), loadCategories(), loadClaims()]);
        // a claim that is no longer approved / of this product is not offered and not re-sent
        const offered = C.claimOptions(state.claims, state.productId).map(c => c.id);
        if (canReadClaims && state.productId) state.selected = state.selected.filter(id => offered.indexOf(id) > -1);
        renderClaims();
        renderCounters();
        renderPreview();
        renderMeta();
        state.dirty = false;
    };

    // ---------------- writes ----------------

    const formValues = () => ({
        productId: $('orProduct').value || null,
        categoryCode: $('orCategory').value,
        question: $('orQuestion').value,
        answer: $('orAnswer').value,
        claimIds: state.selected
    });

    /** Saves the draft; returns the record id (a new record is created first). */
    const save = async () => {
        const values = formValues();
        const productError = $('orProductError');
        if (!values.productId) {
            productError.textContent = L.ProductRequired || '';
            productError.classList.remove('d-none');
            throw null;
        }
        productError.classList.add('d-none');
        if (!state.id) {
            const id = await api.post('/objection-responses', C.editBody(values, null));
            state.id = typeof id === 'string' ? id : (id && (id.id || id.value)) || null;
        } else {
            await api.put(`/objection-responses/${state.id}`, C.editBody(values, state.version));
        }
        state.dirty = false;
        return state.id;
    };

    const busy = on => {
        state.busy = on;
        ['orSaveDraft', 'orSubmit'].forEach(id => { const b = $(id); if (b) b.disabled = on; });
    };

    const onSave = async () => {
        if (state.busy) return;
        busy(true);
        P.hideAlert($('orAlert'));
        try {
            const wasNew = !root.dataset.id;
            const id = await save();
            P.toast(L.Saved);
            if (wasNew) { window.location.href = `/CRM/ObjectionResponses/Edit/${encodeURIComponent(id)}`; return; }
            state.dto = await api.get(`/objection-responses/${id}`);
            state.version = state.dto.version;
            renderMeta();
        } catch (error) {
            if (error) P.showAlert($('orAlert'), error);
        } finally {
            busy(false);
        }
    };

    const onSubmit = async () => {
        if (state.busy || !canSubmit) return;
        busy(true);
        P.hideAlert($('orAlert'));
        try {
            const id = await save();
            await api.post(`/objection-responses/${id}/submit`);
            P.toast(L.Submitted);
            window.location.href = `/CRM/ObjectionResponses/${encodeURIComponent(id)}`;
        } catch (error) {
            if (error) P.showAlert($('orAlert'), error);
            // a created-then-refused draft continues as an edit of that record
            if (state.id && !root.dataset.id) root.dataset.id = state.id;
        } finally {
            busy(false);
        }
    };

    // ---------------- wiring ----------------

    $('orProduct').addEventListener('change', async () => {
        state.productId = $('orProduct').value;
        markDirty();
        await loadClaims();
        const offered = C.claimOptions(state.claims, state.productId).map(c => c.id);
        state.selected = state.selected.filter(id => offered.indexOf(id) > -1);
        renderClaims();
        renderCounters();
        renderPreview();
        renderMeta();
    });
    $('orCategory').addEventListener('change', markDirty);
    ['orQuestion', 'orAnswer'].forEach(id => $(id).addEventListener('input', () => { markDirty(); renderCounters(); renderPreview(); }));
    $('orSaveDraft').addEventListener('click', onSave);
    if ($('orSubmit')) $('orSubmit').addEventListener('click', onSubmit);
    $('orCancel').addEventListener('click', e => {
        if (state.dirty && !window.confirm(L.UnsavedChanges || '')) { e.preventDefault(); return; }
        state.dirty = false;
    });

    window.ObjectionEdit = Object.freeze({ state, reload: load });
    load();
})(window, document);
