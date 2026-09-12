'use strict';

/*
 * S8 — same loader shape as Meetings/index.l10n.js, reading this screen's OWN payload
 * (id="meeting-types-l10n") into window.MeetingTypesL10n. Shared/common keys stay in window.MeetingsL10n.
 */
(function (global) {
    const store = {};
    const payload = document.getElementById('meeting-types-l10n');

    if (payload) {
        try {
            const raw = JSON.parse(payload.textContent || '{}');
            Object.keys(raw).forEach((key) => { store[key] = raw[key]; });
        } catch (error) {
            console.error('Meeting Types localization payload could not be parsed.', error);
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
            console.error(`[MeetingTypesL10n] Missing localization key '${key}'.${casingHint}`);
        }

        return key;
    };

    global.MeetingTypesL10n = { store, t, missingKeys: reported };
})(typeof window !== 'undefined' ? window : globalThis);
