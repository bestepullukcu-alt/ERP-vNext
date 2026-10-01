/**
 * WP-KP-UI-1 — Knowledge Path Studio workspace: header + single status line, identity strip, the "Kurgu" tab.
 *  - The chain skeleton is read-only: no branch / step is created, removed or moved here (D-KP-7).
 *  - Items (contents = path steps, claims) are placed on a chain step and reordered ONLY inside it: a separate Sortable
 *    list per step (never a DataTable tbody) plus up / down buttons for the keyboard.
 *  - The field order is the server StepOrder (branch-first, KP-1); the client never computes it.
 *  - Read-only unless the path is a chain-bound draft and the user may manage.
 * All traffic goes through /CRM/KnowledgePaths/api (studio-common.js).
 */
(function (window, document) {
    'use strict';
    const S = window.KpStudio;
    const root = document.getElementById('kpStudioRoot');
    if (!S || !root) return;
    const { t, esc, api } = S;
    const pathId = root.dataset.pathId;
    const INTRO_KEY = 'kpStudio.introDismissed';

    let model = null;
    let addTarget = null;
    let addKind = 'content';
    let addTimer = null;
    const sortables = [];
    const $ = id => document.getElementById(id);

    // ---------------- load + render ----------------

    const load = async () => {
        S.hideAlert($('wsAlert'));
        try {
            model = (await api.get(`/studio/paths/${pathId}`)).data;
            render();
        } catch (error) {
            S.showAlert($('wsAlert'), error);
        }
    };

    const badge = (text, tone) => text ? `<span class="badge bg-label-${tone}">${esc(text)}</span>` : '';
    const statusTone = s => ({ draft: 'secondary', review: 'info', approved: 'success', published: 'success', inactive: 'warning', archived: 'secondary' }[s] || 'secondary');

    const renderHeader = () => {
        $('wsTitle').textContent = model.pathName || '';
        $('wsSub').textContent = `${model.pathCode || ''} · ${t('VersionShort', model.pathVersion || '')}`;
        document.title = model.pathName || document.title;
        // ONE status line: lifecycle + "on the field vX" + the legacy mark.
        $('wsStatusLine').innerHTML = [
            badge(model.statusLabel, statusTone(model.status)),
            model.liveVersion ? badge(t('OnField', model.liveVersion), 'success') : '',
            model.isLegacyUnapproved ? `<span class="badge bg-label-warning" title="${esc(t('LegacyBadgeTip'))}">${esc(t('LegacyBadge'))}</span>` : ''
        ].join('');
    };

    const renderLegacyBand = () => {
        const band = $('wsLegacyBand');
        band.classList.toggle('d-none', !model.isLegacyUnapproved);
        $('wsBindButton').classList.toggle('d-none', !model.canBind);
        const editor = $('wsLegacyEditor');
        const editable = model.isLegacyUnapproved && model.canManage && !model.isFrozen && !model.isArchived;
        editor.classList.toggle('d-none', !editable);
        if (editable) editor.href = `/CRM/KnowledgePaths/Edit/${pathId}`;
    };

    const renderContext = () => {
        const ctx = model.context;
        $('wsContext').classList.toggle('d-none', !ctx);
        if (!ctx) return;
        const lock = `<i class="bx bx-lock-alt text-muted ms-1" aria-hidden="true"></i>`;
        const item = (label, value, locked, tip) => `
            <div class="col-6 col-md">
                <small class="text-muted d-block">${esc(label)}</small>
                <span class="fw-medium kp-context-value" ${tip ? `title="${esc(tip)}"` : ''}>${esc(value || '—')}${locked ? lock : ''}</span>
                ${locked && tip ? `<span class="visually-hidden">${esc(tip)}</span>` : ''}
            </div>`;
        $('wsContextItems').innerHTML = [
            item(t('CtxChain'), ctx.chainName ? `${ctx.chainName} · ${t('VersionShort', ctx.chainVersion)}` : null, true, t('CtxChainTip')),
            item(t('CtxCountry'), ctx.countryName, true, t('CtxIdentityTip')),
            item(t('CtxLanguage'), ctx.languageNativeName || ctx.languageName, true, t('CtxIdentityTip')),
            item(t('CtxProduct'), ctx.productName, false, t('CtxDerivedTip')),
            item(t('CtxAudience'), (ctx.audiences || []).join(', '), false, t('CtxDerivedTip'))
        ].join('');
    };

    const missingSlots = () => (model.branches || []).flatMap(b => b.slots).filter(s => s.status === 'under').length;

    const renderIntro = () => {
        const dismissed = S.storage.get(INTRO_KEY) === '1';
        $('wsIntro').classList.toggle('d-none', dismissed);
        $('wsHelp').classList.toggle('d-none', dismissed || model.isLegacyUnapproved);
        const missing = missingSlots();
        $('wsNextStep').textContent = model.isLegacyUnapproved ? t('NextBind')
            : missing > 0 ? t('NextFillSlots', missing) : t('NextComplete');
    };

    const renderReadOnly = () => {
        const show = !model.isLegacyUnapproved && !model.canEdit;
        $('wsReadOnly').classList.toggle('d-none', !show);
        $('wsReadOnlyText').textContent = !model.canManage ? t('ReadOnlyNoPermission') : t('ReadOnlyNotDraft');
    };

    const renderSequence = () => {
        const seq = model.sequence || [];
        const host = $('wsSequence');
        host.classList.toggle('d-none', model.isLegacyUnapproved);
        $('wsSkeletonNote').classList.toggle('d-none', model.isLegacyUnapproved);
        host.innerHTML = `<span class="small text-muted me-1">${esc(t('FieldOrder'))}</span>`
            + (seq.length === 0 ? `<span class="small text-muted">${esc(t('SequenceEmpty'))}</span>`
                : seq.map((s, i) => `<span class="badge bg-label-primary" title="${esc([s.branchName, s.slotName].filter(Boolean).join(' · '))}">${esc(`${s.order}. ${s.title || ''}`)}</span>${i < seq.length - 1 ? '<i class="bx bx-chevron-right text-muted" aria-hidden="true"></i>' : ''}`).join(''));
    };

    const slotLimit = s => s.max == null ? t('SlotLimitOpen', s.min) : t('SlotLimit', s.min, s.max);
    const confTone = status => ({ ok: 'success', under: 'warning', over: 'danger' }[status] || 'secondary');

    const contentItem = (item, slot, index, count) => `
        <li class="kp-item" data-kind="content" data-id="${esc(item.stepId)}" data-position="${item.position}">
            ${model.canEdit ? `<i class="bx bx-grid-vertical kp-handle mt-1" title="${esc(t('ItemDrag'))}" aria-hidden="true"></i>` : ''}
            <i class="bx bx-file text-primary mt-1" title="${esc(t('ContentKind'))}" aria-hidden="true"></i>
            <div class="flex-grow-1 min-w-0">
                <div class="small fw-medium text-heading text-break">${esc(item.title || '')}</div>
                <div class="small text-muted">${esc([item.typeLabel, item.languageName, item.statusLabel].filter(Boolean).join(' · '))}</div>
                <div class="small text-muted d-flex flex-wrap gap-2">
                    ${badge(item.isRequired ? t('SlotRequired') : t('SlotOptional'), item.isRequired ? 'primary' : 'secondary')}
                    ${item.durationMinutes ? `<span><i class="bx bx-time-five" aria-hidden="true"></i> ${esc(t('Duration', item.durationMinutes))}</span>` : ''}
                    ${item.prerequisiteLabel ? `<span><i class="bx bx-link" aria-hidden="true"></i> ${esc(t('Prerequisite', item.prerequisiteLabel))}</span>` : ''}
                    ${item.published ? '' : `<span class="kp-reason">${esc(t('ContentNotPublished'))}</span>`}
                </div>
                <div class="kp-settings d-none mt-2" data-settings-for="${esc(item.stepId)}"></div>
            </div>
            ${model.canEdit ? itemControls(item.stepId, index, count, true) : ''}
        </li>`;

    const claimItem = (item, slot, index, count) => `
        <li class="kp-item" data-kind="claim" data-id="${esc(item.claimId)}" data-position="${item.position}">
            ${model.canEdit ? `<i class="bx bx-grid-vertical kp-handle mt-1" title="${esc(t('ItemDrag'))}" aria-hidden="true"></i>` : ''}
            <i class="bx bx-badge-check text-success mt-1" title="${esc(t('ClaimKind'))}" aria-hidden="true"></i>
            <div class="flex-grow-1 min-w-0">
                <div class="small fw-medium text-heading">${esc([item.code, item.name].filter(Boolean).join(' · '))}</div>
                ${item.text ? `<div class="small kp-claim-text text-break">“${esc(item.text)}”</div>` : ''}
                ${item.qualifier ? `<div class="small text-muted text-break">${esc(item.qualifier)}</div>` : ''}
                <div class="small ${item.usable ? 'text-muted' : 'kp-reason'}">
                    ${esc(item.usable
                        ? [item.countryVersion ? t('ClaimVersionLabel', item.countryVersion) : '', item.statusLabel].filter(Boolean).join(' · ')
                        : [item.reasonLabel, item.countryVersion ? t('ClaimVersionLabel', item.countryVersion) : ''].filter(Boolean).join(' · '))}
                </div>
            </div>
            ${model.canEdit ? itemControls(item.claimId, index, count, false) : ''}
        </li>`;

    const itemControls = (id, index, count, settings) => `
        <div class="d-flex flex-column flex-sm-row gap-1">
            <button type="button" class="btn btn-icon btn-xs btn-text-secondary js-move" data-dir="-1" ${index === 0 ? 'disabled' : ''} aria-label="${esc(t('MoveUp'))}" title="${esc(t('MoveUp'))}"><i class="bx bx-up-arrow-alt" aria-hidden="true"></i></button>
            <button type="button" class="btn btn-icon btn-xs btn-text-secondary js-move" data-dir="1" ${index === count - 1 ? 'disabled' : ''} aria-label="${esc(t('MoveDown'))}" title="${esc(t('MoveDown'))}"><i class="bx bx-down-arrow-alt" aria-hidden="true"></i></button>
            ${settings ? `<button type="button" class="btn btn-icon btn-xs btn-text-secondary js-settings" aria-label="${esc(t('ItemSettings'))}" title="${esc(t('ItemSettings'))}"><i class="bx bx-cog" aria-hidden="true"></i></button>` : ''}
            <button type="button" class="btn btn-icon btn-xs btn-text-danger js-remove" aria-label="${esc(t('RemoveItem'))}" title="${esc(t('RemoveItem'))}"><i class="bx bx-x" aria-hidden="true"></i></button>
        </div>`;

    const slotCard = (branch, slot) => {
        const items = slot.items || [];
        return `
        <div class="card shadow-none border" data-branch="${esc(branch.code)}" data-step="${esc(slot.chainStepId)}">
            <div class="card-body p-3 pb-2">
                <div class="d-flex align-items-start gap-2">
                    <span class="fw-medium text-heading flex-grow-1">${esc(slot.name || '—')}</span>
                    ${badge(slot.required ? t('SlotRequired') : t('SlotOptional'), slot.required ? 'primary' : 'secondary')}
                </div>
                <div class="small text-muted d-flex flex-wrap gap-3 mt-1">
                    ${slot.durationMinutes ? `<span><i class="bx bx-time-five" aria-hidden="true"></i> ${esc(t('Duration', slot.durationMinutes))}</span>` : ''}
                    <span><i class="bx bx-layer" aria-hidden="true"></i> ${esc(slotLimit(slot))} <i class="bx bx-lock-alt" title="${esc(t('FromChain'))}" aria-hidden="true"></i></span>
                    ${slot.statusLabel ? badge(slot.statusLabel, confTone(slot.status)) : ''}
                </div>
            </div>
            <ul class="list-unstyled mb-0 border-top kp-items" data-branch="${esc(branch.code)}" data-step="${esc(slot.chainStepId)}" aria-label="${esc(slot.name || '')}">
                ${items.length === 0 ? `<li class="small text-muted px-3 py-2 kp-empty">${esc(t('EmptySlot'))}</li>`
                    : items.map((it, i) => it.kind === 'claim' ? claimItem(it, slot, i, items.length) : contentItem(it, slot, i, items.length)).join('')}
            </ul>
            ${model.canEdit ? `<div class="card-footer p-2"><button type="button" class="btn btn-sm btn-text-primary w-100 js-add" data-branch="${esc(branch.code)}" data-step="${esc(slot.chainStepId)}"><i class="bx bx-plus me-1" aria-hidden="true"></i>${esc(t('AddToStep'))}</button></div>` : ''}
        </div>`;
    };

    const renderBranches = () => {
        sortables.splice(0).forEach(s => { try { s.destroy(); } catch (e) { /* already gone */ } });
        const host = $('wsBranches');
        host.innerHTML = (model.branches || []).map(branch => {
            const total = branch.slots.reduce((n, s) => n + (s.items || []).length, 0);
            return `
            <section class="card kp-branch" aria-label="${esc(branch.name)}">
                <div class="card-header d-flex align-items-center gap-2 py-3">
                    <i class="bx bx-git-branch text-primary" aria-hidden="true"></i>
                    <h6 class="mb-0 flex-grow-1">${esc(branch.name)}</h6>
                    <small class="text-muted">${esc(t('BranchTotal', total))}</small>
                </div>
                <div class="kp-branch-body">${branch.slots.map(slot => slotCard(branch, slot)).join('')}</div>
            </section>`;
        }).join('');

        if (model.canEdit && window.Sortable) {
            host.querySelectorAll('.kp-items').forEach(list => {
                if (!list.querySelector('.kp-item')) return;
                // group: null → an item never leaves its chain step (D-KP-7).
                sortables.push(window.Sortable.create(list, {
                    handle: '.kp-handle', draggable: '.kp-item', animation: 150, ghostClass: 'opacity-50',
                    onEnd: () => persistOrder(list)
                }));
            });
        }
    };

    const renderLegacy = () => {
        const host = $('wsLegacySteps');
        host.classList.toggle('d-none', !model.isLegacyUnapproved);
        if (!model.isLegacyUnapproved) { host.innerHTML = ''; return; }
        const steps = model.legacySteps || [];
        host.innerHTML = `<h6 class="mb-2">${esc(t('LegacyStepsTitle'))}</h6>`
            + (steps.length === 0 ? `<p class="text-muted small">${esc(t('EmptySlot'))}</p>`
                : `<ol class="list-group list-group-numbered">${steps.map(s => `<li class="list-group-item">${esc(s.title || '')}${s.contentTitle && s.contentTitle !== s.title ? ` <small class="text-muted">· ${esc(s.contentTitle)}</small>` : ''}</li>`).join('')}</ol>`);
    };

    const render = () => {
        renderHeader();
        renderLegacyBand();
        renderContext();
        $('wsTabs').classList.remove('d-none');
        renderIntro();
        renderReadOnly();
        renderSequence();
        renderBranches();
        renderLegacy();
    };

    // ---------------- writes ----------------

    const findSlot = (branchCode, chainStepId) => {
        for (const branch of model.branches || []) {
            if (branch.code !== branchCode) continue;
            const slot = branch.slots.find(s => s.chainStepId === chainStepId);
            if (slot) return { branch, slot };
        }
        return null;
    };
    const contentById = id => (model.branches || []).flatMap(b => b.slots).flatMap(s => s.items || []).find(i => i.kind === 'content' && i.stepId === id);

    const stepBody = (item, changes, arrangement) => Object.assign({
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
        arrangement
    }, changes || {});

    const run = async (work, success) => {
        S.hideAlert($('wsAlert'));
        try {
            await work();
            if (success) window.showToast?.(success, 'success');
        } catch (error) {
            S.showAlert($('wsAlert'), error);
        }
        await load();
    };

    /** Positions follow the DOM order of ONE chain step; only changed items are written. */
    const persistOrder = list => run(async () => {
        const branchCode = list.dataset.branch;
        const chainStepId = list.dataset.step;
        const nodes = Array.from(list.querySelectorAll('.kp-item'));
        for (let i = 0; i < nodes.length; i++) {
            const node = nodes[i];
            if (Number(node.dataset.position) === i) continue;
            if (node.dataset.kind === 'claim') {
                await api.post(`/paths/${pathId}/claims/${node.dataset.id}/arrange`, { position: i });
            } else {
                const item = contentById(node.dataset.id);
                if (item) await api.put(`/paths/${pathId}/steps/${item.stepId}`, stepBody(item, null, { chainStepId, branchCode, position: i }));
            }
        }
    }, t('Saved'));

    const removeItem = node => {
        const go = () => run(async () => {
            if (node.dataset.kind === 'claim') await api.post(`/paths/${pathId}/claims/${node.dataset.id}/remove`);
            else await api.post(`/paths/${pathId}/steps/${node.dataset.id}/archive`);
        }, t('Removed'));
        if (window.showConfirm) window.showConfirm(t('RemoveConfirm'), go, { type: 'warning', confirmButtonText: t('RemoveItem') });
        else go();
    };

    const openSettings = node => {
        const item = contentById(node.dataset.id);
        const host = node.querySelector('.kp-settings');
        if (!item || !host) return;
        if (!host.classList.contains('d-none')) { host.classList.add('d-none'); host.innerHTML = ''; return; }
        const others = (model.sequence || []).length ? (model.branches || []).flatMap(b => b.slots).flatMap(s => s.items || [])
            .filter(i => i.kind === 'content' && i.stepId !== item.stepId && (i.stepOrder || 0) < (item.stepOrder || 0)) : [];
        const uid = `kpSet${item.stepId.replace(/-/g, '')}`;
        host.innerHTML = `
            <div class="border rounded p-2 d-grid gap-2">
                <div class="form-check form-switch mb-0">
                    <input class="form-check-input" type="checkbox" id="${uid}Req" ${item.isRequired ? 'checked' : ''}>
                    <label class="form-check-label small" for="${uid}Req">${esc(t('SettingsRequired'))}</label>
                </div>
                <div>
                    <label class="form-label small mb-1" for="${uid}Dur">${esc(t('SettingsDuration'))}</label>
                    <input type="number" class="form-control form-control-sm" id="${uid}Dur" min="1" max="600" value="${item.durationMinutes ?? ''}">
                </div>
                <div>
                    <label class="form-label small mb-1" for="${uid}Pre">${esc(t('SettingsPrerequisite'))}</label>
                    <select class="form-select form-select-sm" id="${uid}Pre">
                        <option value="">${esc(t('SettingsNoPrerequisite'))}</option>
                        ${others.map(o => `<option value="${esc(o.stepId)}" ${o.stepId === item.prerequisiteStepId ? 'selected' : ''}>${esc(o.title || '')}</option>`).join('')}
                    </select>
                </div>
                <small class="text-muted"><i class="bx bx-lock-alt" aria-hidden="true"></i> ${esc(t('SettingsMinMaxNote'))}</small>
                <button type="button" class="btn btn-sm btn-primary js-settings-save">${esc(t('SettingsSave'))}</button>
            </div>`;
        host.classList.remove('d-none');
        host.querySelector('.js-settings-save').addEventListener('click', () => {
            const duration = host.querySelector(`#${uid}Dur`).value;
            const list = node.closest('.kp-items');
            run(() => api.put(`/paths/${pathId}/steps/${item.stepId}`, stepBody(item, {
                isRequired: host.querySelector(`#${uid}Req`).checked,
                estimatedDurationMinutes: duration ? Number(duration) : null,
                prerequisiteStepId: host.querySelector(`#${uid}Pre`).value || null
            }, { chainStepId: list.dataset.step, branchCode: list.dataset.branch, position: item.position })), t('Saved'));
        });
    };

    // ---------------- "Adıma ekle" panel ----------------

    const openAdd = (branchCode, chainStepId) => {
        const found = findSlot(branchCode, chainStepId);
        if (!found) return;
        addTarget = { branchCode, chainStepId, branchName: found.branch.name, slotName: found.slot.name };
        $('kpAddTarget').textContent = t('AddTarget', [found.branch.name, found.slot.name].filter(Boolean).join(' · '));
        $('kpAddSearch').value = '';
        setAddKind('content');
        window.bootstrap?.Offcanvas.getOrCreateInstance($('kpAddPanel')).show();
    };

    const setAddKind = kind => {
        addKind = kind;
        ['kpAddTabContent', 'kpAddTabClaim'].forEach(id => {
            const el = $(id);
            const active = el.dataset.kind === kind;
            el.classList.toggle('active', active);
            el.setAttribute('aria-selected', active ? 'true' : 'false');
        });
        $('kpAddSearch').placeholder = kind === 'claim' ? t('AddClaimSearch') : t('AddSearch');
        $('kpAddNote').textContent = kind === 'claim'
            ? t('AddClaimNote', model.context?.countryName || '')
            : t('AddContentNote', model.context?.languageNativeName || model.context?.languageName || '');
        refreshAddList();
    };

    const slotItems = () => findSlot(addTarget.branchCode, addTarget.chainStepId)?.slot.items || [];

    const addRow = (title, sub, extra, action) => `
        <li class="list-group-item d-flex align-items-start gap-2">
            <div class="flex-grow-1 min-w-0">
                <div class="small fw-medium text-heading text-break">${esc(title || '')}</div>
                ${sub ? `<div class="small text-muted">${esc(sub)}</div>` : ''}
                ${extra || ''}
            </div>
            ${action}
        </li>`;
    const cannot = reason => `<span class="badge bg-label-danger text-wrap text-start" title="${esc(reason || '')}">${esc(t('CannotAdd'))}${reason ? ` · ${esc(reason)}` : ''}</span>`;

    const refreshAddList = async () => {
        if (!addTarget) return;
        const list = $('kpAddList');
        const alert = $('kpAddAlert');
        S.hideAlert(alert);
        list.innerHTML = `<li class="list-group-item small text-muted">${esc(t('Loading'))}</li>`;
        const term = $('kpAddSearch').value.trim();
        try {
            if (addKind === 'content') {
                const rows = (await api.get(`/lookups/path-contents?pathId=${pathId}&branchCode=${encodeURIComponent(addTarget.branchCode)}&chainStepId=${addTarget.chainStepId}`)).data || [];
                const shown = term ? rows.filter(r => (r.title || '').toLocaleLowerCase().includes(term.toLocaleLowerCase())) : rows;
                list.innerHTML = shown.length === 0 ? `<li class="list-group-item small text-muted">${esc(t('AddNone'))}</li>`
                    : shown.map(r => addRow(r.title, [r.typeLabel, r.languageName, r.statusLabel].filter(Boolean).join(' · '), '',
                        r.addable ? `<button type="button" class="btn btn-sm btn-primary js-add-content" data-id="${esc(r.contentId)}">${esc(t('AddButton'))}</button>` : cannot(r.reasonLabel))).join('');
                list.querySelectorAll('.js-add-content').forEach(btn => btn.addEventListener('click', () => addContent(rows.find(r => r.contentId === btn.dataset.id))));
            } else {
                const result = await api.get(`/lookups/path-claims?pathId=${pathId}${term ? `&q=${encodeURIComponent(term)}` : ''}`);
                if (result.disabled) {
                    list.innerHTML = `<li class="list-group-item small text-muted">${esc(t(result.reason) || t('ClaimOptionsUnavailable'))}</li>`;
                    return;
                }
                const rows = result.options || [];
                list.innerHTML = rows.length === 0 ? `<li class="list-group-item small text-muted">${esc(t('AddNone'))}</li>`
                    : rows.map(r => addRow([r.code, r.name].filter(Boolean).join(' · '), null,
                        `${r.text ? `<div class="small kp-claim-text text-break">“${esc(r.text)}”</div>` : ''}
                         <div class="small ${r.usable ? 'text-muted' : 'kp-reason'}">${esc(r.usable
                            ? [r.countryVersion ? t('ClaimVersionLabel', r.countryVersion) : '', r.statusLabel].filter(Boolean).join(' · ')
                            : [r.reasonLabel, r.countryVersion ? t('ClaimVersionLabel', r.countryVersion) : ''].filter(Boolean).join(' · '))}</div>`,
                        r.addable ? `<button type="button" class="btn btn-sm ${r.usable ? 'btn-primary' : 'btn-label-warning'} js-add-claim" data-id="${esc(r.claimId)}">${esc(t('AddButton'))}</button>` : cannot(r.addReasonLabel))).join('');
                list.querySelectorAll('.js-add-claim').forEach(btn => btn.addEventListener('click', () => addClaim(btn.dataset.id)));
            }
        } catch (error) {
            list.innerHTML = '';
            S.showAlert(alert, error);
        }
    };

    const randomCode = () => `S-${Math.random().toString(16).slice(2, 8).toUpperCase()}`;

    const afterAdd = async () => { await load(); await refreshAddList(); };

    const addContent = async row => {
        if (!row) return;
        S.hideAlert($('kpAddAlert'));
        try {
            await api.post(`/paths/${pathId}/steps`, {
                stepOrder: 10,
                stepCode: randomCode(),
                stepTitle: (row.title || '').slice(0, 200),
                stepType: row.defaultStepType || 'core-message',
                contentId: row.contentId,
                isRequired: true,
                arrangement: { chainStepId: addTarget.chainStepId, branchCode: addTarget.branchCode, position: slotItems().length }
            });
            window.showToast?.(t('Added'), 'success');
            await afterAdd();
        } catch (error) {
            S.showAlert($('kpAddAlert'), error);
        }
    };

    const addClaim = async claimId => {
        S.hideAlert($('kpAddAlert'));
        try {
            await api.post(`/paths/${pathId}/claims`, {
                claimId,
                arrangement: { chainStepId: addTarget.chainStepId, branchCode: addTarget.branchCode, position: slotItems().length }
            });
            window.showToast?.(t('Added'), 'success');
            await afterAdd();
        } catch (error) {
            S.showAlert($('kpAddAlert'), error);
        }
    };

    // ---------------- events ----------------

    document.addEventListener('click', event => {
        const add = event.target.closest('.js-add');
        if (add) { openAdd(add.dataset.branch, add.dataset.step); return; }
        const node = event.target.closest('.kp-item');
        if (!node || !model?.canEdit) return;
        if (event.target.closest('.js-remove')) { removeItem(node); return; }
        if (event.target.closest('.js-settings')) { openSettings(node); return; }
        const move = event.target.closest('.js-move');
        if (move) {
            const list = node.closest('.kp-items');
            const sibling = move.dataset.dir === '-1' ? node.previousElementSibling : node.nextElementSibling;
            if (!sibling || !sibling.classList.contains('kp-item')) return;
            if (move.dataset.dir === '-1') list.insertBefore(node, sibling); else list.insertBefore(sibling, node);
            persistOrder(list);
        }
    });
    $('kpAddTabContent').addEventListener('click', () => setAddKind('content'));
    $('kpAddTabClaim').addEventListener('click', () => setAddKind('claim'));
    $('kpAddSearch').addEventListener('input', () => {
        window.clearTimeout(addTimer);
        addTimer = window.setTimeout(refreshAddList, 300);
    });
    $('wsIntroDismiss').addEventListener('click', () => { S.storage.set(INTRO_KEY, '1'); renderIntro(); });
    $('wsBindButton').addEventListener('click', () => S.openBindModal({ pathId, subjectId: model.subjectId, name: model.pathName }, load));

    load();
})(window, document);
