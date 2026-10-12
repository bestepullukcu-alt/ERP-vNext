/**
 * WP-KP-UI-2 — the review tabs of the Knowledge Path Studio workspace (on top of KP-2 / KP-3):
 *  - tabs: Compliance · MLR approval · Output & release · Revisions · Field preview · Usage — a tab is shown only when
 *    the review model says it has something to show (permission + state); arrow keys move between tabs;
 *  - the ONE status line gets the review step ("In review · Legal"); the "next step" band; the submit / withdraw actions;
 *  - Compliance rows (labelled: severity, rule, branch › step › item, message, fix) with "Go to composition";
 *  - "Send for approval" modal: checklist (a blocker keeps Send closed), the change summary against the last revision,
 *    the open-round notice;
 *  - MLR timeline (step name, state, person, date, comment from MOD-0023), pending step + candidate positions, the
 *    reviewer view link and the single-channel note;
 *  - Output & release: render, the archive PDF (file name, size, date, fingerprint, view / download), the release
 *    preconditions (publisher ≠ submitter included), release with the "previous version in use" warning by stage name,
 *    withdrawal with a required reason and the stages that block it (path_in_use);
 *  - Revisions: the three state cards, the history, "Show difference" (two revisions or a revision and the draft);
 *  - Field preview: the approved / released revision's PDF embedded (no HTML preview before SB-4);
 *  - Usage: journey stages + strategy templates with links; no page-view data yet.
 * Data: api/studio/paths/{id}/review (+ /diff, /usage) and the workspace model (kp:workspace). Never a raw code.
 */
(function (window, document) {
    'use strict';
    const S = window.KpStudio;
    const root = document.getElementById('kpStudioRoot');
    if (!S || !root) return;
    const { t, esc, api } = S;
    const pathId = root.dataset.pathId;
    const $ = id => document.getElementById(id);

    let ws = null;          // the workspace model (KP-UI-1 + compliance)
    let review = null;      // the review model
    let active = 'composition';
    let usageLoaded = false;
    let reviewTimer = null;

    const lang = () => document.documentElement.lang || undefined;
    const date = v => v ? new Date(v).toLocaleString(lang(), { dateStyle: 'medium', timeStyle: 'short' }) : '';
    const size = bytes => {
        const n = Number(bytes) || 0;
        const fmt = new Intl.NumberFormat(lang(), { maximumFractionDigits: 1 });
        return n >= 1048576 ? t('SizeMb', fmt.format(n / 1048576)) : t('SizeKb', fmt.format(Math.max(1, n / 1024)));
    };
    const fingerprint = checksum => {
        const value = String(checksum || '').replace(/^sha256:/i, '');
        return value.length > 12 ? `${value.slice(0, 4)}…${value.slice(-4)}` : value;
    };
    const badge = (text, tone) => text ? `<span class="badge bg-label-${tone}">${esc(text)}</span>` : '';
    const STATE_TONES = { approved: 'success', rejected: 'danger', pending: 'warning', queued: 'secondary', cancelled: 'secondary', 'not-reached': 'secondary' };

    // ---------------- loading ----------------

    const loadReview = async () => {
        try {
            review = (await api.get(`/studio/paths/${pathId}/review`)).data;
        } catch (error) {
            review = null;
            S.showAlert($('wsAlert'), error);
        }
        render();
    };

    const reloadAll = async () => {
        if (window.KpWorkspace) await window.KpWorkspace.reload(); else await loadReview();
    };

    document.addEventListener('kp:workspace', event => {
        ws = event.detail;
        window.clearTimeout(reviewTimer);
        reviewTimer = window.setTimeout(loadReview, 0);
    });

    // ---------------- tabs ----------------

    const TABS = ['composition', 'compliance', 'mlr', 'output', 'revisions', 'preview', 'usage'];
    const panelOf = tab => tab === 'composition' ? $('wsComposition') : $(`wsPanel-${tab}`);
    const visibleTabs = () => TABS.filter(tab => !$(`wsTab-${tab}`).closest('li').classList.contains('d-none'));

    const selectTab = (tab, focus) => {
        if (!visibleTabs().includes(tab)) tab = 'composition';
        active = tab;
        TABS.forEach(name => {
            const button = $(`wsTab-${name}`);
            const on = name === tab;
            button.classList.toggle('active', on);
            button.setAttribute('aria-selected', on ? 'true' : 'false');
            button.tabIndex = on ? 0 : -1;
            panelOf(name)?.classList.toggle('d-none', !on);
        });
        if (focus) $(`wsTab-${tab}`).focus();
        if (tab === 'usage' && !usageLoaded) loadUsage();
    };

    const renderTabs = () => {
        const flags = review?.tabs || {};
        const show = {
            composition: true,
            compliance: !!flags.compliance && !!ws && !ws.isLegacyUnapproved,
            mlr: !!flags.mlr, output: !!flags.output, revisions: !!flags.revisions, preview: !!flags.preview, usage: !!flags.usage
        };
        TABS.forEach(tab => $(`wsTab-${tab}`).closest('li').classList.toggle('d-none', !show[tab]));
        const blockers = ws?.compliance?.blockers || 0;
        const countBadge = $('wsComplianceBadge');
        countBadge.textContent = blockers > 0 ? String(blockers) : '';
        countBadge.classList.toggle('d-none', blockers === 0);
        if (blockers > 0) countBadge.setAttribute('aria-label', t('BlockerCount', blockers)); else countBadge.removeAttribute('aria-label');
        selectTab(active, false);
    };

    // ---------------- status line + next band + header actions ----------------

    const renderStatus = () => {
        $('wsStatusDetail')?.remove();
        if (review?.statusDetail) {
            $('wsStatusLine').insertAdjacentHTML('afterbegin', `<span class="badge bg-label-info" id="wsStatusDetail">${esc(review.statusDetail)}</span>`);
            // The status detail REPLACES the plain lifecycle badge on the single status line.
            const plain = $('wsStatusLine').querySelector('.badge:not(#wsStatusDetail)');
            if (plain && plain.textContent === ws?.statusLabel) plain.remove();
        }
    };

    const renderNext = () => {
        const band = $('wsNextBand');
        const next = review?.next;
        band.classList.toggle('d-none', !next);
        if (!next) return;
        $('wsNextText').textContent = next.text || '';
        const action = $('wsNextAction');
        action.classList.toggle('d-none', !next.action);
        action.textContent = next.actionLabel || '';
        action.dataset.action = next.action || '';
    };

    const runNext = action => {
        if (action === 'wizard') { openWizard(); return; }
        if (action === 'submit') { openSubmit(); return; }
        if (TABS.includes(action)) selectTab(action, true);
    };

    const openWizard = () => window.KpLegacyWizard?.open({ pathId, subjectId: ws?.subjectId, name: ws?.pathName, code: ws?.pathCode }, reloadAll);

    const renderActions = () => {
        const submit = review?.submit;
        $('wsSubmitButton').classList.toggle('d-none', !submit?.canSubmit);
        $('wsWithdrawReview').classList.toggle('d-none', !submit?.canWithdrawReview);
    };

    // ---------------- compliance ----------------

    const renderCompliance = () => {
        const c = ws?.compliance || { blockers: 0, warnings: 0, rows: [] };
        $('wsComplianceCounts').innerHTML = `
            <div class="d-flex align-items-center gap-2"><span class="badge bg-label-danger fs-6">${esc(c.blockers)}</span><span>${esc(t('Sev_blocker'))}</span></div>
            <div class="d-flex align-items-center gap-2"><span class="badge bg-label-warning fs-6">${esc(c.warnings)}</span><span>${esc(t('Sev_warning'))}</span></div>`;
        const canFixPlacement = ws?.canManage && ws?.status === 'draft';
        $('wsComplianceRows').innerHTML = c.rows.length === 0
            ? `<tr><td colspan="5" class="text-success"><i class="bx bx-check-circle me-1" aria-hidden="true"></i>${esc(t('ComplianceClean'))}</td></tr>`
            : c.rows.map((r, i) => `
                <tr>
                    <td>${badge(r.severityLabel, r.severity === 'blocker' ? 'danger' : 'warning')}</td>
                    <td class="small fw-medium">${esc(r.ruleLabel)}</td>
                    <td class="small">${esc(r.where || '—')}</td>
                    <td class="small"><div>${esc(r.message)}</div><div class="text-muted">${esc(t('FixPrefix', r.fix))}</div></td>
                    <td class="cell-fit">${r.rule === 'placement'
                        ? (canFixPlacement ? `<button type="button" class="btn btn-sm btn-label-primary js-comp-wizard">${esc(t('MapSteps'))}</button>` : '')
                        : `<button type="button" class="btn btn-sm btn-label-primary js-comp-go" data-index="${i}">${esc(t('GoToComposition'))}</button>`}</td>
                </tr>`).join('');
    };

    const goTo = row => {
        selectTab('composition', false);
        window.setTimeout(() => window.KpWorkspace?.focusItem(row.branchCode, row.chainStepId, row.itemKind, row.itemId), 50);
    };

    // ---------------- submit (modal) ----------------

    const openSubmit = async () => {
        const modalEl = $('kpSubmitModal');
        if (!modalEl || !review) return;
        const submit = review.submit;
        const rows = ws?.compliance?.rows || [];
        const blockers = rows.filter(r => r.severity === 'blocker');
        S.hideAlert($('kpSubmitAlert'));
        $('kpSubmitIntro').textContent = t('SubmitIntro', submit.nextRevisionNumber);
        const open = submit.openRound;
        $('kpSubmitOpenRound').textContent = open ? t('SubmitOpenRound', open.revisionNumber, open.stepName || '') : '';
        $('kpSubmitOpenRound').classList.toggle('d-none', !open);
        $('kpSubmitChecklist').innerHTML = blockers.length === 0
            ? `<li class="list-group-item text-success small"><i class="bx bx-check-circle me-1" aria-hidden="true"></i>${esc(t('SubmitNoBlockers'))}</li>`
            : blockers.map(r => `
                <li class="list-group-item d-flex align-items-start gap-2 small">
                    <i class="bx bx-x-circle text-danger mt-1" aria-hidden="true"></i>
                    <div class="flex-grow-1"><div class="fw-medium">${esc(r.ruleLabel)} · ${esc(r.where || '')}</div><div>${esc(r.message)}</div></div>
                    ${r.rule === 'placement' ? '' : `<button type="button" class="btn btn-sm btn-label-primary js-submit-go" data-index="${rows.indexOf(r)}">${esc(t('GoToComposition'))}</button>`}
                </li>`).join('');
        const send = $('kpSubmitSend');
        send.textContent = t('SubmitSend', submit.nextRevisionNumber);
        send.disabled = !submit.canSubmit || blockers.length > 0 || !!open;
        $('kpSubmitBlocked').textContent = open ? t('SubmitBlockedOpen') : blockers.length > 0 ? t('SubmitBlocked', blockers.length) : '';
        $('kpSubmitChangesTitle').textContent = submit.lastRevisionNumber
            ? t('SubmitChangesTitle', submit.lastRevisionNumber, submit.nextRevisionNumber)
            : t('SubmitFirstTitle');
        $('kpSubmitChanges').innerHTML = submit.lastRevisionId ? `<span class="text-muted">${esc(t('Loading'))}</span>` : `<span class="text-muted">${esc(t('SubmitFirstText'))}</span>`;
        window.bootstrap?.Modal.getOrCreateInstance(modalEl).show();
        if (submit.lastRevisionId) {
            try {
                const diff = (await api.get(`/studio/paths/${pathId}/diff?from=${submit.lastRevisionId}&to=draft`)).data;
                $('kpSubmitChanges').innerHTML = diffList(diff.rows);
            } catch (error) {
                $('kpSubmitChanges').innerHTML = `<span class="text-danger">${esc(error.message)}</span>`;
            }
        }
    };

    const sendSubmit = async () => {
        const send = $('kpSubmitSend');
        send.disabled = true;
        try {
            await api.post(`/paths/${pathId}/submit-review`);
            window.bootstrap?.Modal.getOrCreateInstance($('kpSubmitModal')).hide();
            window.showToast?.(t('SubmitDone'), 'success');
            active = 'mlr';
            await reloadAll();
        } catch (error) {
            S.showAlert($('kpSubmitAlert'), error);
            send.disabled = false;
        }
    };

    const withdrawReview = () => {
        const go = async () => {
            try {
                await api.post(`/paths/${pathId}/withdraw-review`);
                window.showToast?.(t('WithdrawReviewDone'), 'success');
                await reloadAll();
            } catch (error) {
                S.showAlert($('wsAlert'), error);
            }
        };
        if (window.showConfirm) window.showConfirm(t('WithdrawReviewConfirm'), go, { type: 'warning', confirmButtonText: t('WithdrawReview') });
        else go();
    };

    // ---------------- MLR ----------------

    const renderMlr = () => {
        const tl = review?.timeline;
        if (!tl) return;
        $('wsMlrTitle').textContent = t('MlrTitle', tl.revisionNumber, tl.pathVersion || '');
        const link = $('wsReviewerLink');
        link.href = `/CRM/KnowledgePaths/${pathId}/Review/${tl.revisionId}`;
        link.classList.remove('d-none');
        $('wsMlrUnavailable').classList.toggle('d-none', tl.available);
        const submitted = `
            <li>
                <span class="kp-dot is-approved" aria-hidden="true"></span>
                <div class="d-flex flex-wrap align-items-center gap-2"><span class="fw-medium">${esc(t('MlrSubmitted'))}</span>${badge(t('MlrState_submitted'), 'primary')}</div>
                <small class="text-muted">${esc([tl.submittedBy, date(tl.submittedAt)].filter(Boolean).join(' · '))}</small>
            </li>`;
        $('wsMlrTimeline').innerHTML = submitted + tl.steps.map(s => `
            <li>
                <span class="kp-dot is-${esc(s.state)}" aria-hidden="true"></span>
                <div class="d-flex flex-wrap align-items-center gap-2"><span class="fw-medium">${esc(s.stepName)}</span>${badge(s.stateLabel, STATE_TONES[s.state] || 'secondary')}</div>
                <small class="text-muted d-block">${esc([s.actor, date(s.at)].filter(Boolean).join(' · ') || (s.state === 'queued' ? t('MlrQueuedNote') : ''))}</small>
                ${s.comment ? `<div class="kp-quote small mt-1">“${esc(s.comment)}”</div>` : ''}
                ${s.state === 'pending' ? `<small class="d-block mt-1">${esc(t('MlrPendingNote'))}${s.candidates?.length ? ` · ${esc(t('MlrCandidates', s.candidates.join(', ')))}` : ''}</small>` : ''}
            </li>`).join('');
        const pending = tl.steps.find(s => s.state === 'pending');
        const done = tl.steps.filter(s => s.state === 'approved').length;
        const row = (k, v) => `<dt class="col-6 text-muted fw-normal">${esc(k)}</dt><dd class="col-6">${esc(v || '—')}</dd>`;
        $('wsMlrSummary').innerHTML = [
            row(t('MlrSumRevision'), t('CardRevShort', tl.revisionNumber)),
            row(t('MlrSumSubmitter'), tl.submittedBy),
            row(t('MlrSumSubmitted'), date(tl.submittedAt)),
            row(t('MlrSumPending'), pending ? pending.stepName : (tl.isOpen ? '' : t('MlrSumNone'))),
            row(t('MlrSumDone'), t('MlrSumDoneValue', done, tl.steps.length))
        ].join('');
    };

    // ---------------- output & release ----------------

    const artifactCard = (artifact, revisionNumber) => `
        <div class="border rounded p-3">
            <div class="d-flex flex-wrap align-items-start gap-2">
                <i class="bx bxs-file-pdf fs-3 text-danger" aria-hidden="true"></i>
                <div class="flex-grow-1 min-w-0">
                    <div class="fw-medium text-break">${esc(artifact.fileName || '')}</div>
                    <small class="text-muted d-block">${esc([size(artifact.byteSize), date(artifact.renderedAt)].filter(Boolean).join(' · '))}</small>
                    <small class="text-muted d-block">${esc(t('FingerprintLabel'))} <span class="kp-fingerprint" title="${esc(artifact.checksum || '')}">${esc(fingerprint(artifact.checksum))}</span></small>
                    <small class="text-muted d-block">${esc(t('ArtifactFrom', revisionNumber))}</small>
                </div>
                <div class="d-flex gap-1">
                    <a class="btn btn-sm btn-label-secondary" href="${esc(artifact.viewUrl)}" target="_blank" rel="noopener"><i class="bx bx-show me-1" aria-hidden="true"></i>${esc(t('ArtifactView'))}</a>
                    <a class="btn btn-sm btn-label-primary" href="${esc(artifact.downloadUrl)}"><i class="bx bx-download me-1" aria-hidden="true"></i>${esc(t('ArtifactDownload'))}</a>
                </div>
            </div>
        </div>`;

    const renderOutput = () => {
        S.hideAlert($('wsOutputAlert'));
        const candidate = review?.release?.candidate;
        const live = review?.release?.live;
        const host = $('wsOutputArtifact');
        if (candidate) {
            host.innerHTML = candidate.artifact ? artifactCard(candidate.artifact, candidate.revisionNumber)
                : `<div class="border rounded p-3"><div class="fw-medium mb-1">${esc(t('OutputNone', candidate.revisionNumber))}</div>
                   <small class="text-muted d-block mb-2">${esc(t('OutputNoneText'))}</small>
                   ${candidate.canRender ? `<button type="button" class="btn btn-primary btn-sm" id="wsRenderButton"><i class="bx bx-printer me-1" aria-hidden="true"></i>${esc(t('RenderButton'))}</button>` : ''}</div>`;
            $('wsReleaseTitle').textContent = t('ReleaseTitleRev', candidate.revisionNumber);
            $('wsPreconditions').innerHTML = candidate.preconditions.map(p => `
                <li class="d-flex align-items-start gap-2 mb-2">
                    <i class="bx ${p.ok ? 'bx-check-circle text-success' : 'bx-x-circle text-danger'} mt-1" aria-hidden="true"></i>
                    <div><span class="visually-hidden">${esc(p.ok ? t('PreOk') : t('PreMissing'))}</span><div class="small fw-medium">${esc(p.label)}</div>${p.reason ? `<div class="small text-muted">${esc(p.reason)}</div>` : ''}</div>
                </li>`).join('');
            const button = $('wsReleaseButton');
            button.classList.remove('d-none');
            button.disabled = !candidate.canRelease;
            const firstMissing = candidate.preconditions.find(p => !p.ok);
            $('wsReleaseDisabledReason').textContent = candidate.canRelease ? '' : (firstMissing ? t('ReleaseBlocked', firstMissing.reason || firstMissing.label) : '');
        } else {
            host.innerHTML = live?.artifact ? artifactCard(live.artifact, live.revisionNumber) : `<p class="text-muted small">${esc(t('OutputNothing'))}</p>`;
            $('wsReleaseTitle').textContent = t('ReleaseTitle');
            $('wsPreconditions').innerHTML = `<li class="small text-muted">${esc(t('ReleaseNoCandidate'))}</li>`;
            $('wsReleaseButton').classList.add('d-none');
            $('wsReleaseDisabledReason').textContent = '';
        }

        const liveBox = $('wsLive');
        liveBox.classList.toggle('d-none', !live);
        if (live) {
            $('wsLiveTitle').textContent = t('LiveTitle', live.pathVersion || '', live.revisionNumber);
            $('wsLiveSub').textContent = t('LiveSub', live.releasedBy || '', date(live.releasedAt));
            $('wsWithdrawForm').classList.toggle('d-none', !live.canWithdraw);
        }
    };

    const render_ = async () => {
        const candidate = review?.release?.candidate;
        if (!candidate) return;
        const button = $('wsRenderButton');
        if (button) button.disabled = true;
        try {
            await api.post(`/paths/${pathId}/revisions/${candidate.revisionId}/render`);
            window.showToast?.(t('RenderDone'), 'success');
            await loadReview();
        } catch (error) {
            S.showAlert($('wsOutputAlert'), error);
            if (button) button.disabled = false;
        }
    };

    const release = () => {
        const candidate = review?.release?.candidate;
        if (!candidate?.canRelease) return;
        const text = candidate.replacesVersion ? t('ReleaseConfirmReplace', candidate.pathVersion || '', candidate.replacesVersion) : t('ReleaseConfirm', candidate.pathVersion || '');
        const go = async () => {
            $('wsReleaseButton').disabled = true;
            try {
                const result = (await api.post(`/paths/${pathId}/revisions/${candidate.revisionId}/release`)).data || {};
                const stages = (result.previousPathInUse || []).map(s => [s.journeyName, s.stageName].filter(Boolean).join(' › '));
                const warn = $('wsReleaseWarning');
                if ((result.warnings || []).includes('previous_path_in_use') && stages.length) {
                    warn.innerHTML = `<div class="fw-medium">${esc(t('Err_previous_path_in_use'))}</div><ul class="mb-0">${stages.map(s => `<li>${esc(s)}</li>`).join('')}</ul>`;
                    warn.classList.remove('d-none');
                } else {
                    warn.classList.add('d-none');
                }
                window.showToast?.(t('ReleaseDone'), 'success');
                await reloadAll();
            } catch (error) {
                S.showAlert($('wsOutputAlert'), error);
                $('wsReleaseButton').disabled = false;
            }
        };
        if (window.showConfirm) window.showConfirm(text, go, { type: 'question', confirmButtonText: t('ReleaseButton') });
        else go();
    };

    const withdraw = async () => {
        const live = review?.release?.live;
        if (!live) return;
        const reasonEl = $('wsWithdrawReason');
        const errorEl = $('wsWithdrawError');
        const stagesEl = $('wsWithdrawStages');
        stagesEl.classList.add('d-none');
        const reason = reasonEl.value.trim();
        if (!reason) {
            // Required on the client too (CRM answers reason_required anyway).
            reasonEl.classList.add('is-invalid');
            errorEl.textContent = t('Err_reason_required');
            errorEl.classList.remove('d-none');
            reasonEl.focus();
            return;
        }
        reasonEl.classList.remove('is-invalid');
        errorEl.classList.add('d-none');
        const go = async () => {
            try {
                await api.post(`/paths/${pathId}/revisions/${live.revisionId}/withdraw`, { reason });
                reasonEl.value = '';
                window.showToast?.(t('WithdrawDone'), 'success');
                await reloadAll();
            } catch (error) {
                errorEl.textContent = error.message;
                errorEl.classList.remove('d-none');
                if (error.code === 'path_in_use' && error.details?.length) {
                    stagesEl.innerHTML = `<li class="list-unstyled fw-medium">${esc(t('WithdrawStagesTitle'))}</li>` + error.details.map(d => `<li>${esc(d)}</li>`).join('');
                    stagesEl.classList.remove('d-none');
                }
            }
        };
        if (window.showConfirm) window.showConfirm(t('WithdrawConfirm', live.pathVersion || ''), go, { type: 'warning', confirmButtonText: t('WithdrawButton') });
        else go();
    };

    // ---------------- revisions ----------------

    const diffList = rows => !rows || rows.length === 0
        ? `<span class="text-muted">${esc(t('DiffNone'))}</span>`
        : `<ul class="list-unstyled mb-0">${rows.map(r => `
            <li class="d-flex flex-wrap gap-2 py-1 border-bottom">
                ${badge(r.kindLabel, /removed/.test(r.kind) ? 'danger' : /added/.test(r.kind) ? 'success' : 'info')}
                <span class="fw-medium">${esc(r.label)}</span>
                ${r.from || r.to ? `<span class="text-muted">${esc([r.from || '—', r.to || '—'].join(' → '))}</span>` : ''}
            </li>`).join('')}</ul>`;

    const renderRevisions = () => {
        const cards = review?.cards || {};
        const card = (title, value, tone, icon) => `
            <div class="col-12 col-md-4">
                <div class="border rounded p-3 h-100">
                    <small class="text-muted d-block"><i class="bx ${icon} me-1" aria-hidden="true"></i>${esc(title)}</small>
                    <span class="fw-medium ${value ? `text-${tone}` : 'text-muted'}">${esc(value || t('CardNone'))}</span>
                </div>
            </div>`;
        $('wsRevCards').innerHTML = card(t('CardField'), cards.field, 'success', 'bx-broadcast')
            + card(t('CardApproval'), cards.approval, 'info', 'bx-user-check')
            + card(t('CardWorking'), cards.working, 'heading', 'bx-edit');

        const revisions = review?.revisions || [];
        const options = revisions.map(r => `<option value="${esc(r.revisionId)}">${esc(t('CardRev', r.pathVersion || '', r.revisionNumber))}</option>`).join('');
        const from = $('wsDiffFrom');
        const to = $('wsDiffTo');
        const keepFrom = from.value;
        const keepTo = to.value;
        from.innerHTML = options;
        to.innerHTML = (ws?.status === 'draft' ? `<option value="draft">${esc(t('DiffDraft'))}</option>` : '') + options;
        if (keepFrom) from.value = keepFrom;
        if (keepTo) to.value = keepTo;
        if (!keepFrom && revisions.length > 1 && ws?.status !== 'draft') { from.value = revisions[1].revisionId; to.value = revisions[0].revisionId; }

        $('wsRevList').innerHTML = revisions.map((r, i) => `
            <li>
                <span class="kp-dot is-${r.status === 'approved' ? 'approved' : r.status === 'rejected' ? 'rejected' : r.isOpen ? 'pending' : 'queued'}" aria-hidden="true"></span>
                <div class="d-flex flex-wrap align-items-center gap-2">
                    <span class="fw-medium">${esc(t('CardRev', r.pathVersion || '', r.revisionNumber))}</span>
                    ${badge(r.statusLabel, r.status === 'approved' ? 'success' : r.status === 'rejected' ? 'danger' : r.isOpen ? 'info' : 'secondary')}
                    ${r.isLive ? badge(t('OnFieldBadge'), 'success') : ''}
                    ${r.release && !r.isLive ? badge(r.release.stateLabel, 'warning') : ''}
                </div>
                <small class="text-muted d-block">${esc([r.submittedBy, date(r.submittedAt)].filter(Boolean).join(' · '))}${r.changeCount ? ` · ${esc(t('RevChanges', r.changeCount))}` : ''}${r.openNoteCount ? ` · ${esc(t('RevOpenNotes', r.openNoteCount))}` : ''}</small>
                ${(r.decisions || []).map(d => `<small class="d-block">${esc([d.stepName, d.actor, d.stateLabel, date(d.at)].filter(Boolean).join(' · '))}${d.comment ? ` — “${esc(d.comment)}”` : ''}</small>`).join('')}
                ${r.artifact ? `<small class="d-block"><i class="bx bxs-file-pdf me-1" aria-hidden="true"></i><a href="${esc(r.artifact.viewUrl)}" target="_blank" rel="noopener">${esc(r.artifact.fileName || t('ArtifactView'))}</a> · ${esc(t('FingerprintLabel'))} <span class="kp-fingerprint" title="${esc(r.artifact.checksum || '')}">${esc(fingerprint(r.artifact.checksum))}</span></small>` : ''}
                ${r.release ? `<small class="d-block">${esc([r.release.stateLabel, r.release.by, date(r.release.at)].filter(Boolean).join(' · '))}${r.release.reason ? ` — “${esc(r.release.reason)}”` : ''}</small>` : ''}
                <div class="mt-1 d-flex flex-wrap gap-2">
                    ${revisions[i + 1] ? `<button type="button" class="btn btn-xs btn-label-secondary js-rev-diff" data-from="${esc(revisions[i + 1].revisionId)}" data-to="${esc(r.revisionId)}">${esc(t('DiffWith', revisions[i + 1].revisionNumber))}</button>` : ''}
                    <a class="btn btn-xs btn-label-secondary" href="/CRM/KnowledgePaths/${esc(pathId)}/Review/${esc(r.revisionId)}">${esc(t('ReviewerView'))}</a>
                </div>
            </li>`).join('');
    };

    const showDiff = async (from, to) => {
        const host = $('wsDiffResult');
        if (!from || !to || from === to) { host.innerHTML = `<span class="small text-muted">${esc(t('DiffPickTwo'))}</span>`; return; }
        host.innerHTML = `<span class="small text-muted">${esc(t('Loading'))}</span>`;
        try {
            const diff = (await api.get(`/studio/paths/${pathId}/diff?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`)).data;
            host.innerHTML = `<div class="border rounded p-3 small"><div class="fw-medium mb-2">${esc(t('DiffTitle', diff.fromLabel, diff.toLabel))}</div>${diffList(diff.rows)}</div>`;
        } catch (error) {
            host.innerHTML = `<span class="small text-danger">${esc(error.message)}</span>`;
        }
    };

    // ---------------- preview ----------------

    const renderPreview = () => {
        const live = review?.release?.live;
        const candidate = review?.release?.candidate;
        const source = candidate?.artifact ? candidate : live?.artifact ? live : null;
        const host = $('wsPreview');
        if (!source) {
            host.innerHTML = `<div class="alert alert-secondary d-flex align-items-center gap-2 mb-0" role="status"><i class="bx bx-info-circle" aria-hidden="true"></i><span>${esc(candidate ? t('PreviewNoOutput') : t('PreviewDraft'))}</span></div>`;
            return;
        }
        const current = host.querySelector('iframe');
        const caption = t('PreviewCaption', source.revisionNumber, source.pathVersion || '', fingerprint(source.artifact.checksum));
        if (current && current.dataset.src === source.artifact.viewUrl) return;
        host.innerHTML = `
            <div class="d-flex flex-wrap align-items-center gap-2 mb-2">
                <span class="small fw-medium flex-grow-1">${esc(caption)}</span>
                <a class="btn btn-sm btn-label-primary" href="${esc(source.artifact.downloadUrl)}"><i class="bx bx-download me-1" aria-hidden="true"></i>${esc(t('ArtifactDownload'))}</a>
            </div>
            <iframe class="kp-preview-frame" title="${esc(caption)}" src="${esc(source.artifact.viewUrl)}" data-src="${esc(source.artifact.viewUrl)}"></iframe>
            <small class="text-muted d-block mt-2">${esc(t('PreviewPdfNote'))}</small>`;
    };

    // ---------------- usage ----------------

    const loadUsage = async () => {
        usageLoaded = true;
        const host = $('wsUsage');
        host.innerHTML = `<span class="small text-muted">${esc(t('Loading'))}</span>`;
        try {
            const u = (await api.get(`/studio/paths/${pathId}/usage`)).data;
            const journeys = u.journeys || [];
            const templates = u.strategyTemplates || [];
            host.innerHTML = `
                <div class="row g-4">
                    <div class="col-12 col-lg-7">
                        <h6>${esc(t('UsageJourneys'))}</h6>
                        ${journeys.length === 0 ? `<p class="small text-muted">${esc(t('UsageNoJourneys'))}</p>` : `<ul class="list-group">${journeys.map(j => `
                            <li class="list-group-item">
                                <a class="fw-medium" href="${esc(j.url)}">${esc(j.name || '')}</a>
                                <small class="text-muted d-block">${esc([j.stageName, j.statusLabel, j.pinLabel].filter(Boolean).join(' · '))}</small>
                            </li>`).join('')}</ul>`}
                    </div>
                    <div class="col-12 col-lg-5">
                        <h6>${esc(t('UsageTemplates'))}</h6>
                        ${templates.length === 0 ? `<p class="small text-muted">${esc(t('UsageNoTemplates'))}</p>` : `<ul class="list-group">${templates.map(s => `
                            <li class="list-group-item">
                                <a class="fw-medium" href="${esc(s.url)}">${esc(s.name || '')}</a>
                                <small class="text-muted d-block">${esc(s.statusLabel || '')}</small>
                            </li>`).join('')}</ul>`}
                    </div>
                </div>
                <div class="alert alert-secondary small mt-4 mb-0" role="note"><i class="bx bx-bar-chart-alt-2 me-1" aria-hidden="true"></i>${esc(t('UsageViewsLater'))}</div>`;
        } catch (error) {
            usageLoaded = false;
            host.innerHTML = `<span class="small text-danger">${esc(error.message)}</span>`;
        }
    };

    // ---------------- render ----------------

    const render = () => {
        renderTabs();
        renderStatus();
        renderNext();
        renderActions();
        renderCompliance();
        if (!review) return;
        renderMlr();
        renderOutput();
        renderRevisions();
        renderPreview();
        if (active === 'usage') loadUsage();
    };

    // ---------------- events ----------------

    $('wsTabList').addEventListener('click', event => {
        const button = event.target.closest('[data-ws-tab]');
        if (button) selectTab(button.dataset.wsTab, false);
    });
    $('wsTabList').addEventListener('keydown', event => {
        if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return;
        const tabs = visibleTabs();
        const index = tabs.indexOf(active);
        const rtl = document.documentElement.dir === 'rtl';
        const step = (event.key === 'ArrowRight') !== rtl ? 1 : -1;
        const target = event.key === 'Home' ? tabs[0] : event.key === 'End' ? tabs[tabs.length - 1] : tabs[(index + step + tabs.length) % tabs.length];
        event.preventDefault();
        selectTab(target, true);
    });
    $('wsNextAction').addEventListener('click', event => runNext(event.currentTarget.dataset.action));
    $('wsSubmitButton').addEventListener('click', () => void openSubmit());
    $('wsWithdrawReview').addEventListener('click', withdrawReview);
    $('kpSubmitSend').addEventListener('click', () => void sendSubmit());
    $('kpSubmitChecklist').addEventListener('click', event => {
        const go = event.target.closest('.js-submit-go');
        if (!go) return;
        window.bootstrap?.Modal.getOrCreateInstance($('kpSubmitModal')).hide();
        goTo(ws.compliance.rows[Number(go.dataset.index)]);
    });
    $('wsComplianceRows').addEventListener('click', event => {
        const go = event.target.closest('.js-comp-go');
        if (go) { goTo(ws.compliance.rows[Number(go.dataset.index)]); return; }
        if (event.target.closest('.js-comp-wizard')) openWizard();
    });
    $('wsPanel-output').addEventListener('click', event => {
        if (event.target.closest('#wsRenderButton')) void render_();
    });
    $('wsReleaseButton').addEventListener('click', release);
    $('wsWithdrawButton').addEventListener('click', () => void withdraw());
    $('wsDiffButton').addEventListener('click', () => void showDiff($('wsDiffFrom').value, $('wsDiffTo').value));
    $('wsRevList').addEventListener('click', event => {
        const diff = event.target.closest('.js-rev-diff');
        if (!diff) return;
        $('wsDiffFrom').value = diff.dataset.from;
        $('wsDiffTo').value = diff.dataset.to;
        void showDiff(diff.dataset.from, diff.dataset.to);
        $('wsDiffResult').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    });
})(window, document);
