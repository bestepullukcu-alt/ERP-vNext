/**
 * WP-CL-FE-4 — the shared claim evidence module (moved out of FE-3 form.js, behaviour unchanged): evidence cards, the
 * two-step "add evidence" modal (document → pinned version, type, locator, quote, supported phrases marked on the text
 * by mouse or keyboard selection, ≤ 10 phrases) and the "remove evidence" modal (reason required). Used by the core
 * claim form (form.js) and the country version form (country-version.js); the markup is _ClaimEvidenceModal.cshtml and
 * the strings are the ClaimsForm resx family (bridge #claim-form-l10n).
 *
 * window.ClaimEvidence.create({
 *   linkUrl(): string            POST url of a new link (the addressed claim / country version)
 *   source(): { text, languageCode } | null   the saved text the supported phrases are marked on
 *   beforeOpen(): Promise<bool>  e.g. save pending changes first
 *   onChanged(): Promise         reload the evidence list after add / remove
 * }) → { open, openRemove, card, typeLabel, loadTypes }
 */
(function (window, document) {
    'use strict';
    const api = '/CRM/Claims/api/v2';
    const MAX_SPANS = 10;
    const REVIEW_DOC_STATES = ['suspended', 'retired', 'withdrawn'];
    const L = (() => { try { return JSON.parse(document.getElementById('claim-form-l10n')?.textContent || '{}'); } catch (e) { return {}; } })();

    const t = key => L[key] || key;
    const byId = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const fmt = (template, ...args) => String(template ?? '').replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    const toast = (msg, type) => window.showToast?.(msg, type || 'success');
    const date = v => v ? new Date(v).toLocaleDateString() : '';

    /** CRM refusals are [code, message]; Platform / proxy refusals may be a single message. */
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

    // ─── evidence types: the label is the resx text keyed by value_code (EvType_{code}), never the bare code ───
    let types = null;
    const loadTypes = async () => {
        if (types) return types;
        types = {};
        try {
            const data = await request('GET', `${api}/lookups/evidence-types`);
            (Array.isArray(data) ? data : []).forEach(v => { if (v?.code) types[v.code] = v.name || v.code; });
        } catch (e) { /* the picker stays empty; the add call then reports the missing set */ }
        return types;
    };
    const typeLabel = code => L['EvType_' + code] || (types && types[code] !== code ? types[code] : null) || code || '';

    // ─── cards ──────────────────────────────────────────────────────────────────
    // WP-CL-FIX-1 — document lifecycle state label (resx DocStateLabel_{state}); unknown / absent → no card badge.
    const stateLabel = s => s ? (L['DocStateLabel_' + s] || s) : '';
    const STATE_TONES = { effective: 'success', suspended: 'warning', retired: 'secondary', withdrawn: 'danger' };
    const stateBadge = s => s && s !== 'unknown'
        ? ` <span class="badge bg-label-${STATE_TONES[s] || 'secondary'} mb-1">${esc(stateLabel(s))}</span>` : '';

    const previewUrl = item => {
        if (item.documentKind !== 'controlled' || !item.documentId || !item.documentVersionId) return null;
        const page = norm(item.locator?.page);
        const anchor = /^\d+$/.test(page) ? `#page=${page}` : '';
        return `/DocumentManagementControlledDocuments/preview/${encodeURIComponent(item.documentId)}/${encodeURIComponent(item.documentVersionId)}${anchor}`;
    };

    /** One evidence card. options.removable → remove button; options.origin → "from core" / "local" badge. */
    const card = (item, options) => {
        const opts = options || {};
        const locator = item.locator || {};
        const reference = [locator.section, locator.page, locator.table].filter(Boolean).join(' · ');
        // WP-CL-FIX-1 — the document CODE (register code / canonical id / key), the id fragment only as a last resort.
        const code = item.documentCode || (item.documentId ? String(item.documentId).slice(0, 8) : '');
        const meta = [code, item.documentVersionLabel ? fmt(t('EvidencePinned'), item.documentVersionLabel) : '',
            item.documentKind === 'external' ? t('EvKindExternal') : t('EvKindControlled'), reference].filter(Boolean).join(' · ');
        const supports = (item.supportedSpans || []).map(s => s.text).join(' … ');
        const warnings = [];
        if (item.isSuperseded) warnings.push(fmt(t('EvidenceSuperseded'), item.currentVersionLabel || ''));
        if (REVIEW_DOC_STATES.includes(item.documentState)) warnings.push(fmt(t('EvidenceStateWarning'), t('DocState_' + item.documentState)));
        if (item.isExpiring && item.reviewDueAt) warnings.push(fmt(t('EvidenceExpiring'), date(item.reviewDueAt)));
        const url = previewUrl(item);
        const origin = opts.origin
            ? (item.origin === 'core'
                ? `<span class="badge bg-label-primary me-1"><i class="bx bx-globe me-1"></i>${esc(t('EvOriginCore'))}</span>`
                : `<span class="badge bg-label-info me-1"><i class="bx bx-map-pin me-1"></i>${esc(t('EvOriginLocal'))}</span>`)
            : '';
        return `<div class="claim-evidence-card${warnings.length ? ' needs-review' : ''}">`
            + `<div class="d-flex justify-content-between align-items-start gap-2"><div class="min-w-0">`
            + `${origin}<span class="badge bg-label-secondary mb-1">${esc(typeLabel(item.evidenceTypeCode))}</span>${stateBadge(item.documentState)}<div class="fw-medium">${esc(item.documentTitle)}</div>`
            + `<small class="text-muted">${esc(meta)}</small></div><div class="d-flex gap-1 flex-shrink-0">`
            + (url ? `<a class="btn btn-sm btn-label-secondary" href="${esc(url)}" target="_blank" rel="noopener"><i class="bx bx-show me-1"></i>${esc(t('EvidencePreview'))}</a>` : '')
            + (opts.removable ? `<button type="button" class="btn btn-sm btn-icon btn-text-danger js-remove-evidence" data-link-id="${esc(item.linkId)}" aria-label="${esc(t('RemoveAction'))}" title="${esc(t('RemoveAction'))}"><i class="bx bx-trash"></i></button>` : '')
            + '</div></div>'
            + (locator.quote ? `<div class="claim-evidence-quote my-2">“${esc(locator.quote)}”</div>` : '')
            + (supports ? `<small><span class="text-muted">${esc(t('EvidenceSupports'))}</span> ${esc(supports)}</small>` : '')
            + warnings.map(w => `<div class="claim-evidence-warning mt-2"><i class="bx bx-error me-1"></i>${esc(w)}</div>`).join('')
            + '</div>';
    };

    // ─── modals ─────────────────────────────────────────────────────────────────
    const create = options => {
        const opts = options || {};
        const modalEl = byId('claimEvidenceModal');
        const removeModalEl = byId('claimEvidenceRemoveModal');
        const modal = { doc: null, spans: [], docs: [], languageCode: 'en' };
        let removeLinkId = null;
        let searchTimer = null;

        const evAlert = message => { const el = byId('evAlert'); if (el) { el.textContent = message || ''; el.classList.toggle('d-none', !message); } };
        const pinLabel = doc => doc.kind === 'controlled' ? doc.currentVersionLabel : doc.sourceVersion;
        const setStep = step => {
            modalEl?.querySelectorAll('[data-step]').forEach(n => n.classList.toggle('d-none', n.dataset.step !== String(step)));
            modalEl?.querySelectorAll('[data-step-label]').forEach(n => n.classList.toggle('active', n.dataset.stepLabel === String(step)));
            byId('evBack')?.classList.toggle('d-none', step === 1);
            byId('evNext')?.classList.toggle('d-none', step !== 1);
            byId('evAdd')?.classList.toggle('d-none', step !== 2);
            const preview = byId('evPreview');
            const doc = modal.doc;
            const url = step === 2 && doc && doc.kind === 'controlled' && doc.currentVersionId
                ? `/DocumentManagementControlledDocuments/preview/${encodeURIComponent(doc.documentId)}/${encodeURIComponent(doc.currentVersionId)}` : null;
            if (preview) { preview.classList.toggle('d-none', !url); preview.setAttribute('href', url || '#'); }
            evAlert('');
        };
        const renderDocs = () => {
            const host = byId('evDocList');
            if (!host) return;
            if (!modal.docs.length) { host.innerHTML = `<div class="text-muted small p-2">${esc(t('EvNoDocuments'))}</div>`; return; }
            host.innerHTML = modal.docs.map((d, i) => {
                const pinnable = d.kind !== 'controlled' || !!d.currentVersionId;
                // WP-CL-FIX-1 — the lifecycle state (resx label), no longer the item status ("Active").
                const meta = [d.code, d.documentType, pinLabel(d), d.countryCode, stateLabel(d.documentState)].filter(Boolean).join(' · ');
                const active = modal.doc && modal.doc.documentId === d.documentId ? ' active' : '';
                return `<button type="button" class="list-group-item list-group-item-action js-ev-doc${active}" data-index="${i}" role="option" aria-selected="${active ? 'true' : 'false'}"${pinnable ? '' : ' disabled'}>`
                    + `<div class="fw-medium">${esc(d.title)}</div><small class="text-muted">${esc(meta)}</small></button>`;
            }).join('');
        };
        const loadDocs = async () => {
            const search = encodeURIComponent(norm(byId('evSearch')?.value));
            const kind = encodeURIComponent(byId('evKind')?.value || 'controlled');
            try {
                const data = await request('GET', `${api}/claims/evidence/document-options?search=${search}&kind=${kind}`);
                modal.docs = Array.isArray(data) ? data : [];
                evAlert('');
            } catch (error) {
                modal.docs = [];
                evAlert(error.message);
            }
            renderDocs();
        };
        const renderSpans = () => {
            const host = byId('evSpans');
            if (!host) return;
            host.innerHTML = modal.spans.length
                ? modal.spans.map((s, i) => `<span class="ev-span-chip">${esc(s.text)}<button type="button" class="btn-close btn-close-sm js-ev-span-remove" data-index="${i}" aria-label="${esc(t('RemoveAction'))}"></button></span>`).join('')
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
            if (modal.spans.length >= MAX_SPANS) { evAlert(t('EvSpansMax')); return; }
            if (!modal.spans.some(s => s.start === start && s.end === end)) {
                modal.spans.push({ languageCode: modal.languageCode, text: value.slice(start, end), start, end });
                modal.spans.sort((a, b) => a.start - b.start);
            }
            evAlert('');
            renderSpans();
        };
        const open = async () => {
            if (opts.beforeOpen && !(await opts.beforeOpen())) return;
            // A null source means the caller already explained why (e.g. the country page's "save this language first").
            const source = opts.source ? opts.source() : null;
            if (!source) return;
            if (!norm(source.text)) { toast(t('EvSaveTextFirst'), 'warning'); return; }
            modal.doc = null; modal.spans = []; modal.docs = []; modal.languageCode = source.languageCode || 'en';
            ['evSearch', 'evSection', 'evPage', 'evTable', 'evQuote'].forEach(id => { const el = byId(id); if (el) el.value = ''; });
            const sourceEl = byId('evSpanSource');
            if (sourceEl) sourceEl.value = source.text;
            const all = await loadTypes();
            const typeEl = byId('evType');
            if (typeEl) typeEl.innerHTML = '<option value=""></option>' + Object.keys(all).map(code => `<option value="${esc(code)}">${esc(typeLabel(code))}</option>`).join('');
            byId('evNext').disabled = true;
            renderSpans();
            setStep(1);
            if (modalEl && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
            await loadDocs();
        };
        const goStep2 = () => {
            const doc = modal.doc;
            if (!doc) return;
            byId('evDocTitle').textContent = doc.title;
            byId('evDocMeta').textContent = [doc.code, doc.documentType, doc.kind === 'external' ? t('EvKindExternal') : t('EvKindControlled')].filter(Boolean).join(' · ');
            byId('evDocPin').textContent = pinLabel(doc) ? fmt(t('EvWillPin'), pinLabel(doc)) : (doc.kind === 'external' ? t('EvPreviewExternalNote') : '');
            setStep(2);
        };
        const addEvidence = async () => {
            const doc = modal.doc;
            const type = byId('evType')?.value;
            const quote = norm(byId('evQuote')?.value);
            if (!doc || !type || !quote || modal.spans.length === 0) { evAlert(t('EvRequiredMissing')); return; }
            const button = byId('evAdd');
            if (button) button.disabled = true;
            try {
                await request('POST', opts.linkUrl(), {
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
                    supportedSpans: modal.spans
                });
                window.bootstrap?.Modal.getOrCreateInstance(modalEl).hide();
                toast(t('ToastEvidenceAdded'));
                if (opts.onChanged) await opts.onChanged();
            } catch (error) {
                evAlert(error.message);
            } finally {
                if (button) button.disabled = false;
            }
        };

        const openRemove = linkId => {
            removeLinkId = linkId;
            const reason = byId('evRemoveReason');
            if (reason) { reason.value = ''; reason.classList.remove('is-invalid'); }
            if (removeModalEl && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(removeModalEl).show();
        };
        const confirmRemove = async () => {
            const reasonEl = byId('evRemoveReason');
            const reason = norm(reasonEl?.value);
            if (!reason) { reasonEl?.classList.add('is-invalid'); toast(t('Err_removal_reason_required'), 'warning'); return; }
            try {
                await request('POST', `${api}/claims/evidence/${encodeURIComponent(removeLinkId)}/remove`, { reason });
                window.bootstrap?.Modal.getOrCreateInstance(removeModalEl).hide();
                toast(t('ToastEvidenceRemoved'));
                if (opts.onChanged) await opts.onChanged();
            } catch (error) {
                toast(error.message, 'error');
            }
        };

        byId('evSearch')?.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => void loadDocs(), 300); });
        byId('evKind')?.addEventListener('change', () => { modal.doc = null; byId('evNext').disabled = true; void loadDocs(); });
        byId('evNext')?.addEventListener('click', goStep2);
        byId('evBack')?.addEventListener('click', () => setStep(1));
        byId('evAdd')?.addEventListener('click', () => void addEvidence());
        byId('evAddSelection')?.addEventListener('click', addSelection);
        byId('evRemoveConfirm')?.addEventListener('click', () => void confirmRemove());
        document.addEventListener('click', event => {
            const remove = event.target.closest('.js-remove-evidence');
            if (remove) { openRemove(remove.dataset.linkId); return; }
            const doc = event.target.closest('.js-ev-doc');
            if (doc && !doc.disabled) {
                modal.doc = modal.docs[Number(doc.dataset.index)] || null;
                byId('evNext').disabled = !modal.doc;
                renderDocs();
                return;
            }
            const span = event.target.closest('.js-ev-span-remove');
            if (span) { modal.spans.splice(Number(span.dataset.index), 1); renderSpans(); }
        });

        return { open, openRemove, card, typeLabel, loadTypes };
    };

    window.ClaimEvidence = Object.freeze({ create, card, typeLabel, loadTypes });
})(window, document);
