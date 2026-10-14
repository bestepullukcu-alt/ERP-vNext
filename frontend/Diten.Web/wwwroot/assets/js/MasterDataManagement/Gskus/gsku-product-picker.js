/**
 * WP-VW-W4 (W4-FIX-d, G-1) — the GSKU form's "Global Product" picker, PURE (no DOM): the create-options endpoint is read
 * page by page with the typed text, so every product (177+, e.g. KONSET) can be found — not only the first 20.
 *   PAGE_SIZE               the MDM create-options maximum (50)
 *   url(endpoint, term, page)   the same-origin proxy URL (search trimmed, page ≥ 1)
 *   results(data, page)     Select2 { results: [{ id, text }], pagination: { more } } — "more" while a page comes back full
 * Loaded before index.js; the Web tests run it in Node.
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.GskuProductPicker = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    const PAGE_SIZE = 50;
    const MAX_SEARCH = 100;

    const pick = (source, camel, pascal) => (source ? (source[camel] !== undefined ? source[camel] : source[pascal]) : undefined);

    const url = (endpoint, term, page) => {
        const search = String(term == null ? '' : term).trim().slice(0, MAX_SEARCH);
        const p = Number.isInteger(page) && page > 0 ? page : 1;
        return endpoint + '/create-options?pageNumber=' + p + '&pageSize=' + PAGE_SIZE
            + (search ? '&search=' + encodeURIComponent(search) : '');
    };

    const results = (data, page) => {
        const products = pick(data, 'globalProducts', 'GlobalProducts') || [];
        return {
            results: products.map(item => ({
                id: pick(item, 'id', 'Id'),
                text: pick(item, 'canonicalCode', 'CanonicalCode') + ' — ' + pick(item, 'globalProductName', 'GlobalProductName')
            })),
            pagination: { more: products.length === PAGE_SIZE }
        };
    };

    return { PAGE_SIZE, MAX_SEARCH, url, results };
}));
