'use strict';

/*
 * DitenDateField — the ONE date input, and the ONE reason its leading icon is not a picture.
 *
 * ── WHY THIS FILE EXISTS ────────────────────────────────────────────────────────────────────────────────────
 * This behaviour lived in assets/js/Tasks/form.js and NOWHERE else, which is exactly why the pattern stopped at
 * one module: a screen that copied the golden reference's MARKUP got the icon and none of the wiring, and
 * shipped a glyph that opens nothing. The markup contract (.diten-field + .diten-field-icon) is shared, so the
 * behaviour that contract implies has to be shared too — otherwise every new form re-decides it, and half of
 * them decide wrong by omission.
 *
 * ── WHAT IT GUARANTEES ──────────────────────────────────────────────────────────────────────────────────────
 * 1. flatpickr, not <input type="date">. A native date input takes its display format from the OPERATING
 *    SYSTEM's locale, so an Arabic page still rendered gg.aa.yyyy — the page's own language never entered into
 *    it. flatpickr draws the calendar itself, and `dateFormat: 'Y-m-d'` keeps the value the input carries
 *    EXACTLY what the native control produced, so nothing the API receives changes.
 * 2. THE LEADING ICON IS A CONTROL, NOT A PICTURE. It sits ON TOP of the field's inline start, so a user aiming
 *    at "the calendar" hits the glyph and not the input. Left unbound that is a dead icon — the same defect as
 *    a dead button, which this project shipped once and had reported as breakage. `allowInput: true` means the
 *    input itself does not open on focus either, so without this the icon would be the one obvious affordance
 *    that does nothing.
 *
 * Returns the number of inputs it enhanced, so a caller can assert it found what it expected.
 */
(function (global) {
    /*
     * BL-391 — "gg.aa.yyyy" typed by hand is a promise: what the reader typed is what gets saved, or nothing
     * is. `allowInput: true` hands flatpickr free text on close, and flatpickr's OWN parser (`Date.parse`-style
     * leniency) can turn an unrecognised string into a DIFFERENT valid date instead of refusing it — the exact
     * failure mode this bug report described (a typed "2026-09-13" silently became a stored "2026-06-20").
     *
     * The fix does not try to out-guess flatpickr's parser. It watches what the reader actually left in the box
     * (an `input` listener, captured BEFORE flatpickr's own close-time reformat can overwrite that value) and,
     * on close, re-renders whatever flatpickr DID resolve back into the SAME display format. If that re-render
     * does not read back identical to what was typed — or nothing was resolved at all — the field cannot be
     * trusted to mean what it shows, so it is cleared and marked invalid instead.
     *
     * `formatKey` is 'dateFormat' for a plain field (this file's own default: no altInput, the visible input IS
     * the value) or 'altFormat' for an altInput field (Organization/PositionAssignments/form.js's own shape) —
     * whichever format governs the text the reader is actually looking at and typing into.
     */
    const guardAgainstSilentMisparse = (instance, formatKey) => {
        // A real flatpickr instance always has both; a minimal test double (or a future caller that hands this
        // an incomplete object) does not, and the SAME "nothing to enhance, quietly skip" posture `enhance`
        // itself already takes below applies here too — never throw out of a setup path.
        const target = instance?.altInput || instance?.input;
        if (!target || !Array.isArray(instance.config?.onClose) || typeof instance.formatDate !== 'function') {
            return;
        }

        let lastTyped = target.value || '';
        target.addEventListener('input', () => { lastTyped = target.value; });

        instance.config.onClose.push((selectedDates) => {
            const raw = lastTyped.trim();
            if (!raw) {
                target.classList.remove('is-invalid');
                lastTyped = target.value;
                return;
            }

            const resolved = selectedDates[0];
            const reformatted = resolved ? instance.formatDate(resolved, instance.config[formatKey]) : '';
            if (!resolved || reformatted !== raw) {
                instance.clear();
                target.classList.add('is-invalid');
            } else {
                target.classList.remove('is-invalid');
            }
            lastTyped = target.value;
        });
    };

    const enhance = (root, options) => {
        const scope = root || global.document;
        if (!scope) { return 0; }

        const candidates = Array.from(scope.querySelectorAll('.flatpickr-date'));
        const nodes = candidates.filter((node) => typeof node.flatpickr === 'function' && !node._flatpickr);

        /*
         * ⚠ A FIELD THAT COULD NOT BE BOUND SAYS SO (2026-09-02).
         *
         * This filter used to swallow the whole failure: a page carrying `.flatpickr-date` markup without the
         * flatpickr library returned 0, painted a calendar icon over a plain text box, and told nobody. The
         * Task Center shipped that for weeks and it was found by a person typing a date by hand in a demo, not
         * by any of the 2000 green tests — because there was nothing to observe.
         *
         * `filter` is the RIGHT behaviour (an already-bound field is skipped, and skipping is not a failure);
         * what was missing is the difference between "nothing to do" and "something to do and no way to do it".
         * Only the second is reported, and it is reported once with the ids, so a developer opening the console
         * on the broken screen reads the cause instead of hunting for it.
         */
        const unbindable = candidates.filter((node) => typeof node.flatpickr !== 'function');
        if (unbindable.length > 0 && global.console && global.console.error) {
            global.console.error(
                `[DitenDateField] ${unbindable.length} date field(s) cannot be enhanced because the flatpickr `
                + 'library is not loaded on this page — they will stay plain text boxes under a calendar icon '
                + 'that opens nothing. Load assets/vendor/libs/flatpickr/flatpickr.js (the tenant shell layout '
                + 'already does) before this script. Fields: '
                + unbindable.map((node) => node.id || node.name || '(unnamed)').join(', ') + '.');
        }

        nodes.forEach((node) => {
            node.flatpickr(Object.assign(
                { monthSelectorType: 'static', dateFormat: 'Y-m-d', allowInput: true },
                options || {}
            ));

            // BL-391 — `allowInput: true` lets the reader type straight past the picker, and flatpickr's own
            // parser is lenient: text that does not actually match `dateFormat` can still resolve to SOME date
            // with no sign anything went wrong. Guarded on every field this component enhances.
            guardAgainstSilentMisparse(node._flatpickr, 'dateFormat');

            /*
             * The icon is looked up from the CONTROL, not from the wrapper, on purpose: select2 and other
             * enhancers insert their own element between .diten-field and the control, so the icon is the
             * wrapper's child while the control may not be. Walking up to the nearest .diten-field finds it in
             * both shapes; a plain parentElement lookup only worked while the markup stayed flat.
             */
            const field = node.closest ? node.closest('.diten-field') : node.parentElement;
            const icon = field?.querySelector('.diten-field-icon');
            if (icon) {
                icon.addEventListener('click', () => node._flatpickr?.open());
            }
        });

        return nodes.length;
    };

    // guardAgainstSilentMisparse exposed so a test can drive it directly against a real flatpickr instance.
    global.DitenDateField = { enhance, guardAgainstSilentMisparse };
})(typeof window !== 'undefined' ? window : globalThis);
