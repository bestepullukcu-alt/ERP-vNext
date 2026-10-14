/**
 * WP-VW-W3E (W3E-WEB-a) — the institution QR screens' pure rules (no DOM; Node-tested): the account card (Q1 a–d), the
 * history timeline (Q1), the regenerate / revoke dialogs (Q3 / Q4), the bulk page rows / filters / selection (Q5) and the
 * print sheet (Q2). The server is the authority: the card only reflects the CRM state, the buttons only follow the keys.
 *
 *   cardModel(state, perms)          → { status, chipKey, showHistory, canEdit, readonly, inherited, version, nextVersion,
 *                                        issuedBy, issuedAt, lastScannedAt, recent, revokedBy, revokedAt, reason }
 *   issueBody(state, reason)         → POST issue body: expectedVersion only when an ACTIVE code is regenerated
 *   revokeBody(state, reason)        → POST revoke body (null when the reason is not valid)
 *   reasonState(text)                → { length, valid } — required, ≤ 500 after trimming
 *   toastKey(before)                 → 'ToastIssued' | 'ToastRegenerated'
 *   eventLine(ev)                    → { key, version, tone, paperKey, copies, reason, by, at }
 *   whenKey(iso, nowMs, offsetMin)   → 'Today' | 'Yesterday' | null (the page writes the time / the date)
 *   errorKey(response)               → the error text key (Err_<code> | Err_permission | Err_generic)
 *   listQuery(filters, page, size)   → the list query string (empty filters left out)
 *   listRow(item, lookups)           → a Q5 row model
 *   select / toggleAll               → the selection (a Set of ids), at most MAX_BULK
 *   bulkResultArgs(dto)              → [issued, skippedActive]
 *   PRINT: SINGLE_PAPERS, LABEL_PAPERS, labelGrid(paper), printUrl(...)
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) { module.exports = factory(); } else { root.VisitQrCore = factory(); }
}(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    const MAX_REASON = 500;
    const MAX_BULK = 200;
    const STATUSES = ['active', 'revoked', 'none'];

    const statusOf = state => (state && STATUSES.indexOf(state.status) > -1 ? state.status : 'none');

    /** The card (Q1a none · Q1b active · Q1c revoked · Q1d read only). perms = { read, manage }. */
    const cardModel = (state, perms) => {
        const s = state || {};
        const p = perms || {};
        const status = statusOf(s);
        const canEdit = !!p.manage;
        return {
            status: status,
            chipKey: 'Status_' + status,
            showHistory: status !== 'none',
            canEdit: canEdit,
            readonly: !canEdit,
            inherited: status === 'active' ? null : (s.inheritedFrom && s.inheritedFrom.accountName ? s.inheritedFrom : null),
            version: s.version == null ? null : s.version,
            nextVersion: s.version == null ? 1 : s.version + 1,
            issuedBy: s.issuedBy || null,
            issuedAt: s.issuedAt || null,
            lastScannedAt: s.lastScannedAt || null,
            recent: typeof s.recentScanCount === 'number' ? s.recentScanCount : 0,
            revokedBy: s.revokedBy || null,
            revokedAt: s.revokedAt || null,
            reason: s.revokeReason || null
        };
    };

    const trimmed = text => String(text == null ? '' : text).trim();

    /** Required, at most 500 characters (the CRM checks again). */
    const reasonState = text => {
        const t = trimmed(text);
        return { length: t.length, valid: t.length > 0 && t.length <= MAX_REASON };
    };

    /** Generate (no active code) → no expectedVersion; regenerate (active) → the version the user saw. */
    const issueBody = (state, reason) => {
        const r = trimmed(reason);
        return { expectedVersion: statusOf(state) === 'active' ? state.version : null, reason: r.length ? r.slice(0, MAX_REASON) : null };
    };

    const revokeBody = (state, reason) => (reasonState(reason).valid && statusOf(state) === 'active'
        ? { expectedVersion: state.version, reason: trimmed(reason) }
        : null);

    const toastKey = before => (statusOf(before) === 'active' ? 'ToastRegenerated' : 'ToastIssued');

    const TONES = { issued: 'ok', bulk_issued: 'ok', regenerated: 'primary', revoked: 'warning', printed: 'secondary' };

    /** One history event → its line: "Printed · Version 3 · A4, 2 copies", the reason under it. */
    const eventLine = ev => {
        const e = ev || {};
        const kind = Object.prototype.hasOwnProperty.call(TONES, e.kind) ? e.kind : 'issued';
        const paper = kind === 'printed' && e.paper ? String(e.paper).toLowerCase() : null;
        return {
            key: 'Ev_' + kind,
            version: e.version,
            tone: TONES[kind],
            paperKey: paper && ['a4', 'a5', 'labels4', 'labels8'].indexOf(paper) > -1 ? 'Paper_' + paper : null,
            copies: kind === 'printed' && typeof e.copies === 'number' ? e.copies : null,
            reason: e.reason || null,
            by: e.by || null,
            at: e.at || null
        };
    };

    /** Today / yesterday on the user's clock (offsetMinutes east of UTC), else null (the page writes the date). */
    const whenKey = (iso, nowMs, offsetMinutes) => {
        const t = Date.parse(iso || '');
        if (isNaN(t)) { return null; }
        const day = ms => Math.floor((ms + (offsetMinutes || 0) * 60000) / 86400000);
        const diff = day(nowMs) - day(t);
        return diff === 0 ? 'Today' : diff === 1 ? 'Yesterday' : null;
    };

    const KNOWN_ERRORS = [
        'visit_execution_concurrency_conflict', 'visit_qr_signing_unavailable', 'visit_qr_not_active',
        'visit_qr_account_not_found', 'visit_qr_request_invalid'
    ];

    /** A refused call → its text key (never a raw code). */
    const errorKey = response => {
        const errors = response && response.body && Array.isArray(response.body.errors) ? response.body.errors : [];
        const code = errors.find(c => KNOWN_ERRORS.indexOf(c) > -1);
        if (code) { return 'Err_' + code; }
        if (response && response.status === 403) { return 'Err_permission'; }
        return 'Err_generic';
    };

    // ── Q5 ──
    const FILTERS = ['status', 'accountType', 'city', 'territoryId', 'search'];

    const listQuery = (filters, page, pageSize) => {
        const f = filters || {};
        const parts = [];
        FILTERS.forEach(k => {
            const v = trimmed(f[k]);
            if (v) { parts.push(k + '=' + encodeURIComponent(v)); }
        });
        parts.push('page=' + Math.max(1, page || 1));
        parts.push('pageSize=' + Math.min(100, Math.max(1, pageSize || 25)));
        return parts.join('&');
    };

    const hasFilters = filters => FILTERS.some(k => !!trimmed((filters || {})[k]));

    /** A list item → the row: the type / city in the user's language (the lookups), the chip, "parent code applies". */
    const listRow = (item, lookups) => {
        const i = item || {};
        const l = lookups || {};
        const status = statusOf(i);
        return {
            id: i.accountId,
            name: i.accountName || '—',
            parent: i.parentAccountName || null,
            parentId: i.parentAccountId || null,
            typeLabel: (l.types && l.types[i.accountType]) || i.accountType || '—',
            cityLabel: (l.cities && i.cityRef && l.cities[i.cityRef]) || i.cityRef || '—',
            status: status,
            chipKey: 'Chip_' + status,
            inherits: status !== 'active' && !!i.inheritedFrom,
            version: i.version == null ? null : i.version,
            lastScannedAt: i.lastScannedAt || null,
            recent: status === 'active' || (typeof i.recentScanCount === 'number' && i.recentScanCount > 0) ? (i.recentScanCount || 0) : null,
            printable: status === 'active'
        };
    };

    /** Toggle one id; a full selection (200) takes no more. Returns whether it is selected now. */
    const select = (set, id, on) => {
        if (!id) { return false; }
        if (on && !set.has(id)) {
            if (set.size >= MAX_BULK) { return false; }
            set.add(id);
        } else if (!on) {
            set.delete(id);
        }
        return set.has(id);
    };

    /** The page's header checkbox: all of the page (up to the cap) or none of it. */
    const toggleAll = (set, ids, on) => { (ids || []).forEach(id => select(set, id, on)); return set; };

    const bulkResultArgs = dto => [(dto && dto.issued) || 0, (dto && dto.skippedActive) || 0];

    // ── Q2 ──
    const SINGLE_PAPERS = ['a4', 'a5'];
    const LABEL_PAPERS = ['labels4', 'labels8'];

    /** The label sheet: 2 columns; 4 → 2 rows, 8 → 4 rows. */
    const labelGrid = paper => ({ columns: 2, rows: paper === 'labels4' ? 2 : 4, perPage: paper === 'labels4' ? 4 : 8 });

    const printUrl = (base, accountId) => (base || '/CRM/VisitQrCodes') + '/Print?accountId=' + encodeURIComponent(accountId || '');

    const labelsUrl = (base, ids) => (base || '/CRM/VisitQrCodes') + '/Print?mode=labels&ids=' + encodeURIComponent((ids || []).slice(0, MAX_BULK).join(','));

    const copiesOf = value => {
        const n = Math.floor(Number(value));
        return isFinite(n) ? Math.min(50, Math.max(1, n)) : 1;
    };

    return {
        MAX_REASON, MAX_BULK, STATUSES, cardModel, reasonState, issueBody, revokeBody, toastKey, eventLine, whenKey,
        KNOWN_ERRORS, errorKey, FILTERS, listQuery, hasFilters, listRow, select, toggleAll, bulkResultArgs,
        SINGLE_PAPERS, LABEL_PAPERS, labelGrid, printUrl, labelsUrl, copiesOf
    };
}));
