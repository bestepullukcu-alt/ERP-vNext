/**
 * MOD-0173-FU01 S-3 receipt (UI-2) — display only.
 *
 * The receipt is server-rendered; this script does the two things only the browser can: show the contract's UTC
 * instants in the reader's own time zone (pack §8) and format the decimal STRINGS with the reader's number format
 * (Intl.NumberFormat on the string — never a float, UI-13). Nothing is fetched, nothing is posted.
 */
'use strict';

(function () {
    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
    const dateFormat = new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeStyle: 'short' });

    document.querySelectorAll('#movement-receipt time[data-utc]').forEach((el) => {
        const value = el.getAttribute('data-utc');
        const date = value ? new Date(value) : null;
        if (date && !Number.isNaN(date.getTime())) {
            el.setAttribute('datetime', value);
            el.textContent = dateFormat.format(date);
        }
    });

    document.querySelectorAll('#movement-receipt [data-quantity]').forEach((el) => {
        const value = el.getAttribute('data-quantity');
        if (!value) return;
        try { el.textContent = numberFormat.format(value); } catch (error) { /* keep the server text */ }
    });
})();
