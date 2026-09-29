/**
 * WP-CL-FE-4 — claim country version page (mockup scenarios 4 / 5) over the FE-1 v2 proxy:
 *  - read-only core card, country card (lookups/countries name + languages; no licence card — D3)
 *  - one tab per content language of the country: core (en) text + qualifiers as read-only reference, the local text
 *    (counter) and local qualifiers; a language without text is marked
 *  - adaptation type (labels from resx by value_code) + reason (required unless verbatim), validity, audience limited
 *    to the core claim's audiences
 *  - evidence: inherited from the core (origin=core, read-only) + local (shared modal claim-evidence.js)
 *  - readiness, local approval flow preview (CLAIM-LOCAL-MLR-{cc}), status card (save / send / withdraw / new version)
 * CRM [code, message] refusals are shown in the reader's language (Err_{code}).
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('countryVersionRoot');
    if (!root) return;

    const api = '/CRM/Claims/api/v2';
    const $ = window.jQuery;
    const L = (() => { try { return JSON.parse(document.getElementById('claim-country-l10n')?.textContent || '{}'); } catch (e) { return {}; } })();
    const canManage = root.dataset.canManage === 'true';

    const state = {
        claimId: root.dataset.claimId || '',
        countryCode: (root.dataset.country || '').toUpperCase(),
        versionId: root.dataset.versionId || '',
        version: null,
        claim: null,
        coreEvidenceCount: null,
        evidence: null,
        country: null,          // { code, name, languages: [{ code, label }] }
        texts: {},              // lang → { text, qualifiers[] }
        activeLang: null,
        audienceNames: {},
        adaptationTypes: [],
        status: 'draft',
        dirty: false,
        pickers: {}
    };

    // ─── helpers ────────────────────────────────────────────────────────────────
    const byId = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const fmt = (template, ...args) => String(template ?? '').replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    const t = key => L[key] || key;
    const toast = (msg, type) => window.showToast?.(msg, type || 'success');
    const isoDate = v => v ? String(v).slice(0, 10) : '';
    const toInstant = d => d ? `${d}T00:00:00Z` : null;

    const readError = async response => {
        const body = await response.json().catch(() => ({}));
        const errors = Array.isArray(body.errors) ? body.errors.filter(e => typeof e === 'string') : [];
        const code = errors.length > 1 && /^[a-z_]+$/.test(errors[0]) ? errors[0] : null;
        const message = (code && L['Err_' + code]) || (errors.length > 1 ? errors.slice(1).join(' · ') : errors[0]) || body.message || t('ErrorGeneric');
        return Object.assign(new Error(message), { status: response.status, code });
    };
    const request = async (method, url, body) => {
        const init = { method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) { init.headers['Content-Type'] = 'application/json'; init.body = JSON.stringify(body); }
        const response = await fetch(url, init);
        if (!response.ok) throw await readError(response);
        if (response.status === 204) return null;
        const json = await response.json().catch(() => ({}));
        return json && Object.prototype.hasOwnProperty.call(json, 'data') ? json.data : json;
    };
    const tryGet = async url => { try { return await request('GET', url); } catch (e) { return null; } };
    const showAlert = message => { const el = byId('cvAlert'); if (el) { el.textContent = message || ''; el.classList.toggle('d-none', !message); } };

    const isEditable = () => canManage && (!state.versionId || (state.status === 'draft' && !state.version?.isArchived));

    // ─── country + languages (FE-2 languageDetails; plain languages[] as the fallback) ───────────────────────────
    const loadCountry = async () => {
        const data = await tryGet(`${api}/lookups/countries`);
        const row = (Array.isArray(data) ? data : []).find(c => norm(c.code).toUpperCase() === state.countryCode);
        const details = Array.isArray(row?.languageDetails) && row.languageDetails.length
            ? row.languageDetails.map(l => ({ code: l.code, label: `${l.nativeName || l.name || l.code} (${l.code})` }))
            : (row?.languages || []).map(l => ({ code: l, label: l }));
        state.country = { code: state.countryCode, name: row?.name || state.countryCode, languages: details };
    };

    // ─── texts per language ─────────────────────────────────────────────────────
    const languageCodes = () => {
        const fromCountry = (state.country?.languages || []).map(l => l.code);
        const stored = Object.keys(state.texts);
        return Array.from(new Set(fromCountry.concat(stored)));
    };
    const langLabel = code => state.country?.languages.find(l => l.code === code)?.label || code;
    const missingLangs = () => languageCodes().filter(l => !norm(state.texts[l]?.text));

    const renderLanguages = () => {
        const tabs = byId('cvLangTabs');
        const panes = byId('cvLangPanes');
        if (!tabs || !panes) return;
        const langs = languageCodes();
        if (!state.activeLang || !langs.includes(state.activeLang)) state.activeLang = langs[0] || null;
        const editable = isEditable();
        const missing = new Set(missingLangs());
        tabs.innerHTML = langs.map(l => `<li class="nav-item" role="presentation"><button type="button" class="nav-link${l === state.activeLang ? ' active' : ''} js-cv-lang" data-lang="${esc(l)}" role="tab" aria-selected="${l === state.activeLang}">`
            + `${esc(langLabel(l))}${missing.has(l) ? ` <span class="badge rounded-pill bg-label-danger ms-1" title="${esc(t('LanguageMissing'))}">!</span>` : ''}</button></li>`).join('');
        const core = state.claim || {};
        const l = state.activeLang;
        if (!l) { panes.innerHTML = ''; return; }
        const entry = state.texts[l] || { text: '', qualifiers: [] };
        const coreQuals = (core.qualifiers || []).map(q => `<li>${esc(q)}</li>`).join('');
        panes.innerHTML = `<div class="row g-3">`
            + `<div class="col-12"><div class="border rounded p-3 bg-body-tertiary"><small class="text-muted d-block mb-1">${esc(t('CoreReferenceText'))}</small><div>${esc(core.claimText || '—')}</div>`
            + (coreQuals ? `<small class="text-muted d-block mt-2 mb-1">${esc(t('CoreReferenceQualifier'))}</small><ul class="mb-0 ps-3">${coreQuals}</ul>` : '') + '</div></div>'
            + `<div class="col-12"><label for="cvText" class="form-label fw-medium">${esc(fmt(t('LocalTextLabel'), langLabel(l)))} <span class="text-danger">*</span></label>`
            + `<textarea id="cvText" class="form-control" rows="4" maxlength="2000"${editable ? '' : ' disabled'}>${esc(entry.text)}</textarea>`
            + `<div class="form-text text-end" id="cvTextCount">${esc(fmt(t('CharCount'), (entry.text || '').length))}</div></div>`
            + `<div class="col-12"><span class="form-label fw-medium d-block">${esc(fmt(t('LocalQualifiersLabel'), langLabel(l)))}</span>`
            + `<ul class="list-unstyled d-flex flex-column gap-2 mb-2">${(entry.qualifiers || []).map((q, i) => `<li class="claim-qualifier"><span>${esc(q)}</span>`
                + (editable ? `<button type="button" class="btn btn-sm btn-icon btn-text-danger js-cv-remove-qualifier" data-index="${i}" aria-label="${esc(t('RemoveAction'))}" title="${esc(t('RemoveAction'))}"><i class="bx bx-x"></i></button>` : '') + '</li>').join('')}</ul>`
            + (editable ? `<div class="input-group"><input type="text" id="cvQualifierInput" class="form-control" maxlength="500" placeholder="${esc(t('QualifierPlaceholder'))}" />`
                + `<button type="button" class="btn btn-label-primary" id="btnCvAddQualifier"><i class="bx bx-plus me-1"></i>${esc(t('AddAction'))}</button></div>` : '')
            + '</div></div>';
    };

    // ─── adaptation / validity / audience ───────────────────────────────────────
    const adaptLabel = code => L['Adapt_' + code] || state.adaptationTypes.find(a => a.code === code)?.name || code;
    const reasonRequired = () => { const v = byId('cvAdaptType')?.value; return !!v && v !== 'verbatim'; };
    const fillAdaptation = selected => {
        const el = byId('cvAdaptType');
        if (!el) return;
        const codes = state.adaptationTypes.map(a => a.code);
        if (selected && !codes.includes(selected)) codes.push(selected);
        el.innerHTML = codes.map(c => `<option value="${esc(c)}"${c === selected ? ' selected' : ''}>${esc(adaptLabel(c))}</option>`).join('');
        byId('cvAdaptReasonRequired')?.classList.toggle('d-none', !reasonRequired());
    };
    const initDates = () => {
        ['cvValidFrom', 'cvValidTo'].forEach(id => {
            const el = byId(id);
            if (!el) return;
            if (window.flatpickr) {
                state.pickers[id] = window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true, onChange: () => markDirty() });
            } else {
                el.type = 'date';
            }
        });
    };
    const setDate = (id, value) => {
        const el = byId(id);
        if (!el) return;
        if (state.pickers[id]) state.pickers[id].setDate(value || null, false);
        else el.value = value || '';
    };
    const fillAudience = selected => {
        // Narrowing only: the options are the core claim's audiences.
        const el = byId('cvAudience');
        if (!el) return;
        const ids = state.claim?.audienceProfileIds || [];
        const wanted = new Set(selected || []);
        el.innerHTML = ids.map(id => `<option value="${esc(id)}"${wanted.has(id) ? ' selected' : ''}>${esc(state.audienceNames[id] || id)}</option>`).join('');
        if ($ && $.fn.select2) {
            const $s = $(el);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({ width: '100%', placeholder: $s.data('placeholder') || '', dropdownParent: $(document.body) });
            $s.on('change', markDirty);
        } else {
            el.addEventListener('change', markDirty);
        }
    };
    const selectedAudience = () => $ ? ($(byId('cvAudience')).val() || []) : Array.from(byId('cvAudience')?.selectedOptions || []).map(o => o.value);

    // ─── core + country cards ───────────────────────────────────────────────────
    const stateBadge = (id, status) => {
        const el = byId(id);
        if (!el) return;
        el.className = 'claim-state state-' + status;
        el.textContent = t('State_' + status);
    };
    const renderCards = () => {
        const c = state.claim || {};
        byId('cvCoreCode').textContent = c.claimCode || '-';
        byId('cvCoreVersion').textContent = c.claimVersion ? fmt(t('VersionLabel'), c.claimVersion) : '';
        stateBadge('cvCoreStatus', c.status || 'draft');
        byId('cvCoreText').textContent = c.claimText || '—';
        const audience = (c.audienceProfileIds || []).map(id => state.audienceNames[id] || id).join(', ');
        byId('cvCoreMeta').textContent = [
            (c.qualifiers || []).length ? `${t('CoreQualifierLabel')}: ${(c.qualifiers || []).join(' · ')}` : '',
            audience ? `${t('CoreAudienceLabel')}: ${audience}` : '',
            state.coreEvidenceCount == null ? '' : fmt(t('CoreEvidenceCount'), state.coreEvidenceCount)
        ].filter(Boolean).join(' · ') || '—';
        const crumb = byId('cvCrumbClaim');
        if (crumb) { crumb.textContent = c.claimCode || '-'; crumb.setAttribute('href', state.claimId ? `/CRM/Claims/Edit/${encodeURIComponent(state.claimId)}` : '/CRM/Claims'); }
        byId('cvCrumbCountry').textContent = state.country?.name || state.countryCode;
        byId('cvCountryName').textContent = `${state.country?.name || state.countryCode} (${state.countryCode})`;
        byId('cvCountryLanguages').textContent = (state.country?.languages || []).map(l => l.label).join(', ') || '—';
        const title = byId('cvTitle');
        if (title) title.textContent = fmt(state.versionId ? t('PageTitleEdit') : t('PageTitleCreate'), state.country?.name || state.countryCode);
    };

    // ─── evidence ───────────────────────────────────────────────────────────────
    const renderEvidence = () => {
        const saved = !!state.versionId;
        const editable = isEditable();
        byId('cvEvidenceSaveFirst')?.classList.toggle('d-none', saved);
        byId('cvEvidenceLocked')?.classList.toggle('d-none', !saved || editable);
        byId('btnAddLocalEvidence')?.classList.toggle('d-none', !saved || !editable);
        const items = (state.evidence?.items || []).filter(i => String(i.status).toLowerCase() === 'active');
        const inherited = items.filter(i => i.origin === 'core');
        const local = items.filter(i => i.origin !== 'core');
        const card = window.ClaimEvidence.card;
        const inheritedHost = byId('cvEvidenceInherited');
        if (inheritedHost) {
            inheritedHost.innerHTML = inherited.length
                ? inherited.map(i => card(i, { origin: true, removable: false })).join('')
                : `<div class="text-muted small">${esc(t('InheritedEvidenceEmpty'))}</div>`;
        }
        const localHost = byId('cvEvidenceLocal');
        if (localHost) {
            localHost.innerHTML = local.length
                ? local.map(i => card(i, { origin: true, removable: editable })).join('')
                : `<div class="text-muted small">${esc(fmt(t('LocalEvidenceEmpty'), state.country?.name || state.countryCode))}</div>`;
        }
    };
    const loadEvidence = async () => {
        await window.ClaimEvidence.loadTypes();
        if (!state.versionId) {
            // Before the first save only the inherited core evidence exists.
            const core = state.claimId ? await tryGet(`${api}/claims/${encodeURIComponent(state.claimId)}/evidence`) : null;
            state.evidence = core ? { items: (core.items || []).map(i => ({ ...i, origin: 'core' })), effectiveCount: core.effectiveCount } : null;
        } else {
            try {
                state.evidence = await request('GET', `${api}/claims/country-versions/${encodeURIComponent(state.versionId)}/evidence`);
            } catch (error) {
                state.evidence = null;
                showAlert(error.message);
            }
        }
        renderEvidence();
        refreshReadiness();
    };

    // ─── readiness / locks ──────────────────────────────────────────────────────
    const markDirty = () => { state.dirty = true; refreshReadiness(); };
    const refreshReadiness = () => {
        const coreApproved = state.claim?.kind === 'local' || state.claim?.status === 'approved';
        const allLangs = languageCodes().length > 0 && missingLangs().length === 0;
        const reasonOk = !reasonRequired() || !!norm(byId('cvAdaptReason')?.value);
        const validity = !!norm(byId('cvValidFrom')?.value);
        const evidence = (state.evidence?.effectiveCount ?? 0) >= 1;
        const saved = !!state.versionId && !state.dirty;
        const items = [['ReadyCoreApproved', coreApproved], ['ReadyAllLanguages', allLangs], ['ReadyAdaptationReason', reasonOk],
            ['ReadyValidity', validity], ['ReadyEvidence', evidence], ['ReadySaved', saved]];
        const host = byId('cvReadiness');
        if (host) {
            host.innerHTML = items.map(([key, ok]) => `<li class="readiness-item"><span>${esc(t(key))}</span>`
                + (ok ? '<i class="bx bx-check-circle ok" aria-hidden="true"></i>' : `<span class="badge rounded-pill missing">${esc(t('ReadyMissing'))}</span>`) + '</li>').join('');
        }
        const required = missingLangs().length + (reasonOk ? 0 : 1) + (validity ? 0 : 1);
        const tracker = byId('cvRequiredTracker');
        if (tracker) { tracker.textContent = String(required); tracker.classList.toggle('d-none', required === 0 || !isEditable()); }
    };
    const applyLocks = () => {
        const editable = isEditable();
        stateBadge('cvStatusBadge', state.status);
        ['cvAdaptType', 'cvAdaptReason', 'cvValidFrom', 'cvValidTo', 'cvAudience'].forEach(id => { const el = byId(id); if (el) el.disabled = !editable; });
        if ($ && $.fn.select2) $(byId('cvAudience')).trigger('change.select2');
        root.querySelectorAll('[data-requires="draft"]').forEach(b => b.classList.toggle('d-none', !editable));
        byId('btnCvWithdraw')?.classList.toggle('d-none', !canManage || state.status !== 'in-review');
        byId('btnCvNewVersion')?.classList.toggle('d-none', !canManage || !(state.status === 'approved' || state.status === 'review-required') || !!state.version?.isArchived);
        const band = byId('cvLockBand');
        if (band) {
            const lock = state.version?.isArchived || state.status === 'inactive' ? ['LockInactive', 'alert-secondary', 'bx-archive']
                : state.status === 'in-review' ? ['LockInReview', 'alert-info', 'bx-time-five']
                    : state.status === 'approved' ? ['LockApproved', 'alert-success', 'bx-lock-alt']
                        : state.status === 'review-required' ? ['LockReviewRequired', 'alert-warning', 'bx-revision'] : null;
            band.className = 'alert d-flex align-items-center gap-2' + (lock ? ' ' + lock[1] : ' d-none');
            band.innerHTML = lock ? `<i class="bx ${lock[2]}"></i><span>${esc(t(lock[0]))}</span>` : '';
        }
        renderLanguages();
        renderEvidence();
        refreshReadiness();
    };

    // ─── local approval flow preview ────────────────────────────────────────────
    const loadFlow = async () => {
        const host = byId('cvFlow');
        if (!host) return;
        const data = await tryGet(`${api}/lookups/workflow-template?code=${encodeURIComponent('CLAIM-LOCAL-MLR-' + state.countryCode)}`);
        if (!data || data.available !== true) {
            host.innerHTML = `<div class="text-muted small"><i class="bx bx-info-circle me-1"></i>${esc(t('FlowReason_' + (data?.reason || 'WorkflowUnavailable')))}</div>`;
            return;
        }
        let n = 0;
        host.innerHTML = (data.stages || []).flatMap(s => s.steps || []).map(step => {
            n += 1;
            const who = (step.candidates || []).length ? step.candidates.join(', ') : t('FlowNoCandidates');
            return `<div class="flow-step"><span class="flow-step-no">${n}</span><div><div class="fw-medium">${esc(step.name || step.code)}</div><small class="text-muted">${esc(who)}</small></div></div>`;
        }).join('') || `<div class="text-muted small">${esc(t('FlowReason_WorkflowUnavailable'))}</div>`;
    };

    // ─── load / save ────────────────────────────────────────────────────────────
    const applyVersion = v => {
        state.version = v;
        state.status = v?.isArchived ? 'archived' : (v?.status || 'draft');
        state.texts = {};
        (v?.texts || []).forEach(x => { state.texts[x.languageCode] = { text: x.text || '', qualifiers: [] }; });
        (v?.qualifiers || []).forEach(x => {
            if (!state.texts[x.languageCode]) state.texts[x.languageCode] = { text: '', qualifiers: [] };
            state.texts[x.languageCode].qualifiers.push(x.text);
        });
    };
    const loadVersion = async () => {
        const v = await request('GET', `${api}/claims/country-versions/${encodeURIComponent(state.versionId)}`);
        state.claimId = v.claimId;
        state.countryCode = norm(v.countryCode).toUpperCase();
        applyVersion(v);
        return v;
    };
    const collect = () => {
        const texts = [];
        const qualifiers = [];
        Object.entries(state.texts).forEach(([lang, entry]) => {
            if (norm(entry.text)) texts.push({ languageCode: lang, text: norm(entry.text) });
            (entry.qualifiers || []).forEach(q => qualifiers.push({ languageCode: lang, text: q }));
        });
        return {
            texts,
            qualifiers,
            validFrom: toInstant(norm(byId('cvValidFrom')?.value)),
            validTo: toInstant(norm(byId('cvValidTo')?.value)),
            adaptationTypeCode: byId('cvAdaptType')?.value || null,
            adaptationReason: norm(byId('cvAdaptReason')?.value) || null,
            audienceProfileIds: selectedAudience()
        };
    };
    const save = async () => {
        showAlert('');
        const body = collect();
        try {
            if (!state.versionId) {
                const newId = await request('POST', `${api}/claims/${encodeURIComponent(state.claimId)}/country-versions`,
                    { countryCode: state.countryCode, ...body });
                state.versionId = String(newId || '');
                root.dataset.versionId = state.versionId;
                window.history.replaceState(null, '', `/CRM/Claims/CountryVersions/${encodeURIComponent(state.versionId)}/Edit`);
            } else {
                await request('PUT', `${api}/claims/country-versions/${encodeURIComponent(state.versionId)}`, body);
            }
            await loadVersion();
            state.dirty = false;
            renderCards();
            applyLocks();
            await loadEvidence();
            return true;
        } catch (error) {
            showAlert(error.message);
            toast(error.message, 'error');
            return false;
        }
    };
    const postAction = async path => {
        showAlert('');
        try {
            const data = await request('POST', `${api}/claims/country-versions/${encodeURIComponent(state.versionId)}/${path}`);
            return data ?? true;
        } catch (error) {
            showAlert(error.message);
            toast(error.message, 'error');
            return null;
        }
    };
    const reload = async () => {
        await loadVersion();
        state.dirty = false;
        applyLocks();
        await loadEvidence();
    };

    // ─── shared evidence modal ──────────────────────────────────────────────────
    const evidenceUi = window.ClaimEvidence.create({
        linkUrl: () => `${api}/claims/country-versions/${encodeURIComponent(state.versionId)}/evidence`,
        // Supported phrases are marked on the SAVED text of the active language.
        source: () => {
            const lang = state.activeLang;
            const saved = (state.version?.texts || []).find(x => x.languageCode === lang);
            if (!saved || !norm(saved.text)) { toast(t('EvidenceTextFirst'), 'warning'); return null; }
            return { text: saved.text, languageCode: lang };
        },
        beforeOpen: async () => {
            if (!state.versionId || !isEditable()) return false;
            return !state.dirty || await save();
        },
        onChanged: () => loadEvidence()
    });

    // ─── events ─────────────────────────────────────────────────────────────────
    const bind = () => {
        root.addEventListener('click', event => {
            const tab = event.target.closest('.js-cv-lang');
            if (tab) { state.activeLang = tab.dataset.lang; renderLanguages(); return; }
            const rq = event.target.closest('.js-cv-remove-qualifier');
            if (rq && state.activeLang) {
                state.texts[state.activeLang].qualifiers.splice(Number(rq.dataset.index), 1);
                renderLanguages(); markDirty(); return;
            }
            if (event.target.closest('#btnCvAddQualifier')) addQualifier();
        });
        root.addEventListener('input', event => {
            if (event.target.id === 'cvText' && state.activeLang) {
                if (!state.texts[state.activeLang]) state.texts[state.activeLang] = { text: '', qualifiers: [] };
                state.texts[state.activeLang].text = event.target.value;
                const count = byId('cvTextCount');
                if (count) count.textContent = fmt(t('CharCount'), event.target.value.length);
                markDirty();
            }
            if (event.target.id === 'cvAdaptReason') markDirty();
        });
        root.addEventListener('keydown', event => {
            if (event.target.id === 'cvQualifierInput' && event.key === 'Enter') { event.preventDefault(); addQualifier(); }
        });
        // Leaving a tab re-renders it: refresh the missing markers when the text field loses focus.
        root.addEventListener('focusout', event => { if (event.target.id === 'cvText') renderLanguages(); });
        byId('cvAdaptType')?.addEventListener('change', () => {
            byId('cvAdaptReasonRequired')?.classList.toggle('d-none', !reasonRequired());
            markDirty();
        });
        root.querySelectorAll('.js-save-draft').forEach(b => b.addEventListener('click', async () => { if (await save()) toast(t('ToastSaved')); }));
        root.querySelectorAll('.js-submit-review').forEach(b => b.addEventListener('click', () => {
            window.showConfirm?.(t('SubmitConfirm'), async () => {
                if ((!state.versionId || state.dirty) && !(await save())) return;
                if (await postAction('submit-review')) { toast(t('ToastSubmitted')); await reload(); }
            }, { type: 'info', confirmButtonText: t('SubmitLocal') });
        }));
        byId('btnCvWithdraw')?.addEventListener('click', () => {
            window.showConfirm?.(t('WithdrawConfirm'), async () => {
                if (await postAction('withdraw-review')) { toast(t('ToastWithdrawn')); await reload(); }
            }, { type: 'warning', confirmButtonText: t('WithdrawReview') });
        });
        byId('btnCvNewVersion')?.addEventListener('click', () => {
            window.showConfirm?.(t('NewVersionConfirm'), async () => {
                const newId = await postAction('new-version');
                if (newId && newId !== true) window.location.href = `/CRM/Claims/CountryVersions/${encodeURIComponent(newId)}/Edit`;
            }, { type: 'info', confirmButtonText: t('OpenNewVersion') });
        });
        byId('btnAddLocalEvidence')?.addEventListener('click', () => void evidenceUi.open());
        window.addEventListener('beforeunload', e => { if (state.dirty && isEditable()) { e.preventDefault(); e.returnValue = ''; } });
    };
    const addQualifier = () => {
        const input = byId('cvQualifierInput');
        const value = norm(input?.value);
        if (!value || !state.activeLang || !isEditable()) return;
        if (!state.texts[state.activeLang]) state.texts[state.activeLang] = { text: '', qualifiers: [] };
        const list = state.texts[state.activeLang].qualifiers;
        if (!list.includes(value)) list.push(value);
        renderLanguages();
        markDirty();
        byId('cvQualifierInput')?.focus();
    };

    // ─── init ───────────────────────────────────────────────────────────────────
    const init = async () => {
        bind();
        initDates();
        try {
            if (state.versionId) await loadVersion();
            const [claim, adaptation, profiles, coreEvidence] = await Promise.all([
                request('GET', `${api}/claims/${encodeURIComponent(state.claimId)}`),
                tryGet(`${api}/lookups/adaptation-types`),
                tryGet(`${api}/lookups/audience-profiles?includeArchived=true`),
                tryGet(`${api}/claims/${encodeURIComponent(state.claimId)}/evidence`)
            ]);
            state.claim = claim;
            state.coreEvidenceCount = coreEvidence ? coreEvidence.effectiveCount : null;
            state.adaptationTypes = Array.isArray(adaptation) ? adaptation : [];
            (Array.isArray(profiles) ? profiles : (profiles?.items || []))
                .forEach(p => { if (p?.audienceProfileId) state.audienceNames[p.audienceProfileId] = p.profileName || p.profileCode; });
            await loadCountry();
            const v = state.version;
            fillAdaptation(v?.adaptationTypeCode || 'verbatim');
            byId('cvAdaptReason').value = v?.adaptationReason || '';
            setDate('cvValidFrom', v ? isoDate(v.validFrom) : new Date().toISOString().slice(0, 10));
            setDate('cvValidTo', v ? isoDate(v.validTo) : '');
            fillAudience(v ? v.audienceProfileIds : (claim?.audienceProfileIds || []));
            renderCards();
            await Promise.all([loadEvidence(), loadFlow()]);
            state.dirty = false;
            applyLocks();
        } catch (error) {
            showAlert(error.status === 404 ? t('LoadFailed') : (error.message || t('LoadFailed')));
        }
    };

    init();
})(window, document);
