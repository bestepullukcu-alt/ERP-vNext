/**
 * WP-CL-FE-3 — core / local claim page (mockup scenarios 3 / 5 / 6) over the FE-1 v2 proxy (/CRM/Claims/api/v2):
 *  - load / save a draft (create → then the page becomes Edit/{id}; update keeps the unchanged fields)
 *  - scope pickers (MDM product, audience profiles, responsible org unit), qualifiers, local country + text language
 *  - evidence through MOD-0031 via CRM: list (pinned version, quote, supported phrases, superseded / suspended /
 *    expiring warnings), add (two-step modal: document → reference + phrase marking), remove (reason required)
 *  - readiness checklist (live), approval flow preview (lookups/workflow-template), status card
 *  - send for review / withdraw / open new version; CRM [code, message] refusals shown in the reader's language
 * There is no approve action here: a claim is approved only through its MOD-0023 round.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('claimFormRoot');
    if (!root) return;

    const api = '/CRM/Claims/api/v2';
    const $ = window.jQuery;
    const L = (() => { try { return JSON.parse(document.getElementById('claim-form-l10n')?.textContent || '{}'); } catch (e) { return {}; } })();
    const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
    const MAX_SPANS = 10;
    const REVIEW_DOC_STATES = ['suspended', 'retired', 'withdrawn'];

    const state = {
        claimId: root.dataset.claimId || '',
        kind: root.dataset.claimKind === 'local' ? 'local' : 'core',
        claim: null,
        status: 'draft',
        dirty: false,
        codeTouched: false,
        qualifiers: [],
        evidence: null,          // ClaimEvidenceListDto or null (not loaded / unavailable)
        countries: [],
        evidenceTypes: null,     // value_code → name
        product: null,           // { id, text, code }
        existingCodes: null,     // cached for the code suggestion
        modal: { doc: null, spans: [], docs: [] },
        removeLinkId: null
    };

    // ─── helpers ────────────────────────────────────────────────────────────────
    const byId = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const fmt = (template, ...args) => String(template ?? '').replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    const t = key => L[key] || key;
    const toast = (msg, type) => window.showToast?.(msg, type || 'success');
    const date = v => v ? new Date(v).toLocaleDateString() : '';

    const isEditable = () => !state.claimId || (state.status === 'draft' && !state.claim?.isArchived);

    /** CRM refusals are [code, message]; Platform / proxy refusals may be a single message. */
    const readError = async (response, context) => {
        const body = await response.json().catch(() => ({}));
        const errors = Array.isArray(body.errors) ? body.errors.filter(e => typeof e === 'string') : [];
        let code = errors.length > 1 && /^[a-z_]+$/.test(errors[0]) ? errors[0] : null;
        if (!code && context === 'create' && response.status === 409) code = 'duplicate_code';
        const message = (code && L['Err_' + code])
            || (errors.length > 1 ? errors.slice(1).join(' · ') : errors[0])
            || body.message
            || t('ErrorGeneric');
        return Object.assign(new Error(message), { status: response.status, code });
    };
    const request = async (method, url, body, context) => {
        const init = { method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) { init.headers['Content-Type'] = 'application/json'; init.body = JSON.stringify(body); }
        const response = await fetch(url, init);
        if (!response.ok) throw await readError(response, context);
        if (response.status === 204) return null;
        const json = await response.json().catch(() => ({}));
        return json && Object.prototype.hasOwnProperty.call(json, 'data') ? json.data : json;
    };
    const tryGet = async url => { try { return await request('GET', url); } catch (e) { return null; } };

    const showAlert = message => {
        const el = byId('claimFormAlert');
        if (!el) return;
        el.textContent = message || '';
        el.classList.toggle('d-none', !message);
    };

    // ─── lookups ────────────────────────────────────────────────────────────────
    const loadCountries = async () => {
        const data = await tryGet(`${api}/lookups/countries`);
        state.countries = Array.isArray(data) ? data.map(c => ({ code: norm(c.code).toUpperCase(), name: c.name || c.code, languages: c.languages || [] })) : [];
    };
    const loadEvidenceTypes = async () => {
        if (state.evidenceTypes) return state.evidenceTypes;
        const data = await tryGet(`${api}/lookups/evidence-types`);
        state.evidenceTypes = {};
        (Array.isArray(data) ? data : []).forEach(v => { if (v?.code) state.evidenceTypes[v.code] = v.name || v.code; });
        return state.evidenceTypes;
    };
    const fillOptions = (el, options, selected) => {
        if (!el) return;
        const wanted = new Set([].concat(selected || []).map(String));
        el.innerHTML = '<option value=""></option>' + options.map(o =>
            `<option value="${esc(o.value)}"${wanted.has(String(o.value)) ? ' selected' : ''}>${esc(o.text)}</option>`).join('');
    };
    const initSelect2 = (el, extra) => {
        if (!el || !$ || !$.fn.select2) return;
        const $s = $(el);
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2(Object.assign({ width: '100%', placeholder: $s.data('placeholder') || '', allowClear: !el.multiple, dropdownParent: $(document.body) }, extra || {}));
        $s.on('change', () => onFieldChange(el.id));
    };

    const loadAudienceOptions = async selected => {
        const data = await tryGet(`${api}/lookups/audience-profiles?includeArchived=false`);
        const items = Array.isArray(data) ? data : (data?.items || []);
        const el = byId('clmAudience');
        fillOptions(el, items.map(p => ({ value: p.audienceProfileId, text: p.profileName || p.profileCode })), selected);
        if (el) el.querySelector('option[value=""]')?.remove();
        initSelect2(el);
    };
    const loadOrgUnitOptions = async selected => {
        const data = await tryGet(`${api}/lookups/org-units`);
        const items = Array.isArray(data) ? data : (data?.items || []);
        fillOptions(byId('clmOrgUnit'), items.filter(u => u?.id).map(u => ({ value: u.id, text: u.name || u.code })), selected);
        initSelect2(byId('clmOrgUnit'));
    };
    const productLabel = p => {
        const code = norm(p.canonicalCode || p.code);
        const name = norm(p.globalProductName || p.name);
        return code && name ? `${code} — ${name}` : (name || code || p.id);
    };
    const initProductPicker = () => {
        const el = byId('clmProduct');
        if (!el) return;
        if (state.claim?.productId) {
            el.innerHTML = `<option value="${esc(state.claim.productId)}" selected>${esc(state.claim.productDisplay || state.claim.productId)}</option>`;
            state.product = { id: state.claim.productId, text: state.claim.productDisplay || state.claim.productId, code: '' };
        }
        if (!$ || !$.fn.select2) return;
        initSelect2(el, {
            minimumInputLength: 0,
            ajax: {
                url: `${api}/lookups/products`,
                dataType: 'json',
                delay: 250,
                data: params => ({ search: params.term || '', pageNumber: 1, pageSize: 50 }),
                processResults: body => {
                    const disabled = body && body.disabled === true;
                    byId('clmProductUnavailable')?.classList.toggle('d-none', !disabled);
                    const data = body?.data;
                    const items = disabled ? [] : (Array.isArray(data) ? data : (data?.items || []));
                    return { results: items.filter(p => p?.id).map(p => ({ id: p.id, text: productLabel(p), code: norm(p.canonicalCode || p.code) })) };
                }
            }
        });
        $(el).on('select2:select', e => {
            const d = e.params.data || {};
            state.product = { id: d.id, text: d.text, code: d.code || '' };
            void suggestCode();
        });
        $(el).on('select2:clear', () => { state.product = null; });
    };

    // ─── code suggestion CLM-{PRODUCT}-{NN} (create only; never overrides a code the user typed) ─────────────────
    const suggestCode = async () => {
        if (state.claimId || state.codeTouched || !state.product) return;
        const productCode = norm(state.product.code || state.product.text.split('—')[0]).toUpperCase().replace(/[^A-Z0-9]+/g, '');
        if (!productCode) return;
        if (!state.existingCodes) {
            const data = await tryGet(`${api}/claims?includeArchived=true`);
            state.existingCodes = (data?.items || []).map(c => norm(c.claimCode).toUpperCase());
        }
        const prefix = `CLM-${productCode}-`;
        const max = state.existingCodes.filter(c => c.startsWith(prefix))
            .map(c => parseInt(c.slice(prefix.length), 10)).filter(n => Number.isFinite(n)).reduce((a, b) => Math.max(a, b), 0);
        const code = byId('clmCode');
        if (code) code.value = prefix + String(max + 1).padStart(2, '0');
        refreshReadiness();
    };

    // ─── local country + text language ──────────────────────────────────────────
    const fillCountries = selected => {
        fillOptions(byId('clmCountry'), state.countries.map(c => ({ value: c.code, text: `${c.name} (${c.code})` })), selected);
        initSelect2(byId('clmCountry'));
    };
    const fillLanguages = (countryCode, selected) => {
        const country = state.countries.find(c => c.code === norm(countryCode).toUpperCase());
        const langs = country?.languages || [];
        const el = byId('clmTextLanguage');
        if (!el) return;
        el.innerHTML = langs.map(l => `<option value="${esc(l)}"${l === selected ? ' selected' : ''}>${esc(l)}</option>`).join('');
    };

    // ─── fill / read the form ───────────────────────────────────────────────────
    const applyKind = () => {
        const local = state.kind === 'local';
        document.querySelectorAll('.js-local-only').forEach(n => n.classList.toggle('d-none', !local));
        document.querySelectorAll('.js-core-only').forEach(n => n.classList.toggle('d-none', local));
        const label = byId('clmTextLabel');
        if (label) label.innerHTML = `${esc(local ? t('LocalTextLabel') : t('CoreTextLabel'))} <span class="text-danger">*</span>`;
        const hint = byId('clmTextHint');
        if (hint) hint.textContent = local ? t('LocalTextHint') : t('CoreTextHint');
        const flowTitle = byId('clmFlowTitle');
        if (flowTitle) flowTitle.textContent = local ? t('FlowTitleLocal') : t('FlowTitleCore');
    };

    const fillForm = c => {
        byId('clmCode').value = c?.claimCode || '';
        byId('clmName').value = c?.claimName || '';
        byId('clmDescription').value = c?.description || '';
        byId('clmText').value = c?.claimText || '';
        byId('clmVersion').value = c?.claimVersion || '1.0';
        state.qualifiers = [...(c?.qualifiers || [])];
        renderQualifiers();
        updateCount();
    };

    const setStatusBadges = () => {
        ['clmStatus', 'clmStatusCardBadge'].forEach(id => {
            const el = byId(id);
            if (!el) return;
            el.className = 'claim-state state-' + state.status;
            el.textContent = t('State_' + state.status);
        });
    };

    const collect = () => {
        const productId = byId('clmProduct')?.value || null;
        const audience = $ ? ($(byId('clmAudience')).val() || []) : Array.from(byId('clmAudience')?.selectedOptions || []).map(o => o.value);
        const org = byId('clmOrgUnit')?.value || '';
        return {
            claimCode: norm(byId('clmCode')?.value),
            claimName: norm(byId('clmName')?.value),
            claimText: norm(byId('clmText')?.value),
            description: norm(byId('clmDescription')?.value) || null,
            qualifiers: [...state.qualifiers],
            productId,
            productDisplay: productId ? (state.product?.text || state.claim?.productDisplay || null) : null,
            audienceProfileIds: audience.filter(Boolean),
            responsibleOrgUnitId: org || null,
            localCountryCode: state.kind === 'local' ? (byId('clmCountry')?.value || null) : null,
            textLanguageCode: state.kind === 'local' ? (byId('clmTextLanguage')?.value || null) : 'en'
        };
    };

    // ─── qualifiers ─────────────────────────────────────────────────────────────
    const renderQualifiers = () => {
        const host = byId('clmQualifiers');
        if (!host) return;
        const editable = isEditable();
        host.innerHTML = state.qualifiers.map((q, i) => `<li class="claim-qualifier"><span>${esc(q)}</span>`
            + (editable ? `<button type="button" class="btn btn-sm btn-icon btn-text-danger js-remove-qualifier" data-index="${i}" aria-label="${esc(t('RemoveAction'))}" title="${esc(t('RemoveAction'))}"><i class="bx bx-x"></i></button>` : '')
            + '</li>').join('');
    };
    const addQualifier = () => {
        const input = byId('clmQualifierInput');
        const value = norm(input?.value);
        if (!value || !isEditable()) return;
        if (!state.qualifiers.includes(value)) state.qualifiers.push(value);
        input.value = '';
        renderQualifiers();
        markDirty();
    };

    // ─── dirty / readiness / locks ──────────────────────────────────────────────
    const markDirty = () => { state.dirty = true; refreshReadiness(); };
    const onFieldChange = id => {
        if (id === 'clmCountry') {
            fillLanguages(byId('clmCountry')?.value, null);
            void loadFlow();
        }
        markDirty();
    };
    const updateCount = () => {
        const text = byId('clmText')?.value || '';
        const el = byId('clmTextCount');
        if (el) el.textContent = fmt(t('CharCount'), text.length);
    };

    const refreshReadiness = () => {
        const v = collect();
        const evidenceCount = state.evidence?.effectiveCount ?? 0;
        const items = [
            ['ReadyName', !!v.claimName],
            ['ReadyText', !!v.claimText],
            ['ReadyProduct', !!v.productId],
            ['ReadyEvidence', evidenceCount >= 1],
            ['ReadySaved', !!state.claimId && !state.dirty]
        ];
        const host = byId('clmReadiness');
        if (host) {
            host.innerHTML = items.map(([key, ok]) => `<li class="readiness-item"><span>${esc(t(key))}</span>`
                + (ok ? '<i class="bx bx-check-circle ok" aria-hidden="true"></i>'
                    : `<span class="badge rounded-pill missing">${esc(t('ReadyMissing'))}</span>`) + '</li>').join('');
        }
        const required = [v.claimCode, v.claimName, v.claimText, v.productId].filter(x => !x).length
            + (state.kind === 'local' && !v.localCountryCode ? 1 : 0);
        const tracker = byId('claimRequiredTracker');
        if (tracker) {
            tracker.textContent = fmt(t('RequiredCount'), required);
            tracker.classList.toggle('d-none', required === 0 || !isEditable());
        }
    };

    const applyLocks = () => {
        const editable = isEditable();
        setStatusBadges();
        root.querySelectorAll('.card input, .card textarea, .card select').forEach(el => {
            if (el.id === 'clmVersion') return;
            el.disabled = !editable;
        });
        // The code is a business key: editable only before the first save. Country / language fixed after create too.
        const code = byId('clmCode');
        if (code) code.disabled = !!state.claimId;
        const codeHint = byId('clmCodeHint');
        if (codeHint) codeHint.textContent = state.claimId ? t('ClaimCodeLocked') : t('ClaimCodeHint');
        ['clmCountry', 'clmTextLanguage'].forEach(id => { const el = byId(id); if (el && state.claimId) el.disabled = true; });
        if ($ && $.fn.select2) root.querySelectorAll('select.clm-select2').forEach(el => $(el).trigger('change.select2'));
        root.querySelectorAll('.js-editable').forEach(n => n.classList.toggle('d-none', !editable));

        root.querySelectorAll('[data-requires="draft"]').forEach(btn => btn.classList.toggle('d-none', !editable));
        byId('btnWithdrawReview')?.classList.toggle('d-none', state.status !== 'in-review');
        byId('btnOpenNewVersion')?.classList.toggle('d-none',
            !(state.status === 'approved' || state.status === 'review-required') || !!state.claim?.isArchived);

        const band = byId('claimLockBand');
        if (band) {
            const lock = state.claim?.isArchived || state.status === 'inactive' ? ['LockInactive', 'alert-secondary', 'bx-archive']
                : state.status === 'in-review' ? ['LockInReview', 'alert-info', 'bx-time-five']
                    : state.status === 'approved' ? ['LockApproved', 'alert-success', 'bx-lock-alt']
                        : state.status === 'review-required' ? ['LockReviewRequired', 'alert-warning', 'bx-revision'] : null;
            band.className = 'alert d-flex align-items-center gap-2' + (lock ? ' ' + lock[1] : ' d-none');
            band.innerHTML = lock ? `<i class="bx ${lock[2]}"></i><span>${esc(t(lock[0]))}</span>` : '';
        }
        renderQualifiers();
        renderEvidence();
        refreshReadiness();
    };

    // ─── approval flow preview ──────────────────────────────────────────────────
    const flowCode = () => {
        if (state.kind === 'core') return 'CLAIM-CORE-MLR';
        const country = state.claim?.localCountryCode || byId('clmCountry')?.value;
        return country ? `CLAIM-LOCAL-MLR-${norm(country).toUpperCase()}` : null;
    };
    const loadFlow = async () => {
        const host = byId('clmFlow');
        if (!host) return;
        const code = flowCode();
        if (!code) { host.innerHTML = `<div class="text-muted small">${esc(t('FlowPickCountry'))}</div>`; return; }
        host.innerHTML = `<div class="text-muted small">${esc(t('Loading'))}</div>`;
        const data = await tryGet(`${api}/lookups/workflow-template?code=${encodeURIComponent(code)}`);
        if (!data || data.available !== true) {
            const reason = data?.reason || 'WorkflowUnavailable';
            host.innerHTML = `<div class="text-muted small"><i class="bx bx-info-circle me-1"></i>${esc(t('FlowReason_' + reason))}</div>`;
            return;
        }
        let n = 0;
        const steps = (data.stages || []).flatMap(stage => (stage.steps || []));
        host.innerHTML = steps.map(step => {
            n += 1;
            const who = (step.candidates || []).length ? step.candidates.join(', ') : t('FlowNoCandidates');
            return `<div class="flow-step"><span class="flow-step-no">${n}</span><div><div class="fw-medium">${esc(step.name || step.code)}</div>`
                + `<small class="text-muted">${esc(who)}</small></div></div>`;
        }).join('') || `<div class="text-muted small">${esc(t('FlowReason_WorkflowUnavailable'))}</div>`;
    };

    // ─── evidence list ──────────────────────────────────────────────────────────
    const previewUrl = item => {
        if (item.documentKind !== 'controlled' || !item.documentId || !item.documentVersionId) return null;
        const page = norm(item.locator?.page);
        const anchor = /^\d+$/.test(page) ? `#page=${page}` : '';
        return `/DocumentManagementControlledDocuments/preview/${encodeURIComponent(item.documentId)}/${encodeURIComponent(item.documentVersionId)}${anchor}`;
    };
    const evidenceCard = item => {
        const editable = isEditable();
        const type = state.evidenceTypes?.[item.evidenceTypeCode] || item.evidenceTypeCode;
        const locator = item.locator || {};
        const reference = [locator.section, locator.page, locator.table].filter(Boolean).join(' · ');
        const meta = [item.documentId ? String(item.documentId).slice(0, 8) : '', item.documentVersionLabel ? fmt(t('EvidencePinned'), item.documentVersionLabel) : '',
            item.documentKind === 'external' ? t('EvKindExternal') : t('EvKindControlled'), reference].filter(Boolean).join(' · ');
        const supports = (item.supportedSpans || []).map(s => s.text).join(' … ');
        const warnings = [];
        if (item.isSuperseded) warnings.push(fmt(t('EvidenceSuperseded'), item.currentVersionLabel || ''));
        if (REVIEW_DOC_STATES.includes(item.documentState)) warnings.push(fmt(t('EvidenceStateWarning'), t('DocState_' + item.documentState)));
        if (item.isExpiring && item.reviewDueAt) warnings.push(fmt(t('EvidenceExpiring'), date(item.reviewDueAt)));
        const url = previewUrl(item);
        return `<div class="claim-evidence-card${warnings.length ? ' needs-review' : ''}">`
            + `<div class="d-flex justify-content-between align-items-start gap-2"><div class="min-w-0">`
            + `<span class="badge bg-label-primary mb-1">${esc(type)}</span><div class="fw-medium">${esc(item.documentTitle)}</div>`
            + `<small class="text-muted">${esc(meta)}</small></div><div class="d-flex gap-1 flex-shrink-0">`
            + (url ? `<a class="btn btn-sm btn-label-secondary" href="${esc(url)}" target="_blank" rel="noopener"><i class="bx bx-show me-1"></i>${esc(t('EvidencePreview'))}</a>` : '')
            + (editable ? `<button type="button" class="btn btn-sm btn-icon btn-text-danger js-remove-evidence" data-link-id="${esc(item.linkId)}" aria-label="${esc(t('RemoveAction'))}" title="${esc(t('RemoveAction'))}"><i class="bx bx-trash"></i></button>` : '')
            + '</div></div>'
            + (locator.quote ? `<div class="claim-evidence-quote my-2">“${esc(locator.quote)}”</div>` : '')
            + (supports ? `<small><span class="text-muted">${esc(t('EvidenceSupports'))}</span> ${esc(supports)}</small>` : '')
            + warnings.map(w => `<div class="claim-evidence-warning mt-2"><i class="bx bx-error me-1"></i>${esc(w)}</div>`).join('')
            + '</div>';
    };
    const renderEvidence = () => {
        const saved = !!state.claimId;
        const editable = isEditable();
        byId('clmEvidenceSaveFirst')?.classList.toggle('d-none', saved);
        byId('clmEvidenceLocked')?.classList.toggle('d-none', !saved || editable);
        byId('btnAddEvidence')?.classList.toggle('d-none', !saved || !editable);
        const host = byId('clmEvidenceList');
        if (!host) return;
        if (!saved) { host.innerHTML = ''; return; }
        const items = (state.evidence?.items || []).filter(i => String(i.status).toLowerCase() === 'active');
        host.innerHTML = items.length ? items.map(evidenceCard).join('') : `<div class="text-muted small">${esc(t('EvidenceEmpty'))}</div>`;
    };
    const loadEvidence = async () => {
        if (!state.claimId) { state.evidence = null; renderEvidence(); return; }
        try {
            await loadEvidenceTypes();
            state.evidence = await request('GET', `${api}/claims/${encodeURIComponent(state.claimId)}/evidence`);
        } catch (error) {
            state.evidence = null;
            const host = byId('clmEvidenceList');
            renderEvidence();
            if (host) host.innerHTML = `<div class="text-warning small">${esc(error.message)}</div>`;
            refreshReadiness();
            return;
        }
        renderEvidence();
        refreshReadiness();
    };

    // ─── load / save ────────────────────────────────────────────────────────────
    const loadClaim = async () => {
        state.claim = await request('GET', `${api}/claims/${encodeURIComponent(state.claimId)}`);
        state.kind = state.claim?.kind === 'local' ? 'local' : 'core';
        state.status = state.claim?.isArchived ? 'archived' : (state.claim?.status || 'draft');
        fillForm(state.claim);
    };

    const save = async () => {
        showAlert('');
        const v = collect();
        try {
            if (!state.claimId) {
                const newId = await request('POST', `${api}/claims`, {
                    claimCode: v.claimCode, claimName: v.claimName, claimText: v.claimText,
                    effectiveFrom: new Date().toISOString(), description: v.description, qualifiers: v.qualifiers,
                    kind: state.kind, localCountryCode: v.localCountryCode, productId: v.productId,
                    productDisplay: v.productDisplay, audienceProfileIds: v.audienceProfileIds,
                    responsibleOrgUnitId: v.responsibleOrgUnitId, textLanguageCode: v.textLanguageCode
                }, 'create');
                state.claimId = String(newId || '');
                window.history.replaceState(null, '', `/CRM/Claims/Edit/${encodeURIComponent(state.claimId)}`);
                root.dataset.claimId = state.claimId;
                const crumb = byId('claimFormCrumb');
                if (crumb) crumb.textContent = t('EditClaimTitle');
                const title = byId('claimFormTitle');
                if (title) title.textContent = t('EditClaimTitle');
            } else {
                await request('PUT', `${api}/claims/${encodeURIComponent(state.claimId)}`, {
                    claimName: v.claimName, claimText: v.claimText,
                    effectiveFrom: state.claim?.effectiveFrom || new Date().toISOString(),
                    effectiveTo: state.claim?.effectiveTo || null, description: v.description, qualifiers: v.qualifiers,
                    productId: v.productId, productDisplay: v.productDisplay, audienceProfileIds: v.audienceProfileIds,
                    // An explicit empty id clears the team (null keeps the stored value).
                    responsibleOrgUnitId: v.responsibleOrgUnitId || EMPTY_GUID,
                    textLanguageCode: v.textLanguageCode
                }, 'update');
            }
            await loadClaim();
            state.dirty = false;
            applyLocks();
            await loadEvidence();
            return true;
        } catch (error) {
            showAlert(error.message);
            toast(error.message, 'error');
            return false;
        }
    };

    const postAction = async (path, okMessage) => {
        showAlert('');
        try {
            const data = await request('POST', `${api}/claims/${encodeURIComponent(state.claimId)}/${path}`);
            if (okMessage) toast(okMessage);
            return data ?? true;
        } catch (error) {
            showAlert(error.message);
            toast(error.message, 'error');
            return null;
        }
    };

    const reloadAll = async () => {
        await loadClaim();
        state.dirty = false;
        applyLocks();
        await loadEvidence();
    };

    const submitReview = () => {
        window.showConfirm?.(t('SubmitConfirm'), async () => {
            // Unsaved changes are saved first: the round always reviews what is stored.
            if ((!state.claimId || state.dirty) && !(await save())) return;
            if (await postAction('submit-review', t('ToastSubmitted'))) await reloadAll();
        }, { type: 'info', confirmButtonText: t('SubmitReview') });
    };
    const withdrawReview = () => {
        window.showConfirm?.(t('WithdrawConfirm'), async () => {
            if (await postAction('withdraw-review', t('ToastWithdrawn'))) await reloadAll();
        }, { type: 'warning', confirmButtonText: t('WithdrawReview') });
    };
    const openNewVersion = () => {
        window.showConfirm?.(t('NewVersionConfirm'), async () => {
            const newId = await postAction('new-version', null);
            if (newId && newId !== true) window.location.href = `/CRM/Claims/Edit/${encodeURIComponent(newId)}`;
        }, { type: 'info', confirmButtonText: t('OpenNewVersion') });
    };

    // ─── evidence modal ─────────────────────────────────────────────────────────
    const modalEl = byId('claimEvidenceModal');
    const evAlert = message => { const el = byId('evAlert'); if (el) { el.textContent = message || ''; el.classList.toggle('d-none', !message); } };
    const setStep = step => {
        modalEl?.querySelectorAll('[data-step]').forEach(n => n.classList.toggle('d-none', n.dataset.step !== String(step)));
        modalEl?.querySelectorAll('[data-step-label]').forEach(n => n.classList.toggle('active', n.dataset.stepLabel === String(step)));
        byId('evBack')?.classList.toggle('d-none', step === 1);
        byId('evNext')?.classList.toggle('d-none', step !== 1);
        byId('evAdd')?.classList.toggle('d-none', step !== 2);
        const preview = byId('evPreview');
        const doc = state.modal.doc;
        const url = step === 2 && doc && doc.kind === 'controlled' && doc.currentVersionId
            ? `/DocumentManagementControlledDocuments/preview/${encodeURIComponent(doc.documentId)}/${encodeURIComponent(doc.currentVersionId)}` : null;
        if (preview) { preview.classList.toggle('d-none', !url); preview.setAttribute('href', url || '#'); }
        evAlert('');
    };
    const pinLabel = doc => doc.kind === 'controlled' ? doc.currentVersionLabel : doc.sourceVersion;
    const renderDocs = () => {
        const host = byId('evDocList');
        if (!host) return;
        const docs = state.modal.docs;
        if (!docs.length) { host.innerHTML = `<div class="text-muted small p-2">${esc(t('EvNoDocuments'))}</div>`; return; }
        host.innerHTML = docs.map((d, i) => {
            const pinnable = d.kind !== 'controlled' || !!d.currentVersionId;
            const meta = [d.code, d.documentType, pinLabel(d), d.countryCode, d.sourceStatus || d.status].filter(Boolean).join(' · ');
            const active = state.modal.doc && state.modal.doc.documentId === d.documentId ? ' active' : '';
            return `<button type="button" class="list-group-item list-group-item-action js-ev-doc${active}" data-index="${i}" role="option" aria-selected="${active ? 'true' : 'false'}"${pinnable ? '' : ' disabled'}>`
                + `<div class="fw-medium">${esc(d.title)}</div><small class="text-muted">${esc(meta)}</small></button>`;
        }).join('');
    };
    let searchTimer = null;
    const loadDocs = async () => {
        const search = encodeURIComponent(norm(byId('evSearch')?.value));
        const kind = encodeURIComponent(byId('evKind')?.value || 'controlled');
        try {
            const data = await request('GET', `${api}/claims/evidence/document-options?search=${search}&kind=${kind}`);
            state.modal.docs = Array.isArray(data) ? data : [];
            evAlert('');
        } catch (error) {
            state.modal.docs = [];
            evAlert(error.message);
        }
        renderDocs();
    };
    const renderSpans = () => {
        const host = byId('evSpans');
        if (!host) return;
        host.innerHTML = state.modal.spans.length
            ? state.modal.spans.map((s, i) => `<span class="ev-span-chip">${esc(s.text)}<button type="button" class="btn-close btn-close-sm js-ev-span-remove" data-index="${i}" aria-label="${esc(t('RemoveAction'))}"></button></span>`).join('')
            : `<span class="text-muted small">${esc(t('EvSpansEmpty'))}</span>`;
    };
    const addSelection = () => {
        const ta = byId('evSpanSource');
        if (!ta) return;
        let start = ta.selectionStart;
        let end = ta.selectionEnd;
        const value = ta.value;
        while (start < end && /\s/.test(value[start])) start++;
        while (end > start && /\s/.test(value[end - 1])) end--;
        if (end <= start) return;
        if (state.modal.spans.length >= MAX_SPANS) { evAlert(t('EvSpansMax')); return; }
        if (!state.modal.spans.some(s => s.start === start && s.end === end)) {
            state.modal.spans.push({ languageCode: state.claim?.textLanguageCode || 'en', text: value.slice(start, end), start, end });
            state.modal.spans.sort((a, b) => a.start - b.start);
        }
        evAlert('');
        renderSpans();
    };
    const openEvidenceModal = async () => {
        if (!state.claimId || !isEditable()) return;
        if (state.dirty && !(await save())) return;
        if (!norm(state.claim?.claimText)) { toast(t('EvSaveTextFirst'), 'warning'); return; }
        state.modal = { doc: null, spans: [], docs: [] };
        ['evSearch', 'evSection', 'evPage', 'evTable', 'evQuote'].forEach(id => { const el = byId(id); if (el) el.value = ''; });
        const source = byId('evSpanSource');
        if (source) source.value = state.claim.claimText;
        const types = await loadEvidenceTypes();
        const typeEl = byId('evType');
        if (typeEl) typeEl.innerHTML = '<option value=""></option>' + Object.entries(types).map(([code, name]) => `<option value="${esc(code)}">${esc(name)}</option>`).join('');
        byId('evNext').disabled = true;
        renderSpans();
        setStep(1);
        if (modalEl && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
        await loadDocs();
    };
    const goStep2 = () => {
        const doc = state.modal.doc;
        if (!doc) return;
        byId('evDocTitle').textContent = doc.title;
        byId('evDocMeta').textContent = [doc.code, doc.documentType, doc.kind === 'external' ? t('EvKindExternal') : t('EvKindControlled')].filter(Boolean).join(' · ');
        byId('evDocPin').textContent = pinLabel(doc) ? fmt(t('EvWillPin'), pinLabel(doc)) : (doc.kind === 'external' ? t('EvPreviewExternalNote') : '');
        setStep(2);
    };
    const addEvidence = async () => {
        const doc = state.modal.doc;
        const type = byId('evType')?.value;
        const quote = norm(byId('evQuote')?.value);
        if (!doc || !type || !quote || state.modal.spans.length === 0) { evAlert(t('EvRequiredMissing')); return; }
        const button = byId('evAdd');
        if (button) button.disabled = true;
        try {
            await request('POST', `${api}/claims/${encodeURIComponent(state.claimId)}/evidence`, {
                documentKind: doc.kind,
                documentId: doc.documentId,
                documentVersionId: doc.kind === 'controlled' ? doc.currentVersionId : null,
                evidenceTypeCode: type,
                locator: {
                    quote,
                    section: norm(byId('evSection')?.value) || null,
                    page: norm(byId('evPage')?.value) || null,
                    table: norm(byId('evTable')?.value) || null
                },
                supportedSpans: state.modal.spans
            });
            window.bootstrap?.Modal.getOrCreateInstance(modalEl).hide();
            toast(t('ToastEvidenceAdded'));
            await loadEvidence();
        } catch (error) {
            evAlert(error.message);
        } finally {
            if (button) button.disabled = false;
        }
    };

    // ─── remove evidence (reason required) ──────────────────────────────────────
    const removeModalEl = byId('claimEvidenceRemoveModal');
    const openRemove = linkId => {
        state.removeLinkId = linkId;
        const reason = byId('evRemoveReason');
        if (reason) { reason.value = ''; reason.classList.remove('is-invalid'); }
        if (removeModalEl && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(removeModalEl).show();
    };
    const confirmRemove = async () => {
        const reasonEl = byId('evRemoveReason');
        const reason = norm(reasonEl?.value);
        if (!reason) { reasonEl?.classList.add('is-invalid'); toast(t('Err_removal_reason_required'), 'warning'); return; }
        try {
            await request('POST', `${api}/claims/evidence/${encodeURIComponent(state.removeLinkId)}/remove`, { reason });
            window.bootstrap?.Modal.getOrCreateInstance(removeModalEl).hide();
            toast(t('ToastEvidenceRemoved'));
            await loadEvidence();
        } catch (error) {
            toast(error.message, 'error');
        }
    };

    // ─── events ─────────────────────────────────────────────────────────────────
    const bind = () => {
        ['clmName', 'clmDescription', 'clmText'].forEach(id => byId(id)?.addEventListener('input', () => { if (id === 'clmText') updateCount(); markDirty(); }));
        byId('clmCode')?.addEventListener('input', () => { state.codeTouched = true; markDirty(); });
        byId('clmTextLanguage')?.addEventListener('change', markDirty);
        byId('btnAddQualifier')?.addEventListener('click', addQualifier);
        byId('clmQualifierInput')?.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); addQualifier(); } });
        byId('btnAddEvidence')?.addEventListener('click', () => void openEvidenceModal());
        byId('btnWithdrawReview')?.addEventListener('click', withdrawReview);
        byId('btnOpenNewVersion')?.addEventListener('click', openNewVersion);
        root.querySelectorAll('.js-save-draft').forEach(b => b.addEventListener('click', async () => { if (await save()) toast(t('ToastSaved')); }));
        root.querySelectorAll('.js-submit-review').forEach(b => b.addEventListener('click', submitReview));
        byId('evSearch')?.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => void loadDocs(), 300); });
        byId('evKind')?.addEventListener('change', () => { state.modal.doc = null; byId('evNext').disabled = true; void loadDocs(); });
        byId('evNext')?.addEventListener('click', goStep2);
        byId('evBack')?.addEventListener('click', () => setStep(1));
        byId('evAdd')?.addEventListener('click', () => void addEvidence());
        byId('evAddSelection')?.addEventListener('click', addSelection);
        byId('evRemoveConfirm')?.addEventListener('click', () => void confirmRemove());

        root.addEventListener('click', event => {
            const q = event.target.closest('.js-remove-qualifier');
            if (q) { state.qualifiers.splice(Number(q.dataset.index), 1); renderQualifiers(); markDirty(); return; }
            const removeEvidence = event.target.closest('.js-remove-evidence');
            if (removeEvidence) { openRemove(removeEvidence.dataset.linkId); return; }
            const doc = event.target.closest('.js-ev-doc');
            if (doc && !doc.disabled) {
                state.modal.doc = state.modal.docs[Number(doc.dataset.index)] || null;
                byId('evNext').disabled = !state.modal.doc;
                renderDocs();
                return;
            }
            const span = event.target.closest('.js-ev-span-remove');
            if (span) { state.modal.spans.splice(Number(span.dataset.index), 1); renderSpans(); }
        });

        window.addEventListener('beforeunload', e => { if (state.dirty && isEditable()) { e.preventDefault(); e.returnValue = ''; } });
    };

    // ─── init ───────────────────────────────────────────────────────────────────
    const init = async () => {
        bind();
        try {
            await loadCountries();
            if (state.claimId) {
                await loadClaim();
            } else {
                fillForm(null);
            }
            applyKind();
            setStatusBadges();
            fillCountries(state.claim?.localCountryCode ? [norm(state.claim.localCountryCode).toUpperCase()] : []);
            fillLanguages(state.claim?.localCountryCode, state.claim?.textLanguageCode);
            initProductPicker();
            await Promise.all([
                loadAudienceOptions(state.claim?.audienceProfileIds || []),
                loadOrgUnitOptions(state.claim?.responsibleOrgUnitId ? [state.claim.responsibleOrgUnitId] : []),
                loadEvidence(),
                loadFlow()
            ]);
            state.dirty = false;
            applyLocks();
        } catch (error) {
            showAlert(error.status === 404 ? t('LoadFailed') : (error.message || t('LoadFailed')));
        }
    };

    init();
})(window, document);
