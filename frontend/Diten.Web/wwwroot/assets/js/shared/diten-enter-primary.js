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
 *   · a held key (`repeat`): nothing — one press, one action;
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
 * Esc is NOT handled here: Bootstrap's offcanvas already closes on Esc (keyboard: true, no panel overrides it) and
 * no panel asks about unsaved changes today — measured in the UX-COMMON report.
 */
(function (global) {
    const doc = global.document;
    if (!doc) { return; }

    const PANEL_SELECTOR = '.offcanvas.show, .offcanvas.showing';
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
        if (event.key !== 'Enter' || event.defaultPrevented || event.repeat) { return; }
        if (event.isComposing || event.keyCode === 229) { return; }
        const target = event.target;
        const kind = fieldKind(target);
        if (!kind) { return; }
        const panel = target.closest && target.closest(PANEL_SELECTOR);
        if (!panel) { return; }

        const chord = event.ctrlKey || event.metaKey;
        if (kind === 'multi' && !chord) { return; }      // a new line in the description
        if (kind === 'single' && event.altKey) { return; }
        if (pickerAtStart.get(event) || pickerOpen(target)) { return; }

        const primary = primaryOf(panel);
        if (!primary) { return; }
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
    global.addEventListener('keydown', onKeydownCapture, true);
    global.addEventListener('keydown', onKeydown);

    global.DitenEnterPrimary = {
        primaryOf,
        __uninstall: () => {
            global.removeEventListener('keydown', onKeydownCapture, true);
            global.removeEventListener('keydown', onKeydown);
        }
    };
})(typeof window !== 'undefined' ? window : globalThis);
