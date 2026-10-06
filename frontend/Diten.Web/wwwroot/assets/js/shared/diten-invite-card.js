'use strict';

/*
 * DitenInviteCard — the ONE meeting-invitation card (WP-UI-MEETINGS-CALENDAR-01, MOD-0357 S3b 2c, BL-365).
 *
 * The Task Center's calendar panel ("Davetler") and the Meetings page's calendar ("Yanıt bekleyen davetlerim") draw
 * an invitation with THIS function. There is no second card: the owner's rule (2026-09-11) is that list/card parts
 * are written once and reused, and a guard test fails if either page draws its own.
 *
 * ── WHAT IT DECIDES, AND WHAT IT DOES NOT ─────────────────────────────────────────────────────────────────
 * It decides how an invitation LOOKS: the type label, the title, when, who organised it, and the two answer
 * buttons. It does not decide how an answer is SENT — the host passes each button's attributes (the Task Center's
 * `data-wcn-action`, the Meetings page's `data-mc-invite-answer`) and handles the click itself. An invitation is
 * never draggable: it is answered, not planned (owner, 2026-09-17).
 *
 * ── THE ACCEPT WARNING ────────────────────────────────────────────────────────────────────────────────────
 * `confirmAcceptOverlap` asks the calendar feed whether the meeting collides with one of the reader's own plan
 * blocks (`overlapsPlan`, computed by the ENGINE, half-open) and, only if it does, asks the reader with the shared
 * `showConfirm`, naming the block and its hours. It never refuses on its own: a failed read or a missing dialog
 * lets the acceptance through, because the rule is a warning, not a gate (owner, 2026-09-17). Declining asks
 * nothing.
 *
 * Strings come from SharedResource (7 languages) through the calendar payload (#diten-calendar-l10n, written by
 * Views/Shared/_CalendarAssets.cshtml) — the card is shared, so its words must not live in one page's resx.
 */
(function (global) {
    const esc = (value) => String(value == null ? '' : value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    const readLabels = () => {
        const node = global.document && global.document.getElementById('diten-calendar-l10n');
        if (!node) { return {}; }
        try { return JSON.parse(node.textContent || '{}'); } catch (_) { return {}; }
    };

    const label = (key, labels) => {
        const source = labels || readLabels();
        return Object.prototype.hasOwnProperty.call(source, key) ? source[key] : key;
    };

    const format = (template, ...args) => args.reduce(
        (text, arg, index) => String(text).split(`{${index}}`).join(String(arg)), template);

    const pageLanguage = () => ((global.document && global.document.documentElement.lang) || 'tr').slice(0, 2).toLowerCase();

    const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;

    /**
     * When the meeting is. A bare date ("2026-10-09") prints as a date; an instant prints date and time in the
     * given zone (the tenant's, from the feed), in the page language — the browser's Intl, no hand-made format.
     */
    const formatWhen = (when, zone, language) => {
        if (!when) { return ''; }
        const lang = language || pageLanguage();
        try {
            if (DATE_ONLY.test(String(when))) {
                return new Intl.DateTimeFormat(lang, { timeZone: 'UTC', dateStyle: 'medium' }).format(new Date(`${when}T00:00:00Z`));
            }
            const options = { dateStyle: 'medium', timeStyle: 'short' };
            if (zone) { options.timeZone = zone; }
            return new Intl.DateTimeFormat(lang, options).format(new Date(when));
        } catch (_) {
            return String(when);
        }
    };

    /** "09:00–10:00" in the zone — the hours of the block an acceptance would collide with. */
    const formatHours = (startAt, endAt, zone, language) => {
        const lang = language || pageLanguage();
        try {
            const options = { hour: '2-digit', minute: '2-digit' };
            if (zone) { options.timeZone = zone; }
            const f = new Intl.DateTimeFormat(lang, options);
            return `${f.format(new Date(startAt))}–${f.format(new Date(endAt))}`;
        } catch (_) {
            return '';
        }
    };

    const attrsOf = (attrs) => Object.keys(attrs || {})
        .map((name) => ` ${name}="${esc(attrs[name])}"`)
        .join('');

    const buttonHtml = (button) => {
        const tone = button.tone === 'accept' ? 'success' : 'secondary';
        return `<button type="button" class="btn btn-xs btn-label-${tone} dic-card-btn dic-card-btn-${button.tone === 'accept' ? 'accept' : 'decline'}"`
            + `${attrsOf(button.attrs)}${button.disabled ? ' disabled' : ''}>${esc(button.label)}</button>`;
    };

    /**
     * One invitation card.
     *
     * model: { id, title, typeName, when (date or instant), zone, organizerName,
     *          buttons: [{ tone: 'accept'|'decline', label, attrs: {…}, disabled }], extras (HTML, host-escaped),
     *          attrs: {…} — the host's own wiring on the card itself (e.g. the Task Center's `data-wcn-row`) }
     */
    const render = (model) => {
        const m = model || {};
        const when = formatWhen(m.when, m.zone, m.language);
        const meta = [
            when ? `<span class="dic-card-when"><i class="bx bx-time-five"></i>${esc(when)}</span>` : '',
            m.organizerName ? `<span class="dic-card-organizer"><i class="bx bx-user"></i>${esc(m.organizerName)}</span>` : ''
        ].join('');
        const buttons = (m.buttons || []).map(buttonHtml).join('');
        return `<article class="card dic-card" data-dic-invite="${esc(m.id)}"${attrsOf(m.attrs)} draggable="false">
            <div class="dic-card-head">
                <span class="dic-card-type"><i class="bx bx-calendar-event"></i>${esc(label('InviteCardType', m.labels))}</span>
                ${m.typeName ? `<span class="dic-card-kind">${esc(m.typeName)}</span>` : ''}
            </div>
            <div class="dic-card-title">${esc(m.title)}</div>
            ${meta ? `<div class="dic-card-meta">${meta}</div>` : ''}
            ${m.extras || ''}
            ${buttons ? `<div class="dic-card-actions">${buttons}</div>` : ''}
        </article>`;
    };

    const addDays = (isoDate, days) => {
        const date = new Date(`${isoDate}T00:00:00Z`);
        date.setUTCDate(date.getUTCDate() + days);
        return date.toISOString().slice(0, 10);
    };

    /** The feed range that surely holds the meeting's local day, whatever the tenant's offset: its UTC day ± 1. */
    const rangeAround = (when) => {
        const text = String(when || '');
        const day = DATE_ONLY.test(text) ? text : new Date(text).toISOString().slice(0, 10);
        return { from: addDays(day, -1), to: addDays(day, 1) };
    };

    /**
     * Before an acceptance: does the meeting collide with one of MY planned blocks? Resolves true to go on, false
     * when the reader chose not to. Asks ONLY when the feed says `overlapsPlan`.
     *
     * options: { meetingId, when, feed (optional, already loaded), fetchCalendar(from, to) → { ok, data },
     *            confirm (defaults to window.showConfirm), labels }
     */
    const confirmAcceptOverlap = async (options) => {
        const o = options || {};
        let feed = o.feed || null;
        let meeting = feed && (feed.meetings || []).find((row) => row.meetingId === o.meetingId);
        if (!meeting && typeof o.fetchCalendar === 'function' && o.when) {
            try {
                const range = rangeAround(o.when);
                const result = await o.fetchCalendar(range.from, range.to);
                feed = result && result.ok ? result.data : null;
                meeting = feed && (feed.meetings || []).find((row) => row.meetingId === o.meetingId);
            } catch (_) {
                meeting = null;
            }
        }
        if (!meeting || !meeting.overlapsPlan || !meeting.planOverlap) { return true; }

        const confirm = o.confirm || global.showConfirm;
        if (typeof confirm !== 'function') { return true; }
        const block = meeting.planOverlap;
        const zone = feed && feed.timeZoneId;
        // showConfirm writes its sub-text as TEXT (WP-SHARED-CONFIRM-XSS-01): the block title somebody typed goes in
        // as it is — escaping it here would show the entities.
        const subtext = format(label('InviteOverlapText', o.labels),
            block.title, formatHours(block.startAt, block.endAt, zone));
        return new Promise((resolve) => {
            confirm(label('InviteOverlapTitle', o.labels), () => resolve(true), {
                type: 'warning',
                subtext,
                confirmButtonText: label('InviteAcceptAnyway', o.labels),
                onCancel: () => resolve(false)
            });
        });
    };

    global.DitenInviteCard = { render, confirmAcceptOverlap, label: (key) => label(key), formatWhen, formatHours, rangeAround };
})(typeof window !== 'undefined' ? window : globalThis);
