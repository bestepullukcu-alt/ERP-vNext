// ─────────────────────────────────────────────────────────────────────────────
// DitenCheckItem — ONE checklist row, TWO modes.
//
// The same checklist item was drawn twice, by two files, in two vocabularies:
// `task-checklist-*` in the create form and `wcn-check*` on the task detail
// page. They had drifted exactly as far as you would expect. The create form
// could set a level, flag evidence, reorder and remove; the detail page could
// tick a box and nothing else — and its paperclip was not a control at all, only
// a mark saying a decision had been made somewhere the reader could not reach.
//
// Copying one screen's markup into the other would have closed the gap for a
// day. This session has already paid that bill twice: the tag box was drawn in
// two bundles until they disagreed, and four separate visual defects turned out
// to be one class name colliding with a component nobody had opened in months.
// So the row is built ONCE, here, and the two screens differ only in which mode
// they ask for.
//
//   authoring (create): move · text · level · evidence · remove
//   working   (detail): tick · text · level · evidence · move · remove
//
// The tick is the only control the create form lacks, and for a plain reason:
// there is no task yet to have made progress on.
//
// WHY THIS RETURNS A NODE, NOT A STRING. The detail page builds HTML strings and
// can take `.outerHTML` from what it gets back; the create form needs a node.
// Serving the node keeps `textContent` doing the escaping for the one field that
// carries what a user typed, instead of an `esc()` helper each caller has to
// remember to use.
//
// Styles live in backbone-custom.css under `.diten-checkitem*` (FG-003): this
// file writes classes, attributes and text, never a style attribute. Strings
// arrive through the `t` function each screen already has, so neither screen's
// resources are made to depend on the other's.
//
// Usage:
//   DitenCheckItem.row({ id, text, requirement, evidenceRequired, done,
//                        templateOwned }, { mode, t, readOnly, first, last })
//   DitenCheckItem.applyMoveState(listElement)
// ─────────────────────────────────────────────────────────────────────────────
(function (global) {
    const ROOT = 'diten-checkitem';

    /*
     * The three levels, resolved by literal calls rather than `t('level' + requirement)`.
     *
     * The l10n guard scans callers for literal `t('…')` keys and checks each one against the serialized bridge
     * payload. A key assembled from a variable is invisible to it, so a missing translation reaches a user
     * instead of a test. Each screen passes its own already-resolved labels in for exactly that reason.
     */
    const levelLabel = (requirement, labels) =>
        requirement === 'Blocking' ? labels.blocking
            : requirement === 'Required' ? labels.required
                : labels.optional;

    const button = (className, dataAttr, dataValue, label, glyph, pressed) => {
        const el = document.createElement('button');
        el.type = 'button';
        el.className = className;
        el.setAttribute(dataAttr, dataValue);
        if (label) {
            el.title = label;
            el.setAttribute('aria-label', label);
        }
        if (pressed !== undefined) { el.setAttribute('aria-pressed', pressed ? 'true' : 'false'); }
        const icon = document.createElement('i');
        icon.className = 'bx ' + glyph;
        el.appendChild(icon);
        return el;
    };

    /**
     * One row.
     *
     * @param {object} item {id, text, requirement, evidenceRequired, done, templateOwned}
     * @param {object} options {mode:'authoring'|'working', labels, readOnly}
     * @returns {HTMLLIElement}
     */
    const row = (item, options) => {
        const labels = options.labels;
        const working = options.mode === 'working';
        const ro = !!options.readOnly;

        const el = document.createElement('li');
        el.className = ROOT + (working && item.done ? ' done' : '');
        el.setAttribute('data-diten-check-row', item.id === undefined ? '' : String(item.id));
        el.setAttribute('data-requirement', item.requirement || 'Optional');
        el.setAttribute('data-evidence', item.evidenceRequired ? '1' : '');
        /*
         * A template item's TEXT is not the holder's to reword — the server refuses it with its own reason code
         * (CHECKLIST_ITEM_TEMPLATE_OWNED), and the row says so before anyone tries. Its level and its evidence
         * flag are NOT marked: those describe how strictly this particular task is being run, which is the
         * judgement the person holding it is there to make.
         */
        if (item.templateOwned) { el.setAttribute('data-template-owned', '1'); }

        if (working) {
            const box = button(
                ROOT + '-box', 'data-diten-check-toggle', String(item.id),
                labels.toggle, item.done ? 'bxs-check-square' : 'bx-square', item.done);
            if (ro) { box.disabled = true; }
            el.appendChild(box);
        } else {
            /*
             * THE GRIP — the mouse path, and only the mouse path.
             *
             * `aria-hidden` with no tab stop on purpose: it is a surface to grab, not a second control.
             * Announcing a handle that Enter cannot operate promises an interaction that does not exist. The
             * move buttons below are what a keyboard uses.
             */
            const grip = document.createElement('span');
            grip.className = ROOT + '-grip';
            grip.setAttribute('data-diten-check-grip', '');
            grip.setAttribute('aria-hidden', 'true');
            const gripIcon = document.createElement('i');
            gripIcon.className = 'bx bx-grid-vertical';
            grip.appendChild(gripIcon);
            el.appendChild(grip);
        }

        /*
         * MOVE, as two buttons.
         *
         * NOT a fallback for the drag. WCAG 2.2 §2.5.7 requires a single-pointer alternative to every dragging
         * movement and these are it; they are also the entire keyboard story, because Sortable has none.
         * Wherever a row can be dragged, these must exist — and on the working side, where there is no drag at
         * all yet, they are the only way to reorder anything.
         */
        const move = document.createElement('span');
        move.className = ROOT + '-move';
        [['up', 'bx-chevron-up', labels.moveUp], ['down', 'bx-chevron-down', labels.moveDown]]
            .forEach(([direction, glyph, label]) => {
                const btn = button(
                    ROOT + '-btn', 'data-diten-check-move', direction, label, glyph, undefined);
                if (ro) {
                    btn.disabled = true;
                    // MARKED, not merely disabled. `applyMoveState` re-derives `disabled` from position on every
                    // reorder, and would hand a closed task's arrows straight back if the only record of the
                    // read-only decision were the property it is about to overwrite.
                    btn.setAttribute('data-diten-check-readonly', '');
                }
                move.appendChild(btn);
            });

        const text = document.createElement('span');
        text.className = ROOT + '-text';
        // textContent, not innerHTML — this is the line a person typed.
        text.textContent = item.text;

        /*
         * THE LEVEL, as a chip that cycles. Three states on one control rather than a select, because the row
         * has to stay one line and the level is read far more often than it is changed.
         *
         * The chip always NAMES the current level, including Optional. Showing only the strict states would
         * leave Optional as a blank, and a blank reads as "not set" rather than as a choice someone made.
         */
        const level = document.createElement('button');
        level.type = 'button';
        level.className = ROOT + '-level';
        level.setAttribute('data-diten-check-level', item.id === undefined ? '' : String(item.id));
        level.textContent = levelLabel(item.requirement, labels);
        level.title = labels.levelHint;
        if (ro) { level.disabled = true; }

        /*
         * EVIDENCE — a BUTTON on both screens now, where the detail page had only a mark.
         *
         * It still attaches nothing. It writes the flag that says this item WILL want evidence; the attachment
         * itself belongs to MOD-0031 and is BL-080. That is why the sentence under the list stays: a paperclip
         * that opens no file picker has to be explained by something, and a flag shown as a button that silently
         * does nothing is the same "stored but inert" defect wearing a nicer shirt.
         */
        const evidence = button(
            ROOT + '-btn ' + ROOT + '-evidence', 'data-diten-check-evidence',
            item.id === undefined ? '' : String(item.id),
            labels.evidenceToggle, 'bx-paperclip', !!item.evidenceRequired);
        if (ro) { evidence.disabled = true; }

        const remove = button(
            ROOT + '-btn ' + ROOT + '-remove', 'data-diten-check-remove',
            item.id === undefined ? '' : String(item.id), labels.remove, 'bx-x', undefined);
        if (ro) { remove.disabled = true; }

        // The one ordering difference between the modes, and the reason the mode exists at all: authoring reads
        // as "arrange these", working reads as "tick these off", so what comes first differs.
        if (working) {
            el.appendChild(text);
            el.appendChild(level);
            el.appendChild(evidence);
            el.appendChild(move);
            el.appendChild(remove);
        } else {
            el.appendChild(move);
            el.appendChild(text);
            el.appendChild(level);
            el.appendChild(evidence);
            el.appendChild(remove);
        }

        return el;
    };

    /**
     * Puts every row's move controls into the state its POSITION justifies.
     *
     * <p>Separate from rendering, and called after every reorder, because reordering must NOT rebuild the rows:
     * a rebuild detaches the node under the pointer and strands the second press — the defect a live click found
     * on the level chip earlier in this module's life. So the DOM moves, and this re-derives what the move
     * controls should say about it.</p>
     *
     * <p>Three states, all read off the list rather than remembered:</p>
     * <ul>
     *   <li><b>One row</b> — no grip, no arrows. There is nowhere to move it, and a control that can only ever
     *       refuse is worse than no control.</li>
     *   <li><b>First row</b> — up disabled. <b>Last row</b> — down disabled.</li>
     *   <li>Anything between — both live.</li>
     * </ul>
     *
     * <p>A disabled button is also out of the tab order, which is the point: tabbing onto a control that cannot
     * act is the same dead end as clicking it.</p>
     */
    const applyMoveState = (list) => {
        if (!list) { return; }
        const rows = [...list.querySelectorAll('[data-diten-check-row]')];
        const single = rows.length < 2;
        rows.forEach((el, index) => {
            // HIDDEN, not merely disabled, when there is only one row: a disabled arrow still says "this list can
            // be reordered", which is a promise a one-item list cannot keep. The whole control goes, not each
            // button — leaving the empty container behind keeps its gap in the row.
            el.querySelector('[data-diten-check-grip]')?.classList.toggle('d-none', single);
            el.querySelector('.' + ROOT + '-move')?.classList.toggle('d-none', single);

            el.querySelectorAll('[data-diten-check-move]').forEach((btn) => {
                const direction = btn.getAttribute('data-diten-check-move');
                const atEnd = direction === 'up' ? index === 0 : index === rows.length - 1;
                // `disabled` rather than a class: it removes the tab stop too, which a class cannot do. Tabbing
                // onto a control that cannot act is the same dead end as clicking it.
                btn.disabled = single || atEnd || btn.hasAttribute('data-diten-check-readonly');
            });
        });
    };

    global.DitenCheckItem = { row, applyMoveState, ROOT };
}(window));
