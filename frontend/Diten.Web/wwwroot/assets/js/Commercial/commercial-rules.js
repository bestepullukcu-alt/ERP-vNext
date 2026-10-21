/**
 * WP-VW-W6 (W6-WEB-a) — the shared commercial rule messages: the ONE place a MOD-0156 finding or a MOD-0168 refusal becomes
 * words. The server sends a CODE + parameters (never a sentence, AC-PRICE-4); _CommercialRulesL10n.cshtml writes every
 * `Rule.{code}` of the shared resource (7 languages) as JSON, this merges it into window.L10n and words it:
 *
 *   text(finding, opts)  → "Yönetici onayı gerekir: iskonto %12 > sınır %10"   (plain text; data isolated FSI…PDI)
 *   html(finding, opts)  → the same, escaped, each parameter in <bdi>         (RTL)
 *   outcome(o)           → "Sınır içi" | "Onay gerekir" | "Girilemez"         (Rule.Outcome.{ok|soft|hard})
 *   error(code)          → the refusal's words, or null when the code has none (the caller then says its own generic text)
 *   number(v)            → an invariant number ("12.5") in the page's culture ("12,5"); anything else ("10+2") as it is
 *
 * `opts.skuName(id)` names a product for {skuId}; the culture is <html lang> (the application's language). Used by the
 * pre-order screens (W6-WEB-a) and the price list / rule screens (W6-WEB-b). Node-testable (module.exports).
 */
(function (root) {
    'use strict';

    const PREFIX = 'Rule.';
    const FSI = String.fromCharCode(0x2068);
    const PDI = String.fromCharCode(0x2069);
    const NUMBER = /^-?\d+(\.\d+)?$/;

    const doc = () => (typeof document !== 'undefined' ? document : null);
    const culture = () => {
        const d = doc();
        return ((d && d.documentElement && d.documentElement.lang) || '').trim() || 'en';
    };
    const words = () => (root.L10n || {});
    const esc = (s) => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    /** An invariant number in the page's culture, one decimal at most (the percentages are 0,1-rounded, D7). */
    const number = (value, cultureName) => {
        const s = String(value == null ? '' : value).trim();
        if (!NUMBER.test(s)) return s;
        try { return new Intl.NumberFormat(cultureName || culture(), { maximumFractionDigits: 1 }).format(Number(s)); } catch (e) { return s; }
    };

    const paramText = (name, value, opts) => {
        if (name === 'skuId' && opts && typeof opts.skuName === 'function') {
            const label = opts.skuName(value);
            if (label) return String(label);
        }
        return number(value, opts && opts.culture);
    };

    /** The template of a code, or '' when the resource has none. */
    const template = (code) => {
        const t = words()[PREFIX + String(code || '')];
        return typeof t === 'string' ? t : '';
    };

    const fill = (code, params, opts, wrap) => {
        const t = template(code);
        if (!t) return '';
        const p = params || {};
        return t.split(/(\{[A-Za-z]+\})/).map((part) => {
            const m = /^\{([A-Za-z]+)\}$/.exec(part);
            if (!m) return wrap ? esc(part) : part;
            const v = paramText(m[1], p[m[1]], opts);
            return wrap ? '<bdi>' + esc(v) + '</bdi>' : FSI + v + PDI;
        }).join('');
    };

    const api = {
        PREFIX: PREFIX,
        OUTCOMES: ['ok', 'soft', 'hard'],
        number: number,
        has: (code) => !!template(code),
        text: (finding, opts) => fill(finding && finding.code, finding && finding.params, opts, false),
        html: (finding, opts) => fill(finding && finding.code, finding && finding.params, opts, true),
        outcome: (o) => words()[PREFIX + 'Outcome.' + String(o || '')] || '',
        error: (code) => template(code) || null,
        /** The worst of a list of outcomes (hard > soft > ok). */
        worst: (outcomes) => (outcomes || []).reduce((w, o) => (o === 'hard' || w === 'hard' ? 'hard' : (o === 'soft' || w === 'soft' ? 'soft' : 'ok')), 'ok'),
        /** Merges a payload object (key → text) into window.L10n. */
        merge: (payload) => { root.L10n = Object.assign({}, root.L10n || {}, payload || {}); }
    };

    const d = doc();
    const node = d && d.getElementById ? d.getElementById('commercial-rules-l10n') : null;
    if (node) {
        try { api.merge(JSON.parse(node.textContent || '{}')); } catch (e) { console.error('[CommercialRules] The rule message payload could not be parsed.', e); }
    }

    root.CommercialRules = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
