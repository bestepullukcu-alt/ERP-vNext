'use strict';

/*
 * DitenTour — the product's ONE step-by-step tour layer (WP-UX-COMMON-01, BL-495).
 *
 * Pages never call the tour library; they call this file. The library (Driver.js, MIT, one pinned version vendored
 * under assets/vendor/libs/driverjs/, no CDN) is touched ONLY here, so swapping or removing it is a one-file change.
 *
 *   DitenTour.register(pageCode, { version, steps: [{ target, titleKey, textKey, side? }] })
 *   DitenTour.isAvailable(pageCode) → true only when the library is loaded, the page registered a tour, the screen is
 *                                     at least `md` wide and at least one step's target is on the page
 *   DitenTour.start(pageCode)       → runs the tour; false (never an exception) when it cannot
 *   DitenTour.isCompleted(pageCode) → "seen this tour" for THIS version, from the browser (localStorage)
 *
 * `target`   the value of a `data-tour="…"` attribute on the page (never a CSS selector a page builds by hand).
 * `titleKey` / `textKey` keys in the page's tour strings (`Tour.*` in SharedResource, emitted by the help partial).
 *
 * ── TEXT IS TEXT ───────────────────────────────────────────────────────────────────────────────────────────
 * The library writes title and description with innerHTML. Every string handed to it is HTML-escaped first, so a
 * translation that happens to contain "<" shows a "<" — it can never become markup (or a script).
 *
 * ── "SEEN" IS A CONVENIENCE, NOT A RECORD ──────────────────────────────────────────────────────────────────
 * Stored in localStorage under a key carrying the tour's version; a new version is offered again. Storage that is
 * unavailable (private mode, blocked) is not an error: the tour is simply offered every time.
 *
 * ── NO LIBRARY, NO TOUR, NO ERROR ──────────────────────────────────────────────────────────────────────────
 * Until the vendored file is on the page this layer answers "no tour" everywhere and the help panel hides its
 * "Start tour" button.
 */
(function (global) {
    const doc = global.document;
    const STORAGE_PREFIX = 'diten.tour.seen.';
    const L10N_ELEMENT_ID = 'diten-tour-l10n';
    const MIN_WIDTH = 768; // Bootstrap `md`
    const tours = new Map();

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    let strings = null;
    const readStrings = () => {
        if (strings) { return strings; }
        const node = doc && doc.getElementById(L10N_ELEMENT_ID);
        if (!node) { return {}; }
        try { strings = JSON.parse(node.textContent || '{}'); } catch (error) { strings = {}; }
        return strings;
    };
    const t = (key) => {
        const value = readStrings()[key];
        return typeof value === 'string' && value.length ? value : key;
    };

    // The library's own entry point, looked up at call time (the vendored IIFE exposes `window.driver.js.driver`).
    const library = () => {
        const ns = global.driver && global.driver.js;
        return ns && typeof ns.driver === 'function' ? ns.driver : null;
    };

    const storageKey = (pageCode, version) => `${STORAGE_PREFIX}${pageCode}.v${version}`;
    const storage = () => { try { return global.localStorage || null; } catch (error) { return null; } };

    const targetOf = (step) => doc && doc.querySelector(`[data-tour="${String(step.target).replace(/"/g, '')}"]`);

    const wideEnough = () => {
        const width = global.innerWidth || (doc && doc.documentElement && doc.documentElement.clientWidth) || 0;
        return width >= MIN_WIDTH;
    };

    const register = (pageCode, tour) => {
        if (!pageCode || !tour || !Array.isArray(tour.steps) || tour.steps.length === 0) {
            if (global.console && global.console.error) {
                global.console.error('[DitenTour] register needs a page code and at least one step.', pageCode);
            }
            return false;
        }
        tours.set(pageCode, { version: tour.version || 1, steps: tour.steps.slice() });
        return true;
    };

    const isAvailable = (pageCode) => {
        const tour = tours.get(pageCode);
        return !!(tour && library() && wideEnough() && tour.steps.some((step) => targetOf(step)));
    };

    const isCompleted = (pageCode) => {
        const tour = tours.get(pageCode);
        if (!tour) { return false; }
        const store = storage();
        try { return !!(store && store.getItem(storageKey(pageCode, tour.version)) === '1'); } catch (error) { return false; }
    };

    const markCompleted = (pageCode, version) => {
        const store = storage();
        try { if (store) { store.setItem(storageKey(pageCode, version), '1'); } } catch (error) { /* offered again next time */ }
    };

    const start = (pageCode) => {
        const tour = tours.get(pageCode);
        const createDriver = library();
        if (!tour || !createDriver || !wideEnough()) { return false; }

        // Steps whose target is not on the page (a hidden control, a role that does not see it) are skipped.
        const steps = tour.steps
            .map((step) => ({ step, element: targetOf(step) }))
            .filter((x) => x.element)
            .map(({ step, element }) => ({
                element,
                popover: {
                    title: esc(t(step.titleKey)),
                    description: esc(t(step.textKey)),
                    side: step.side || 'bottom'
                }
            }));
        if (steps.length === 0) { return false; }

        const rtl = !!(doc && doc.documentElement && doc.documentElement.getAttribute('dir') === 'rtl');
        const instance = createDriver({
            steps,
            showProgress: true,
            allowKeyboardControl: true,
            animate: true,
            nextBtnText: esc(t('Tour.Next')),
            prevBtnText: esc(t('Tour.Previous')),
            doneBtnText: esc(t('Tour.Done')),
            progressText: esc(t('Tour.Progress')),
            popoverClass: rtl ? 'diten-tour-popover diten-tour-rtl' : 'diten-tour-popover',
            onDestroyed: () => markCompleted(pageCode, tour.version)
        });
        instance.drive();
        return true;
    };

    // Test seam only: forget registrations and cached strings.
    const reset = () => { tours.clear(); strings = null; };

    global.DitenTour = { register, start, isAvailable, isCompleted, _reset: reset, _escape: esc };
})(typeof window !== 'undefined' ? window : globalThis);
