/**
 * WP-KP-UI-2 — the reviewer view of one frozen knowledge path revision (/CRM/KnowledgePaths/{pathId}/Review/{rev}).
 *  - left: the frozen composition (branch › step) with open-note counts; centre: the selected step's contents and
 *    claim blocks (the path-language text + qualifier of the pinned country version); a claim click opens its
 *    READ-ONLY evidence on the right (Claims v2 evidence proxy + the claim-evidence.js card; no add / remove here);
 *  - notes on a step or a block (StepRef / BlockRef): the stream (author, time, resolved, carried from an earlier
 *    revision), add, resolve (author or manager — CRM decides);
 *  - decision (K1, one channel = the MOD-0023 task): the step name, Approve (comment optional) / Reject (comment
 *    REQUIRED — checked here too). The submitter never gets the panel (person-based SoD, decided server-side from the
 *    session identity; CRM answers 403 anyway); a non-candidate's 403 is shown as text.
 */
(function (window, document) {
    'use strict';
    const S = window.KpStudio;
    const root = document.getElementById('kpReviewRoot');
    if (!S || !root) return;
    const { t, esc, api } = S;
    const pathId = root.dataset.pathId;
    const revisionId = root.dataset.revisionId;
    const $ = id => document.getElementById(id);
    const claimsApi = '/CRM/Claims/api/v2';

    let model = null;
    let selected = null;      // stepRef of the selected chain step
    let activeBlock = null;   // blockRef of the claim whose evidence is shown

    const lang = () => document.documentElement.lang || undefined;
    const date = v => v ? new Date(v).toLocaleString(lang(), { dateStyle: 'medium', timeStyle: 'short' }) : '';
    const badge = (text, tone) => text ? `<span class="badge bg-label-${tone}">${esc(text)}</span>` : '';

    /** Person-based SoD on the client: the panel is offered only when the server said this user may decide. */
    const decisionAllowed = m => !!m && m.canDecide === true && m.isSubmitter !== true && m.roundOpen === true;

    /** A rejection needs a comment (the reason the author will act on) — the same rule CRM enforces. */
    const rejectCommentMissing = (decision, comment) => decision === 'reject' && !String(comment || '').trim();

    // ---------------- load + render ----------------

    const load = async () => {
        S.hideAlert($('rvAlert'));
        try {
            model = (await api.get(`/studio/paths/${pathId}/revisions/${revisionId}/view`)).data;
            const slots = model.branches.flatMap(b => b.slots);
            if (!selected || !slots.some(s => s.stepRef === selected)) selected = slots[0]?.stepRef || null;
            render();
        } catch (error) {
            S.showAlert($('rvAlert'), error);
        }
    };

    const slotOf = stepRef => {
        for (const branch of model.branches) {
            const slot = branch.slots.find(s => s.stepRef === stepRef);
            if (slot) return { branch, slot };
        }
        return null;
    };

    const renderHeader = () => {
        $('rvTitle').textContent = `${model.pathName || ''} · ${t('ReviewerHeader', model.revisionNumber, model.pathVersion || '')}`;
        document.title = model.pathName || document.title;
        $('rvSub').textContent = [
            t('ReviewerFrozen', model.submittedBy || '', date(model.submittedAt)),
            model.currentStepName ? t('ReviewerWaiting', model.currentStepName) : model.statusLabel
        ].filter(Boolean).join(' · ');
        $('rvContext').textContent = [model.context?.productName, model.context?.countryName, model.context?.languageName].filter(Boolean).join(' · ');
    };

    const renderTree = () => {
        $('rvTree').innerHTML = model.branches.map(b => `
            <div>
                <div class="small fw-medium text-muted mb-1"><i class="bx bx-git-branch me-1" aria-hidden="true"></i>${esc(b.name)}</div>
                <div class="list-group">
                    ${b.slots.map(s => {
                        const notes = s.noteCount + s.items.reduce((n, i) => n + (i.noteCount || 0), 0);
                        return `<button type="button" class="list-group-item list-group-item-action d-flex align-items-center gap-2 kp-tree-step${s.stepRef === selected ? ' active' : ''}" data-step-ref="${esc(s.stepRef)}" aria-current="${s.stepRef === selected ? 'true' : 'false'}">
                            <span class="flex-grow-1">${esc(s.name)}</span>
                            <span class="badge bg-label-secondary" title="${esc(t('ReviewerItems', s.items.length))}">${s.items.length}</span>
                            ${notes ? `<span class="badge bg-label-warning" title="${esc(t('ReviewerOpenNotes', notes))}"><i class="bx bx-message-square-dots" aria-hidden="true"></i> ${notes}</span>` : ''}
                        </button>`;
                    }).join('')}
                </div>
            </div>`).join('');
    };

    const renderItems = () => {
        const found = slotOf(selected);
        $('rvStepTitle').textContent = found ? `${found.branch.name} › ${found.slot.name}` : '';
        $('rvStepSub').textContent = found ? t('ReviewerItems', found.slot.items.length) : '';
        const items = found?.slot.items || [];
        $('rvItems').innerHTML = items.length === 0
            ? `<li class="list-group-item small text-muted">${esc(t('EmptySlot'))}</li>`
            : items.map(i => i.kind === 'claim'
                ? `<li class="list-group-item kp-block${i.blockRef === activeBlock ? ' active' : ''}" tabindex="0" role="button" data-block-ref="${esc(i.blockRef)}" aria-label="${esc(t('EvidenceOpen', i.code || ''))}">
                       <div class="d-flex align-items-start gap-2">
                           <i class="bx bx-badge-check text-success mt-1" aria-hidden="true"></i>
                           <div class="flex-grow-1 min-w-0 small">
                               <div class="fw-medium text-heading">${esc([i.code, i.name].filter(Boolean).join(' · '))}</div>
                               ${i.text ? `<div class="kp-claim-text text-break">“${esc(i.text)}”</div>` : `<div class="kp-reason">${esc(t('ReviewerNoText'))}</div>`}
                               ${i.qualifier ? `<div class="text-muted text-break">${esc(i.qualifier)}</div>` : ''}
                               <div class="text-muted">${esc([i.versionLabel, i.statusLabel].filter(Boolean).join(' · '))}</div>
                           </div>
                           ${i.noteCount ? badge(String(i.noteCount), 'warning') : ''}
                       </div>
                   </li>`
                : `<li class="list-group-item">
                       <div class="d-flex align-items-start gap-2">
                           <i class="bx bx-file text-primary mt-1" aria-hidden="true"></i>
                           <div class="flex-grow-1 min-w-0 small">
                               <div class="fw-medium text-heading text-break">${esc(i.title || '')}</div>
                               <div class="text-muted">${esc([i.code, i.versionLabel].filter(Boolean).join(' · '))}</div>
                               ${badge(i.isRequired ? t('SlotRequired') : t('SlotOptional'), i.isRequired ? 'primary' : 'secondary')}
                           </div>
                           ${i.noteCount ? badge(String(i.noteCount), 'warning') : ''}
                       </div>
                   </li>`).join('');
    };

    // ---------------- notes ----------------

    const renderNotes = () => {
        const found = slotOf(selected);
        const blockRefs = new Set((found?.slot.items || []).map(i => i.blockRef));
        const blockLabel = ref => {
            const item = (found?.slot.items || []).find(i => i.blockRef === ref);
            return item ? (item.kind === 'claim' ? item.code : item.title) : null;
        };
        const notes = model.notes.filter(n => n.stepRef === selected || blockRefs.has(n.blockRef) || (!n.stepRef && !n.blockRef));
        $('rvNotes').innerHTML = notes.length === 0
            ? `<li class="small text-muted">${esc(t('NotesNone'))}</li>`
            : notes.map(n => `
                <li class="kp-note${n.isResolved ? ' is-resolved' : ''} border rounded p-2 small">
                    <div class="d-flex flex-wrap align-items-center gap-2 mb-1">
                        <span class="fw-medium">${esc(n.author || '')}</span>
                        <span class="text-muted">${esc(date(n.createdAt))}</span>
                        ${blockLabel(n.blockRef) ? badge(blockLabel(n.blockRef), 'secondary') : ''}
                        ${n.carriedLabel ? badge(n.carriedLabel, 'info') : ''}
                        ${n.isResolved ? badge(t('NoteResolved'), 'success') : ''}
                    </div>
                    <div class="text-break">${esc(n.text || '')}</div>
                    ${n.isResolved && n.resolvedBy ? `<div class="text-muted mt-1">${esc(t('NoteResolvedBy', n.resolvedBy))}</div>` : ''}
                    ${n.canResolve ? `<button type="button" class="btn btn-xs btn-label-success mt-2 js-note-resolve" data-note-id="${esc(n.noteId)}">${esc(t('NoteResolve'))}</button>` : ''}
                </li>`).join('');

        const target = $('rvNoteTarget');
        const keep = target.value;
        target.innerHTML = found
            ? `<option value="step">${esc(t('NoteOnStep', found.slot.name))}</option>` + found.slot.items.map(i =>
                `<option value="${esc(i.blockRef)}">${esc(t('NoteOnBlock', i.kind === 'claim' ? (i.code || '') : (i.title || '')))}</option>`).join('')
            : '';
        if (keep && Array.from(target.options).some(o => o.value === keep)) target.value = keep;
    };

    const addNote = async () => {
        const text = $('rvNoteText').value.trim();
        const error = $('rvNoteError');
        if (!text) {
            error.textContent = t('Err_note_text_required');
            error.classList.remove('d-none');
            $('rvNoteText').focus();
            return;
        }
        error.classList.add('d-none');
        const target = $('rvNoteTarget').value;
        try {
            await api.post(`/paths/${pathId}/revisions/${revisionId}/notes`, {
                stepRef: selected, blockRef: target && target !== 'step' ? target : null, text
            });
            $('rvNoteText').value = '';
            window.showToast?.(t('NoteAdded'), 'success');
            await load();
        } catch (failure) {
            error.textContent = failure.message;
            error.classList.remove('d-none');
        }
    };

    const resolveNote = async noteId => {
        try {
            await api.post(`/paths/${pathId}/revisions/${revisionId}/notes/${noteId}/resolve`);
            await load();
        } catch (error) {
            S.showAlert($('rvAlert'), error);
        }
    };

    // ---------------- decision ----------------

    const renderDecision = () => {
        $('rvDecisionTitle').textContent = model.currentStepName ? t('DecisionTitleStep', model.currentStepName) : t('DecisionTitle');
        const allowed = decisionAllowed(model);
        // The submitter (person-based) and a closed round never see the decision form.
        $('rvDecisionForm').classList.toggle('d-none', !allowed);
        const note = $('rvDecisionNote');
        note.textContent = allowed ? '' : (model.decisionNote || t('DecisionClosed'));
        note.classList.toggle('d-none', allowed);
    };

    const decide = async decision => {
        if (!decisionAllowed(model)) return;
        const comment = $('rvDecisionComment').value;
        const error = $('rvDecisionError');
        if (rejectCommentMissing(decision, comment)) {
            $('rvDecisionComment').classList.add('is-invalid');
            error.textContent = t('DecisionRejectNeedsComment');
            error.classList.remove('d-none');
            $('rvDecisionComment').focus();
            return;
        }
        $('rvDecisionComment').classList.remove('is-invalid');
        error.classList.add('d-none');
        ['rvApprove', 'rvReject'].forEach(id => { $(id).disabled = true; });
        try {
            await api.post(`/paths/${pathId}/revisions/${revisionId}/decision`, { decision, comment: comment.trim() || null });
            const done = $('rvDecisionDone');
            done.textContent = decision === 'approve' ? t('DecisionApproved', model.currentStepName || '') : t('DecisionRejected');
            done.classList.remove('d-none');
            $('rvDecisionComment').value = '';
            await load();
        } catch (failure) {
            // 403 approval_forbidden (not a candidate of this step) / sod_submitter_cannot_decide arrive as user text.
            error.textContent = failure.message;
            error.classList.remove('d-none');
        } finally {
            ['rvApprove', 'rvReject'].forEach(id => { $(id).disabled = false; });
        }
    };

    // ---------------- evidence (read only) ----------------

    const showEvidence = async blockRef => {
        activeBlock = blockRef;
        renderItems();
        const item = model.branches.flatMap(b => b.slots).flatMap(s => s.items).find(i => i.blockRef === blockRef);
        const host = $('rvEvidence');
        if (!item) return;
        $('rvEvidenceTitle').textContent = t('EvidenceTitleFor', item.code || '');
        host.innerHTML = `<p class="small text-muted mb-0">${esc(t('Loading'))}</p>`;
        const url = item.countryVersionId
            ? `${claimsApi}/claims/country-versions/${encodeURIComponent(item.countryVersionId)}/evidence`
            : `${claimsApi}/claims/${encodeURIComponent(item.claimId)}/evidence`;
        try {
            const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (response.status === 403) { host.innerHTML = `<p class="small text-muted mb-0">${esc(t('EvidenceNoPermission'))}</p>`; return; }
            if (!response.ok) { host.innerHTML = `<p class="small text-danger mb-0">${esc(t('EvidenceUnavailable'))}</p>`; return; }
            const body = await response.json().catch(() => ({}));
            const items = (body?.data?.items || body?.items || []);
            const head = `${item.text ? `<div class="kp-quote small mb-2">“${esc(item.text)}”</div>` : ''}<small class="text-muted d-block mb-2">${esc([item.versionLabel, item.statusLabel].filter(Boolean).join(' · '))}</small>`;
            host.innerHTML = head + (items.length === 0
                ? `<p class="small text-muted mb-0">${esc(t('EvidenceNone'))}</p>`
                : items.map(e => window.ClaimEvidence ? window.ClaimEvidence.card(e, { origin: !!item.countryVersionId }) : '').join(''));
        } catch (error) {
            host.innerHTML = `<p class="small text-danger mb-0">${esc(t('EvidenceUnavailable'))}</p>`;
        }
    };

    // ---------------- events ----------------

    const render = () => {
        renderHeader();
        renderTree();
        renderItems();
        renderNotes();
        renderDecision();
    };

    $('rvTree').addEventListener('click', event => {
        const step = event.target.closest('[data-step-ref]');
        if (!step) return;
        selected = step.dataset.stepRef;
        activeBlock = null;
        renderTree();
        renderItems();
        renderNotes();
    });
    $('rvItems').addEventListener('click', event => {
        const block = event.target.closest('[data-block-ref]');
        if (block) void showEvidence(block.dataset.blockRef);
    });
    $('rvItems').addEventListener('keydown', event => {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        const block = event.target.closest('[data-block-ref]');
        if (!block) return;
        event.preventDefault();
        void showEvidence(block.dataset.blockRef);
    });
    $('rvNotes').addEventListener('click', event => {
        const resolve = event.target.closest('.js-note-resolve');
        if (resolve) void resolveNote(resolve.dataset.noteId);
    });
    $('rvNoteAdd').addEventListener('click', () => void addNote());
    $('rvApprove').addEventListener('click', () => void decide('approve'));
    $('rvReject').addEventListener('click', () => void decide('reject'));

    load();
})(window, document);
