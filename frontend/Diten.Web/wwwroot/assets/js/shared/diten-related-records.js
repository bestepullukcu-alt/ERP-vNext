'use strict';

/*
 * DitenRelatedRecords — the one related/linked-record ROW, so a second screen that lists "records that point to
 * this one" does not redraw it from scratch (WP-WC-SHARED-UI-01, E3).
 *
 * Moved here from WorkCenterNext/app.js's `renderRelated`, which had the only worked copy: a type badge, the
 * title, the record's own id as a subtitle, and a chevron. Deliberately ROW-LEVEL ONLY — no section wrapper,
 * no heading, no "N kayıt" count. app.js's own `sectionHead('bx-link', 'RelatedRecordsTitle')` and
 * `.wcn-related-list` wrapper stay exactly where they are; only the per-row markup moved.
 *
 * `resolveTypeLabel` is OPTIONAL and its own decision per caller, not a fixed part of the row: WCN's
 * relatedRecords are genuinely mixed (parent/child/transaction/document), so the badge tells the reader
 * something; a caller whose "related" list is all one kind (Meetings' linked tasks — always a task) passes
 * none, and the row prints without a badge rather than repeating the same word on every line.
 */
(function (global) {
    /**
     * One row, as an HTML string.
     *
     * @param {{id, title, link}} record
     * @param {object} [options]
     * @param {Function} [options.esc]              HTML-escapes a value; defaults to a plain String() (the
     *                                                caller almost always already has one — app.js's `esc`).
     * @param {string}   [options.rowClass]           Defaults to WCN's own 'wcn-related-row'.
     * @param {string}   [options.typeClass]          Defaults to WCN's own 'wcn-related-type'.
     * @param {Function} [options.resolveTypeLabel]   (record) => string|null|undefined. Omitted/falsy → no badge.
     * @param {boolean}  [options.showId]             Defaults to true (WCN always shows the record's own id).
     * @param {boolean}  [options.showChevron]        Defaults to true.
     */
    const relatedRecordRow = (record, options) => {
        const opts = options || {};
        const escFn = typeof opts.esc === 'function' ? opts.esc : (value) => String(value ?? '');
        const rowClass = opts.rowClass || 'wcn-related-row';
        const typeClass = opts.typeClass || 'wcn-related-type';
        /*
         * The badge SPAN is always emitted, empty or not: `.wcn-related-row`'s CSS is a 3-column grid (badge /
         * text / chevron) and an omitted element would shift the chevron into the text column instead of
         * dropping a column, for every caller that reuses the same class rather than styling its own. An
         * empty span is invisible and costs the grid nothing.
         */
        const typeLabel = typeof opts.resolveTypeLabel === 'function' ? opts.resolveTypeLabel(record) : null;
        const typeHtml = `<span class="${typeClass}">${typeLabel ? escFn(typeLabel) : ''}</span>`;
        const showId = opts.showId !== false;
        const idHtml = showId ? `<small>${escFn(record.id)}</small>` : '';
        const chevronHtml = opts.showChevron === false ? '' : '<i class="bx bx-chevron-right"></i>';
        return `<a class="${rowClass}" href="${escFn(record.link)}">${typeHtml}<span><strong>${escFn(record.title)}</strong>${idHtml}</span>${chevronHtml}</a>`;
    };

    /** Every row, joined — what a caller drops straight into its own list wrapper. */
    const renderRelatedRows = (records, options) => (records || []).map((record) => relatedRecordRow(record, options)).join('');

    global.DitenRelatedRecords = { relatedRecordRow, renderRelatedRows };
})(typeof window !== 'undefined' ? window : globalThis);
