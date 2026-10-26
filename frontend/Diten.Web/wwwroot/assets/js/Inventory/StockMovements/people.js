/**
 * WP-MVP1-NAMES1 (FU-0173-UI-USER-01, BG-05 "Recorded by") — the PURE rule of the "Kaydeden" cell of the movement history (Node measures this
 * file): the name the posting recorded (INVENTORY 1.5.0 createdBy.name — the name at the time of the posting), else the localized "unknown
 * user". The id is never shown as a name: it is only the hover title of an unnamed poster. A row without a poster shows nothing.
 */
'use strict';

const StockMovementPeople = (function () {
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    /** The text of the cell: the recorded name, else `unknown`; '' when the row has no poster. */
    const text = (row, unknown) => (row?.createdByName ? String(row.createdByName) : row?.createdById ? String(unknown ?? '') : '');

    /** The cell's markup: the text, escaped; an unnamed poster's id only as the title. */
    const html = (row, unknown) => (row?.createdById && !row?.createdByName
        ? `<span title="${esc(row.createdById)}">${esc(text(row, unknown))}</span>`
        : esc(text(row, unknown)));

    return { text, html };
})();

if (typeof window !== 'undefined') window.StockMovementPeople = StockMovementPeople;
