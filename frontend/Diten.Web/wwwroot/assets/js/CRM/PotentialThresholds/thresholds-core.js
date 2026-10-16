/**
 * WP-VW-W4 (W4-WEB-b) — the potential thresholds page's PURE rules: no DOM, no fetch. Loaded before
 * potential-thresholds.js; the Web tests run these functions in Node. Texts are KEYS (window.PotentialThresholdsL10n).
 * The CRM decides (DoctorInsightsRules: 0 < LowBelow ≤ HighAbove ≤ 100000; product › default › built-in); this mirror lets
 * the manager see the refusal before the save.
 *
 *   validate(low, high)            Az < X · Orta · Çok > Y — whole numbers, 0 < X ≤ Y ≤ 100000 (else an error key per field)
 *   classOf(patients, row)         low (< X) · medium · high (> Y) — the preview of a monthly patient count
 *   rowsOf(dto, names)             the default row first, then the product rows by product name
 *   diff(before, low, high)        the save confirm's changed rows (field, old, new); none = nothing to save
 *   putBody(low, high, version)    the PUT body
 *   historyRows(items, names)      the change history (newest first, the product named, "default" for the tenant row)
 *   isolateRange(text)             W3E-E4R2-FIX-d (8) — every "a–b" number range kept left-to-right as ONE isolate (LRI…PDI;
 *                                  plain text, so it works in a localized template): in Arabic "5–20" never reads "20–5";
 *                                  the digits stay the culture's (Latin or Arabic-Indic)
 *   errorKey(response)             a CRM refusal → the user's text key
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.PotentialThresholdsCore = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    const MAX = 100000;
    const DEFAULT = 'default';

    const intOf = v => {
        if (v == null || String(v).trim() === '') { return null; }
        const n = Number(String(v).trim());
        return Number.isInteger(n) ? n : NaN;
    };

    /** The CRM rule, field by field: both required whole numbers, 0 < low, low ≤ high, high ≤ 100000. */
    const validate = (low, high) => {
        const l = intOf(low);
        const h = intOf(high);
        const e = {};
        if (l === null) { e.low = 'ErrRequired'; } else if (Number.isNaN(l) || l < 1 || l > MAX) { e.low = 'ErrRange'; }
        if (h === null) { e.high = 'ErrRequired'; } else if (Number.isNaN(h) || h < 1 || h > MAX) { e.high = 'ErrRange'; }
        if (!e.low && !e.high && l > h) { e.high = 'ErrOrder'; }
        return { ok: !e.low && !e.high, errors: e, low: l, high: h };
    };

    const classOf = (patients, row) => {
        const n = Number(patients);
        if (!row || !Number.isFinite(n)) { return null; }
        return n < row.lowBelow ? 'low' : n > row.highAbove ? 'high' : 'medium';
    };

    const keyOf = productId => (productId ? String(productId) : DEFAULT);

    const rowsOf = (dto, names) => {
        const n = names || {};
        const out = [];
        if (dto && dto.default) { out.push(Object.assign({}, dto.default, { key: DEFAULT, productId: null, name: null })); }
        const products = (dto && Array.isArray(dto.products) ? dto.products : [])
            .map(p => Object.assign({}, p, { key: keyOf(p.productId), name: n[p.productId] || null }));
        products.sort((a, b) => String(a.name || a.productId).localeCompare(String(b.name || b.productId)));
        return out.concat(products);
    };

    const diff = (before, low, high) => {
        const rows = [];
        const b = before || {};
        if (b.lowBelow !== low) { rows.push({ field: 'lowBelow', oldValue: b.lowBelow == null ? null : b.lowBelow, newValue: low }); }
        if (b.highAbove !== high) { rows.push({ field: 'highAbove', oldValue: b.highAbove == null ? null : b.highAbove, newValue: high }); }
        // a built-in / inherited row saved unchanged still creates the product's own row (a decision worth confirming)
        if (!rows.length && b.source && b.source !== 'product' && b.key !== DEFAULT) {
            rows.push({ field: 'own', oldValue: b.source, newValue: 'product' });
        }
        return rows;
    };

    const putBody = (low, high, version) => ({ lowBelow: low, highAbove: high, expectedVersion: version == null ? null : version });

    const historyRows = (items, names) => (Array.isArray(items) ? items.slice() : [])
        .sort((a, b) => String(b.at || '').localeCompare(String(a.at || '')))
        .map(x => ({
            at: x.at, by: x.by, field: x.field, oldValue: x.oldValue, newValue: x.newValue,
            productId: x.productId || null, target: x.productId ? ((names || {})[x.productId] || null) : DEFAULT
        }));

    const ERROR_CODES = ['potential_threshold_invalid', 'version_conflict', 'doctor_insights_product_invalid', 'doctor_insights_invalid',
        'reference_data_unavailable', 'dependency_unavailable'];

    const errorKey = response => {
        const b = response && response.body;
        const codes = [];
        if (b && Array.isArray(b.errors)) { b.errors.forEach(e => codes.push(String(e))); }
        if (b && b.code) { codes.push(String(b.code)); }
        const hit = codes.find(c => ERROR_CODES.indexOf(c) > -1);
        if (hit) { return 'Err_' + hit; }
        if (response && response.status === 409) { return 'Err_version_conflict'; }
        if (response && response.status === 403) { return 'Err_permission_denied'; }
        if (!response || response.status === 0 || response.status >= 500) { return 'Err_unavailable'; }
        return 'Err_generic';
    };

    // the G-6 / VisitPlanningFormat.ratio pattern: a number range isolated left-to-right as a whole
    const LRI = String.fromCharCode(0x2066), PDI = String.fromCharCode(0x2069);
    const RANGE = /[0-9\u0660-\u0669\u06F0-\u06F9][0-9\u0660-\u0669\u06F0-\u06F9.,\u066B\u066C\u00A0\u202F]*\s*–\s*[0-9\u0660-\u0669\u06F0-\u06F9][0-9\u0660-\u0669\u06F0-\u06F9.,\u066B\u066C\u00A0\u202F]*/g;
    const isolateRange = text => String(text == null ? '' : text).replace(RANGE, m => LRI + m + PDI);

    return { MAX, DEFAULT, intOf, validate, classOf, keyOf, rowsOf, diff, putBody, historyRows, ERROR_CODES, errorKey, isolateRange };
}));
