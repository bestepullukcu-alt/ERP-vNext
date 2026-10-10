'use strict';

/*
 * The product's tours, in one place (WP-UX-COMMON-01, BL-495; UXH-001 §5).
 *
 * Tours exist ONLY for the five flows the owner chose (2026-10-09): Task Center, Timesheet, Global Product / item
 * submission, user + role setup, Meetings. Every other page has its "?" text and no tour.
 *
 * Each tour is keyed by the page's stable code (the help panel's `data-page-code`) and points at `[data-tour]` marks
 * that page draws. A step whose mark is not on the page right now is skipped by DitenTour. Text keys are
 * `Tour.{CODE}.Step{n}.Title|Text` in PageHelpResource (7 languages). A tour's `version` goes up when its steps
 * change meaning, so readers who saw the old one are offered the new one.
 *
 * tests/diten-tour-targets.test.js measures that every target below exists in its page's markup.
 */
(function (global) {
    const tour = global.DitenTour;
    if (!tour || typeof tour.register !== 'function') { return; }

    const steps = (code, targets) => targets.map((target, index) => ({
        target,
        titleKey: `Tour.${code}.Step${index + 1}.Title`,
        textKey: `Tour.${code}.Step${index + 1}.Text`
    }));

    const CATALOG = {
        // /WorkCenterNext — list → open a task → accept / complete → approve → delegate. Marks drawn by app.js.
        WORKCENTER: { version: 1, targets: ['wcn-tabs', 'wcn-search', 'wcn-list', 'wcn-row-actions', 'wcn-new'] },
        // /TimeEntry — enter time → submit the week. Marks in Views/TimeEntry/Index.cshtml.
        MYTIMESHEET: { version: 1, targets: ['te-week', 'te-add-row', 'te-grid', 'te-submit'] },
        // /TimeEntry/Approvals — the approver: open a week → approve / return; approve several. Marks in its Index.cshtml.
        TIMEAPPROVALS: { version: 1, targets: ['ta-header', 'ta-list', 'ta-list'] }
    };

    Object.keys(CATALOG).forEach((code) => {
        tour.register(code, { version: CATALOG[code].version, steps: steps(code, CATALOG[code].targets) });
    });

    global.DitenTourCatalog = CATALOG;
})(typeof window !== 'undefined' ? window : globalThis);
