/**
 * WP-VW-W3 (W3-WEB-c) — the knowledge path studio's "Sunum notları" (presentation notes) tab (K-B1).
 *
 * It listens to the workspace's own `kp:workspace` event (workspace.js is not changed) and adds:
 *  - the path-level completeness bar: "Sunum notları: n / t adım dolu" (filled = key message + talk track), the missing
 *    steps as chips that open their editor;
 *  - a "Sunum notları" button on every content step (published paths too: then read-only);
 *  - the step editor (offcanvas) with two tabs: "İçerik" (the step's content, read-only — its settings stay on the card's
 *    own gear, unchanged) and "Sunum notları": key message ≤ 500, talk track ≤ 4000, target 10–3600 s (empty → the step's
 *    estimate, MINUTES × 60), transition ≤ 500, expected objections ≤ 10 (ONLY approved answers of the path's product —
 *    K-A4), "create in the library" (with the unsaved-changes warning), the doctor / rep previews.
 *  - Save = the existing step PUT with the step's full body AND the COMPLETE presentation object (ObjectionCore
 *    .presentationBody: every key, always — an object replaces the stored notes, a partial one would wipe the rest). A
 *    linked answer that is no longer approved / of this product is shown flagged and dropped on save.
 */
(function (window, document) {
    'use strict';
    const S = window.KpStudio;
    const C = window.ObjectionCore;
    const root = document.getElementById('kpStudioRoot');
    if (!S || !C || !root) return;
    const { t, esc, api } = S;
    const pathId = root.dataset.pathId;
    const $ = id => document.getElementById(id);

    let model = null;
    const state = { item: null, fields: null, selected: [], rows: [], productId: null, disabled: false, reason: null, dirty: false, pickerOpen: false };

    // ---------------- model helpers ----------------

    const contentItems = () => (model?.branches || []).flatMap(b => b.slots.flatMap(s => (s.items || [])
        .filter(i => i.kind === 'content')
        .map(i => Object.assign({}, i, { branchCode: b.code, chainStepId: s.chainStepId, slotName: s.name }))))
        .sort((a, b) => (a.stepOrder || 0) - (b.stepOrder || 0));
    const itemById = id => contentItems().find(i => i.stepId === id) || null;
    const indexOf = id => contentItems().findIndex(i => i.stepId === id) + 1;

    const seconds = n => {
        if (n == null) return '';
        const m = Math.floor(n / 60);
        const s = n % 60;
        return m > 0 ? t('PnDuration', m, s) : t('PnSecondsValue', s);
    };

    // ---------------- the completeness bar + the step buttons ----------------

    const renderBar = () => {
        const bar = $('kpNotesBar');
        if (!bar) return;
        const items = contentItems();
        const show = !!model && !model.isLegacyUnapproved && items.length > 0;
        bar.classList.toggle('d-none', !show);
        if (!show) return;
        const c = C.completeness(items);
        const pct = c.total ? Math.round(c.filled * 100 / c.total) : 0;
        $('kpNotesBarText').textContent = t('PnBar', c.filled, c.total);
        const progress = $('kpNotesBarProgress');
        progress.style.inlineSize = `${pct}%`;
        progress.setAttribute('aria-valuenow', String(pct));
        progress.className = `progress-bar ${c.filled === c.total ? 'bg-success' : 'bg-warning'}`;
        $('kpNotesBarMissing').innerHTML = c.missing.length === 0 ? ''
            : `<span class="small text-muted">${esc(t('PnMissing'))}</span>`
              + c.missing.map(i => `<button type="button" class="badge bg-label-warning border-0 js-notes-open" data-step-id="${esc(i.stepId)}">${esc(`${indexOf(i.stepId)} · ${i.title || ''}`)} <i class="bx bx-chevron-right" aria-hidden="true"></i></button>`).join('');
    };

    const decorateItems = () => {
        if (!model || model.isLegacyUnapproved) return;
        document.querySelectorAll('#wsBranches .kp-item[data-kind="content"]').forEach(node => {
            const item = itemById(node.dataset.id);
            const meta = node.querySelector('.flex-grow-1 .d-flex.flex-wrap');
            if (!item || !meta || meta.querySelector('.js-notes-open')) return;
            const filled = C.notesFilled(item.presentation);
            meta.insertAdjacentHTML('beforeend', `<button type="button" class="btn btn-xs ${filled ? 'btn-label-success' : 'btn-label-warning'} js-notes-open" data-step-id="${esc(item.stepId)}" title="${esc(t('PnButton'))}">
                <i class="bx bx-microphone me-1" aria-hidden="true"></i>${esc(filled ? t('PnFilled') : t('PnEmpty'))}</button>`);
        });
    };

    // ---------------- the editor ----------------

    const readFields = () => ({
        keyMessage: $('pnKeyMessage').value,
        talkTrack: $('pnTalkTrack').value,
        targetSeconds: $('pnTarget').value,
        transition: $('pnTransition').value,
        objectionResponseIds: state.selected
    });

    const canEdit = () => !!model?.canEdit;

    const renderCounters = () => {
        [['pnKeyMessage', 'pnKeyMessageCount', C.MAX_KEY_MESSAGE], ['pnTalkTrack', 'pnTalkTrackCount', C.MAX_TALK_TRACK], ['pnTransition', 'pnTransitionCount', C.MAX_TRANSITION]]
            .forEach(([input, counter, max]) => {
                const c = C.counter($(input).value, max);
                $(counter).textContent = c.text;
                $(counter).classList.toggle('text-danger', c.over);
            });
        const hint = C.targetHint(null, state.item.durationMinutes);
        $('pnTargetHint').textContent = hint.seconds ? t('PnTargetHint', hint.seconds) : t('PnTargetHintNone');
        const effective = C.targetHint($('pnTarget').value, state.item.durationMinutes);
        $('pnEstimate').textContent = effective.seconds ? t('PnEstimate', effective.seconds) : '';
    };

    const objectionRow = (row, removable, unusable) => `
        <div class="border rounded p-2 d-flex align-items-start gap-2 ${unusable ? 'border-warning' : ''}" data-objection-id="${esc(row.id)}">
            <i class="bx bx-message-alt-error text-danger mt-1" aria-hidden="true"></i>
            <div class="flex-grow-1 min-w-0">
                <div class="small fw-medium text-heading text-break">“${esc(row.question || '')}”</div>
                <div class="small d-flex flex-wrap gap-2 align-items-center">
                    ${row.categoryCode ? `<span class="badge bg-label-${C.categoryTone(row.categoryCode)}">${esc(row.categoryLabel || row.categoryCode)}</span>` : ''}
                    <span class="text-muted">${esc(t('PnClaimCount', row.claimCount || 0))}</span>
                </div>
                ${unusable ? `<div class="small text-warning">${esc(t('PnUnusable'))}</div>` : ''}
            </div>
            ${removable ? `<button type="button" class="btn btn-icon btn-xs btn-text-secondary js-pn-remove" data-id="${esc(row.id)}" aria-label="${esc(t('PnRemove'))}" title="${esc(t('PnRemove'))}"><i class="bx bx-x" aria-hidden="true"></i></button>` : ''}
        </div>`;

    const renderObjections = () => {
        const split = C.splitLinked(state.selected, state.rows, state.productId);
        const editable = canEdit();
        $('pnObjectionsTitle').textContent = t('PnObjections', split.usable.length, C.MAX_OBJECTIONS);
        $('pnObjectionsHint').textContent = state.disabled
            ? t('PnReason' + (state.reason || 'ObjectionNoProduct'))
            : t('PnObjectionsHint', model?.context?.productName || '');
        const rows = split.usable.map(id => state.rows.find(r => String(r.id).toLowerCase() === id.toLowerCase())).filter(Boolean);
        $('pnObjectionList').innerHTML = rows.map(r => objectionRow(r, editable, false)).join('')
            + split.unusable.map(u => objectionRow(u.row || { id: u.id, question: u.id }, editable, true)).join('');
        const add = $('pnAddObjection');
        add.classList.toggle('d-none', !editable || state.disabled);
        add.disabled = split.usable.length >= C.MAX_OBJECTIONS;
        $('pnCreateInLibrary').classList.toggle('d-none', !editable || state.disabled || !model?.canManageObjections);
        renderPicker();
        renderPreview();
    };

    const renderPicker = () => {
        const host = $('pnPicker');
        host.classList.toggle('d-none', !state.pickerOpen);
        if (!state.pickerOpen) return;
        const term = ($('pnPickerSearch').value || '').trim().toLocaleLowerCase();
        const options = C.objectionOptions(state.rows, state.productId)
            .filter(r => !state.selected.some(id => id.toLowerCase() === String(r.id).toLowerCase()))
            .filter(r => !term || `${r.question || ''} ${r.categoryLabel || ''}`.toLocaleLowerCase().includes(term));
        const atMax = C.splitLinked(state.selected, state.rows, state.productId).usable.length >= C.MAX_OBJECTIONS;
        $('pnPickerList').innerHTML = (atMax ? `<div class="small text-warning">${esc(t('PnPickerMax', C.MAX_OBJECTIONS))}</div>` : '')
            + (options.length === 0 ? `<div class="small text-muted">${esc(t('PnPickerEmpty'))}</div>`
                : options.map(r => `<div class="d-flex align-items-start gap-2 border-bottom py-2">
                    <div class="flex-grow-1 min-w-0">
                        <div class="small text-heading text-break">“${esc(r.question || '')}”</div>
                        ${r.categoryCode ? `<span class="badge bg-label-${C.categoryTone(r.categoryCode)}">${esc(r.categoryLabel || r.categoryCode)}</span>` : ''}
                    </div>
                    <button type="button" class="btn btn-sm btn-label-primary js-pn-add" data-id="${esc(r.id)}" ${atMax ? 'disabled' : ''}>${esc(t('PnAddObjection'))}</button>
                </div>`).join(''));
    };

    const renderPreview = () => {
        const f = readFields();
        const item = state.item;
        $('pnDoctorProduct').textContent = model?.context?.productName || '';
        $('pnDoctorTitle').textContent = item.title || '';
        $('pnDoctorSub').textContent = [item.typeLabel, item.languageName].filter(Boolean).join(' · ');
        $('pnRemoteKey').textContent = (f.keyMessage || '').trim() || '—';
        const effective = C.targetHint(f.targetSeconds, item.durationMinutes);
        $('pnRemoteTarget').textContent = effective.seconds ? t('PnTargetShort', seconds(effective.seconds)) : '';
        $('pnRemoteTalk').textContent = (f.talkTrack || '').trim() || '—';
        const usable = C.splitLinked(state.selected, state.rows, state.productId).usable
            .map(id => state.rows.find(r => String(r.id).toLowerCase() === id.toLowerCase())).filter(Boolean);
        $('pnRemoteObjections').innerHTML = usable.map(r => `<details class="border rounded px-2 py-1 mb-1"><summary class="small text-heading">“${esc(r.question || '')}”</summary><div class="small mt-1">${esc(r.answer || '')}</div></details>`).join('');
        $('pnRemoteObjectionsBlock').classList.toggle('d-none', usable.length === 0);
        $('pnRemoteTransition').textContent = (f.transition || '').trim();
        $('pnRemoteTransitionBlock').classList.toggle('d-none', !(f.transition || '').trim());
    };

    const loadObjections = async () => {
        const ids = (state.item.presentation?.objectionResponseIds || []).join(',');
        try {
            const result = await api.get(`/lookups/path-objections?pathId=${encodeURIComponent(pathId)}${ids ? `&ids=${encodeURIComponent(ids)}` : ''}`);
            state.rows = result.options || [];
            state.productId = result.productId || null;
            state.disabled = !!result.disabled;
            state.reason = result.reason || null;
        } catch (error) {
            state.rows = [];
            state.disabled = true;
            state.reason = 'ObjectionNoProduct';
            S.showAlert($('pnAlert'), error);
        }
    };

    const setTab = tab => {
        ['content', 'notes'].forEach(name => {
            const button = $(`pnTab-${name}`);
            const panel = $(`pnPanel-${name}`);
            const active = name === tab;
            button.classList.toggle('active', active);
            button.setAttribute('aria-selected', active ? 'true' : 'false');
            panel.classList.toggle('d-none', !active);
        });
    };

    const renderContentTab = item => {
        $('pnContentTitle').textContent = item.title || '';
        $('pnContentMeta').textContent = [item.typeLabel, item.languageName, item.statusLabel].filter(Boolean).join(' · ');
        $('pnContentRequired').textContent = item.isRequired ? t('SlotRequired') : t('SlotOptional');
        $('pnContentDuration').textContent = item.durationMinutes ? t('Duration', item.durationMinutes) : '—';
        $('pnContentPrerequisite').textContent = item.prerequisiteLabel || '—';
    };

    const open = async stepId => {
        const item = itemById(stepId);
        if (!item) return;
        state.item = item;
        state.selected = (item.presentation?.objectionResponseIds || []).map(String);
        state.dirty = false;
        state.pickerOpen = false;
        S.hideAlert($('pnAlert'));
        const p = item.presentation || {};
        $('pnKeyMessage').value = p.keyMessage || '';
        $('pnTalkTrack').value = p.talkTrack || '';
        $('pnTarget').value = p.targetSeconds == null ? '' : String(p.targetSeconds);
        $('pnTransition').value = p.transition || '';
        const editable = canEdit();
        ['pnKeyMessage', 'pnTalkTrack', 'pnTarget', 'pnTransition'].forEach(id => { $(id).readOnly = !editable; });
        $('pnReadOnly').classList.toggle('d-none', editable);
        $('pnSave').classList.toggle('d-none', !editable);
        $('pnStepOf').textContent = t('PnStepOf', indexOf(item.stepId), contentItems().length);
        $('pnStepTitle').textContent = item.title || '';
        renderContentTab(item);
        setTab('notes');
        renderCounters();
        $('pnObjectionList').innerHTML = `<div class="small text-muted">${esc(t('Loading'))}</div>`;
        window.bootstrap?.Offcanvas.getOrCreateInstance($('kpNotesPanel')).show();
        if (model?.canReadObjections === false) {
            state.rows = [];
            state.disabled = true;
            state.reason = 'ObjectionReadMissing';
        } else {
            await loadObjections();
        }
        renderObjections();
    };

    /** The step's full body (the existing full-replace step PUT) + the COMPLETE presentation object. */
    const stepBody = (item, presentation) => ({
        stepOrder: item.stepOrder || 10,
        stepCode: item.stepCode,
        stepTitle: item.stepTitle,
        stepType: item.stepType,
        contentId: item.contentId,
        isRequired: item.isRequired,
        versionPinPolicy: item.versionPinPolicy,
        completionRule: item.completionRule,
        prerequisiteStepId: item.prerequisiteStepId || null,
        conceptNodeId: item.conceptNodeId || null,
        estimatedDurationMinutes: item.durationMinutes ?? null,
        notes: item.notes || null,
        branchConditions: item.branchConditions || [],
        arrangement: { chainStepId: item.chainStepId, branchCode: item.branchCode, position: item.position },
        presentation
    });

    const save = async () => {
        if (!canEdit() || !state.item) return;
        S.hideAlert($('pnAlert'));
        const fields = readFields();
        // an answer that is no longer usable is dropped (CRM would refuse the whole save with path_step_objection_invalid)
        fields.objectionResponseIds = C.splitLinked(state.selected, state.rows, state.productId).usable;
        if (C.validatePresentation(fields).length > 0) {
            S.showAlert($('pnAlert'), { message: t('PnInvalid') });
            return;
        }
        const button = $('pnSave');
        button.disabled = true;
        try {
            await api.put(`/paths/${pathId}/steps/${state.item.stepId}`, stepBody(state.item, C.presentationBody(fields)));
            state.dirty = false;
            window.showToast?.(t('PnSaved'), 'success');
            window.bootstrap?.Offcanvas.getOrCreateInstance($('kpNotesPanel')).hide();
            await window.KpWorkspace?.reload();
        } catch (error) {
            S.showAlert($('pnAlert'), error);
        } finally {
            button.disabled = false;
        }
    };

    // ---------------- wiring ----------------

    document.addEventListener('kp:workspace', event => {
        model = event.detail;
        renderBar();
        decorateItems();
    });

    document.addEventListener('click', event => {
        const opener = event.target.closest ? event.target.closest('.js-notes-open') : null;
        if (opener) { open(opener.dataset.stepId); return; }
        const remove = event.target.closest ? event.target.closest('.js-pn-remove') : null;
        if (remove) {
            state.selected = state.selected.filter(id => id.toLowerCase() !== remove.dataset.id.toLowerCase());
            state.dirty = true;
            renderObjections();
            return;
        }
        const add = event.target.closest ? event.target.closest('.js-pn-add') : null;
        if (add) {
            const split = C.splitLinked(state.selected, state.rows, state.productId);
            const next = C.toggleObjection(split.usable, add.dataset.id);
            if (!next.refused) { state.selected = next.selected.concat(split.unusable.map(u => u.id)); state.dirty = true; }
            renderObjections();
        }
    });

    const panel = $('kpNotesPanel');
    if (panel) {
        ['pnKeyMessage', 'pnTalkTrack', 'pnTarget', 'pnTransition'].forEach(id => $(id).addEventListener('input', () => {
            state.dirty = true;
            renderCounters();
            renderPreview();
        }));
        $('pnTab-content').addEventListener('click', () => setTab('content'));
        $('pnTab-notes').addEventListener('click', () => setTab('notes'));
        $('pnAddObjection').addEventListener('click', () => { state.pickerOpen = !state.pickerOpen; renderPicker(); });
        $('pnPickerSearch').addEventListener('input', renderPicker);
        $('pnRefresh').addEventListener('click', async () => { await loadObjections(); renderObjections(); });
        $('pnCreateInLibrary').addEventListener('click', event => {
            event.preventDefault();
            if (state.dirty && !window.confirm(t('PnCreateUnsaved'))) return;
            const url = `/CRM/ObjectionResponses/Create${state.productId ? `?productId=${encodeURIComponent(state.productId)}` : ''}`;
            window.open(url, '_blank', 'noopener');
        });
        $('pnSave').addEventListener('click', save);
        panel.addEventListener('hide.bs.offcanvas', event => {
            if (state.dirty && canEdit() && !window.confirm(t('PnUnsavedClose'))) event.preventDefault();
            else state.dirty = false;
        });
    }

    window.KpPresentationNotes = Object.freeze({ open, save, state, stepBody });
})(window, document);
