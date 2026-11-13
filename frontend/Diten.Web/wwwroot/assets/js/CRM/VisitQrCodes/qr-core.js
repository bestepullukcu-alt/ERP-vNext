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
 *   listRow(item, lookups)           → a Q5 row model (W3E-FIX: the Golden Compact list's renderers read it; paging, filters
 *                                        and the selection are the shared list factory's now)
 *   personName(name) / cityLabel(code, cities)   W3E-FIX G-4 (a name, never an id) / G-3 (the city in words)
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

    /**
     * W3E-FIX (G-4) — the person to show: the NAME the CRM resolved (issuedByName / revokedByName / byName; this tenant's
     * users only), never the id field next to it, and never anything shaped like an id. No name → null (the page writes "—").
     */
    const GUID = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
    const personName = name => {
        const t = String(name == null ? '' : name).trim();
        return t && !GUID.test(t) ? t : null;
    };

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
            issuedBy: personName(s.issuedByName),
            issuedAt: s.issuedAt || null,
            lastScannedAt: s.lastScannedAt || null,
            recent: typeof s.recentScanCount === 'number' ? s.recentScanCount : 0,
            revokedBy: personName(s.revokedByName),
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
            by: personName(e.byName),
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
    /**
     * W3E-FIX (G-3) — the city in words (the twin of Web's CrmCityLabels): the city set's label for the code; for a
     * "CC-NN-NAME" code (the account's TR-42-KONYA) the set's label of NAME (konya → "Konya", istanbul → "İstanbul"); else
     * a readable name from NAME (Turkish letters cased the Turkish way: İSTANBUL → "İstanbul"); any other shape as it is.
     */
    const TR_LETTERS = /[İıĞğŞşÇçÖöÜü]/;
    const fold = s => String(s).toLocaleLowerCase('tr').replace(/ı/g, 'i').replace(/ğ/g, 'g').replace(/ş/g, 's').replace(/ç/g, 'c').replace(/ö/g, 'o').replace(/ü/g, 'u');
    const lookup = (map, key) => {
        if (!map || key == null) { return null; }
        if (Object.prototype.hasOwnProperty.call(map, key) && map[key]) { return map[key]; }
        const k = String(key).toLowerCase();
        const hit = Object.keys(map).find(x => x.toLowerCase() === k);
        return hit && map[hit] ? map[hit] : null;
    };
    const readable = name => {
        const tr = TR_LETTERS.test(name);
        return String(name).split(/[-_\s]+/).filter(Boolean).map(w => {
            const lower = tr ? w.toLocaleLowerCase('tr') : w.toLowerCase();
            return (tr ? lower.charAt(0).toLocaleUpperCase('tr') : lower.charAt(0).toUpperCase()) + lower.slice(1);
        }).join(' ');
    };
    const cityLabel = (code, cities) => {
        const c = String(code == null ? '' : code).trim();
        if (!c) { return null; }
        const direct = lookup(cities, c);
        if (direct) { return direct; }
        const m = /^[A-Za-z]{2}-\d{1,3}-(.+)$/.exec(c);
        if (!m) { return c; }
        return lookup(cities, m[1]) || lookup(cities, fold(m[1])) || lookup(cities, fold(m[1]).replace(/-/g, '')) || readable(m[1]);
    };

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
            // G-2: the BRD account-type label in the reader's language (the set's code, any case); none → the code
            typeLabel: lookup(l.types, i.accountType) || i.accountType || '—',
            cityLabel: cityLabel(i.cityRef, l.cities) || '—',
            status: status,
            chipKey: 'Chip_' + status,
            inherits: status !== 'active' && !!i.inheritedFrom,
            version: i.version == null ? null : i.version,
            lastScannedAt: i.lastScannedAt || null,
            recent: status === 'active' || (typeof i.recentScanCount === 'number' && i.recentScanCount > 0) ? (i.recentScanCount || 0) : null,
            printable: status === 'active'
        };
    };

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
        KNOWN_ERRORS, errorKey, listRow, bulkResultArgs, personName, cityLabel,
        SINGLE_PAPERS, LABEL_PAPERS, labelGrid, printUrl, labelsUrl, copiesOf
    };
}));
