'use strict';

/*
 * MOD-0024 — localization bridge. Reads the server-rendered #tasks-l10n payload so every string in the Tasks
 * JS comes from the 7-language resx; a missing key falls back to the key itself so the gap stays visible.
 */
(function (global) {
    const store = {};
    const payload = document.getElementById('tasks-l10n');

    if (payload) {
        try {
            const raw = JSON.parse(payload.textContent || '{}');
            Object.keys(raw).forEach((key) => { store[key] = raw[key]; });
        } catch (error) {
            console.error('Tasks localization payload could not be parsed.', error);
        }
    }

    /*
     * A miss returns the key so the page still renders, but it is NOT silent: two bugs (a genuinely absent key and
     * a PascalCase/camelCase mismatch) both reached the screen as raw key text precisely because the fallback said
     * nothing. Payload keys are camelCase — Json.Serialize applies MVC's camelCase policy to the C# property
     * names — so `t('ErrorOccurred')` can never resolve; `t('errorOccurred')` is the correct form.
     */
    const reported = new Set();
    const t = (key) => {
        if (Object.prototype.hasOwnProperty.call(store, key)) { return store[key]; }

        if (!reported.has(key)) {
            reported.add(key);   // once per key, so a re-rendering list cannot flood the console
            const casingHint = /^[A-Z]/.test(key)
                ? ` Payload keys are camelCase — did you mean '${key[0].toLowerCase()}${key.slice(1)}'?`
                : '';
            console.error(`[TasksL10n] Missing localization key '${key}'.${casingHint}`);
        }

        return key;
    };

    global.TasksL10n = { store, t, missingKeys: reported };
})(typeof window !== 'undefined' ? window : globalThis);
