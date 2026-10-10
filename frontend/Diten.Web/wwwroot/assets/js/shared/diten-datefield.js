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
    /*
     * WCN-FIX-04 — does the typed text MEAN the date flatpickr resolved? Compared by COMPONENT, not by text.
     *
     * The display formats come from the culture's ShortDatePattern, and five of the seven tenant languages have
     * unpadded parts (measured, .NET: en M/d/yyyy → n/j/Y, tr d.MM.yyyy → j.m.Y, es d/M/yyyy → j/n/Y, zh yyyy/M/d →
     * Y/n/j, ar d‏/M‏/yyyy → j‏/n‏/Y with RIGHT-TO-LEFT MARKs around the separators). A text comparison refused
     * "05/03/2026" where the format writes "5/3/2026", and refused EVERY date typed in Arabic (nobody types the
     * invisible marks). So: the numbers are read from the typed text in the order of the format's numeric tokens and
     * each must equal the resolved date's own component. An overflow ("31.02" → 03.03) still differs and is refused.
     * A 4-digit year token takes 4 digits ("26" is not 2026, and must not silently become year 26).
     * A format with text tokens (month / weekday names, AM/PM, …) falls back to the exact text comparison.
     */
    const NUMERIC_TOKENS = { d: 'day', j: 'day', m: 'month', n: 'month', Y: 'year', y: 'year2', H: 'hour', G: 'hour',
        h: 'hour', i: 'minute', S: 'second', s: 'second' };
    const TEXT_TOKENS = 'DlMFKUZwW';
    const typedReadsBack = (raw, resolved, fmt, instance) => {
        const format = String(fmt || '');
        const tokens = [];
        let hasText = false;
        for (let i = 0; i < format.length; i += 1) {
            const ch = format[i];
            if (ch === '\\') { i += 1; continue; }
            if (NUMERIC_TOKENS[ch]) { tokens.push(ch); } else if (TEXT_TOKENS.indexOf(ch) >= 0) { hasText = true; }
        }
        if (hasText || tokens.length === 0) {
            return !!(instance && instance.formatDate(resolved, format) === raw);
        }
        if (/\p{L}/u.test(raw)) { return false; }
        const numbers = raw.match(/\d+/g) || [];
        if (numbers.length !== tokens.length) { return false; }
        return tokens.every((token, index) => {
            const text = numbers[index];
            const value = Number(text);
            switch (NUMERIC_TOKENS[token]) {
                case 'day': return value === resolved.getDate();
                case 'month': return value === resolved.getMonth() + 1;
                case 'year': return text.length === 4 && value === resolved.getFullYear();
                case 'year2': return text.length <= 2 && value === resolved.getFullYear() % 100;
                case 'hour': return token === 'h' ? value === (resolved.getHours() % 12 || 12) : value === resolved.getHours();
                case 'minute': return value === resolved.getMinutes();
                default: return value === resolved.getSeconds();
            }
        });
    };

    const guardAgainstSilentMisparse = (instance, formatKey) => {
        // A real flatpickr instance always has both; a minimal test double (or a future caller that hands this
        // an incomplete object) does not, and the SAME "nothing to enhance, quietly skip" posture `enhance`
        // itself already takes below applies here too — never throw out of a setup path.
        const target = instance?.altInput || instance?.input;
        if (!target || !Array.isArray(instance.config?.onClose) || typeof instance.formatDate !== 'function') {
            return false;
        }
        // WCN-FIX-03 — guarding twice would bind every listener twice (two judgments, two clears).
        if (instance._ditenGuarded) { return true; }
        instance._ditenGuarded = true;

        /*
         * BL-588 (WP-WCN-FIX-01) — ONLY text TYPED by the reader is judged; a calendar pick never is.
         *
         * MEASURED (jsdom + the real vendored flatpickr 4.6.13): flatpickr's selectDate runs updateValue →
         * close (onClose) → and only then onChange / `input`. The guard used to compare the pick with a stale copy
         * of the box, so a field that already held a value lost its first pick.
         *
         * WCN-FIX-02 — WHAT RESETS THE TYPED TEXT, AND WHAT MUST NOT.
         *  - A pick FROM THE CALENDAR resets it: a click on a selectable day (capture phase, before flatpickr's own
         *    selectDate → close) and Enter on a focused day. A disabled / not-allowed day selects nothing, so it
         *    resets nothing; week-number cells carry `flatpickr-day` too (measured: no other class), so a "day" is
         *    one inside `daysContainer`.
         *  - onValueUpdate must NOT reset it: flatpickr fires it from updateValue BEFORE the close on every path
         *    that commits typed text (Enter, Tab, enableTime outside click).
         *  - flatpickr's own `input` event, dispatched right after onChange with the NORMALISED value, must not
         *    overwrite what the reader typed. `isTrusted` cannot tell the two apart (scripted input is untrusted
         *    too), so the echo is recognised by its cause: onChange marks the next `input` as the library's own.
         *    The echo goes to `instance.input` only — on an altInput field it never reaches the box the reader types
         *    in, so the mark is set only where it will be consumed. The mark expires at the end of the task: a
         *    later onChange hook that throws stops flatpickr before it dispatches the echo, and an unexpired mark
         *    would then swallow the reader's next typing (measured in the tests).
         *
         * WCN-FIX-03 — THE CLOSED CALENDAR. The judgment used to run only in onClose. But the box keeps focus with
         * the calendar CLOSED after every pick (selectDate → focusAndClose) and after Escape, and flatpickr's
         * onBlur still commits what is typed there (setDate(value, true, fmt)) without any close. So the judgment
         * is one function, called from onClose AND from a `blur` listener on the box while the calendar is not
         * open (bound after flatpickr's own, so onBlur has already committed). Blur with the calendar open is left
         * to the close that follows (the focus moving away closes it).
         *  - onOpen resets the typed text only when the box no longer shows it (code rewrote the box without an
         *    event): text the reader typed into the closed box is still theirs to be judged when it closes.
         *  - Escape inside the open calendar CANCELS the typing: nothing is judged, the box is rewritten from the
         *    dates that were already selected (the previous valid value stays).
         *  - Moving into the time inputs (enableTime) first commits and judges what was typed in the box: a valid
         *    "2026-02-20 10:00" followed by an hour change is then not read as a misparse at close, and a typo is
         *    still caught (resetting there instead would let documentClick commit the typo unjudged).
         */
        let lastTyped = '';
        let libraryEcho = false;
        let cancelled = false;
        // A refused text stays refused until the reader types again, picks a day, or the PAGE writes the field.
        let rejected = false;
        // The time inputs were used after a refusal: they fill in "today" on an empty enableTime field.
        let timeEditedSinceReject = false;
        // WCN-FIX-04 — what the field held when the reader's typing run began; Escape puts THIS back, never the
        // live value (flatpickr may already have committed the misparse on a blur, e.g. alt-tab).
        let restorePoint = null;
        // The guard's own setDate / clear calls, and calls made from inside one (flatpickr's setDate → clear).
        let own = 0;
        let depth = 0;
        let markedByGuard = false;
        const echoReachesTarget = target === instance.input;
        const format = () => instance.config[formatKey];
        const ownCall = (fn) => { own += 1; try { return fn(); } finally { own -= 1; } };

        const mark = () => { markedByGuard = true; target.classList.add('is-invalid'); };
        const unmark = () => {
            if (!markedByGuard) { return; } // a mark the PAGE set (its own required check) is the page's
            markedByGuard = false;
            target.classList.remove('is-invalid');
        };

        const judge = (selectedDates) => {
            const raw = lastTyped.trim();
            lastTyped = '';
            if (!raw) { settle(selectedDates); return; }
            const resolved = (selectedDates || [])[0];
            if (!resolved || !typedReadsBack(raw, resolved, format(), instance)) {
                // Marked BEFORE the clear: clear() runs the page's onChange hooks, and one that throws must not
                // leave an emptied field that does not say why.
                rejected = true;
                timeEditedSinceReject = false;
                mark();
                ownCall(() => instance.clear());
            } else {
                rejected = false;
                restorePoint = null;
                markedByGuard = false;
                target.classList.remove('is-invalid');
            }
        };

        // Nothing typed to judge: a refused field stays empty and marked (emptied again if the time inputs filled in
        // "today" since); anything else loses a mark the guard set — an empty optional field is not invalid.
        const settle = (selectedDates) => {
            if (rejected) {
                if (timeEditedSinceReject && (selectedDates || []).length > 0) { ownCall(() => instance.clear()); }
                timeEditedSinceReject = false;
                mark();
                return;
            }
            unmark();
        };

        // Escape: the reader's typing is withdrawn and the field shows what it held before that typing began.
        const cancelTyping = () => {
            lastTyped = '';
            const back = restorePoint;
            restorePoint = null;
            if (!back) { return; }
            rejected = false;
            timeEditedSinceReject = false;
            unmark();
            ownCall(() => instance.setDate(back.slice(), false));
        };

        // WCN-FIX-04 — a value the PAGE writes (quick-create's default, a saved view's filter, a reset) ends the
        // reader's pending typing and any refusal: it is not the reader's text, and judging or re-clearing it would
        // delete a value nobody typed. flatpickr's own commits of the typed text pass the box's text itself
        // (onBlur, Enter, documentClick: setDate(self._input.value, …)); those are not the page's.
        const pageWrote = () => {
            lastTyped = '';
            restorePoint = null;
            rejected = false;
            timeEditedSinceReject = false;
            unmark();
        };
        if (typeof instance.setDate === 'function' && typeof instance.clear === 'function') {
            const nativeSetDate = instance.setDate;
            const nativeClear = instance.clear;
            instance.setDate = function (date, ...rest) {
                if (own === 0 && depth === 0 && !(typeof date === 'string' && date === target.value)) { pageWrote(); }
                depth += 1;
                try { return nativeSetDate.call(this, date, ...rest); } finally { depth -= 1; }
            };
            instance.clear = function (...args) {
                if (own === 0 && depth === 0) { pageWrote(); }
                depth += 1;
                try { return nativeClear.apply(this, args); } finally { depth -= 1; }
            };
        }

        target.addEventListener('input', () => {
            if (libraryEcho) { libraryEcho = false; return; }
            if (restorePoint === null) { restorePoint = (instance.selectedDates || []).slice(); }
            lastTyped = target.value;
            rejected = false;
            timeEditedSinceReject = false;
        });
        if (echoReachesTarget && Array.isArray(instance.config.onChange)) {
            instance.config.onChange.push(() => {
                libraryEcho = true;
                Promise.resolve().then(() => { libraryEcho = false; });
            });
        }
        if (Array.isArray(instance.config.onOpen)) {
            instance.config.onOpen.push(() => {
                cancelled = false;
                if (target.value.trim() !== lastTyped.trim()) { lastTyped = ''; }
            });
        }

        target.addEventListener('blur', (event) => {
            if (!instance.isOpen) { judge(instance.selectedDates); return; }
            // Open calendar: focus moving INTO it is not leaving the field.
            const to = event.relatedTarget;
            if (to && instance.calendarContainer && instance.calendarContainer.contains(to)) { return; }
            /*
             * WCN-FIX-04 — open calendar, focus gone elsewhere. Usually the focus landing outside closes it
             * (documentClick) and onClose judges. But a window switch (alt-tab: no relatedTarget) or, on a touch-capable
             * desktop where flatpickr listens to `touchstart` instead of `mousedown`, a mouse click on something that
             * takes no focus, closes nothing — while flatpickr's onBlur HAS committed the typed text. So the judgment is
             * deferred one task: if the calendar is still open and the text is still unjudged, it is judged then.
             */
            global.setTimeout(() => {
                if (instance.isOpen && lastTyped.trim() && global.document && global.document.activeElement !== target) {
                    judge(instance.selectedDates);
                }
            }, 0);
        });
        // Escape in the box cancels the typing too (flatpickr ignores Escape in an allowInput box).
        target.addEventListener('keydown', (event) => {
            if (event.key !== 'Escape' || (!lastTyped.trim() && !(rejected && restorePoint))) { return; }
            if (instance.isOpen) { cancelled = true; instance.close(); } else { cancelTyping(); }
        });

        const calendar = instance.calendarContainer;
        if (calendar && typeof calendar.addEventListener === 'function') {
            const fromDay = (event) => {
                const day = event.target && typeof event.target.closest === 'function'
                    && event.target.closest('.flatpickr-day:not(.flatpickr-disabled):not(.notAllowed)');
                return !!(day && instance.daysContainer && instance.daysContainer.contains(day));
            };
            const picked = () => { lastTyped = ''; restorePoint = null; rejected = false; timeEditedSinceReject = false; };
            calendar.addEventListener('click', (event) => { if (fromDay(event)) { picked(); } }, true);
            calendar.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' && fromDay(event)) { picked(); }
                if (event.key === 'Escape' && instance.isOpen) { cancelled = true; }
            }, true);
            calendar.addEventListener('focusin', (event) => {
                const time = instance.timeContainer;
                if (!time || !time.contains(event.target)) { return; }
                if (lastTyped.trim()) {
                    ownCall(() => instance.setDate(lastTyped, true, format()));
                    judge(instance.selectedDates);
                }
                // The reader is in the time inputs now; after a refusal, what they fill in is not a chosen date.
                if (rejected) { timeEditedSinceReject = true; }
            });
        }

        instance.config.onClose.push((selectedDates) => {
            if (cancelled) {
                cancelled = false;
                cancelTyping();
                if (rejected) { settle(instance.selectedDates); }
                return;
            }
            judge(selectedDates);
        });
        return true;
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
    global.DitenDateField = { enhance, guardAgainstSilentMisparse, typedReadsBack };
})(typeof window !== 'undefined' ? window : globalThis);
