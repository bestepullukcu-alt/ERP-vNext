'use strict';

// WP-MVP1-FIX-LIVE3 — the list component's shared words for the MVP-1 list pages (Views/Inventory/_MvpOneListL10n.cshtml, SharedResource,
// 7 languages) into window.L10n, which dt-defaults.js / diten-datatable.js read. Only a key the page has NOT set (absent or empty) is
// filled: a page's own sentence always wins. A missing or empty value is reported, never replaced by hardcoded text.
const MvpOneListL10n = (function () {
    const fill = (target, words) => {
        const out = Object.assign({}, target || {});
        Object.keys(words || {}).forEach((key) => {
            if (!words[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            if (!out[key]) out[key] = words[key];
        });
        return out;
    };

    const apply = (root) => {
        const payload = root.document.getElementById('mvp1-list-l10n');
        if (!payload) return;
        try {
            root.L10n = fill(root.L10n, JSON.parse(payload.textContent || '{}'));
        } catch (error) {
            console.error('[MVP-1 lists] Localization payload could not be parsed.', error);
        }
    };

    return { fill, apply };
})();

if (typeof window !== 'undefined') {
    window.MvpOneListL10n = MvpOneListL10n;
    MvpOneListL10n.apply(window);
}
