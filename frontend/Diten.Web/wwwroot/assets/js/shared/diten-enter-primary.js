'use strict';

/*
 * DitenEnterPrimary — Enter runs a side panel's primary action (WP-UX-COMMON-01, BL-586).
 *
 * Every create/edit side panel (Bootstrap offcanvas) used to need the mouse for "Save": the save buttons are
 * `type="button"` (no form submission to ride on) and nothing listened for Enter. One listener here, at document
 * level, gives every panel the same keyboard: type, press Enter, the panel's primary action runs — exactly as if
 * its button had been clicked, so the page's own save handler, validation and double-submit guard all still apply.
 *
 * ── WHICH BUTTON IS PRIMARY ───────────────────────────────────────────────────────────────────────────────
 *   1. the button marked `data-dt-primary` inside the open panel (a marked button hidden right now does not count);
 *   2. otherwise the panel's ONE `.btn-primary` — and only in a panel that carries form fields (a read-only quick
 *      view has a single "Edit" `.btn-primary`; Enter there must not open an editor);
 *   3. two or more unmarked `.btn-primary`, or none: Enter is left alone (the panel needs a `data-dt-primary` mark).
 *
 * ── WHEN ENTER IS NOT "SAVE" ──────────────────────────────────────────────────────────────────────────────
 *   · textarea: Enter is a new line; Ctrl+Enter (Cmd+Enter on macOS) saves;
 *   · an open select2, dropdown, autocomplete or date picker: Enter picks the highlighted entry;
 *   · IME composition (`isComposing`, keyCode 229): Enter confirms the composed text;
 *   · a disabled primary button (`disabled`, `.disabled`, `aria-disabled="true"`): nothing — no double submission;
 *   · a held key (`repeat`): nothing — one press, one action (and no native submission, see R1 below);
 *   · focus on a button, link, checkbox, radio, file or select: those keep their own Enter;
 *   · a handler earlier in the chain already took the key (`defaultPrevented`).
 *
 * ── CONSEQUENTIAL ACTIONS: `data-dt-primary="chord"` (UX-COMMON-FIX1, CT decision) ──────────────────────────
 * A primary action that cannot be taken back — publish, approve, reject, submit / send, delete, end, retire,
 * deactivate, delegate — is marked `data-dt-primary="chord"`: a bare Enter does NOTHING there (and no native form
 * submission either); Ctrl+Enter (Cmd+Enter) runs it. Bare Enter stays for reversible work: save a draft, create,
 * edit a record, filter, search. tests/diten-enter-primary.test.js derives which panels must be chord-marked from the
 * primary button's label (its consequential-word list) and fails on an unmarked one.
 *
 * ── ONE PRESS, ONE ACTION ─────────────────────────────────────────────────────────────────────────────────
 *   · a field with a `list` (datalist) keeps Enter — some browsers commit a suggestion with it;
 *   · after this script clicks a primary, the same panel's primary is not clicked again for a short moment (two
 *     quick Enters are one save, even where the page's own handler has no busy lock);
 *   · "a picker is open" is read in the CAPTURE phase, BEFORE the field's own handlers run: an `allowInput` flatpickr
 *     commits the typed date and closes its calendar on Enter, and reading the state afterwards saw it closed.
 *
 * ── NO NATIVE FORM SUBMISSION BEHIND THE RULE'S BACK (UX-COMMON-FIX2, R1) ──────────────────────────────────────────
 * The panel and its primary are found FIRST, before any of the early returns below. When the primary is chord-marked
 * or is itself a submit button (`type="submit"`, or a <button> with no type), Enter on an input or a <select> — a
 * checkbox, a closed select, a datalist field, a held key, Alt+Enter — is `preventDefault`ed even where this script then
 * clicks nothing: otherwise the browser's IMPLICIT submission presses that submit button (an unconfirmed subscription
 * cancel, a repeat POST from a held key). Not in a textarea, on a button or link, while an IME composes, or while a
 * picker is open (those keep their own Enter).
 *
 * ── TELLING THE READER (R3) ──────────────────────────────────────────────────────────────────────────────────────
 * A chord-marked button gets `aria-keyshortcuts="Control+Enter Meta+Enter"` and, when it has none, a `title` naming the
 * chord (the help panel's localized `data-diten-chord-title`), each time its panel opens: a mark set by a page script
 * (Tenants' subscription actions) is decorated too.
 *
 * Esc is NOT handled here: Bootstrap's offcanvas already closes on Esc (keyboard: true, no panel overrides it) and
 * no panel asks about unsaved changes today — measured in the UX-COMMON report.
 */
(function (global) {
    const doc = global.document;
    if (!doc) { return; }

    // FIX2 — Bootstrap keeps `show` while a panel slides OUT (`.hiding`): that panel is closing, not open.
    const PANEL_SELECTOR = '.offcanvas.show:not(.hiding), .offcanvas.showing:not(.hiding)';
    const CHORD_KEYS = 'Control+Enter Meta+Enter';
    const CHORD_TITLE_FALLBACK = 'Ctrl+Enter (\u2318+Enter on a Mac) \u2014 a bare Enter does not run this action.';
    const FIELD_SELECTOR = 'input:not([type="hidden"]), textarea, select, [contenteditable]:not([contenteditable="false"])';
    const SINGLE_LINE_TYPES = /^(text|search|email|url|tel|password|number|date|datetime-local|month|week|time)$/;
    const OPEN_PICKER_SELECTOR = [
        '.select2-container--open',
        '.flatpickr-calendar.open',
        '.dropdown-menu.show',
        '.ui-autocomplete:not([style*="display: none"])',
        '.aa-Panel'
    ].join(', ');

    const IN_FLIGHT_MS = 800;
    const lastClick = new WeakMap();      // panel → time this script last clicked its primary
    const pickerAtStart = new WeakMap();  // event → "a picker was open when the key went down" (capture phase)

    const isDisabled = (button) => !!(button.disabled
        || button.classList.contains('disabled')
        || button.getAttribute('aria-disabled') === 'true');

    // The panel's primary action, or null when it cannot be told without a `data-dt-primary` mark.
    // A marked button that is hidden right now (`d-none` / `hidden`) is not this moment's primary: a panel may mark
    // two that take turns (CRM VisitExecution: "submit report" before finalising, "amend report" after).
    const shown = (element) => !(element.hidden || (element.closest && element.closest('.d-none, [hidden]')));

    const primaryOf = (panel) => {
        const marked = Array.from(panel.querySelectorAll('[data-dt-primary]')).filter(shown);
        if (marked.length === 1) { return marked[0]; }
        if (marked.length > 1) { return null; }
        if (!panel.querySelector(FIELD_SELECTOR)) { return null; }
        // UX-COMMON-FIX1 — an unmarked candidate hidden right now (d-none, an inactive tab, a hidden step) is not it.
        const candidates = Array.from(panel.querySelectorAll('button.btn-primary, input[type="submit"].btn-primary'))
            .filter(shown);
        return candidates.length === 1 ? candidates[0] : null;
    };

    // What kind of field the key was pressed in: 'single', 'multi' (textarea), or null (Enter is not ours there).
    const fieldKind = (target) => {
        if (!target || target.nodeType !== 1) { return null; }
        if (target.tagName === 'TEXTAREA') { return 'multi'; }
        if (target.tagName === 'INPUT') {
            const type = (target.getAttribute('type') || 'text').toLowerCase();
            if (target.hasAttribute('list')) { return null; } // a datalist field: Enter may commit a suggestion
            return SINGLE_LINE_TYPES.test(type) ? 'single' : null;
        }
        return null;
    };

    // R1 — the browser's implicit submission presses a submit button; a <button> with no type IS a submit button.
    const submitsNatively = (button) => {
        if (button.tagName === 'INPUT') { return (button.getAttribute('type') || '').toLowerCase() === 'submit'; }
        const type = (button.getAttribute('type') || 'submit').toLowerCase();
        return button.tagName === 'BUTTON' && type === 'submit';
    };

    // R1 — an input (not a button-like one) or a <select>: the elements whose Enter can trigger an implicit submission.
    const NON_FIELD_INPUTS = /^(button|submit|reset|image)$/;
    const isFormControl = (target) => {
        if (target.tagName === 'SELECT') { return true; }
        if (target.tagName !== 'INPUT') { return false; }
        return !NON_FIELD_INPUTS.test((target.getAttribute('type') || 'text').toLowerCase());
    };

    const chordTitle = () => {
        const help = doc.getElementById('diten-help-panel');
        return (help && help.getAttribute('data-diten-chord-title')) || CHORD_TITLE_FALLBACK;
    };

    // R3 — what a chord-marked button tells keyboard and screen-reader users.
    const decorate = (root) => {
        if (!root || !root.querySelectorAll) { return; }
        root.querySelectorAll('[data-dt-primary="chord"]').forEach((button) => {
            button.setAttribute('aria-keyshortcuts', CHORD_KEYS);
            if (!button.getAttribute('title')) { button.setAttribute('title', chordTitle()); }
        });
    };

    const pickerOpen = (target) => {
        if (target.getAttribute('aria-expanded') === 'true') { return true; }
        return !!doc.querySelector(OPEN_PICKER_SELECTOR);
    };

    // Capture phase on window: the FIRST stop of the key, before the field's own handlers (flatpickr's included).
    const onKeydownCapture = (event) => {
        if (event.key !== 'Enter' || !event.target || event.target.nodeType !== 1) { return; }
        pickerAtStart.set(event, pickerOpen(event.target));
    };

    const onKeydown = (event) => {
        if (event.key !== 'Enter' || event.defaultPrevented) { return; }
        const target = event.target;
        if (!target || target.nodeType !== 1) { return; }
        const panel = target.closest && target.closest(PANEL_SELECTOR);
        if (!panel) { return; }
        const primary = primaryOf(panel);
        if (!primary) { return; }

        // R1 — found BEFORE the early returns: no implicit submission past a chord mark or a submit primary.
        const composing = event.isComposing || event.keyCode === 229;
        const guarded = primary.getAttribute('data-dt-primary') === 'chord' || submitsNatively(primary);
        if (guarded && !composing && isFormControl(target) && !(pickerAtStart.get(event) || pickerOpen(target))) {
            event.preventDefault();
        }

        if (event.repeat || composing) { return; }
        const kind = fieldKind(target);
        if (!kind) { return; }

        const chord = event.ctrlKey || event.metaKey;
        if (kind === 'multi' && !chord) { return; }      // a new line in the description
        if (kind === 'single' && event.altKey) { return; }
        if (pickerAtStart.get(event) || pickerOpen(target)) { return; }

        // From here the key is ours either way: a disabled button means "not now", never a native form submit.
        event.preventDefault();
        if (isDisabled(primary)) { return; }
        if (primary.getAttribute('data-dt-primary') === 'chord' && !chord) { return; } // consequential: Ctrl+Enter only
        const now = Date.now();
        if (now - (lastClick.get(panel) || 0) < IN_FLIGHT_MS) { return; }
        lastClick.set(panel, now);
        primary.click();
    };

    if (global.DitenEnterPrimary && typeof global.DitenEnterPrimary.__uninstall === 'function') {
        global.DitenEnterPrimary.__uninstall();
    }
    // On `window`, bubble phase: the LAST stop of a key press. A page that gives Enter its own meaning in a panel
    // (WorkCenterNext's subtask / comment inputs, CRM's qualifier inputs) handles it on the field or on `document`
    // and calls preventDefault — which this listener, running after them, respects. Registering on `document`
    // instead would put the layout's listener BEFORE every page's (layout scripts load first).
    const onPanelShow = (event) => decorate(event.target);

    global.addEventListener('keydown', onKeydownCapture, true);
    global.addEventListener('keydown', onKeydown);
    doc.addEventListener('show.bs.offcanvas', onPanelShow);

    global.DitenEnterPrimary = {
        primaryOf,
        decorate,
        __uninstall: () => {
            global.removeEventListener('keydown', onKeydownCapture, true);
            global.removeEventListener('keydown', onKeydown);
            doc.removeEventListener('show.bs.offcanvas', onPanelShow);
        }
    };
})(typeof window !== 'undefined' ? window : globalThis);
