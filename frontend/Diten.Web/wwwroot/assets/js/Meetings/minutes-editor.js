'use strict';

/*
 * MOD-0357 S6 — the Minutes editor page (pack §5 "2a. TAB ≠ PAGE", K4). Its own root, #minutesEditorRoot, is
 * the ONLY element this file looks for — a page without it (every other Meetings screen) leaves this script a
 * no-op, mirroring Meetings/form.js's own "which branch runs is decided by which root element the page has".
 *
 * K4's own shape, reflected here: a DRAFT is edited in place and resubmitted whole (SaveMinutesDraft);
 * PUBLISHING locks it (fields go read-only, content stops being resubmitted); the only way past a lock is
 * CorrectPublishedMinutes, which asks for the SAME content plus a mandatory reason and produces a new,
 * already-published version — never a second "draft" state the reader has to publish again.
 */
(function () {
    const t = (key) => window.MinutesEditorL10n?.t?.(key) ?? key;
    const tShared = (key) => window.MeetingsL10n?.t?.(key) ?? key;

    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    const root = document.getElementById('minutesEditorRoot');
    if (!root) { return; }

    const meetingId = root.dataset.meetingId;
    const canWrite = root.dataset.canWrite === 'true';
    const canPublish = root.dataset.canPublish === 'true';

    // Wire ints (System.Text.Json's default enum encoding — no [JsonStringEnumConverter] in this API), the
    // same convention Meetings/form.js's own MEETING_LIFECYCLE constant already relies on.
    const MINUTES_STATUS = { DRAFT: 0, PUBLISHED: 1 };
    const ATTENDANCE_STATUS = { PRESENT: 0, ABSENT: 1, EXCUSED: 2 };

    let currentMeeting = null;
    let eligiblePeopleById = {};
    let versions = []; // newest first (server order)
    let linkedTasksByRecordLinkId = {};

    // Local editable copy — only meaningful while the latest version is Draft-or-absent; a Published latest
    // version is rendered straight from `versions[0]`, never through this local state.
    let localAttendance = {}; // userId -> ATTENDANCE_STATUS value or null (unset)
    let localDecisions = []; // [{ code, text, decidedByUserId, recordLinkId }]

    const latestVersion = () => versions[0] || null;
    const isLocked = () => latestVersion()?.status === MINUTES_STATUS.PUBLISHED;

    const attendanceLabel = (status) => ({
        [ATTENDANCE_STATUS.PRESENT]: t('attendancePresent'),
        [ATTENDANCE_STATUS.ABSENT]: t('attendanceAbsent'),
        [ATTENDANCE_STATUS.EXCUSED]: t('attendanceExcused')
    }[status] ?? '-');

    // BL-390 — the same "ekranda GUID yok" fix as Meetings/index.js and Meetings/form.js: a participant whose
    // account no longer resolves through the eligible-people lookup renders the shared unknown-user label, never
    // their raw id.
    const personName = (userId) => eligiblePeopleById[userId] || tShared('unknownUser');

    const newLocalStateFromVersion = (version) => {
        localAttendance = {};
        (version?.attendance || []).forEach((a) => { localAttendance[a.attendeeUserId] = a.status; });
        localDecisions = (version?.decisions || []).map((d) => ({
            code: d.code, text: d.text, decidedByUserId: d.decidedByUserId || null, recordLinkId: d.recordLinkId || null
        }));
    };

    // ── Rendering ────────────────────────────────────────────────────────────────────────────────────────────

    const renderStatusBadge = () => {
        const badge = document.getElementById('mStatusBadge');
        const v = latestVersion();
        if (!v) {
            badge.textContent = t('statusDraft');
            badge.className = 'badge bg-label-secondary';
            return;
        }
        if (v.status === MINUTES_STATUS.PUBLISHED) {
            badge.textContent = `${t('statusPublishedPrefix')}${v.versionNumber}`;
            badge.className = 'badge bg-label-success';
        } else {
            badge.textContent = t('statusDraft');
            badge.className = 'badge bg-label-secondary';
        }
    };

    const renderActionBar = () => {
        const locked = isLocked();
        document.getElementById('btnSaveDraft')?.classList.toggle('d-none', !canWrite || locked);
        document.getElementById('btnPublish')?.classList.toggle('d-none', !canPublish || locked);
        document.getElementById('btnCorrect')?.classList.toggle('d-none', !canPublish || !locked);
        document.getElementById('btnAddDecision')?.classList.toggle('d-none', !canWrite || locked);
    };

    const renderAttendance = () => {
        const list = document.getElementById('attendanceList');
        const attendees = currentMeeting?.attendees || [];
        document.getElementById('noAttendeesHint')?.classList.toggle('d-none', attendees.length > 0);
        list.innerHTML = attendees.map((a) => {
            const current = localAttendance[a.userId];
            const locked = isLocked() || !canWrite;
            const options = [ATTENDANCE_STATUS.PRESENT, ATTENDANCE_STATUS.ABSENT, ATTENDANCE_STATUS.EXCUSED]
                .map((value) => `<option value="${value}" ${current === value ? 'selected' : ''}>${esc(attendanceLabel(value))}</option>`)
                .join('');
            return `<li class="list-group-item d-flex align-items-center justify-content-between gap-2">
                <span>${esc(personName(a.userId))}</span>
                <select class="form-select form-select-sm w-auto js-attendance-status" data-user-id="${esc(a.userId)}" ${locked ? 'disabled' : ''}>
                    <option value="">-</option>
                    ${options}
                </select>
            </li>`;
        }).join('');
    };

    const decisionTaskRow = (decision) => {
        const linked = decision.recordLinkId ? linkedTasksByRecordLinkId[decision.recordLinkId] : null;
        if (linked) {
            const rowHtml = window.DitenRelatedRecords.relatedRecordRow(
                { id: linked.taskId, title: linked.title, link: linked.link }, { esc });
            const addedLater = linked.createdAfterMinutesPublished
                ? `<span class="badge bg-label-warning ms-2">${esc(t('addedLaterBadge'))}</span>` : '';
            return `<div class="mt-2">${rowHtml}${addedLater}</div>`;
        }
        // No task yet — offer to create one. Deliberately NOT gated on `isLocked()`: K4/ADR-003 explicitly
        // allow a decision inside an ALREADY-PUBLISHED version to still produce a task later (flagged "added
        // later" via CreatedAfterMinutesPublished) — only the decision's own TEXT freezes, not this action.
        return `<div class="mt-2">
            <button type="button" class="btn btn-label-primary btn-sm js-create-task-from-decision" data-decision-code="${esc(decision.code)}">
                <i class="bx bx-task me-1"></i>${esc(t('createTaskFromDecision'))}
            </button>
        </div>`;
    };

    const renderDecisions = () => {
        const list = document.getElementById('decisionsList');
        document.getElementById('noDecisionsHint')?.classList.toggle('d-none', localDecisions.length > 0);
        const locked = isLocked() || !canWrite;
        const attendeeOptions = (currentMeeting?.attendees || [])
            .map((a) => `<option value="${esc(a.userId)}">${esc(personName(a.userId))}</option>`).join('');

        list.innerHTML = localDecisions.map((decision, index) => `
            <div class="border rounded p-3 js-decision-row" data-index="${index}">
                <div class="d-flex align-items-start gap-2">
                    <span class="badge bg-label-secondary mt-1">${esc(decision.code)}</span>
                    <textarea class="form-control js-decision-text" rows="2" maxlength="2000"
                        placeholder="${esc(t('decisionTextPlaceholder'))}" ${locked ? 'disabled' : ''}>${esc(decision.text)}</textarea>
                    ${locked ? '' : `<button type="button" class="btn btn-icon btn-text-danger js-remove-decision" title="${esc(t('removeDecision'))}"><i class="bx bx-trash"></i></button>`}
                </div>
                <div class="d-flex align-items-center gap-2 mt-2">
                    <label class="form-label mb-0 text-nowrap">${esc(t('decidedByLabel'))}</label>
                    <select class="form-select form-select-sm w-auto js-decision-decided-by" ${locked ? 'disabled' : ''}>
                        <option value="">${esc(t('decidedByPlaceholder'))}</option>
                        ${attendeeOptions}
                    </select>
                </div>
                ${decisionTaskRow(decision)}
            </div>`).join('');

        // Set AFTER insertion (a native <select>'s value cannot be expressed as a `selected` attribute on a
        // dynamically-escaped option list without a second pass) — done regardless of `locked`, so a read-only
        // Published version still SHOWS who decided, not a blank placeholder.
        list.querySelectorAll('.js-decision-row').forEach((rowEl) => {
            const index = Number(rowEl.dataset.index);
            const decidedBySelect = rowEl.querySelector('.js-decision-decided-by');
            if (decidedBySelect) { decidedBySelect.value = localDecisions[index].decidedByUserId || ''; }
        });

        renderAddedLaterTasks();
    };

    /*
     * K4/ADR-003 — a task created from a decision AFTER that decision's own version published never gets
     * written back onto the (frozen) decision row (the source guard `CreateTaskFromMeetingHandler` itself
     * enforces), so `decisionTaskRow` above can never discover it by `decision.recordLinkId`. It is real and it
     * belongs to this meeting, so it is listed here instead — every linked task flagged
     * `createdAfterMinutesPublished` that no CURRENT decision row already claims (the "no current decision
     * claims it" half matters for a correction: a later correction MAY re-embed the same link into a decision,
     * at which point it belongs in the decision row, not here, and must not be shown twice).
     */
    const renderAddedLaterTasks = () => {
        const section = document.getElementById('addedLaterTasksSection');
        const list = document.getElementById('addedLaterTasksList');
        const claimed = new Set(localDecisions.filter((d) => d.recordLinkId).map((d) => d.recordLinkId));
        const addedLater = Object.values(linkedTasksByRecordLinkId)
            .filter((task) => task.createdAfterMinutesPublished && !claimed.has(task.recordLinkId));

        section.classList.toggle('d-none', addedLater.length === 0);
        list.innerHTML = addedLater
            .map((task) => window.DitenRelatedRecords.relatedRecordRow({ id: task.taskId, title: task.title, link: task.link }, { esc }))
            .join('');
    };

    const renderHistory = () => {
        const list = document.getElementById('versionHistoryList');
        list.innerHTML = versions.map((v) => {
            const label = v.status === MINUTES_STATUS.PUBLISHED
                ? `${t('historyPublishedPrefix')}${v.versionNumber}`
                : `${t('historyDraftLabel')} v${v.versionNumber}`;
            const correction = v.correctionOfVersionNumber
                ? `<div class="text-muted small">${esc(t('historyCorrectionOfPrefix'))}${v.correctionOfVersionNumber}${v.correctionReason ? ' — ' + esc(v.correctionReason) : ''}</div>`
                : '';
            // BL-390 — the same fix as personName(): a publisher whose display name did not come back with the
            // version never falls back to their raw id.
            const publishedBy = v.publishedAtUtc
                ? `<div class="text-muted small">${esc(v.publishedByDisplayName || tShared('unknownUser'))} · ${new Date(v.publishedAtUtc).toLocaleString(window.CurrentLanguage || undefined)}</div>`
                : '';
            return `<li class="list-group-item">
                <div class="fw-semibold">${esc(label)}</div>
                ${publishedBy}${correction}
            </li>`;
        }).join('');
    };

    const renderAll = () => {
        document.getElementById('mMeetingTitle').textContent = currentMeeting?.title || '-';
        renderStatusBadge();
        renderActionBar();
        renderAttendance();
        renderDecisions();
        renderHistory();
    };

    // ── Data loading ─────────────────────────────────────────────────────────────────────────────────────────

    const readLocalFromDom = () => {
        document.querySelectorAll('.js-attendance-status').forEach((select) => {
            const userId = select.dataset.userId;
            localAttendance[userId] = select.value === '' ? null : Number(select.value);
        });
        document.querySelectorAll('.js-decision-row').forEach((rowEl) => {
            const index = Number(rowEl.dataset.index);
            const text = rowEl.querySelector('.js-decision-text')?.value || '';
            const decidedByUserId = rowEl.querySelector('.js-decision-decided-by')?.value || null;
            if (localDecisions[index]) {
                localDecisions[index].text = text;
                localDecisions[index].decidedByUserId = decidedByUserId || null;
            }
        });
    };

    const loadLinkedTasks = async () => {
        const result = await window.MeetingsApi.linkedTasks(meetingId);
        linkedTasksByRecordLinkId = {};
        if (result.ok) {
            (result.data || []).forEach((task) => { linkedTasksByRecordLinkId[task.recordLinkId] = task; });
        }
    };

    const reload = async () => {
        const [meetingResult, minutesResult, peopleResult] = await Promise.all([
            window.MeetingsApi.get(meetingId),
            window.MeetingsApi.getMinutes(meetingId),
            window.MeetingsApi.lookupAttendees()
        ]);

        if (!meetingResult.ok || !minutesResult.ok) {
            document.getElementById('minutesNotFound')?.classList.remove('d-none');
            document.getElementById('minutesEditorBody')?.classList.add('d-none');
            return;
        }

        document.getElementById('minutesNotFound')?.classList.add('d-none');
        document.getElementById('minutesEditorBody')?.classList.remove('d-none');

        currentMeeting = meetingResult.data;
        eligiblePeopleById = {};
        // BL-390 — same posture as Meetings/form.js: an eligible-people entry missing its own displayName does
        // not get backfilled with the raw id (personName()/publishedBy above are what read this dictionary).
        (peopleResult.ok ? peopleResult.data?.people || [] : []).forEach((p) => { eligiblePeopleById[p.userId] = p.displayName || tShared('unknownUser'); });
        versions = minutesResult.data?.versions || [];

        await loadLinkedTasks();
        newLocalStateFromVersion(latestVersion());
        renderAll();
    };

    // ── Actions ──────────────────────────────────────────────────────────────────────────────────────────────

    const buildDraftPayload = () => {
        readLocalFromDom();
        return {
            attendance: Object.keys(localAttendance)
                .filter((userId) => localAttendance[userId] !== null && localAttendance[userId] !== undefined)
                .map((userId) => ({ attendeeUserId: userId, status: localAttendance[userId] })),
            decisions: localDecisions
                .filter((d) => String(d.text || '').trim().length > 0)
                .map((d) => ({ text: d.text.trim(), decidedByUserId: d.decidedByUserId || null }))
        };
    };

    const saveDraft = async () => {
        const payload = Object.assign(buildDraftPayload(), { expectedVersion: latestVersion()?.version ?? null });
        const result = await window.MeetingsApi.saveMinutesDraft(meetingId, payload);
        if (!result.ok) {
            window.DitenModal?.error?.({ title: tShared('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
            return;
        }
        window.DitenModal?.success?.({ title: t('toastDraftSaved'), timer: 1200 });
        await reload();
    };

    const publish = () => {
        const v = latestVersion();
        if (!v) { return; }
        window.showConfirm(t('publishConfirmTitle'), async () => {
            const result = await window.MeetingsApi.publishMinutes(meetingId, { expectedVersion: v.version });
            if (!result.ok) {
                window.DitenModal?.error?.({ title: tShared('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
                return;
            }
            window.DitenModal?.success?.({ title: t('toastPublished'), timer: 1200 });
            await reload();
        }, { subtext: t('publishConfirmText'), confirmButtonText: t('publish') });
    };

    const openCorrectDialog = () => {
        if (!window.Swal || !window.DitenDialog) { return; }
        readLocalFromDom();
        const draft = buildDraftPayload();
        const dialogLook = window.DitenDialog.dialogLook();
        const dialogIcon = window.DitenDialog.dialogIcon('warning', 'bx-edit-alt');

        window.Swal.fire(Object.assign({
            title: dialogIcon + '<span>' + esc(t('publishCorrection')) + '</span>',
            html: `<div class="text-start mb-2">${esc(t('publishConfirmText'))}</div>`
                + `<label class="form-label d-block text-start" for="mtgCorrectionReason">${esc(t('correctionReasonLabel'))}</label>`
                + `<textarea id="mtgCorrectionReason" class="form-control" rows="3" placeholder="${esc(t('correctionReasonPlaceholder'))}"></textarea>`,
            showCancelButton: true,
            confirmButtonText: t('publishCorrection'),
            cancelButtonText: tShared('cancel'),
            preConfirm: () => {
                const reason = String(document.getElementById('mtgCorrectionReason')?.value || '').trim();
                if (!reason) { window.Swal.showValidationMessage(t('correctionReasonRequired')); return false; }
                return { reason };
            }
        }, dialogLook)).then(async (res) => {
            if (!res.isConfirmed || !res.value) { return; }
            const result = await window.MeetingsApi.correctPublishedMinutes(meetingId, Object.assign(
                { correctionReason: res.value.reason }, draft));
            if (!result.ok) {
                window.DitenModal?.error?.({ title: tShared('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
                return;
            }
            window.DitenModal?.success?.({ title: t('toastCorrected'), timer: 1200 });
            await reload();
        });
    };

    // ── Wiring ───────────────────────────────────────────────────────────────────────────────────────────────

    document.getElementById('btnSaveDraft')?.addEventListener('click', saveDraft);
    document.getElementById('btnPublish')?.addEventListener('click', publish);
    document.getElementById('btnCorrect')?.addEventListener('click', openCorrectDialog);

    document.getElementById('btnAddDecision')?.addEventListener('click', () => {
        readLocalFromDom();
        localDecisions.push({ code: `D-${localDecisions.length + 1}`, text: '', decidedByUserId: null, recordLinkId: null });
        renderDecisions();
    });

    document.getElementById('decisionsList')?.addEventListener('click', (event) => {
        const removeBtn = event.target.closest('.js-remove-decision');
        if (removeBtn) {
            readLocalFromDom();
            const index = Number(removeBtn.closest('.js-decision-row')?.dataset.index);
            localDecisions.splice(index, 1);
            renderDecisions();
            return;
        }

        const createTaskBtn = event.target.closest('.js-create-task-from-decision');
        if (createTaskBtn) {
            const decisionCode = createTaskBtn.dataset.decisionCode;
            window.MeetingsTaskFromMeetingDialog.open({
                meetingId,
                decisionCode,
                t: tShared,
                onCreated: () => reload()
            });
        }
    });

    reload();
})();
