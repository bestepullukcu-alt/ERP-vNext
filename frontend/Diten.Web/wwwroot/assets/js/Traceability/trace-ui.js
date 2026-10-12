/**
 * MOD-0174 P3 — what the lot / serial screens share: the vocabulary (window.TraceL10n, filled by the page's
 * index.l10n.js), the pack §14 button table (window.TraceActions, from the page's _TraceActions partial), formatting and
 * the same-origin fetch helpers. NO business rule lives here: the MVC proxies and the service decide; this file draws.
 *
 * The browser talks only to same-origin /Lots, /Serials, /LotMovements, /LotTrace — never to the Gateway, 5067 or /api/trace.
 */
'use strict';

window.TraceUi = (function () {
    const actions = (() => {
        try { return JSON.parse(document.getElementById('trace-actions')?.textContent || '{}'); } catch (error) { return {}; }
    })();

    // A missing key prints nothing (never the key, never English) — index.l10n.js has already reported it.
    // Each inserted value is a bidi ISOLATE (FSI…PDI), as the FU01 form does: numbers and codes keep their order in RTL.
    const t = (key, ...args) => String((window.TraceL10n || {})[key] || '').replace(/\{(\d)\}/g, (m, i) => `⁨${args[i] ?? ''}⁩`);

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
    const qty = (value) => {
        if (value === null || value === undefined || value === '') return '';
        try { return numberFormat.format(String(value)); } catch (error) { return String(value); }
    };
    const date = (wire) => {
        if (!wire) return '';
        const d = new Date(wire);
        if (Number.isNaN(d.getTime())) return '';
        return new Intl.DateTimeFormat(lang, { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC' }).format(d);
    };
    const dateTime = (wire) => {
        if (!wire) return '';
        const d = new Date(wire);
        return Number.isNaN(d.getTime()) ? '' : new Intl.DateTimeFormat(lang, { dateStyle: 'short', timeStyle: 'short' }).format(d);
    };
    const day = (wire) => (wire ? String(wire).slice(0, 10) : '');

    const STATUS_CLASS = { Draft: 'bg-label-secondary', Released: 'bg-label-success', Blocked: 'bg-label-danger', Expired: 'bg-label-dark' };
    const SERIAL_CLASS = { Active: 'bg-label-success', Consumed: 'bg-label-secondary', Blocked: 'bg-label-danger' };
    const statusBadge = (status) => status ? `<span class="badge ${STATUS_CLASS[status] || 'bg-label-secondary'}">${esc(t('Status.' + status))}</span>` : '';
    const serialBadge = (status) => status ? `<span class="badge ${SERIAL_CLASS[status] || 'bg-label-secondary'}">${esc(t('Serial.Status.' + status))}</span>` : '';

    // SKT pill (pack §11.3): past = red "Doldu", within 90 days = orange "N gün"; the date itself always shown.
    const expiryCell = (wire, state) => {
        if (!wire) return '';
        const text = esc(date(wire));
        if (state === 'Expired') return `${text} <span class="badge bg-label-danger ms-1">${esc(t('Expiry.Past'))}</span>`;
        if (state === 'ExpiringSoon') {
            const days = Math.max(0, Math.ceil((new Date(day(wire) + 'T00:00:00Z') - new Date(new Date().toISOString().slice(0, 10) + 'T00:00:00Z')) / 86400000));
            return `${text} <span class="badge bg-label-warning ms-1">${esc(t('Expiry.Days', days))}</span>`;
        }
        return text;
    };
    const retestCell = (wire, due) => !wire ? '' : `${esc(date(wire))}${due ? ` <i class="bx bx-error-circle text-warning" title="${esc(t('Retest.Past'))}" aria-label="${esc(t('Retest.Past'))}"></i>` : ''}`;

    const getJson = async (url) => {
        try {
            const res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            const json = await res.json().catch(() => null);
            return { ok: res.ok, status: res.status, json };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const postJson = async (url, body, token) => {
        try {
            const res = await fetch(url, {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json', RequestVerificationToken: token || '' },
                body: JSON.stringify(body)
            });
            const json = await res.json().catch(() => null);
            return { ok: res.ok, status: res.status, json };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const antiforgery = (scope) => (scope || document).querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    // A refusal in the reader's language; the correlation id only in the details disclosure (pack §13).
    const showAlert = (box, message, correlationId) => {
        if (!box) return;
        const text = box.querySelector('[data-alert-text]') || box;
        text.textContent = message || t('Error.Generic');
        const reference = box.querySelector('[data-alert-reference]');
        if (reference) {
            reference.classList.toggle('d-none', !correlationId);
            const c = reference.querySelector('[data-alert-correlation]');
            if (c) c.textContent = correlationId || '';
        }
        box.classList.remove('d-none');
    };
    const errorText = (reply) => reply?.json?.error?.message || (reply?.status === 0 ? t('Error.ServiceUnavailable') : t('Error.Generic'));

    const lotUrl = (lotId) => `/Lots/Details?${new URLSearchParams({ lotId })}`;

    return {
        t, esc, qty, date, dateTime, day, statusBadge, serialBadge, expiryCell, retestCell, getJson, postJson, antiforgery,
        showAlert, errorText, lotUrl,
        // The pack §14 table: a button is drawn only when its ACTION CODE is allowed for this user (never a key here).
        can: (code) => actions[code] === true
    };
})();
