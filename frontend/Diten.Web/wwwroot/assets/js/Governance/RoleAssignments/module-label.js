'use strict';

/**
 * FIX-ROLEPERMS-MODULE-LABEL — friendly module names on the Role Permissions screen.
 *
 * The permission catalog carries the RAW module slug ("work-aggregation", "product-item-sku-master",
 * "test-beta-mod"). Printing it verbatim in the group headers and the module filter left the user guessing
 * which module is which. This module resolves a slug to a DISPLAY label only; the grouping key, the filter
 * value and the permission key are untouched (identity/traceability stays on the code).
 *
 * PURE + DOM-FREE on purpose: `resolve` is the whole label decision and is unit-tested directly
 * (tests/role-assignments-module-label.test.js), so a regression that leaks a raw slug fails the build.
 *
 * `normalize` is the JS twin of NavNameLocalizer.Normalize (uppercase, non-alphanumerics dropped). The nav
 * catalog emits the same module in several spellings ("work-aggregation" / "WorkAggregation" /
 * "WORK-AGGREGATION") and the resx keys are authored in this canonical form, so a permission's `module`
 * segment and a nav module code only meet if BOTH go through this one transform.
 */
(function (global) {
    const normalize = (value) => String(value ?? '').replace(/[^a-z0-9]/gi, '').toUpperCase();

    // Fallback label derived from the CODE itself, so a module the nav has never heard of still reads as
    // words: "test-beta-mod" → "Test Beta Mod". Language-neutral by design (a product term, not a sentence).
    const humanize = (value) => String(value ?? '')
        .split(/[-._\s]+/)
        .filter(Boolean)
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');

    /**
     * code       raw module slug from the permission (any casing/separator)
     * map        { NORMALIZEDCODE: label } — see buildMap for how it is layered
     * humanizeFn code→words fallback (defaults to the humanize above; injectable so the caller can reuse its own)
     *
     * Returns a friendly label ALWAYS: a raw slug can never come out of here for a non-empty code.
     */
    const resolve = (code, map, humanizeFn) => {
        const key = normalize(code);
        if (!key) return '';
        const hit = map && map[key];
        if (typeof hit === 'string' && hit.trim().length > 0) return hit.trim();
        return (humanizeFn || humanize)(code);
    };

    /**
     * Layers the label sources in NavNameLocalizer's precedence, weakest first (later writes win):
     *   1. nav server default name   — the catalog's own English name
     *   2. resx Nav.Module.{CODE}    — the user's language (synchronous: shipped in the page's L10n bridge)
     *   3. tenant override           — free text, rendered AS-TYPED, never localized
     * so the Role Permissions label reads identically to the sidebar and Ctrl+K.
     *
     * navModules is the /TenantNavigation/api/menu payload (may be empty/absent — see the caller: the screen
     * renders before it arrives and simply upgrades afterwards).
     */
    const buildMap = (resxNames, navModules) => {
        const map = {};
        const put = (code, label) => {
            const key = normalize(code);
            const text = String(label ?? '').trim();
            if (key && text) map[key] = text;
        };

        (Array.isArray(navModules) ? navModules : []).forEach((m) => {
            if (m && !m.moduleDisplayNameIsOverride) put(m.moduleCode, m.moduleDisplayName);
        });
        Object.keys(resxNames || {}).forEach((code) => put(code, resxNames[code]));
        (Array.isArray(navModules) ? navModules : []).forEach((m) => {
            if (m && m.moduleDisplayNameIsOverride) put(m.moduleCode, m.moduleDisplayName);
        });

        return map;
    };

    global.ModuleLabel = { normalize, humanize, resolve, buildMap };
})(typeof window !== 'undefined' ? window : globalThis);
