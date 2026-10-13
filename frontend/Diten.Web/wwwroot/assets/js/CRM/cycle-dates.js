/**
 * WP-CYC-UI-FIX-2 — the ONE date formatter of the Cycle Periods and Cycle Capacity screens.
 *
 * Both modules showed the same day two ways ("1.10.2026" from a Razor "d" format, "01 Eki 2026" from the browser).
 * Every day on these pages now goes through day(): day + short month name + year, in the READER's language (the
 * <html lang> the shell renders). A period day is a stored UTC-midnight calendar DAY, so it is formatted in UTC — a
 * browser west of Greenwich would otherwise show the day before. stamp() is a real instant (updated / activated /
 * closed at) and follows the reader's clock.
 *
 * Server-rendered pages write <span data-day="yyyy-MM-dd">yyyy-MM-dd</span> (or data-stamp="…"); formatAll() turns
 * them into the reader's format on load, so a page that never runs script still shows an unambiguous ISO day.
 */
(function (window, document) {
    'use strict';

    const locale = (document.documentElement.getAttribute('lang') || '').trim() || undefined;
    const DAY = { year: 'numeric', month: 'short', day: '2-digit', timeZone: 'UTC' };
    const STAMP = { year: 'numeric', month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' };

    const safe = fn => { try { return fn(); } catch (e) { return null; } };
    const toDate = v => {
        if (!v) return null;
        // "yyyy-MM-dd" is a calendar day: read it as UTC midnight so it never drifts by the reader's offset.
        const d = /^\d{4}-\d{2}-\d{2}$/.test(String(v)) ? new Date(`${v}T00:00:00Z`) : new Date(v);
        return Number.isNaN(d.getTime()) ? null : d;
    };
    const day = v => { const d = toDate(v); return d ? (safe(() => d.toLocaleDateString(locale, DAY)) || d.toISOString().slice(0, 10)) : '—'; };
    const stamp = v => { const d = toDate(v); return d ? (safe(() => d.toLocaleString(locale, STAMP)) || d.toISOString()) : '—'; };

    const formatAll = root => {
        (root || document).querySelectorAll('[data-day]').forEach(el => { el.textContent = day(el.dataset.day); });
        (root || document).querySelectorAll('[data-stamp]').forEach(el => { el.textContent = stamp(el.dataset.stamp); });
    };

    window.CycleDates = { locale, toDate, day, stamp, formatAll };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => formatAll());
    } else {
        formatAll();
    }
})(window, document);
