/**
 * SCMM-14-UI (CAND-CAP-0011) Content Studio workspace. Loads a ContentSet draft + its pinned composition template, and
 * drives every mutation through the same-origin proxy — the browser never sees a service URL or token, and no
 * arrangement / cardinality / version-pin logic lives here (the CrmService CQRS is authoritative). Every selection is a
 * searchable select2 (no raw id entry — D14-e). apply-eligibility renders the per-claim snapshot with
 * Eligible / Blocked / Unresolved badges. No freeze / render / release here (SCMM-15/16/17).
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('workspace');
    if (!root) return;
    const setId = root.getAttribute('data-set-id');
    const $ = window.jQuery;
    const L = window.SetWorkspaceL10n || {};
    const api = '/CRM/ContentSets/api';

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const headers = { Accept: 'application/json' };
    const jsonHeaders = { Accept: 'application/json', 'Content-Type': 'application/json' };
    const toast = (m, t) => window.showToast?.(m, t);

    let set = null;
    let template = null;
    const conceptTypeMap = {};   // conceptTypeId -> "code — name"
    const contentMap = {};       // contentId -> { label, translationStatus }
    const claimMap = {};         // claimId -> "code — name"

    const envelope = async res => {
        const body = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data;
    };
    const getJson = async path => envelope(await fetch(`${api}${path}`, { credentials: 'same-origin', headers }));
    const post = async (path, payload) => envelope(await fetch(`${api}${path}`, {
        method: 'POST', credentials: 'same-origin', headers: payload ? jsonHeaders : headers,
        body: payload ? JSON.stringify(payload) : undefined
    }));

    const showError = msg => {
        const el = document.getElementById('wsError');
        if (el) { el.textContent = msg; el.classList.remove('d-none'); }
    };

    // ─── load reference maps (labels; no raw id shown) ──────────────────────────
    const loadMaps = async () => {
        try {
            const types = (await getJson('/concept-types?includeArchived=true'))?.items || [];
            types.forEach(t => { conceptTypeMap[t.conceptTypeId] = [t.conceptTypeCode, t.conceptTypeName].filter(Boolean).join(' — '); });
        } catch (e) { /* labels fall back to a short id */ }
        try {
            const contents = (await getJson('/contents?includeArchived=true'))?.items || [];
            contents.forEach(c => { contentMap[c.contentId] = { label: [c.contentCode, c.contentTitle].filter(Boolean).join(' — '), translationStatus: c.translationStatus }; });
        } catch (e) { /* ignore */ }
        try {
            const claims = (await getJson('/claims?includeArchived=true'))?.items || [];
            claims.forEach(c => { claimMap[c.claimId] = [c.claimCode, c.claimName].filter(Boolean).join(' — '); });
        } catch (e) { /* ignore */ }
    };

    const shortId = id => (id ? String(id).slice(0, 8) : '');
    const contentLabel = id => contentMap[id]?.label || shortId(id);
    const claimLabel = id => claimMap[id] || shortId(id);
    const typeLabel = id => conceptTypeMap[id] || shortId(id);
    const statusLabel = s => L['Status' + (s ? s[0].toUpperCase() + s.slice(1) : '')] || s || '—';

    // ─── template slots (branch/step, or the legacy spine) ──────────────────────
    const slots = () => {
        const out = [];
        if (template?.branches && template.branches.length) {
            template.branches.slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)).forEach(b => {
                (b.steps || []).forEach(s => out.push({
                    branchId: b.branchCode, branchName: b.branchName || b.branchCode,
                    stepId: s.conceptTypeId, min: s.minSelection, max: s.maxSelection
                }));
            });
        } else {
            (template?.orderedConceptTypes || []).forEach(id => out.push({ branchId: null, branchName: null, stepId: id, min: null, max: null }));
        }
        return out;
    };

    const eligibilityBySelection = () => {
        const map = {};
        (set?.eligibilitySnapshot?.items || []).forEach(i => { map[i.selectionId] = i; });
        return map;
    };
    const eligBadge = item => {
        if (!item) return `<span class="badge bg-label-secondary ms-2">${esc(L.NotEvaluated)}</span>`;
        const tone = { eligible: 'success', blocked: 'danger', unresolved: 'warning' }[item.state] || 'secondary';
        const label = { eligible: L.Eligible, blocked: L.Blocked, unresolved: L.Unresolved }[item.state] || item.state;
        const title = item.reason ? ` title="${esc(item.reason)}"` : '';
        return `<span class="badge bg-label-${tone} ms-2"${title}>${esc(label)}</span>`;
    };

    // ─── render ─────────────────────────────────────────────────────────────────
    const renderHeader = () => {
        document.getElementById('wsSetName').textContent = set.setName || set.setCode || '';
        document.getElementById('wsSetCode').textContent = set.setCode || '-';
        const badge = document.getElementById('wsStatusBadge');
        if (badge) { badge.textContent = statusLabel(set.status); badge.classList.remove('d-none'); }
        document.getElementById('wsTemplate').textContent = template
            ? `${[template.chainCode, template.chainName].filter(Boolean).join(' — ')} (v${set.template?.chainVersion || template.chainVersion})`
            : (set.template?.chainVersion ? `v${set.template.chainVersion}` : '—');
        document.getElementById('wsScope').textContent = set.scope ? `v${set.scope.scopeVersion}` : L.NoScope;
        document.getElementById('wsArchivedBanner').classList.toggle('d-none', !set.isArchived);

        const summary = document.getElementById('wsEligibilitySummary');
        const items = set.eligibilitySnapshot?.items || [];
        if (summary) {
            if (!set.eligibilitySnapshot) { summary.classList.add('d-none'); }
            else {
                const count = st => items.filter(i => i.state === st).length;
                summary.classList.remove('d-none');
                summary.innerHTML = `<strong>${esc(L.EligibilitySummary)}</strong> `
                    + `${esc(L.Eligible)}: ${count('eligible')} · ${esc(L.Blocked)}: ${count('blocked')} · ${esc(L.Unresolved)}: ${count('unresolved')}`;
            }
        }
    };

    const slotBodyHtml = (slot, elig) => {
        const readOnly = set.isArchived;
        const key = `${slot.branchId || ''}|${slot.stepId}`;
        const comps = (set.selectedComponents || []).filter(c => c.arrangement.templateStepId === slot.stepId
            && (c.arrangement.branchId || '') === (slot.branchId || ''))
            .sort((a, b) => a.arrangement.position - b.arrangement.position);
        const claims = (set.selectedClaims || []).filter(c => c.arrangement.templateStepId === slot.stepId
            && (c.arrangement.branchId || '') === (slot.branchId || ''))
            .sort((a, b) => a.arrangement.position - b.arrangement.position);

        const compRow = c => {
            const cm = contentMap[c.knowledgeContentId];
            const warn = cm && cm.translationStatus === 'needs_assessment'
                ? `<span class="badge bg-label-warning ms-2" title="${esc(L.NeedsAssessmentWarn)}"><i class="bx bx-error-circle"></i></span>` : '';
            return `<li class="d-flex align-items-center justify-content-between border rounded p-2 mb-1">
                <span class="min-w-0 text-truncate">${esc(contentLabel(c.knowledgeContentId))}
                    <span class="text-muted small">· v${esc(c.contentVersion)} · ${esc(c.languageCode)}</span>${warn}</span>
                <span class="d-flex align-items-center gap-1 flex-shrink-0">
                    <span class="badge bg-label-secondary">#${c.arrangement.position}</span>
                    ${readOnly ? '' : `<button type="button" class="btn btn-icon btn-xs btn-label-secondary ws-move" data-kind="component" data-sel="${esc(c.selectionId)}" data-dir="-1" data-step="${esc(slot.stepId)}" data-branch="${esc(slot.branchId || '')}" data-pos="${c.arrangement.position}" title="↑"><i class="bx bx-up-arrow-alt"></i></button>
                    <button type="button" class="btn btn-icon btn-xs btn-label-secondary ws-move" data-kind="component" data-sel="${esc(c.selectionId)}" data-dir="1" data-step="${esc(slot.stepId)}" data-branch="${esc(slot.branchId || '')}" data-pos="${c.arrangement.position}" title="↓"><i class="bx bx-down-arrow-alt"></i></button>
                    <button type="button" class="btn btn-icon btn-xs btn-label-danger ws-remove" data-kind="component" data-sel="${esc(c.selectionId)}" title="${esc(L.Remove)}"><i class="bx bx-x"></i></button>`}
                </span></li>`;
        };
        const claimRow = c => `<li class="d-flex align-items-center justify-content-between border rounded p-2 mb-1">
                <span class="min-w-0 text-truncate">${esc(claimLabel(c.claimId))}
                    <span class="text-muted small">· v${esc(c.claimVersion)}</span>${eligBadge(elig[c.selectionId])}</span>
                <span class="d-flex align-items-center gap-1 flex-shrink-0">
                    <span class="badge bg-label-secondary">#${c.arrangement.position}</span>
                    ${readOnly ? '' : `<button type="button" class="btn btn-icon btn-xs btn-label-secondary ws-move" data-kind="claim" data-sel="${esc(c.selectionId)}" data-dir="-1" data-step="${esc(slot.stepId)}" data-branch="${esc(slot.branchId || '')}" data-pos="${c.arrangement.position}" title="↑"><i class="bx bx-up-arrow-alt"></i></button>
                    <button type="button" class="btn btn-icon btn-xs btn-label-secondary ws-move" data-kind="claim" data-sel="${esc(c.selectionId)}" data-dir="1" data-step="${esc(slot.stepId)}" data-branch="${esc(slot.branchId || '')}" data-pos="${c.arrangement.position}" title="↓"><i class="bx bx-down-arrow-alt"></i></button>
                    <button type="button" class="btn btn-icon btn-xs btn-label-danger ws-remove" data-kind="claim" data-sel="${esc(c.selectionId)}" title="${esc(L.Remove)}"><i class="bx bx-x"></i></button>`}
                </span></li>`;

        const cardinality = slot.max != null || slot.min != null
            ? `<span class="text-muted small ms-2">${esc(L.Min)}: ${slot.min ?? 0}${slot.max != null ? ` · ${esc(L.Max)}: ${slot.max}` : ''}</span>` : '';

        return `<div class="card mb-3" data-slot="${esc(key)}">
            <div class="card-body p-3">
                <div class="d-flex align-items-center mb-2">
                    <h6 class="mb-0">${slot.branchName ? `<span class="badge bg-label-primary me-2">${esc(slot.branchName)}</span>` : ''}${esc(typeLabel(slot.stepId))}</h6>
                    ${cardinality}
                </div>
                <div class="row g-3">
                    <div class="col-12 col-md-6">
                        <label class="form-label small text-muted">${esc(L.Components)}</label>
                        <ul class="list-unstyled mb-2">${comps.map(compRow).join('') || `<li class="text-muted small">${esc(L.NoComponents)}</li>`}</ul>
                        ${readOnly ? '' : `<select class="form-select form-select-sm ws-add-component" data-step="${esc(slot.stepId)}" data-branch="${esc(slot.branchId || '')}" data-pos="${comps.length}" data-placeholder="${esc(L.ComponentPickerPlaceholder)}"></select>`}
                    </div>
                    <div class="col-12 col-md-6">
                        <label class="form-label small text-muted">${esc(L.Claims)}</label>
                        <ul class="list-unstyled mb-2">${claims.map(claimRow).join('') || `<li class="text-muted small">${esc(L.NoClaims)}</li>`}</ul>
                        ${readOnly ? '' : `<select class="form-select form-select-sm ws-add-claim" data-step="${esc(slot.stepId)}" data-branch="${esc(slot.branchId || '')}" data-pos="${claims.length}" data-placeholder="${esc(L.ClaimPickerPlaceholder)}"></select>`}
                    </div>
                </div>
            </div></div>`;
    };

    const render = () => {
        renderHeader();
        const host = document.getElementById('wsArrangement');
        const elig = eligibilityBySelection();
        const list = slots();
        host.innerHTML = list.length
            ? list.map(s => slotBodyHtml(s, elig)).join('')
            : `<div class="alert alert-secondary">${esc(L.EmptyArrangement)}</div>`;
        host.classList.remove('d-none');
        document.getElementById('wsLoading')?.classList.add('d-none');
        initSlotPickers();
        // Buttons are meaningful only on a live (non-archived) set.
        ['btnApplyEligibility', 'btnCloneSet', 'btnArchiveSet'].forEach(id => {
            const b = document.getElementById(id);
            if (b && id !== 'btnCloneSet') b.classList.toggle('d-none', set.isArchived);
        });
    };

    // ─── select2 add-pickers (ajax, name-search only) ───────────────────────────
    const ajaxSelect = (el, url, map) => {
        if (!$ || !$.fn.select2) return;
        $(el).select2({
            width: '100%',
            placeholder: el.getAttribute('data-placeholder') || '',
            allowClear: true,
            minimumInputLength: 0,
            ajax: {
                dataType: 'json', delay: 250,
                transport: (params, success, failure) => {
                    params.url = `${api}${url}?search=${encodeURIComponent(params.data.term || '')}&includeArchived=false`;
                    const req = $.ajax(params); req.then(success); req.fail(failure); return req;
                },
                data: params => ({ term: params.term || '' }),
                processResults: body => {
                    const items = (body && body.data && (body.data.items || body.data)) || [];
                    return { results: (Array.isArray(items) ? items : []).map(map).filter(r => r.id) };
                }
            }
        });
    };
    const initSlotPickers = () => {
        if (!$ || !$.fn.select2) return;
        root.querySelectorAll('.ws-add-component').forEach(el => {
            ajaxSelect(el, '/contents', r => ({ id: r.contentId, text: [r.contentCode, r.contentTitle].filter(Boolean).join(' — ') }));
            $(el).off('select2:select').on('select2:select', function (e) {
                const contentId = e.params.data.id;
                $(this).val(null).trigger('change');
                addComponent(el.getAttribute('data-step'), el.getAttribute('data-branch'), Number(el.getAttribute('data-pos')) || 0, contentId);
            });
        });
        root.querySelectorAll('.ws-add-claim').forEach(el => {
            ajaxSelect(el, '/claims', r => ({ id: r.claimId, text: [r.claimCode, r.claimName].filter(Boolean).join(' — ') }));
            $(el).off('select2:select').on('select2:select', function (e) {
                const claimId = e.params.data.id;
                $(this).val(null).trigger('change');
                addClaim(el.getAttribute('data-step'), el.getAttribute('data-branch'), Number(el.getAttribute('data-pos')) || 0, claimId);
            });
        });
    };

    // ─── mutations (proxy → reload → render) ────────────────────────────────────
    const reload = async () => { set = await getJson(`/content-sets/${setId}`); render(); };
    const guard = async (fn, okMsg) => {
        try { await fn(); if (okMsg) toast(okMsg, 'success'); await reload(); }
        catch (e) { toast(e.message || L.ErrorState, 'error'); }
    };

    const addComponent = (stepId, branchId, position, contentId) => guard(() =>
        post(`/content-sets/${setId}/components`, { knowledgeContentId: contentId, templateStepId: stepId, branchId: branchId || null, position }));
    const addClaim = (stepId, branchId, position, claimId) => guard(() =>
        post(`/content-sets/${setId}/claims`, { claimId, templateStepId: stepId, branchId: branchId || null, position }));

    root.addEventListener('click', event => {
        const rm = event.target.closest('.ws-remove');
        if (rm) {
            const kind = rm.getAttribute('data-kind'); const sel = rm.getAttribute('data-sel');
            guard(() => post(`/content-sets/${setId}/${kind === 'claim' ? 'claims' : 'components'}/${sel}/remove`, null));
            return;
        }
        const mv = event.target.closest('.ws-move');
        if (mv) {
            const kind = mv.getAttribute('data-kind'); const sel = mv.getAttribute('data-sel');
            const step = mv.getAttribute('data-step'); const branch = mv.getAttribute('data-branch');
            const pos = Number(mv.getAttribute('data-pos')) || 0; const dir = Number(mv.getAttribute('data-dir')) || 0;
            const next = Math.max(0, pos + dir);
            guard(() => post(`/content-sets/${setId}/${kind === 'claim' ? 'claims' : 'components'}/${sel}/arrange`,
                { templateStepId: step, branchId: branch || null, position: next }));
        }
    });

    // Header buttons.
    document.getElementById('btnArchiveSet')?.addEventListener('click', () => {
        const run = () => guard(() => post(`/content-sets/${setId}/archive`, null), L.RecordArchived)
            .then(() => { window.location.href = '/CRM/ContentSets'; });
        window.showConfirm ? window.showConfirm(L.ArchiveSetConfirm, run, { type: 'warning' }) : (window.confirm(L.ArchiveSetConfirm) && run());
    });
    document.getElementById('btnApplyEligibility')?.addEventListener('click', () =>
        guard(() => post(`/content-sets/${setId}/apply-eligibility`, null), L.EligibilityApplied));
    document.getElementById('btnCloneSet')?.addEventListener('click', async () => {
        const code = window.prompt(L.CloneSetCodePrompt, `${set.setCode}-COPY`);
        if (!code) return;
        const name = window.prompt(L.CloneSetNamePrompt, `${set.setName} (copy)`) || undefined;
        try {
            const newId = await post(`/content-sets/${setId}/clone`, { newSetCode: code, newSetName: name });
            toast(L.RecordCloned, 'success');
            if (newId) window.location.href = `/CRM/ContentSets/Edit/${newId}`;
        } catch (e) { toast(e.message || L.ErrorState, 'error'); }
    });

    const init = async () => {
        try {
            set = await getJson(`/content-sets/${setId}`);
            if (set.template?.conceptChainTemplateId) {
                try { template = await getJson(`/templates/${set.template.conceptChainTemplateId}`); } catch (e) { template = null; }
            }
            await loadMaps();
            render();
        } catch (error) {
            document.getElementById('wsLoading')?.classList.add('d-none');
            showError(error.message || L.ErrorState);
        }
    };

    init();
})(window, document);
