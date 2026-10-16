/**
 * WP-VW-W3 (W3-WEB-c) — A3 · one answer (the Work Center deep link too).
 *  - Status band: in review → "Onay bekliyor · who, when"; approved → "cannot be changed, retire + new"; retired →
 *    "retired · who, when"; a draft that came back → the reviewer's note (or the SoD / claim reason).
 *  - Actions = ObjectionCore.actions(dto): CRM's abilities AND the status. Approve / return are the reviewer's (the return
 *    needs a note — the dialog's button stays disabled until one is written, and the proxy refuses an empty one too);
 *    the submitter sees the SoD hint instead of an approve button.
 *  - Retire: a dialog naming how many steps use the answer; it leaves published presentations at once (K-A5).
 *  - "Kullanıldığı yerler" (GET usages) and the history (rounds + decisions + retire), newest first.
 */
(function (window, document) {
    'use strict';
    const C = window.ObjectionCore;
    const P = window.ObjectionPage;
    const root = document.getElementById('orDetailsRoot');
    if (!C || !P || !root) return;
    const L = P.L;
    const api = P.api(root.dataset.apiBase);
    const id = root.dataset.id;
    const canReadClaims = root.dataset.canReadClaims === 'true';
    const $ = x => document.getElementById(x);
    const esc = P.esc;

    let dto = null;
    let usages = [];
    let claims = [];

    const modal = el => (window.bootstrap && window.bootstrap.Modal ? window.bootstrap.Modal.getOrCreateInstance(el) : null);
    const open = el => { const m = modal(el); if (m) m.show(); else { el.classList.add('show'); el.style.display = 'block'; } };
    const close = el => { const m = modal(el); if (m) m.hide(); else { el.classList.remove('show'); el.style.display = 'none'; } };

    // ---------------- render ----------------

    const names = () => (dto && dto.displayNames) || {};
    const who = userId => C.personName(userId, names(), L.UnknownUser);

    const ACTION_BUTTONS = {
        edit: () => `<a class="btn btn-sm btn-label-primary" href="/CRM/ObjectionResponses/Edit/${encodeURIComponent(id)}"><i class="bx bx-edit me-1" aria-hidden="true"></i>${esc(L.EditButton)}</a>`,
        submit: () => `<button type="button" class="btn btn-sm btn-primary" data-action="submit"><i class="bx bx-send me-1" aria-hidden="true"></i>${esc(L.SubmitForApproval)}</button>`,
        withdraw: () => `<button type="button" class="btn btn-sm btn-label-secondary" data-action="withdraw"><i class="bx bx-undo me-1" aria-hidden="true"></i>${esc(L.WithdrawButton)}</button>`,
        return: () => `<button type="button" class="btn btn-sm btn-outline-secondary" data-action="return">${esc(L.ReturnToDraft)}</button>`,
        approve: () => `<button type="button" class="btn btn-sm btn-success" data-action="approve"><i class="bx bx-check me-1" aria-hidden="true"></i>${esc(L.ApproveButton)}</button>`,
        retire: () => `<button type="button" class="btn btn-sm btn-outline-danger" data-action="retire"><i class="bx bx-archive me-1" aria-hidden="true"></i>${esc(L.RetireButton)}</button>`,
        copy: () => `<button type="button" class="btn btn-sm btn-primary" data-action="copy"><i class="bx bx-copy me-1" aria-hidden="true"></i>${esc(L.Copy)}</button>`
    };
    // the band's order: secondary actions first, the primary one last (mockup)
    const ORDER = ['edit', 'withdraw', 'return', 'retire', 'submit', 'approve', 'copy'];

    const renderBand = () => {
        const band = $('orBand');
        const icon = $('orBandIcon');
        const text = $('orBandText');
        const tone = { draft: 'alert-secondary', in_review: 'alert-warning', approved: 'alert-info', retired: 'alert-dark' }[dto.status] || 'alert-secondary';
        band.className = `alert ${tone} d-flex flex-wrap align-items-center gap-2`;
        let line = '';
        let ic = 'bx-info-circle';
        if (dto.status === 'in_review') {
            const p = C.pendingLine(dto, names(), L.UnknownUser);
            line = C.fmt(L.PendingLine || '{0} {1}', p.by, P.dateTime(p.at));
            ic = 'bx-time-five';
        } else if (dto.status === 'approved') {
            line = L.ApprovedNote || '';
            ic = 'bx-lock-alt';
        } else if (dto.status === 'retired') {
            line = C.fmt(L.RetiredLine || '{0} {1}', who(dto.retiredBy), P.dateTime(dto.retiredAt));
            ic = 'bx-archive';
        } else {
            const back = C.returnNote(dto);
            if (back && back.kind === 'returned' && !back.reasonCode) {
                line = C.fmt(L.DraftReturnedLine || '{0} {1} {2}', who(back.by), P.dateTime(back.at), back.comment || '');
                ic = 'bx-revision';
            } else if (back && back.reasonCode) {
                line = C.fmt(L.DraftReturnedReason || '{0}', L['Err_' + back.reasonCode] || back.reasonCode);
                ic = 'bx-error';
            }
        }
        icon.className = `bx fs-5 ${ic}`;
        text.textContent = line;
        const allowed = C.actions(dto);
        $('orActions').innerHTML = ORDER.filter(a => allowed.indexOf(a) > -1).map(a => ACTION_BUTTONS[a]()).join('');
        band.classList.toggle('d-none', !line && allowed.length === 0);

        // The submitter cannot approve their own answer: say so where the approve button would be.
        const sod = $('orSodHint');
        const own = dto.status === 'in_review' && allowed.indexOf('approve') < 0 && allowed.indexOf('withdraw') > -1;
        sod.classList.toggle('d-none', !own);
        sod.querySelector('span').textContent = own ? (L.SodHint || '') : '';
    };

    const renderContent = () => {
        $('orCrumbProduct').textContent = dto.productName || '';
        $('orStatusChips').innerHTML = P.statusBadge(dto.status);
        $('orProductText').textContent = [dto.productName, dto.productForm].filter(Boolean).join(' · ') || '—';
        $('orCategoryText').innerHTML = P.categoryChip(dto.categoryCode) || '—';
        $('orQuestionText').textContent = dto.question ? `“${dto.question}”` : '—';
        $('orAnswerText').textContent = dto.answer || '—';
        const ids = dto.claimIds || [];
        $('orClaimsTotal').textContent = String(ids.length);
        $('orClaimsList').innerHTML = ids.map(cid => {
            const c = claims.find(x => String(x.id).toLowerCase() === String(cid).toLowerCase());
            const live = c && String(c.status || '').toLowerCase() === 'approved' && !c.isArchived;
            return `<div class="border rounded p-2 small">
                ${c ? `<code>${esc(c.code || '')}</code><div class="text-heading text-break">${esc(c.text || '')}</div>${c.name ? `<div class="text-muted">${esc(c.name)}</div>` : ''}` : `<code>${esc(String(cid).slice(0, 8))}</code>`}
                ${!live && canReadClaims ? `<div class="text-danger"><i class="bx bx-error-circle me-1" aria-hidden="true"></i>${esc(L.ClaimNotApprovedAnymore || '')}</div>` : ''}
            </div>`;
        }).join('');
        P.renderPreview(dto.question, dto.answer, ids.map(cid => claims.find(x => String(x.id).toLowerCase() === String(cid).toLowerCase())).filter(Boolean).map(P.claimRef));
    };

    const pathStatusBadge = s => {
        const tone = { published: 'success', approved: 'success', draft: 'secondary', review: 'info', inactive: 'warning', archived: 'secondary' }[s] || 'secondary';
        return `<span class="badge bg-label-${tone}">${esc(L['PathStatus_' + s] || s || '')}</span>`;
    };

    const renderUsages = () => {
        $('orUsageCount').textContent = C.fmt(L.UsageCount || '{0}', usages.length);
        $('orUsages').innerHTML = usages.length === 0
            ? `<p class="text-muted text-center small py-4 mb-0">${esc(L.UsagesNone || '')}</p>`
            : `<div class="table-responsive"><table class="table table-sm mb-0">
                <thead><tr><th>${esc(L.UsagePath)}</th><th>${esc(L.UsageVersion)}</th><th>${esc(L.UsageStep)}</th></tr></thead>
                <tbody>${usages.map(u => `<tr>
                    <td><a href="/CRM/KnowledgePaths/${encodeURIComponent(u.pathId)}">${esc(u.pathName || '')}</a></td>
                    <td><span class="text-nowrap">${esc(u.pathVersion ? 'v' + u.pathVersion : '')}</span> ${pathStatusBadge(u.pathStatus)}</td>
                    <td>${esc(C.fmt(L.UsageStepLine || '{0} · {1}', u.stepOrder, u.stepTitle || ''))}</td>
                </tr>`).join('')}</tbody></table></div>`;
    };

    const DOTS = { created: 'is-created', submitted: 'is-pending', approved: 'is-approved', returned: 'is-rejected', withdrawn: 'is-neutral', timeout: 'is-neutral', retired: 'is-neutral' };
    const renderHistory = () => {
        $('orHistoryTitle').textContent = L.HistoryTitle || '';
        const events = C.timeline(dto, names(), L.UnknownUser);
        $('orHistory').innerHTML = events.map(e => `<li>
            <span class="or-dot ${DOTS[e.kind] || ''}" aria-hidden="true"></span>
            <div class="fw-medium text-heading">${esc(L['Event_' + e.kind] || e.kind)}</div>
            <small class="text-muted">${esc(C.fmt(L.EventLine || '{0} · {1}', e.by, P.dateTime(e.at)))}</small>
            ${e.comment ? `<div class="small or-quote mt-1">${esc(e.comment)}</div>` : ''}
            ${e.reasonCode ? `<div class="small text-danger mt-1">${esc(L['Err_' + e.reasonCode] || e.reasonCode)}</div>` : ''}
        </li>`).join('');
    };

    const render = () => {
        renderBand();
        renderContent();
        renderUsages();
        renderHistory();
    };

    // ---------------- load ----------------

    const load = async () => {
        P.hideAlert($('orAlert'));
        try {
            dto = await api.get(`/objection-responses/${id}`);
        } catch (error) {
            P.showAlert($('orAlert'), error.status === 404 ? L.NotFound : (L.LoadFailed || error));
            $('orBody').classList.add('d-none');
            return;
        }
        const [u, cl] = await Promise.all([
            api.get(`/objection-responses/${id}/usages`).catch(() => []),
            canReadClaims && dto.productId ? api.get(`/lookups/claims?productId=${encodeURIComponent(dto.productId)}&scope=all`).catch(() => []) : Promise.resolve([]),
            P.loadCategories(root.dataset.apiBase).catch(() => [])
        ]);
        usages = u || [];
        claims = cl || [];
        render();
    };

    // ---------------- actions ----------------

    const run = async (work, success, after) => {
        P.hideAlert($('orAlert'));
        try {
            const result = await work();
            if (success) P.toast(success);
            if (after) { after(result); return; }
        } catch (error) {
            P.showAlert($('orAlert'), error);
        }
        await load();
    };

    const confirmThen = (text, go) => {
        if (window.showConfirm) window.showConfirm(text, go, { type: 'warning' });
        else if (window.confirm(text)) go();
    };

    const onAction = action => {
        switch (action) {
            case 'submit':
                confirmThen(L.SubmitConfirm, () => run(() => api.post(`/objection-responses/${id}/submit`), L.Submitted));
                break;
            case 'withdraw':
                confirmThen(L.WithdrawConfirm, () => run(() => api.post(`/objection-responses/${id}/withdraw`), L.Withdrawn));
                break;
            case 'approve':
                confirmThen(L.ApproveConfirm, () => run(() => api.post(`/objection-responses/${id}/decision`, C.decisionBody('approve', null).body), L.Approved));
                break;
            case 'return':
                $('orReturnNote').value = '';
                $('orReturnConfirm').disabled = true;
                $('orReturnNoteError').classList.add('d-none');
                open($('orReturnModal'));
                break;
            case 'retire': {
                const n = usages.length || dto.usageCount || 0;
                $('orRetireUsed').textContent = n > 0 ? C.fmt(L.RetireUsed || '{0}', n) : '';
                $('orRetireUsed').classList.toggle('d-none', n === 0);
                open($('orRetireModal'));
                break;
            }
            case 'copy':
                run(() => api.post(`/objection-responses/${id}/copy`), L.Copied, newId => {
                    const target = typeof newId === 'string' ? newId : (newId && (newId.id || newId.value));
                    if (target) window.location.href = `/CRM/ObjectionResponses/Edit/${encodeURIComponent(target)}`;
                });
                break;
            default:
                break;
        }
    };

    $('orActions').addEventListener('click', e => {
        const button = e.target.closest ? e.target.closest('[data-action]') : null;
        if (button) onAction(button.getAttribute('data-action'));
    });

    // The return dialog: the note is required (K-A1).
    $('orReturnTitle').textContent = L.ReturnTitle || '';
    $('orReturnNoteLabel').textContent = L.ReturnNoteLabel || '';
    $('orReturnCancel').textContent = L.CancelButton || '';
    $('orReturnConfirm').textContent = L.ReturnConfirm || '';
    $('orReturnNote').addEventListener('input', () => {
        $('orReturnConfirm').disabled = !C.decisionBody('return', $('orReturnNote').value).ok;
    });
    $('orReturnConfirm').addEventListener('click', () => {
        const decision = C.decisionBody('return', $('orReturnNote').value);
        if (!decision.ok) {
            $('orReturnNoteError').textContent = L.ReturnNoteRequired || '';
            $('orReturnNoteError').classList.remove('d-none');
            return;
        }
        close($('orReturnModal'));
        run(() => api.post(`/objection-responses/${id}/decision`, decision.body), L.Returned);
    });

    // The retire dialog (K-A5: it leaves published presentations at once).
    $('orRetireTitle').textContent = L.RetireTitle || '';
    $('orRetireText').textContent = L.RetireText || '';
    $('orRetireCancel').textContent = L.CancelButton || '';
    $('orRetireConfirm').textContent = L.RetireConfirm || '';
    $('orRetireConfirm').addEventListener('click', () => {
        close($('orRetireModal'));
        run(() => api.post(`/objection-responses/${id}/retire`, { expectedVersion: dto.version }), L.Retired);
    });

    window.ObjectionDetails = Object.freeze({ reload: load, act: onAction });
    load();
})(window, document);
