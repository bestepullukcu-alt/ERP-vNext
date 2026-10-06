'use strict';

/*
 * MOD-0357 S7 (K6) — "Devam toplantısı planla" dialog, the same raw-Swal multi-field shape
 * task-from-meeting-dialog.js already establishes (title/type/date fields no single showConfirm input type
 * covers). Openable from a Scheduled, Cancelled OR Completed meeting's Details page — continuing does not undo
 * a cancellation, and a Completed meeting is exactly the common case a follow-up gets scheduled from.
 */
(function (global) {
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    // Same generator task-from-meeting-dialog.js uses for its own K11 client key.
    const newIdempotencyKey = () => {
        if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') { return crypto.randomUUID(); }
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
            const r = (Math.random() * 16) | 0;
            const v = c === 'x' ? r : (r & 0x3) | 0x8;
            return v.toString(16);
        });
    };

    /**
     * @param {object} options
     * @param {object} options.meeting        the CURRENT meeting (source of the follow-up)
     * @param {Function} options.t            translate(key)
     * @param {Function} [options.onScheduled] called with the result's `data` on success, BEFORE navigation
     */
    const open = (options) => {
        const t = options.t;
        const source = options.meeting;
        if (!global.Swal || !global.DitenDialog) { return; }
        const dialogLook = global.DitenDialog.dialogLook();
        const dialogIcon = global.DitenDialog.dialogIcon('info', 'bx-calendar-plus');

        global.Swal.fire(Object.assign({
            title: dialogIcon + '<span>' + esc(t('scheduleFollowUp')) + '</span>',
            html: `<p class="text-muted small text-start mb-3">${esc(t('scheduleFollowUpHint'))}</p>`
                + `<label class="form-label d-block text-start" for="mtgFollowUpTitle">${esc(t('title'))}</label>`
                + `<input type="text" id="mtgFollowUpTitle" class="form-control" maxlength="200" autocomplete="off" placeholder="${esc(source.title)} (${esc(t('followUpTitleSuffix'))})" />`
                + `<label class="form-label d-block text-start mt-3" for="mtgFollowUpType">${esc(t('meetingType'))}</label>`
                + `<select id="mtgFollowUpType" class="form-select"></select>`
                + `<label class="form-label d-block text-start mt-3" for="mtgFollowUpStartAt">${esc(t('startAt'))}</label>`
                + `<input type="text" id="mtgFollowUpStartAt" class="form-control wcn-date-input" autocomplete="off" />`
                + `<label class="form-label d-block text-start mt-3" for="mtgFollowUpEndAt">${esc(t('endAt'))}</label>`
                + `<input type="text" id="mtgFollowUpEndAt" class="form-control wcn-date-input" autocomplete="off" />`,
            showCancelButton: true,
            confirmButtonText: t('scheduleFollowUp'),
            cancelButtonText: t('cancel'),
            didOpen: async (popup) => {
                if (global.flatpickr) {
                    global.flatpickr(document.getElementById('mtgFollowUpStartAt'), { enableTime: true, dateFormat: 'Y-m-d H:i', disableMobile: true });
                    global.flatpickr(document.getElementById('mtgFollowUpEndAt'), { enableTime: true, dateFormat: 'Y-m-d H:i', disableMobile: true });
                }
                const typesResult = await global.MeetingsApi.lookupTypes();
                const types = typesResult.ok ? (typesResult.data || []) : [];
                const select = document.getElementById('mtgFollowUpType');
                types.forEach((type) => select.appendChild(new Option(type.name, type.id)));
                select.value = source.meetingTypeId;
                global.DitenDialog.bindDialogSelect2(select, popup, { allowClear: false });
            },
            preConfirm: () => {
                const meetingTypeId = String(document.getElementById('mtgFollowUpType')?.value || '');
                const startAtLocal = String(document.getElementById('mtgFollowUpStartAt')?.value || '').trim();
                const endAtLocal = String(document.getElementById('mtgFollowUpEndAt')?.value || '').trim();
                if (!meetingTypeId || !startAtLocal || !endAtLocal) {
                    global.Swal.showValidationMessage(t('formValidationError'));
                    return false;
                }
                const startAt = new Date(startAtLocal.replace(' ', 'T'));
                const endAt = new Date(endAtLocal.replace(' ', 'T'));
                if (Number.isNaN(startAt.getTime()) || Number.isNaN(endAt.getTime())) {
                    global.Swal.showValidationMessage(t('formValidationError'));
                    return false;
                }
                if (endAt <= startAt) {
                    global.Swal.showValidationMessage(t('errorEndBeforeStart'));
                    return false;
                }
                const title = String(document.getElementById('mtgFollowUpTitle')?.value || '').trim() || null;
                return { title, meetingTypeId, startAt: startAt.toISOString(), endAt: endAt.toISOString() };
            }
        }, dialogLook)).then(async (res) => {
            if (!res.isConfirmed || !res.value) { return; }
            const result = await global.MeetingsApi.scheduleFollowUp(source.id, {
                title: res.value.title,
                meetingTypeId: res.value.meetingTypeId,
                startAt: res.value.startAt,
                endAt: res.value.endAt,
                location: null,
                organizerUserId: null,
                description: null,
                attendeeUserIds: null,
                idempotencyKey: newIdempotencyKey()
            });
            if (!result.ok) {
                global.DitenModal?.error?.({ title: t('errorOccurred'), message: global.MeetingsApi.failureMessage(result) });
                return;
            }
            options.onScheduled?.(result.data);
            // AC5 — no open work still creates the meeting; the user is told, not left to notice an empty agenda.
            const message = result.data.carriedAgendaItemCount > 0
                ? t('toastFollowUpScheduled')
                : t('toastFollowUpScheduledNoOpenWork');
            global.DitenModal?.success?.({ title: message, timer: 1600 });
            global.setTimeout(() => { global.location.href = `/Meetings/${result.data.meetingId}`; }, 1600);
        });
    };

    global.MeetingsFollowUpDialog = { open };
})(typeof window !== 'undefined' ? window : globalThis);
