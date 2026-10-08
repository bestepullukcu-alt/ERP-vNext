/**
 * WP-VP-4H (0) — the ONE date / number formatter of Visit Planning, in the APPLICATION's language: the layout writes the
 * current UI culture into <html lang> (TenantShell), so a Turkish user with an English browser reads "Pzt 5 Eki", never
 * "Mon Oct 5". No Visit Planning script formats in the browser's language (a toLocale call without a culture) or in a fixed US English.
 *
 *   culture()      → <html lang> (fallback 'tr')
 *   dayShort(d)    → "Pzt"                 dayMonth(d) → "5 Eki"          dayLabel(d) → "Pzt 5 Eki"
 *   range(a, b)    → "5–9 Eki" · "28 Eyl–2 Eki" (a month change repeats the month); workRange(a, b) → Mon–Fri of it
 *   hours(min, t)  → "6,5 sa" (the decimal separator of the language; t = the "{0} h" template)
 *   number(n)      → grouped / decimal by the language
 *   dateShort(d)   → "5 Eki 26"            dateTime(d) → "5 Eki 2026 14:30"   weekdayLong(d) → "Pazartesi"
 *   productLabel(p) → productName ?? productCode
 *   bidi(text)     → "<bdi>018 KLİNİK</bdi>" (escaped) — every data name in the page goes through it (RTL)
 * English dates read day-first ("Mon 5 Oct", the mockup order), so 'en' formats dates as en-GB. Arabic keeps its own
 * digits and month names (the page itself is dir="rtl").
 * Loaded on every Visit Planning page before the page scripts.
 */
(function (window, document) {
    'use strict';
    if (window.VisitPlanningFormat) return;

    const culture = () => ((document.documentElement && document.documentElement.lang) || '').trim() || 'tr';
    // Dates day-first for English (the mockup's "Mon 5 Oct"); every other language as it is.
    const dateCulture = () => { const c = culture(); return /^en(-|$)/i.test(c) ? 'en-GB' : c; };
    const asDate = v => (v instanceof Date ? v : (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v) ? new Date(v + 'T00:00:00') : new Date(v)));
    const valid = d => d instanceof Date && !isNaN(d);
    const dtf = (options, loc) => {
        try { return new Intl.DateTimeFormat(loc || dateCulture(), options); } catch (e) { return new Intl.DateTimeFormat('tr', options); }
    };
    const nf = options => {
        try { return new Intl.NumberFormat(culture(), options); } catch (e) { return new Intl.NumberFormat('tr', options); }
    };
    // WP-VP-4I (5) — English short months are the three-letter ones ("28 Sep"): en-GB's CLDR writes September "Sept", so
    // in English the month part is cut to its first three letters (the day-first order of en-GB stays).
    const isEnglish = () => /^en(-|$)/i.test(culture());
    const fmtDate = (v, options) => {
        const d = asDate(v);
        if (!valid(d)) return '—';
        const f = dtf(options);
        if (!isEnglish() || options.month !== 'short' || typeof f.formatToParts !== 'function') return f.format(d);
        return f.formatToParts(d).map(p => (p.type === 'month' ? p.value.replace(/\.$/, '').slice(0, 3) : p.value)).join('');
    };
    // WP-VP-4I (7) — every DATA text (institution, doctor, product, period, rep) goes into the page isolated: in Arabic a
    // Latin name starting with digits or punctuation ("018 KLİNİK", "75.YIL …") would otherwise be reordered by the
    // surrounding right-to-left text. Escaped, then wrapped in <bdi>.
    const escapeHtml = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const bidi = text => '<bdi>' + escapeHtml(text) + '</bdi>';

    const dayShort = v => fmtDate(v, { weekday: 'short' }).replace(/\.$/, '');
    const dayMonth = v => fmtDate(v, { day: 'numeric', month: 'short' });
    const dayNumber = v => fmtDate(v, { day: 'numeric' });
    const dayLabel = v => { const d = asDate(v); return valid(d) ? dayShort(d) + ' ' + dayMonth(d) : '—'; };
    const range = (from, to) => {
        const a = asDate(from), b = asDate(to);
        if (!valid(a) || !valid(b)) return '—';
        const sameMonth = a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth();
        return (sameMonth ? dayNumber(a) : dayMonth(a)) + '–' + dayMonth(b);
    };
    const number = (n, digits) => nf({ maximumFractionDigits: digits == null ? 1 : digits }).format(Number(n) || 0);
    const hours = (minutes, template) => {
        if (minutes == null || isNaN(minutes)) return '—';
        const h = Math.round((Number(minutes) / 60) * 10) / 10;
        return String(template || '{0} h').split('{0}').join(number(h, 1));
    };
    // A week's working days, "5–9 Eki": from its first day to its Friday (or its last day inside the period, if earlier).
    const workRange = (from, to) => {
        const a = asDate(from), b = asDate(to);
        if (!valid(a) || !valid(b)) return '—';
        const friday = new Date(a.getFullYear(), a.getMonth(), a.getDate() + Math.max(0, 4 - ((a.getDay() + 6) % 7)));
        return range(a, b < friday ? b : friday);
    };
    const dateShort = v => fmtDate(v, { day: 'numeric', month: 'short', year: '2-digit' });
    const dateTime = v => fmtDate(v, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    const weekdayLong = v => fmtDate(v, { weekday: 'long' });
    // WP-VP-4H (7) — a product reads by its NAME; the code only when the server gave no name (4G productName).
    const productLabel = p => (p && (p.productName || p.productCode)) || '—';

    // WP-VP-4H (6) — the ONE reading of a week's load, for the header's "this week" card AND the Targets summary (they
    // used to disagree): planned = the week's 4E days[] (fixed + placed visits with their buffers), else the week's
    // weekCapacity.plannedMinutes; capacity = weekCapacity.capacityMinutes.
    const weekLoad = (preview, weekStart) => {
        const p = preview || {};
        const week = Array.isArray(p.weekCapacity) ? p.weekCapacity.find(c => c.weekStart === weekStart) || null : null;
        const days = Array.isArray(p.days) ? p.days.filter(d => d.weekStart === weekStart) : [];
        const planned = days.length ? days.reduce((sum, d) => sum + (Number(d.plannedMinutes) || 0), 0) : (week ? week.plannedMinutes : null);
        return { planned: planned, capacity: week ? week.capacityMinutes : null, week: week };
    };

    window.VisitPlanningFormat = Object.freeze({
        culture, dateCulture, asDate, bidi, dayShort, dayMonth, dayNumber, dayLabel, range, workRange, number, hours, dateShort, dateTime, weekdayLong, productLabel, weekLoad
    });
})(window, document);
