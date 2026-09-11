'use strict';

/*
 * DitenDialog — the SweetAlert-adapter pieces every screen that opens a `window.showConfirm` dialog with a
 * select2 control (or that needs the product's dialog LOOK for a raw, multi-field `Swal.fire`) can call, so a
 * third and fourth hand-rolled copy of this never gets written (WP-WC-SHARED-UI-01, E1).
 *
 * WorkCenterNext/app.js's own `bindDialogSelect2`/`dialogLook` used to keep real, duplicate bodies here rather
 * than delegating — three of app.js's own tests pinned their literal source, and this file's `bindDialogSelect2`
 * did not yet accept an `options` override for the class names those tests also pinned. WP-WC-SHARED-UI-01 M2b
 * (2026-09-11) closed that gap: `bindDialogSelect2` now takes `containerCssClass`/`dropdownCssClass` overrides
 * (below), the four pinning tests were retargeted to read the mechanism from THIS file while keeping the same
 * behavioural claims, and app.js's own declarations became one-line delegations — like `dialogIcon` and
 * `dialogDescriptionClass` already were.
 *
 * `bindDialogSelect2` DEFAULTS to its own class names (`diten-dialog-select` / `diten-dialog-select-dropdown`,
 * styled in backbone-custom.css under "THE SHARED CONFIRM'S SELECT2, PUBLISHED"); a caller with its own pinned
 * CSS hook (WorkCenterNext's `wcn-dialog-select`) passes it as an option instead — a shared component does not
 * reach into a module's own CSS hook.
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
