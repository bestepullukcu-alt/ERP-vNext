'use strict';

/*
 * MOD-0357 S3 — localization bridge, mirroring Tasks/index.l10n.js exactly. Reads the server-rendered
 * #meetings-l10n payload so every string in the Meetings JS comes from the 7-language resx; a missing key
 * falls back to the key itself so the gap stays visible rather than silent.
 */
(function (global) {
    const store = {};
    const payload = document.getElementById('meetings-l10n');

    if (payload) {
        try {
            const raw = JSON.parse(payload.textContent || '{}');
            Object.keys(raw).forEach((key) => { store[key] = raw[key]; });
        } catch (error) {
            console.error('Meetings localization payload could not be parsed.', error);
        }
    }

    const reported = new Set();
    const t = (key) => {
        if (Object.prototype.hasOwnProperty.call(store, key)) { return store[key]; }

        if (!reported.has(key)) {
            reported.add(key);
            const casingHint = /^[A-Z]/.test(key)
                ? ` Payload keys are camelCase — did you mean '${key[0].toLowerCase()}${key.slice(1)}'?`
                : '';
            console.error(`[MeetingsL10n] Missing localization key '${key}'.${casingHint}`);
        }

        return key;
    };

    global.MeetingsL10n = { store, t, missingKeys: reported };
})(typeof window !== 'undefined' ? window : globalThis);
