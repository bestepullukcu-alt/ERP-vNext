'use strict';

// ─────────────────────────────────────────────────────────────────────────────
// DitenModal — the single premium alert helper (MOD-0013 premium modal standard).
//
// The standard FORBIDS bare, unstyled SweetAlert2 dialogs: every error, success,
// warning and info popup must carry the Sneat premium chrome (rounded-4 + shadow-lg,
// 2.5rem 1.5rem 2rem padding, a .swal-icon-circle icon well with the default Swal
// icon animation off, buttonsStyling:false and Sneat button classes).
//
// Before this helper existed the chrome was re-typed by hand in every caller, so a
// new page could silently ship a bare Swal.fire. Call these functions instead.
//
//   DitenModal.error({ title, message })                → Promise
//   DitenModal.success({ title, message, timer })        → Promise
//   DitenModal.warning({ title, message })               → Promise
//   DitenModal.info({ title, message })                  → Promise
//   DitenModal.confirm({ title, ... }, onConfirm)        → delegates to window.showConfirm
//
// Every text argument must arrive ALREADY LOCALIZED — this file holds no strings.
// `message` is escaped by default; pass `html: true` only for markup you built and
// escaped yourself.
//
// Styling note (FG-003): no inline CSS here. The `.swal-icon-circle` rules ship
// globally in Views/Shared/_GlobalConfirmation.cshtml, which every layout page
// loads, so class names alone reproduce the exact visuals the app already shows.
// Layout-LESS pages (Layout = null, e.g. Account/login) cannot see that CSS and
// must keep writing the standard's inline-styled icon markup by hand.
// ─────────────────────────────────────────────────────────────────────────────
(function (global) {
    // Icon wells, byte-identical in class terms to _GlobalConfirmation.cshtml so a
    // DitenModal dialog and a showConfirm dialog cannot look like different products.
    const ICONS = {
        error: '<div class="swal-icon-circle bg-label-danger border-danger border-opacity-25">'
             + '<i class="bx bx-error-circle text-danger"></i></div>',
        success: '<div class="swal-icon-circle bg-label-success border-success border-opacity-25">'
               + '<i class="bx bx-check-circle text-success"></i></div>',
        warning: '<div class="swal-icon-circle bg-label-warning border-warning border-opacity-25">'
               + '<i class="bx bx-error text-warning"></i></div>',
        info: '<div class="swal-icon-circle bg-label-primary border-primary border-opacity-25">'
            + '<i class="bx bx-info-circle text-primary"></i></div>'
    };

    const CONFIRM_BUTTON_CLASS = {
        error: 'btn btn-primary waves-effect waves-light px-5',
        success: 'btn btn-primary waves-effect waves-light px-5',
        warning: 'btn btn-primary waves-effect waves-light px-5',
        info: 'btn btn-primary waves-effect waves-light px-5'
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    const normalize = (input) => (typeof input === 'string' ? { message: input } : (input || {}));

    const show = (type, input) => {
        const options = normalize(input);
        const swal = global.Swal;

        if (!swal || typeof swal.fire !== 'function') {
            // A bare browser alert() is also forbidden by the standard, so fail loudly in the
            // console rather than degrading to a dialog the standard bans.
            console.error('[DitenModal] SweetAlert2 is not loaded; message not shown:',
                options.title || '', options.message || '');
            return Promise.resolve({ isConfirmed: false, isDismissed: true });
        }

        const body = options.message === undefined || options.message === null
            ? ''
            : `<div class="mb-1 text-muted">${options.html ? options.message : escapeHtml(options.message)}</div>`;

        const config = {
            title: options.title || '',
            html: body,
            iconHtml: ICONS[type] || ICONS.info,
            confirmButtonText: options.confirmButtonText || 'OK',
            width: options.width || '400px',
            padding: '2.5rem 1.5rem 2rem',
            customClass: {
                popup: 'rounded-4 shadow-lg',
                title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                htmlContainer: 'mb-1 d-block w-100 text-center',
                actions: 'd-flex justify-content-center mt-4 w-100 gap-2',
                confirmButton: CONFIRM_BUTTON_CLASS[type] || CONFIRM_BUTTON_CLASS.info,
                cancelButton: 'btn btn-label-secondary waves-effect px-5',
                icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
            },
            buttonsStyling: false,
            // Both prevent the navbar shift when the popup opens; _GlobalConfirmation patches
            // Swal.fire to default them, and they are set here too so the helper is correct on
            // its own (see feedback: sidebar stability is fixed in CSS/config, never ad hoc).
            scrollbarPadding: false,
            heightAuto: false,
            reverseButtons: true
        };

        // A self-dismissing acknowledgement (used for "saved"/"claimed" style feedback).
        if (options.timer) {
            config.timer = options.timer;
            config.showConfirmButton = false;
        }

        return swal.fire(config);
    };

    global.DitenModal = {
        ICONS,
        error: (input) => show('error', input),
        success: (input) => show('success', input),
        warning: (input) => show('warning', input),
        info: (input) => show('info', input),

        /**
         * Confirmations stay with the pre-existing global so there is exactly ONE confirm
         * implementation in the app; this is a named seam, not a second one.
         */
        confirm: (titleOrKey, onConfirm, options) => {
            if (typeof global.showConfirm === 'function') {
                return global.showConfirm(titleOrKey, onConfirm, options);
            }

            console.error('[DitenModal] window.showConfirm is unavailable (is _GlobalConfirmation loaded?).');
            return undefined;
        }
    };
})(typeof window !== 'undefined' ? window : globalThis);
