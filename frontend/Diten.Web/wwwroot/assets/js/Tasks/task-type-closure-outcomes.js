/*
 * ── THE CLOSURE OUTCOME DICTIONARY EDITOR ───────────────────────────────────────────────────────────────────
 *
 * Add, remove, and keep each row's two label halves in agreement. Everything it needs is already in the DOM —
 * the catalogue's codes, keys, dispositions and default reason flags ride on the <option> elements the server
 * rendered — so this file holds no vocabulary of its own. A copy of the five system codes here would be a
 * second place for them to live, and the one in C# is the one bound to the seven translations.
 *
 * ⚠ WHY NOT `jquery.repeater`. Sneat's own repeater pages (forms-extras.html, app-invoice-add.html) drive
 * `data-repeater-list` with that plugin. MEASURED: it is not in wwwroot/vendor/libs and no view loads it, so
 * using its markup would have produced a static list that silently never added a row. The VISUAL shape is
 * borrowed from those pages; the behaviour is this.
 */
(function () {
    'use strict';

    var LIST = '[data-closure-outcome-list]';
    var ROW = '[data-closure-outcome-row]';
    var TEMPLATE = '[data-closure-outcome-template]';

    /*
     * A fresh binding key per added row.
     *
     * ⚠ NEVER THE ROW COUNT. Add three, delete the middle one, add another: a count-based key would reuse `r2`
     * and the model binder would collapse two rows into one, silently dropping whichever lost. A monotonic
     * counter seeded past whatever the server rendered cannot collide with those either.
     */
    var nextKey = (function () {
        var n = Date.now() % 100000;
        return function () { n += 1; return 'n' + n; };
    })();

    function rowsIn(list) {
        return Array.prototype.slice.call(list.querySelectorAll(ROW));
    }

    /** The empty-state sentence appears only when there is genuinely nothing — "asks nothing" is a real state. */
    function syncEmptyState(section) {
        var list = section.querySelector(LIST);
        var empty = section.querySelector('[data-closure-outcome-empty]');
        if (list && empty) {
            empty.classList.toggle('d-none', rowsIn(list).length > 0);
        }
    }

    /**
     * One row's label halves, after its source changed.
     *
     * ⚠ THE TEXT BOX IS CLEARED, NOT MERELY HIDDEN. The server refuses an outcome carrying BOTH a resource key
     * and its own text (OutcomeLabelAmbiguousMessage), and a hidden input still posts — so leaving the words a
     * user typed behind a chosen system outcome would turn Save into a 400 they cannot see the cause of.
     */
    function applySource(row) {
        var source = row.querySelector('[data-closure-outcome-source]');
        if (!source) { return; }

        var option = source.options[source.selectedIndex];
        var isSystem = !!(option && option.value);

        var key = row.querySelector('[data-closure-outcome-key]');
        var code = row.querySelector('[data-closure-outcome-code]');
        var systemLabel = row.querySelector('[data-closure-outcome-system-label]');
        var text = row.querySelector('[data-closure-outcome-text]');
        var disposition = row.querySelector('[data-closure-outcome-disposition]');
        var requiresReason = row.querySelector('[data-closure-outcome-requires-reason]');

        if (isSystem) {
            if (key) { key.value = option.getAttribute('data-resource-key') || ''; }
            if (code) {
                code.value = option.value;
                // readonly, never disabled: a disabled input posts nothing and this code is what gets stored.
                code.readOnly = true;
            }
            if (systemLabel) {
                systemLabel.value = option.getAttribute('data-label') || option.value;
                systemLabel.classList.remove('d-none');
            }
            if (text) {
                text.value = '';
                text.classList.add('d-none');
            }
            /*
             * The catalogue's values are DEFAULTS, applied on selection and editable afterwards. The server
             * stores whatever the type says, so pinning them here would be this screen enforcing a rule the
             * engine does not have — and the two would disagree the first time the catalogue changed.
             */
            if (disposition) { disposition.value = option.getAttribute('data-disposition') || 'Completed'; }
            if (requiresReason) {
                requiresReason.checked = option.getAttribute('data-requires-reason') === 'true';
            }
            return;
        }

        // Back to a tenant outcome: the key goes, the code becomes the author's to type, the text box returns.
        if (key) { key.value = ''; }
        if (code) { code.readOnly = false; }
        if (systemLabel) {
            systemLabel.value = '';
            systemLabel.classList.add('d-none');
        }
        if (text) { text.classList.remove('d-none'); }
    }

    function addRow(section) {
        var list = section.querySelector(LIST);
        var template = section.querySelector(TEMPLATE);
        if (!list || !template) { return; }

        /*
         * `__key__` is replaced across the WHOLE row markup, not per input: the binding key appears in
         * `ClosureOutcomes.Index` and in five `ClosureOutcomes[key].Field` names, and a replacement that missed
         * one would produce a row whose fields bound to two different indexes.
         */
        var markup = template.innerHTML.split('__key__').join(nextKey());
        var holder = document.createElement('div');
        holder.innerHTML = markup;

        var row = holder.querySelector(ROW);
        if (!row) { return; }

        list.appendChild(row);
        applySource(row);
        syncEmptyState(section);

        var code = row.querySelector('[data-closure-outcome-code]');
        if (code) { code.focus(); }
    }

    function bind(section) {
        if (section.dataset.closureOutcomesBound === 'true') { return; }
        section.dataset.closureOutcomesBound = 'true';

        /*
         * DELEGATED, so rows added after load behave exactly like the ones the server rendered. Binding each row
         * at startup is how a clone ends up inert — the defect that makes an Add button look broken.
         */
        section.addEventListener('click', function (event) {
            if (event.target.closest('[data-closure-outcome-add]')) {
                event.preventDefault();
                addRow(section);
                return;
            }

            var remove = event.target.closest('[data-closure-outcome-remove]');
            if (remove) {
                event.preventDefault();
                var row = remove.closest(ROW);
                if (row) { row.remove(); }
                /*
                 * No renumbering, and that is what `ClosureOutcomes.Index` buys: the surviving rows keep their
                 * keys, so nothing the user typed has to be re-rendered to close the gap.
                 */
                syncEmptyState(section);
            }
        });

        section.addEventListener('change', function (event) {
            var source = event.target.closest('[data-closure-outcome-source]');
            if (source) {
                var row = source.closest(ROW);
                if (row) { applySource(row); }
            }
        });

        syncEmptyState(section);
    }

    document.addEventListener('DOMContentLoaded', function () {
        Array.prototype.forEach.call(
            document.querySelectorAll('[data-closure-outcome-list]'),
            function (list) {
                var section = list.closest('section') || list.parentElement;
                if (section) { bind(section); }
            });
    });
})();
