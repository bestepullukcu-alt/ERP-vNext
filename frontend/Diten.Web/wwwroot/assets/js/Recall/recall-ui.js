/**
 * MOD-0177 R6 — the shared helpers of the recall pages (pack §11.2). No recall rule lives here: the server decides (RECALL-BUNDLE 1.5.0 —
 * KPIs, the trace, the union list). This file holds the vocabulary reader, formatting, the same-origin calls (/Recall/** — antiforgery +
 * Idempotency-Key on writes) and the badges. Buttons are shown by action code (can) — never by a permission key.
 */
'use strict';

window.RecallUi = (function () {
    const actions = (() => {
        try { return JSON.parse(document.getElementById('recall-actions')?.textContent || '{}'); } catch (error) { return {}; }
    })();
    const L = () => window.RecallL10n || {};
    const t = (key, ...args) => String(L()[key] || '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);
    const has = (key) => !!L()[key];
    const can = (code) => actions[code] === true;
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
    const qty = (value) => {
        const n = Number(value);
        return Number.isFinite(n) ? numberFormat.format(n) : String(value ?? '');
    };
    const date = (wire) => {
        if (!wire) return '';
        const d = new Date(String(wire).slice(0, 10) + 'T00:00:00Z');
        return Number.isNaN(d.getTime()) ? String(wire) : d.toLocaleDateString(lang, { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC' });
    };
    const byId = (id) => document.getElementById(id);
    const newKey = () => 'r6-' + (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`).replace(/[^A-Za-z0-9-]/g, '');
    const antiforgery = () => document.querySelector('#rc-antiforgery input[name="__RequestVerificationToken"]')?.value || '';

    const call = async (method, url, body) => {
        const headers = { Accept: 'application/json' };
        if (method !== 'GET') {
            headers['Content-Type'] = 'application/json';
            headers.RequestVerificationToken = antiforgery();
        }
        try {
            const res = await fetch(url, { method, credentials: 'same-origin', headers, body: body === undefined ? undefined : JSON.stringify(body) });
            const json = await res.json().catch(() => null);
            return { ok: res.ok, status: res.status, json };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const get = (url) => call('GET', url);
    const post = (url, body) => call('POST', url, body);
    const codeOf = (reply) => reply?.json?.error?.code || null;
    const errorText = (reply) => {
        const code = codeOf(reply);
        if (code && has(`Recall.Errors.${code}`)) return t(`Recall.Errors.${code}`);
        if (reply?.status === 0 || reply?.status >= 500) return t('Recall.Errors.SERVICE_UNAVAILABLE');
        if (reply?.status === 401 || reply?.status === 403) return t('Recall.Errors.FORBIDDEN');
        return t('Recall.Errors.GENERIC');
    };
    const showError = (box, reply) => {
        if (!box) return;
        box.textContent = typeof reply === 'string' ? reply : errorText(reply);
        box.classList.remove('d-none');
    };
    const hideError = (box) => box?.classList.add('d-none');

    // ── display (pure; RecallView in the view scripts reuses them) ──────────────────────────────────────────────────
    const duration = (minutes) => RecallFormat.duration(minutes, t);
    const STATUS = { Draft: 'bg-label-secondary', PendingScopeApproval: 'bg-label-warning', Active: 'bg-label-danger', EffectivenessCheck: 'bg-label-info',
        Closed: 'bg-label-success', Cancelled: 'bg-label-secondary' };
    const DRILL = { Running: 'bg-label-warning', Completed: 'bg-label-primary', Cancelled: 'bg-label-secondary' };
    const CLASS = { 1: 'bg-label-danger', 2: 'bg-label-warning', 3: 'bg-label-info' };
    const statusBadge = (status) => `<span class="badge ${STATUS[status] || 'bg-label-secondary'}">${esc(t('Recall.Status.' + status) || status)}</span>`;
    const drillStatusBadge = (status) => `<span class="badge ${DRILL[status] || 'bg-label-secondary'}">${esc(t('Recall.DrillStatus.' + status) || status)}</span>`;
    const classBadge = (c) => `<span class="badge ${CLASS[c] || 'bg-label-secondary'}">${esc(t('Recall.Class.N', c))}</span>`;
    const typeBadge = (kind) => kind === 'Drill'
        ? `<span class="badge bg-label-primary">${esc(t('Recall.Type.Drill'))}</span>`
        : `<span class="badge bg-label-danger">${esc(t('Recall.Type.Real'))}</span>`;
    const productCell = (row, lot) => `<div class="d-flex flex-column"><span class="fw-medium text-heading">${esc(row.itemLabel || row.item?.itemId || '')}</span>`
        + `<small class="text-muted font-monospace">${esc(lot || '')}</small></div>`;

    return {
        t, has, can, esc, qty, date, byId, newKey, get, post, errorText, showError, hideError,
        duration, statusBadge, drillStatusBadge, classBadge, typeBadge, productCell
    };
})();

/** Pure formatting shared with the Node tests (no DOM). */
var RecallFormat = (function () {
    /** Minutes → "1 sa 42 dk" / "45 dk" in the reader's words. */
    const duration = (minutes, t) => {
        const m = Math.max(0, Math.round(Number(minutes) || 0));
        return m >= 60 ? t('Recall.Duration.HoursMinutes', Math.floor(m / 60), m % 60) : t('Recall.Duration.Minutes', m);
    };
    return { duration };
})();

if (typeof window !== 'undefined') window.RecallFormat = RecallFormat;
