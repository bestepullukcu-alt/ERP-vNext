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
        product: null            // { id, text, code, name }
    };

    // ─── helpers ────────────────────────────────────────────────────────────────
    const byId = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const fmt = (template, ...args) => String(template ?? '').replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    const t = key => L[key] || key;
    const toast = (msg, type) => window.showToast?.(msg, type || 'success');

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
                    return { results: items.filter(p => p?.id).map(p => ({ id: p.id, text: productLabel(p), code: norm(p.canonicalCode || p.code), name: norm(p.globalProductName || p.name) })) };
                }
            }
        });
        $(el).on('select2:select', e => {
            const d = e.params.data || {};
            state.product = { id: d.id, text: d.text, code: d.code || '', name: d.name || '' };
            void suggestCode();
        });
        $(el).on('select2:clear', () => { state.product = null; });
    };

    // ─── code suggestion CLM-{PRODUCT NAME}-{NN} (create only; never overrides a code the user typed) ─────────
    // WP-CL-FE-4: the prefix comes from the product NAME (not the MDM canonical code) and NN is the next number
    // among the existing codes — computed server-side (api/v2/claims/code-suggestion, ClaimCodeSuggestion).
    const suggestCode = async () => {
        if (state.claimId || state.codeTouched || !state.product) return;
        const name = norm(state.product.name) || norm(String(state.product.text).split('—').pop());
        if (!name) return;
        const data = await tryGet(`${api}/claims/code-suggestion?productName=${encodeURIComponent(name)}`);
        const code = byId('clmCode');
        if (data?.code && code && !state.codeTouched) code.value = data.code;
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

    // ─── evidence list (cards from the shared module claim-evidence.js) ──────────
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
        host.innerHTML = items.length ? items.map(i => window.ClaimEvidence.card(i, { removable: isEditable() })).join('') : `<div class="text-muted small">${esc(t('EvidenceEmpty'))}</div>`;
    };
    const loadEvidence = async () => {
        if (!state.claimId) { state.evidence = null; renderEvidence(); return; }
        try {
            await window.ClaimEvidence.loadTypes();
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

    // ─── evidence modals (shared module claim-evidence.js — WP-CL-FE-4) ────────
    const evidenceUi = window.ClaimEvidence.create({
        linkUrl: () => `${api}/claims/${encodeURIComponent(state.claimId)}/evidence`,
        // The supported phrases are marked on the SAVED claim text in its text language.
        source: () => ({ text: state.claim?.claimText || '', languageCode: state.claim?.textLanguageCode || 'en' }),
        beforeOpen: async () => {
            if (!state.claimId || !isEditable()) return false;
            return !state.dirty || await save();
        },
        onChanged: () => loadEvidence()
    });

    // ─── events ─────────────────────────────────────────────────────────────────
    const bind = () => {
        ['clmName', 'clmDescription', 'clmText'].forEach(id => byId(id)?.addEventListener('input', () => { if (id === 'clmText') updateCount(); markDirty(); }));
        byId('clmCode')?.addEventListener('input', () => { state.codeTouched = true; markDirty(); });
        byId('clmTextLanguage')?.addEventListener('change', markDirty);
        byId('btnAddQualifier')?.addEventListener('click', addQualifier);
        byId('clmQualifierInput')?.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); addQualifier(); } });
        byId('btnAddEvidence')?.addEventListener('click', () => void evidenceUi.open());
        byId('btnWithdrawReview')?.addEventListener('click', withdrawReview);
        byId('btnOpenNewVersion')?.addEventListener('click', openNewVersion);
        root.querySelectorAll('.js-save-draft').forEach(b => b.addEventListener('click', async () => { if (await save()) toast(t('ToastSaved')); }));
        root.querySelectorAll('.js-submit-review').forEach(b => b.addEventListener('click', submitReview));
        root.addEventListener('click', event => {
            const q = event.target.closest('.js-remove-qualifier');
            if (q) { state.qualifiers.splice(Number(q.dataset.index), 1); renderQualifiers(); markDirty(); }
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
