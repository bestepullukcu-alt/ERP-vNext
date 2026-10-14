'use strict';

/*
 * MOD-0022 E5W2 — Doğrulama (pack §11.3; mockup v2 tab 3). Page: Views/ESignature/Verification.cshtml.
 *
 *   ?module=&type=&id=  → GET signatures/by-subject (SignatureRecord[] signedAt ascending; 404 ESIGN_SUBJECT_NOT_FOUND)
 *   ?signature={id}     → GET signatures/{id} → its subject → the same list
 *   each record         → POST signatures/{id}/verify (esign.signatures.verify) → valid | changed | invalidated | tampered |
 *                         subjectUnresolvable, the hash at signing and the current hash (no state change; audited)
 *   Geçersiz kıl        → red reasoned confirm (≥ 10) → password → POST auth/reauth {intent: invalidate} → POST
 *                         signatures/{id}/invalidate {ticket, reason} (esign.signatures.invalidate); the record never changes
 *
 * The appearance shown is the record's own: a saved sample's image (GET samples/{id}/image — esign.signatures.read may read it),
 * a drawing's image hash (the contract serves no drawn image), or the name only. The password is emptied after every attempt.
 */
(function (global) {
    const E = global.ESign;
    const P = global.ESignPages;
    const t = E.t;
    const esc = E.escapeHtml;
    const $ = (id) => document.getElementById(id);

    let subject = null;      // { module, subjectType, subjectId }
    let records = [];
    let results = {};        // signatureId → VerificationResult
    let companies = [];
    let pending = null;      // { record, reason } while the password modal is open

    const canVerify = () => P.can('esign.signatures.verify');
    const canInvalidate = () => P.can('esign.signatures.invalidate') && !!$('esignInvalidatePwd');

    function show(state) {
        $('esignVerifyEmptyState').classList.toggle('d-none', state !== 'empty');
        $('esignVerifyResult').classList.toggle('d-none', state !== 'result');
    }

    function setForm(s) {
        $('esignVerifyModule').value = s.module;
        $('esignVerifyType').value = s.subjectType;
        $('esignVerifyId').value = s.subjectId;
    }

    async function loadSubject(s, pushUrl) {
        subject = s;
        setForm(s);
        if (pushUrl) {
            const q = new URLSearchParams({ module: s.module, type: s.subjectType, id: s.subjectId });
            global.history.replaceState(null, '', `${global.location.pathname}?${q.toString()}`);
        }
        const q = new URLSearchParams({ module: s.module, subjectType: s.subjectType, subjectId: s.subjectId });
        const r = await E.call('GET', `${P.API}/signatures/by-subject?${q.toString()}`);
        if (!r.ok) {
            show('empty');
            P.failed(r);
            return;
        }
        records = E.dataOf(r) || [];
        results = {};
        await verifyAll();
        render();
    }

    async function verifyAll() {
        if (!canVerify()) return;
        for (const rec of records) {
            const r = await E.call('POST', `${P.API}/signatures/${encodeURIComponent(rec.id)}/verify`);
            if (r.ok) results[rec.id] = E.dataOf(r);
        }
    }

    function counts() {
        const c = { valid: 0, changed: 0, invalidated: 0, tampered: 0, subjectUnresolvable: 0 };
        Object.values(results).forEach((v) => { if (v && c[v.status] !== undefined) c[v.status]++; });
        return c;
    }

    function render() {
        show('result');
        const last = records[records.length - 1] || null;
        $('esignVerifyModuleBadge').textContent = P.moduleLabel(subject.module);
        const company = last && last.legalEntityId ? companies.find((c) => c.id === last.legalEntityId) : null;
        $('esignVerifyCompany').textContent = company ? company.name : '';
        $('esignVerifyTitle').textContent = (last && last.subjectDisplay) || `${subject.subjectType} · ${subject.subjectId}`;
        const anyResult = Object.values(results).find((v) => v && v.currentSubjectHash);
        $('esignVerifyHash').textContent = anyResult ? `sha256:${anyResult.currentSubjectHash}` : '—';
        const verifiedAt = Object.values(results).map((v) => v && v.verifiedAtUtc).filter(Boolean).sort().pop();
        $('esignVerifyAt').textContent = verifiedAt ? t('ESign.Verify.LastVerified', P.at(verifiedAt)) : (canVerify() ? '' : t('ESign.Verify.NoVerifyPermission'));

        const c = counts();
        const chips = [
            `<span class="badge bg-label-primary"><i class="bx bx-pen me-1"></i>${esc(t('ESign.Verify.Count.Total', records.length))}</span>`
        ];
        if (canVerify()) {
            chips.push(`<span class="badge bg-label-success"><i class="bx bx-check-shield me-1"></i>${esc(t('ESign.Verify.Count.Valid', c.valid))}</span>`);
            if (c.changed) chips.push(`<span class="badge bg-label-warning"><i class="bx bx-git-compare me-1"></i>${esc(t('ESign.Verify.Count.Changed', c.changed))}</span>`);
            if (c.invalidated) chips.push(`<span class="badge bg-label-danger"><i class="bx bx-block me-1"></i>${esc(t('ESign.Verify.Count.Invalidated', c.invalidated))}</span>`);
            if (c.tampered) chips.push(`<span class="badge bg-label-danger"><i class="bx bx-error me-1"></i>${esc(t('ESign.Verify.Count.Tampered', c.tampered))}</span>`);
        }
        $('esignVerifyCounts').innerHTML = chips.join('');

        $('esignVerifyNone').classList.toggle('d-none', records.length > 0);
        $('esignVerifyTimeline').innerHTML = records.map(card).join('');
    }

    function appearanceImage(rec) {
        const a = rec.appearance || {};
        return a.type === 'sample' && a.sampleId ? `${P.API}/samples/${encodeURIComponent(a.sampleId)}/image` : null;
    }

    function card(rec) {
        const v = results[rec.id];
        const status = v ? v.status : (rec.invalidation ? 'invalidated' : null);
        const a = rec.appearance || {};
        const img = appearanceImage(rec);
        const signer = rec.signer || {};
        const visual = img
            ? `<img alt="" src="${esc(img)}" style="max-height:64px;max-width:160px">`
            : `<span class="text-center small"><span class="fw-medium d-block">${esc(signer.name || '')}</span><span class="text-muted">${esc(t(a.type === 'drawn' ? 'ESign.Verify.DrawnNoImage' : 'ESign.Verify.NameOnly'))}</span></span>`;
        const line = (label, value) => value ? `<div class="col-12 col-md-6"><span class="text-muted">${esc(label)}</span> <span class="font-monospace text-break">${esc(value)}</span></div>` : '';
        const changed = status === 'changed' && v
            ? `<div class="alert alert-warning d-flex gap-2 align-items-start mt-3 mb-0 py-2"><i class="bx bx-git-compare fs-5"></i><span>${esc(t('ESign.Verify.ChangedNote', v.subjectHashAtSign ? v.subjectHashAtSign.slice(0, 12) : '', v.currentSubjectHash ? v.currentSubjectHash.slice(0, 12) : '—'))}</span></div>` : '';
        const inv = rec.invalidation
            ? `<div class="alert alert-danger d-flex gap-2 align-items-start mt-3 mb-0 py-2"><i class="bx bx-block fs-5"></i><span><span class="d-block">${esc(t('ESign.Verify.InvalidatedReason', rec.invalidation.reason || ''))}</span><small>${esc(rec.invalidation.byName || rec.invalidation.by || '')} · ${esc(P.at(rec.invalidation.atUtc))}</small></span></div>` : '';
        let actions = '';
        if (!rec.invalidation) {
            actions = canInvalidate()
                ? `<div class="mt-3 text-end"><button type="button" class="btn btn-sm btn-label-danger" data-esign-invalidate="${esc(rec.id)}"><i class="bx bx-block me-1"></i>${esc(t('ESign.Verify.Invalidate'))}</button></div>`
                : `<div class="mt-3 small text-muted"><i class="bx bx-lock-alt me-1"></i>${esc(t('ESign.Verify.InvalidateNoPermission'))}</div>`;
        }
        return `<div class="card mb-3" data-esign-signature="${esc(rec.id)}"><div class="card-body">
            <div class="d-flex flex-wrap gap-3 align-items-start">
                <button type="button" class="border rounded bg-white d-grid p-2" style="min-width:170px;min-height:80px;place-items:center;cursor:zoom-in" data-esign-zoom="${esc(rec.id)}" title="${esc(t('ESign.Zoom.Enlarge'))}">${visual}</button>
                <div class="flex-grow-1">
                    <div class="d-flex flex-wrap align-items-center gap-2"><span class="fw-medium text-heading">${esc(signer.name || '')}</span><small class="text-muted">${esc(signer.login || '')}</small><span class="badge bg-label-primary">${esc(rec.meaningText || rec.meaningCode || '')}</span></div>
                    <small class="text-muted">${esc(P.at(rec.signedAtUtc))}</small>
                    ${rec.note ? `<div class="small mt-1">${esc(t('ESign.Verify.Note', rec.note))}</div>` : ''}
                </div>
                <div>${status ? P.verifyBadge(status) : ''}</div>
            </div>
            <div class="row g-1 small mt-2">
                ${line(t('ESign.Verify.AppearanceType'), t(`ESign.Dialog.AppType.${a.type || 'none'}`))}
                ${line(t('ESign.Verify.SignatureId'), rec.signatureNumber || rec.id)}
                ${line(t('ESign.Verify.HashAtSign'), rec.subjectHash ? `sha256:${rec.subjectHash}` : '')}
                ${line(t('ESign.Verify.ImageHash'), a.imageHash ? `sha256:${a.imageHash}` : '')}
            </div>
            ${changed}${inv}${actions}
        </div></div>`;
    }

    function zoom(rec) {
        const a = rec.appearance || {};
        const signer = rec.signer || {};
        E.zoom({
            img: appearanceImage(rec), signer: signer.name, login: signer.login, type: t(`ESign.Dialog.AppType.${a.type || 'none'}`),
            at: P.at(rec.signedAtUtc), storage: '', hash: a.imageHash ? `sha256:${a.imageHash}` : '—', sub: rec.signatureNumber || rec.id
        });
    }

    // ── invalidate: red reason → password → ticket → invalidate ────────────────────────────────────────────────────
    async function invalidate(rec) {
        const reason = await P.reason({
            title: t('ESign.Verify.Invalidate.Title'),
            subtext: t('ESign.Verify.Invalidate.Sub', rec.signatureNumber || rec.id, (rec.signer && rec.signer.name) || ''),
            confirmText: t('ESign.Common.Continue'),
            type: 'danger'
        });
        if (!reason) return;
        pending = { record: rec, reason };
        $('esignInvalidatePwdSub').textContent = rec.signatureNumber || rec.id;
        $('esignInvalidateFormError').classList.add('d-none');
        fieldError('');
        $('esignInvalidatePassword').value = '';
        global.bootstrap.Modal.getOrCreateInstance($('esignInvalidatePwd')).show();
    }

    function fieldError(text) {
        $('esignInvalidatePassword').classList.toggle('is-invalid', !!text);
        $('esignInvalidatePasswordError').textContent = text || '';
    }

    async function submitInvalidate(e) {
        e.preventDefault();
        if (!pending) return;
        const input = $('esignInvalidatePassword');
        const button = $('esignInvalidateSubmit');
        let password = input.value;
        if (!password) { fieldError(t('ESign.Dialog.Err.PasswordRequired')); return; }
        button.disabled = true;
        try {
            const auth = await E.call('POST', '/ESignature/auth/reauth', { password, intent: 'invalidate' });
            password = null;
            if (!auth.ok) {
                const code = E.codeOf(auth);
                const left = E.paramsOf(auth).remainingAttempts;
                fieldError(code === 'ESIGN_REAUTH_FAILED' && left != null ? t('ESign.Dialog.Err.WrongPassword', left) : E.message(code));
                return;
            }
            const ticket = (E.dataOf(auth) || {}).ticket;
            const r = await E.call('POST', `${P.API}/signatures/${encodeURIComponent(pending.record.id)}/invalidate`, { ticket, reason: pending.reason, idempotencyKey: E.newKey() });
            if (!r.ok) {
                const box = $('esignInvalidateFormError');
                box.textContent = E.message(E.codeOf(r));
                box.classList.remove('d-none');
                return;
            }
            const number = pending.record.signatureNumber || pending.record.id;
            pending = null;
            global.bootstrap.Modal.getOrCreateInstance($('esignInvalidatePwd')).hide();
            P.toast(t('ESign.Verify.Invalidated', number));
            await loadSubject(subject, false);
        } finally {
            input.value = '';
            password = null;
            button.disabled = false;
        }
    }

    // ── recent records (only with the export key: searchSignatures is the one operation that lists records) ───────────
    async function loadRecent() {
        if (!P.can('esign.signatures.export')) return;
        const r = await E.call('GET', '/ESignature/list/signatures?start=0&length=100');
        if (!r.ok) return;
        const seen = new Map();
        ((E.dataOf(r) || {}).items || []).forEach((row) => {
            const s = row.signature && row.signature.subject;
            if (!s) return;
            const key = `${s.module}|${s.subjectType}|${s.subjectId}`;
            if (!seen.has(key)) seen.set(key, { value: key, text: `${P.moduleLabel(s.module)} · ${row.signature.subjectDisplay || s.subjectId}` });
        });
        if (!seen.size) return;
        P.fill($('esignVerifyRecent'), [...seen.values()], t('ESign.Verify.RecentPick'));
        $('esignVerifyRecentWrap').classList.remove('d-none');
    }

    function wire() {
        $('esignVerifyForm').addEventListener('submit', (e) => {
            e.preventDefault();
            const s = { module: $('esignVerifyModule').value, subjectType: $('esignVerifyType').value.trim(), subjectId: $('esignVerifyId').value.trim() };
            if (s.module && s.subjectType && s.subjectId) loadSubject(s, true);
        });
        $('esignVerifyRecent').addEventListener('change', (e) => {
            const [module, subjectType, subjectId] = String(e.target.value || '').split('|');
            if (module && subjectType && subjectId) loadSubject({ module, subjectType, subjectId }, true);
        });
        $('esignReverify')?.addEventListener('click', () => { if (subject) loadSubject(subject, false); });
        $('esignVerifyTimeline').addEventListener('click', (e) => {
            const z = e.target.closest('[data-esign-zoom]');
            if (z) { const rec = records.find((x) => x.id === z.getAttribute('data-esign-zoom')); if (rec) zoom(rec); return; }
            const inv = e.target.closest('[data-esign-invalidate]');
            if (inv) { const rec = records.find((x) => x.id === inv.getAttribute('data-esign-invalidate')); if (rec) invalidate(rec); }
        });
        const modal = $('esignInvalidatePwd');
        if (modal) {
            $('esignInvalidatePwdForm').addEventListener('submit', submitInvalidate);
            $('esignInvalidatePassword').addEventListener('input', () => fieldError(''));
            modal.addEventListener('hidden.bs.modal', () => { $('esignInvalidatePassword').value = ''; pending = null; });
        }
    }

    async function init() {
        wire();
        companies = await P.companies();
        loadRecent();
        const q = new URLSearchParams(global.location.search);
        const signatureId = q.get('signature');
        if (signatureId) {
            const r = await E.call('GET', `${P.API}/signatures/${encodeURIComponent(signatureId)}`);
            if (!r.ok) { show('empty'); P.failed(r); return; }
            const s = (E.dataOf(r) || {}).subject;
            if (s) { await loadSubject(s, true); return; }
        }
        const module = q.get('module');
        const type = q.get('type');
        const id = q.get('id');
        if (module && type && id) { await loadSubject({ module, subjectType: type, subjectId: id }, false); return; }
        show('empty');
    }

    init();
})(window);
