'use strict';

/*
 * DitenDialog — the SweetAlert-adapter pieces every screen that opens a `window.showConfirm` dialog with a
 * select2 control (or that needs the product's dialog LOOK for a raw, multi-field `Swal.fire`) can call, so a
 * third and fourth hand-rolled copy of this never gets written (WP-WC-SHARED-UI-01, E1).
 *
 * ⚠ WHY WorkCenterNext/app.js KEEPS ITS OWN COPIES OF `bindDialogSelect2` AND `dialogLook`, RATHER THAN CALLING
 * THIS FILE — a DELIBERATE, REPORTED EXCEPTION, not an oversight. Three tests pin their exact literal source
 * inside app.js: `wcn-dialog-seven-defects.test.js` and `wcn-dialog-rhythm.test.js` slice
 * `const bindDialogSelect2 = (` straight out of app.js's own text and assert on its body (including the literal
 * class name `wcn-dialog-select`, which also has its own pinned CSS rules), and `wcn-dialog-one-language.test.js`
 * asserts `APP` (app.js's source) contains the literal string `global.DitenDialogAppearance(options)` — the
 * body of `dialogLook`. Deleting either declaration from app.js in favour of a one-line delegation would not
 * change any user-visible behaviour, but it would turn all three tests red for a purely structural reason (this
 * WP's own stop condition treats a red WCN test as "the refactor changed behaviour — revert"). So the SAME
 * logic is written here, fresh, for every OTHER consumer (Meetings today; S5/S6 next), while WorkCenterNext's
 * own copies — already covered by their own tests — are left exactly as they are. `dialogIcon` and
 * `dialogDescriptionClass` are NOT pinned this way (only their CALL SITES are asserted, e.g.
 * `dialogIcon('info', inboxActionIcon(action))`), so app.js's own declarations were safely turned into one-line
 * delegations to this file — no third copy of THEIR bodies either.
 *
 * `bindDialogSelect2` here defaults to ITS OWN class names (`diten-dialog-select` /
 * `diten-dialog-select-dropdown`, styled in backbone-custom.css under "THE SHARED CONFIRM'S SELECT2, PUBLISHED")
 * rather than reusing `wcn-dialog-select` — a shared component does not reach into a module's own CSS hook, and
 * WCN's pinned class stays WCN's.
 */
(function (global) {
    const dialogLook = (options) => {
        if (typeof global.DitenDialogAppearance !== 'function') {
            console.error('[DitenDialog] window.DitenDialogAppearance is unavailable (is _GlobalConfirmation loaded?).');
            return {};
        }
        return global.DitenDialogAppearance(options);
    };

    const dialogIcon = (type, glyph) => (typeof global.DitenDialogAppearance === 'function'
        && typeof global.DitenDialogAppearance.iconHtml === 'function'
        ? global.DitenDialogAppearance.iconHtml(type, glyph)
        : '');

    const dialogDescriptionClass = () => (typeof global.DitenDialogAppearance === 'function'
        ? global.DitenDialogAppearance.description
        : '');

    /*
     * select2 INSIDE a SweetAlert popup — parented to the popup itself (never `document.body`), because a
     * dropdown parented anywhere else opens BEHIND the modal's own backdrop; measured once, in WorkCenterNext,
     * the defect this same shape exists to avoid a second time.
     */
    const bindDialogSelect2 = (element, popup, options) => {
        const jq = global.jQuery;
        if (!element || !jq || !jq.fn || !jq.fn.select2) { return false; }
        const $s = jq(element);
        if ($s.hasClass('select2-hidden-accessible')) { return true; }

        const opts = options || {};
        const config = {
            dropdownParent: jq(popup || element.closest('.swal2-popup') || global.document.body),
            containerCssClass: opts.containerCssClass || 'diten-dialog-select',
            dropdownCssClass: opts.dropdownCssClass || 'diten-dialog-select-dropdown',
            minimumResultsForSearch: opts.minimumResultsForSearch ?? 10,
            width: '100%',
            allowClear: !!opts.allowClear
        };
        const declared = String($s.data('placeholder') || '');
        // No `placeholder` key unless there is one: select2's placeholder decorator hides the first empty-value
        // option even when it is a real choice, not a prompt (measured against WCN's own waiting-on picker).
        if (declared) { config.placeholder = declared; }
        if (opts.dropdownAdapter) { config.dropdownAdapter = opts.dropdownAdapter; }
        $s.select2(config);
        return true;
    };

    global.DitenDialog = { dialogLook, dialogIcon, dialogDescriptionClass, bindDialogSelect2 };
})(typeof window !== 'undefined' ? window : globalThis);
