/**
 * WP-CYC-UI-1 — helpers shared by the Cycle Periods list, panel and details scripts.
 *  - esc(): every value that reaches innerHTML goes through it; plain text goes through textContent.
 *  - Dates and numbers are formatted in the READER's culture (the <html lang> the shell renders). A period day is a
 *    stored UTC-midnight calendar DAY, so day() pins timeZone UTC — a browser west of Greenwich would otherwise show
 *    the previous day. A stamp (created / activated / closed at) is a real instant and uses the reader's clock.
 *  - All traffic goes to the same-origin proxy /CRM/CyclePeriods/api (never a gateway URL, never a bearer token).
 */
(function (window, document) {
    'use strict';

    const L = () => window.CyclePeriodsL10n || window.L10n || {};
    const locale = (document.documentElement.getAttribute('lang') || '').trim() || undefined;
    const endpoint = '/CRM/CyclePeriods/api';

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));

    const safe = fn => { try { return fn(); } catch (e) { return null; } };
    const toDate = v => {
        if (!v) return null;
        // "yyyy-MM-dd" is a calendar day: read it as UTC midnight so it never drifts by the reader's offset.
        const d = /^\d{4}-\d{2}-\d{2}$/.test(String(v)) ? new Date(`${v}T00:00:00Z`) : new Date(v);
        return Number.isNaN(d.getTime()) ? null : d;
    };
    const day = v => { const d = toDate(v); return d ? (safe(() => d.toLocaleDateString(locale, { year: 'numeric', month: 'short', day: '2-digit', timeZone: 'UTC' })) || d.toISOString().slice(0, 10)) : '—'; };
    const stamp = v => { const d = toDate(v); return d ? (safe(() => d.toLocaleString(locale, { year: 'numeric', month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' })) || d.toISOString()) : '—'; };
    const number = v => (v === null || v === undefined || v === '') ? '—' : (safe(() => new Intl.NumberFormat(locale).format(Number(v))) || String(v));
    const plain = v => (v === null || v === undefined || v === '') ? '—' : String(v);
    const monthName = (year, month) => safe(() => new Date(Date.UTC(year, month - 1, 1)).toLocaleDateString(locale, { month: 'long', year: 'numeric', timeZone: 'UTC' })) || `${year}-${String(month).padStart(2, '0')}`;
    const isoDay = d => `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}-${String(d.getUTCDate()).padStart(2, '0')}`;

    const statusLabel = v => ({ draft: L().StatusDraft, active: L().StatusActive, closed: L().StatusClosed }[v] || v || '—');
    const statusTone = v => v === 'active' ? 'success' : v === 'closed' ? 'secondary' : 'warning';
    const scopeLabel = v => ({
        tenant: L().ScopeTypeTenant,
        country: L().ScopeTypeCountry,
        'legal-entity': L().ScopeTypeLegalEntity,
        'business-unit': L().ScopeTypeBusinessUnit
    }[v] || v || '—');
    const badge = (text, tone) => `<span class="badge bg-label-${esc(tone)}">${esc(text)}</span>`;

    /** Reads the gateway envelope; a non-2xx throws with the runtime's own messages (they name the blocking period). */
    const envelope = async response => {
        if (response.status === 204) return null;
        const body = await response.json().catch(() => ({}));
        if (!response.ok) {
            const errors = Array.isArray(body.errors) ? body.errors.filter(e => typeof e === 'string' && !/^[a-z_]+$/.test(e)) : [];
            throw Object.assign(new Error((errors.length ? errors : [body.message || L().ErrorOccurred]).join(' · ')), { status: response.status, codes: body.errors || [] });
        }
        return body.data;
    };
    const headers = { Accept: 'application/json', 'Content-Type': 'application/json' };
    const getJson = async path => envelope(await fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers }));
    const sendJson = async (method, path, body) => envelope(await fetch(`${endpoint}${path}`, {
        method, credentials: 'same-origin', headers, body: body === undefined ? undefined : JSON.stringify(body)
    }));

    /** Formats every server-rendered [data-day] / [data-stamp] / [data-number] under root in the reader's culture. */
    const formatAll = root => {
        (root || document).querySelectorAll('[data-day]').forEach(el => { el.textContent = day(el.dataset.day); });
        (root || document).querySelectorAll('[data-stamp]').forEach(el => { el.textContent = stamp(el.dataset.stamp); });
        (root || document).querySelectorAll('[data-number]').forEach(el => { el.textContent = el.dataset.plain ? plain(el.dataset.number) : number(el.dataset.number); });
    };

    const flags = (() => {
        try { return JSON.parse(document.getElementById('cycleperiod-page-flags')?.textContent || '{}'); }
        catch (e) { return {}; }
    })();

    window.CyclePeriodsShared = {
        L, locale, endpoint, esc, day, stamp, number, plain, monthName, isoDay, toDate,
        statusLabel, statusTone, scopeLabel, badge, envelope, getJson, sendJson, formatAll,
        canManage: !!flags.canManage, canActivate: !!flags.canActivate
    };
})(window, document);
