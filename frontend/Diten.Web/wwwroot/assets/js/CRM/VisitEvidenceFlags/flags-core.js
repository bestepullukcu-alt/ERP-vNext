/**
 * WP-VW-W3E (W3E-WEB-b) — the manager's visit evidence flags page (mockup "W3E Ek Kanitlar" M1–M6): its PURE rules — no
 * DOM, no fetch, no clock (the page passes "today"). Loaded before visit-evidence-flags.js; the Web tests run these
 * functions in Node. Texts are KEYS (the page resolves them through window.VisitEvidenceFlagsL10n), never words.
 *
 *   FLAGS / CATS                 the CRM flag codes (VisitExecutionFlags) → their label key, category (location · QR ·
 *                                device · duration · exception: the chip's tone + icon) and the value a chip carries
 *   chipsOf(row)                 a row's chips in a stable order + "QR not scanned" when qr.status = missing — that one is
 *                                NOT a flag: it is never offered by the flag filter and never sent as one (D-6)
 *   PERIODS / rangeOf(key, today, custom)   last 7 days (the baseline "") · last 30 days · this month · a custom range
 *                                (≤ 92 days) — the twin of the Web proxy's ResolveRange (the proxy computes the CRM window)
 *   rangeError(from, to) / customRangeError(period, from, to)   a custom range: from ≤ to, both present, at most 92 days
 *                                (both ends included); a preset never has an error — a bad range is never sent (W3E-FLAGS-STD)
 *   summaryQuery(applied)        the summary read of the list's APPLIED filters (period, custom from / to, rep) — the card
 *                                and the rep summary follow the one filter state of the list
 *   summaryBars(byFlag, max)     the flag-type bars: the top `max`, the rest summed as "Other (N types)" (D-11)
 *   duration(seconds)            "18 dk" / "1 dk 40 sn" / "40 sn" as { key, args }
 *   distance(meters)             "820 m" / "198,3 km" (≥ 1000 m, one decimal) as { key, args, digits } (W3E-FIX G-8)
 *   accountOptions(entries) / sameNameIds(entries)   the institution filter: same names told apart by city / code (G-7)
 *   qrResult(qr, kind, exception)   the drawer's QR result key; device(row)  the platform / app / verdict keys
 *   repRate(started, flagged)    the rate in tenths
 *   errorKey(response)           a CRM refusal → the user's text key (never a raw code)
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitEvidenceFlagsCore = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    /** Chip categories (the mockup's CAT): background / text tone + icon. */
    const CATS = {
        loc: { tone: 'info', icon: 'bx-map-pin' },
        qr: { tone: 'primary', icon: 'bx-qr' },
        dev: { tone: 'warning', icon: 'bx-mobile-alt' },
        dur: { tone: 'secondary', icon: 'bx-time-five' },
        exc: { tone: 'secondary', icon: 'bx-shield-quarter' }
    };

    /**
     * The CRM flags (VisitExecutionFlags.All) in the mockup's order. `value` names the row field a chip shows after its
     * label ("Uzak konum · 820 m"): the distance, the accuracy, the visit duration.
     */
    const FLAGS = [
        // the chip order of the mockup's rows: device → QR → location → duration → exception ("QR not scanned" last)
        { code: 'device_unverified', cat: 'dev' },
        { code: 'mock_location', cat: 'dev' },
        { code: 'device_integrity_failed', cat: 'dev' },
        { code: 'qr_not_issued', cat: 'qr' },
        { code: 'qr_wrong_account', cat: 'qr' },
        { code: 'qr_revoked', cat: 'qr' },
        { code: 'qr_invalid', cat: 'qr' },
        { code: 'qr_unverifiable', cat: 'qr' },
        { code: 'location_far', cat: 'loc', value: 'distance' },
        { code: 'location_missing', cat: 'loc' },
        { code: 'location_inaccurate', cat: 'loc', value: 'accuracy' },
        { code: 'account_location_unknown', cat: 'loc' },
        { code: 'short_visit', cat: 'dur', value: 'duration' },
        { code: 'started_with_exception', cat: 'exc' }
    ];
    const FLAG_CODES = FLAGS.map(f => f.code);
    const flagOf = code => FLAGS.find(f => f.code === code) || null;

    /** The chip that is NOT a flag (D-6): the QR code was not scanned (qr.status = missing). */
    const NOT_SCANNED = 'qr_missing';

    const duration = seconds => {
        const s = Math.max(0, Math.round(Number(seconds) || 0));
        const m = Math.floor(s / 60), r = s % 60;
        if (m === 0) { return { key: 'Seconds', args: [r] }; }
        return r === 0 || m >= 10 ? { key: 'Minutes', args: [m >= 10 ? Math.round(s / 60) : m] } : { key: 'MinSec', args: [m, r] };
    };

    /**
     * W3E-FIX (G-8) — a distance: under 1000 m in whole metres ("820 m"), from 1000 m in kilometres with ONE decimal
     * ("198,3 km" — the page writes the number in the user's language, so the decimal separator is theirs).
     * → { key, args, digits } (digits = the fraction digits the page formats args[0] with).
     */
    const distance = meters => {
        const m = Math.max(0, Number(meters) || 0);
        return m < 1000 ? { key: 'UnitMeters', args: [Math.round(m)], digits: 0 } : { key: 'UnitKm', args: [Math.round(m / 100) / 10], digits: 1 };
    };

    /** The exact form (the drawer's slide time, the mockup "11 dk 50 sn"): minutes + seconds, never rounded. */
    const durationExact = seconds => {
        const s = Math.max(0, Math.round(Number(seconds) || 0));
        const m = Math.floor(s / 60), r = s % 60;
        return m === 0 ? { key: 'Seconds', args: [r] } : r === 0 ? { key: 'Minutes', args: [m] } : { key: 'MinSec', args: [m, r] };
    };

    /**
     * A row's chips: its flags in FLAGS order (an unknown future code keeps its code as the key, so it is still shown),
     * then "QR not scanned" when the start's QR verdict is missing. → [{ code, key, cat, value: { key, args } | null, flag }]
     */
    const chipsOf = row => {
        const r = row || {};
        const flags = (r.flags || []).slice().sort((a, b) => {
            const ia = FLAG_CODES.indexOf(a), ib = FLAG_CODES.indexOf(b);
            return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
        });
        const out = flags.map(code => {
            const f = flagOf(code);
            const loc = r.location || {};
            let value = null;
            if (f && f.value === 'distance' && loc.distanceMeters != null) { value = distance(loc.distanceMeters); }
            if (f && f.value === 'accuracy' && loc.accuracyMeters != null) { value = distance(loc.accuracyMeters); }
            if (f && f.value === 'duration' && r.durationSeconds != null) { value = duration(r.durationSeconds); }
            return { code: code, key: 'Flag_' + code, cat: f ? f.cat : 'exc', value: value, flag: true };
        });
        if (r.qr && r.qr.status === 'missing') { out.push({ code: NOT_SCANNED, key: 'Chip_qr_missing', cat: 'qr', value: null, flag: false }); }
        return out;
    };

    // ── the period ───────────────────────────────────────────────────────────────────────────────────────────
    const MAX_RANGE_DAYS = 92;
    /** The period filter's values: "" (the baseline) is the last 7 days. */
    const PERIODS = ['', 'last30', 'month', 'custom'];

    const dayNo = iso => {
        const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(iso || ''));
        return m ? Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])) / 864e5 : null;
    };
    const isoOf = n => new Date(n * 864e5).toISOString().slice(0, 10);

    /** From / to (UTC days, both included) of a preset; a custom range as given. */
    const rangeOf = (key, today, custom) => {
        const t = dayNo(today);
        if (key === 'last30') { return { from: isoOf(t - 29), to: today }; }
        if (key === 'month') { return { from: String(today).slice(0, 8) + '01', to: today }; }
        if (key === 'custom') { return { from: (custom && custom.from) || '', to: (custom && custom.to) || '' }; }
        return { from: isoOf(t - 6), to: today };   // "" / last7
    };

    /** A custom range's error key, or null (the CRM refuses > 92 days with visit_evidence_flags_range_invalid). */
    const rangeError = (from, to) => {
        const a = dayNo(from), b = dayNo(to);
        if (a == null || b == null) { return 'ErrRangeMissing'; }
        if (b < a) { return 'ErrRangeOrder'; }
        return b - a + 1 > MAX_RANGE_DAYS ? 'ErrRangeLong' : null;
    };

    /** The staged / applied period's error key, or null: only a custom range can be wrong (W3E-FLAGS-STD — never sent). */
    const customRangeError = (period, from, to) => (period === 'custom' ? rangeError(from, to) : null);

    const qs = pairs => pairs.filter(p => p[1] !== null && p[1] !== undefined && p[1] !== '')
        .map(p => encodeURIComponent(p[0]) + '=' + encodeURIComponent(p[1])).join('&');

    /**
     * The summary read of the list's APPLIED filters (the factory's filter keys): the period, the custom from / to (only
     * for "custom"), the rep. Never a page size (the rep summary is a bounded set, client mode) and never a flag.
     */
    const summaryQuery = applied => {
        const a = applied || {};
        const custom = a.period === 'custom';
        return '?' + qs([['period', a.period], ['from', custom ? a.from : ''], ['to', custom ? a.to : ''], ['resourceId', a.resourceId]]);
    };

    /** The flag-type bars: [{ code, key, count, pct }] of the top `max` + { count, types } of the rest ("Other"). */
    const summaryBars = (byFlag, max) => {
        const entries = Object.keys(byFlag || {}).map(code => ({ code: code, count: Number(byFlag[code]) || 0 }))
            .filter(e => e.count > 0)
            .sort((a, b) => b.count - a.count || FLAG_CODES.indexOf(a.code) - FLAG_CODES.indexOf(b.code));
        const top = entries.slice(0, max || 6);
        const rest = entries.slice(max || 6);
        const peak = entries.length ? entries[0].count : 0;
        const other = rest.length ? { count: rest.reduce((n, e) => n + e.count, 0), types: rest.length } : null;
        const scale = Math.max(peak, other ? other.count : 0) || 1;
        return {
            bars: top.map(e => ({ code: e.code, key: 'Flag_' + e.code, count: e.count, pct: Math.round(e.count * 100 / scale) })),
            other: other ? Object.assign(other, { pct: Math.round(other.count * 100 / scale) }) : null
        };
    };

    /** The rate in percent with one decimal (9.0), 0 without starts. */
    const repRate = (started, flagged) => (started > 0 ? Math.round(flagged * 1000 / started) / 10 : 0);

    // ── the drawer ───────────────────────────────────────────────────────────────────────────────────────────
    /** The QR result's label key: not asked (off / remote), each verdict, "not scanned · exception". */
    const qrResult = (qr, kind, exception) => {
        if (kind === 'remote') { return 'QrSt_remote'; }
        if (!qr) { return 'QrSt_not_applicable'; }
        if (qr.status === 'missing' && exception) { return 'QrSt_missing_exception'; }
        return 'QrSt_' + qr.status;
    };

    /** The device lines: { platformKey, appVersion | appKey, verdictKey } (D-2: web → the browser summary in appVersion). */
    const device = row => {
        const r = row || {};
        const d = r.device;
        if (r.kind === 'remote') { return { platformKey: d && d.platform ? 'Plat_' + d.platform : null, platformExtra: d && d.platform === 'web' ? d.appVersion : null, appKey: d && d.platform === 'web' ? 'AppWeb' : null, appVersion: d && d.platform !== 'web' ? d.appVersion : null, verdictKey: 'Verdict_remote' }; }
        if (!d || !d.platform) { return { platformKey: null, platformExtra: null, appKey: null, appVersion: null, verdictKey: 'Verdict_none' }; }
        const web = d.platform === 'web';
        let verdictKey = 'Verdict_unavailable';
        if (d.mockLocation) { verdictKey = 'Verdict_mock'; }
        else if (d.integrityVerdict === 'failed') { verdictKey = 'Verdict_failed'; }
        else if (d.integrityVerdict === 'passed') { verdictKey = 'Verdict_passed'; }
        else if (web) { verdictKey = 'Verdict_web'; }
        return { platformKey: 'Plat_' + d.platform, platformExtra: web ? d.appVersion || null : null, appKey: web ? 'AppWeb' : null, appVersion: web ? null : d.appVersion || null, verdictKey: verdictKey };
    };

    /**
     * W3E-FIX (G-7) — the institution filter's options: [{ id, name, city, code }] → [{ id, text }] sorted by name. Two
     * institutions with the same name ("100.YIL ECZANESİ" × 3) are told apart by their city; when the city does not tell
     * them apart either (or is unknown), by the institution code. A unique name stays as it is. `compare` sorts (the
     * page passes its culture's localeCompare).
     */
    const accountOptions = (entries, compare) => {
        const list = (entries || []).filter(e => e && e.id);
        const key = s => String(s == null ? '' : s).trim().toLocaleLowerCase();
        const count = (fn) => { const m = {}; list.forEach(e => { const k = fn(e); m[k] = (m[k] || 0) + 1; }); return m; };
        const byName = count(e => key(e.name));
        const byNameCity = count(e => key(e.name) + '\u0000' + key(e.city));
        const sorter = compare || ((a, b) => String(a).localeCompare(String(b)));
        return list.map(e => {
            const name = String(e.name || '—');
            if (byName[key(e.name)] < 2) { return { id: e.id, text: name }; }
            const parts = [];
            if (e.city) { parts.push(e.city); }
            if (e.code && (!e.city || byNameCity[key(e.name) + '\u0000' + key(e.city)] > 1)) { parts.push(e.code); }
            return { id: e.id, text: parts.length ? name + ' · ' + parts.join(' · ') : name, name: name, suffix: parts.join(' · ') };
        }).sort((a, b) => sorter(a.text, b.text));
    };

    /** The ids whose name another institution in the list also carries (the ones that need a city / code). */
    const sameNameIds = entries => {
        const list = (entries || []).filter(e => e && e.id);
        const key = s => String(s == null ? '' : s).trim().toLocaleLowerCase();
        const n = {}; list.forEach(e => { n[key(e.name)] = (n[key(e.name)] || 0) + 1; });
        return list.filter(e => n[key(e.name)] > 1).map(e => e.id);
    };

    const initials = name => String(name || '').split(/\s+/).filter(Boolean).map(w => w[0]).join('').slice(0, 2).toUpperCase() || '·';

    // ── refusals ─────────────────────────────────────────────────────────────────────────────────────────────
    const ERROR_CODES = ['visit_evidence_flags_range_invalid', 'visit_evidence_flags_filter_invalid', 'visit_evidence_flags_not_found', 'permission_denied'];
    const errorKey = response => {
        const errors = response && response.body && Array.isArray(response.body.errors) ? response.body.errors : [];
        const hit = errors.find(c => ERROR_CODES.indexOf(c) > -1);
        if (hit) { return 'Err_' + hit; }
        if (response && response.status === 403) { return 'Err_permission_denied'; }
        if (response && response.status === 404) { return 'Err_visit_evidence_flags_not_found'; }
        if (!response || response.status === 0 || response.status >= 500) { return 'Err_unavailable'; }
        return 'Err_generic';
    };

    return {
        CATS, FLAGS, FLAG_CODES, NOT_SCANNED, MAX_RANGE_DAYS, PERIODS, flagOf, chipsOf, duration, durationExact, distance, accountOptions, sameNameIds, rangeOf, rangeError,
        customRangeError, summaryQuery, summaryBars, repRate, qrResult, device, initials, ERROR_CODES, errorKey
    };
}));
