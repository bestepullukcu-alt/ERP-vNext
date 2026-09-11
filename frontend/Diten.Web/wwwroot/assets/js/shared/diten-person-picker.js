'use strict';

/*
 * DitenPersonPicker — the avatar+name(+unit) picker row, the grouped-<select> filler it rides on, and the
 * select2 searchable-dropdown adapter every screen that opens one of these needed its own copy of, so a third
 * and fourth hand-rolled copy of any of them never gets written (WP-WC-SHARED-UI-01, E2).
 *
 * Moved here verbatim from wwwroot/assets/js/Tasks/form.js, which had the only fully-worked copy (avatar,
 * grouping-by-unit-with-disambiguation, the empty-picker hint). `window.TaskForm`'s own exports
 * (personInitials/personOptionNode/personSelectionNode/renderPersonOptions/formatPersonLabel/pickerRowTemplate)
 * now delegate here rather than duplicating the bodies — nothing scans Tasks/form.js's own source text for
 * these declarations (only WorkCenterNext's dialog adapter is pinned that way; see shared/diten-dialog.js's
 * own top comment for that exception), so the move is a plain, safe delegation.
 *
 * `rowNode`, `joinParts` and the grouped-fill pair (`fillGroupedOptions`/`applyPlaceholder`) are exported too,
 * even though none of them is "a person" by itself: Tasks/form.js's POSITION/seat picker (which has no other
 * consumer today and stays local to that file) is built from the exact same row shape and the exact same
 * grouped-<select> filling, and a second copy of either would recreate the problem this file exists to close.
 */
(function (global) {
    /*
     * The circle. Two words or more → first and LAST initial, so a middle name never wins. ONE word → its first
     * two letters, because a lone glyph reads as a rendering error rather than a monogram. No name at all → a
     * neutral "?", never a letter borrowed from the id (BL-049: the id is not shown, not even one character).
     */
    const personInitials = (name) => {
        const words = String(name || '').trim().split(/\s+/).filter((word) => word.length > 0);
        if (words.length === 0) { return '?'; }
        if (words.length === 1) { return words[0].slice(0, 2).toLocaleUpperCase(); }
        return (words[0][0] + words[words.length - 1][0]).toLocaleUpperCase();
    };

    // "Muhasebe Md · Finans" — a middot, not an em dash: the dash was carrying the same weight as the words.
    const SECONDARY_SEPARATOR = ' · ';

    const joinParts = (parts) => parts
        .map((part) => (part === null || part === undefined ? '' : String(part).trim()))
        .filter((part) => part.length > 0)
        .join(SECONDARY_SEPARATOR);

    const rowNode = (avatar, primary, secondary) => {
        const doc = global.document;
        const root = doc.createElement('span');
        root.className = secondary === null ? 'diten-opt diten-opt--single' : 'diten-opt';
        root.appendChild(avatar);

        const body = doc.createElement('span');
        body.className = 'diten-opt-body';

        const primaryLine = doc.createElement('span');
        primaryLine.className = 'diten-opt-primary';
        primaryLine.textContent = primary;
        body.appendChild(primaryLine);

        if (secondary !== null) {
            const secondaryLine = doc.createElement('span');
            secondaryLine.className = 'diten-opt-secondary';
            secondaryLine.textContent = secondary;
            body.appendChild(secondaryLine);
        }

        root.appendChild(body);
        return root;
    };

    const initialsAvatar = (name) => {
        const avatar = global.document.createElement('span');
        avatar.className = 'diten-opt-avatar';
        avatar.textContent = personInitials(name);
        return avatar;
    };

    const personName = (row, labels) => row.displayName || (labels && labels.nameUnavailable) || '';

    const personOptionNode = (row, labels) => {
        const name = personName(row, labels);
        return rowNode(
            initialsAvatar(row.displayName),
            name,
            joinParts([row.positionName || row.positionCode, row.organizationUnitName || row.organizationUnitCode]));
    };

    // Chosen → one line. The closed control is 38px; a second line would grow it, and the unit is the part the
    // user has already decided about, so it is what the collapsed line drops.
    const personSelectionNode = (row, labels) => rowNode(
        initialsAvatar(row.displayName),
        joinParts([personName(row, labels), row.positionName || row.positionCode]),
        null);

    /*
     * WHICH GROUP a row belongs to. The owner's question was "three factories under me — do their people not
     * blur together?", and in a flat list they do: two facilities can each have a "Üretim Şefi" and only the
     * trailing unit label told them apart. So rows sit under their unit.
     *
     * A unit NAME can repeat across companies, and two headings reading "Üretim" are worse than none — they
     * say the two lists are one. Where the name is ambiguous the unit CODE disambiguates it. The code is real
     * data; the COMPANY name would be the natural label and Platform does not have it (see the note in
     * renderPersonOptions), so nothing is invented here.
     */
    const groupLabels = (rows) => {
        const byId = new Map();
        rows.forEach((row) => {
            const id = String(row.organizationUnitId || '');
            if (!byId.has(id)) {
                byId.set(id, {
                    name: row.organizationUnitName || row.organizationUnitCode || '',
                    code: row.organizationUnitCode || ''
                });
            }
        });

        const nameCounts = new Map();
        byId.forEach((unit) => nameCounts.set(unit.name, (nameCounts.get(unit.name) || 0) + 1));

        const labelsById = new Map();
        byId.forEach((unit, id) => {
            const ambiguous = (nameCounts.get(unit.name) || 0) > 1 && unit.code;
            labelsById.set(id, ambiguous ? `${unit.name} (${unit.code})` : unit.name);
        });
        return labelsById;
    };

    /*
     * Fill a select with grouped options.
     *
     * The option's TEXT stays the full one-line label on purpose: select2 searches the option text, so a user
     * typing "Finans" must still match a row whose unit only appears on the second layer. What changed is how
     * the line is DISPLAYED, not what it says.
     *
     * The row object rides on the option (`option.ditenRow`) so the template never has to re-parse a label back
     * into fields.
     */
    const fillGroupedOptions = (selectEl, rows, kind, valueOf, textOf) => {
        const doc = global.document;
        const byId = groupLabels(rows);
        const groups = new Map();

        selectEl.setAttribute('data-diten-rows', kind);

        rows.forEach((row) => {
            const id = String(row.organizationUnitId || '');
            let group = groups.get(id);
            if (!group) {
                group = doc.createElement('optgroup');
                group.label = byId.get(id) || '';
                groups.set(id, group);
                selectEl.appendChild(group);
            }

            const option = doc.createElement('option');
            option.value = valueOf(row);
            option.textContent = textOf(row);
            option.ditenRow = row;
            group.appendChild(option);
        });
    };

    /*
     * THE PLACEHOLDER IS NOT A ROW.
     *
     * It used to be an <option> carrying "Kişi seçin…", so it appeared inside the dropdown as a selectable line
     * — and choosing it emptied the field. select2 takes a placeholder of its own and removes the matching
     * empty option from the results by id, so the prompt shows in the closed control and nowhere else. The
     * option still has to EXIST (select2 requires a blank first option on a single select) but it carries no
     * text: even if something rendered it, it could not read as a choice.
     */
    const applyPlaceholder = (selectEl, text, multiple) => {
        if (multiple) { return; }
        selectEl.setAttribute('data-placeholder', text || '');
        const blank = global.document.createElement('option');
        blank.value = '';
        blank.textContent = '';
        selectEl.appendChild(blank);
    };

    /*
     * A person label MUST carry position AND unit: two people holding "QA Specialist" in different facilities are
     * otherwise indistinguishable — the position picker's trap, transposed onto people.
     * The user id is NEVER shown; when the name cannot be resolved the caller supplies a fallback label.
     */
    const formatPersonLabel = (row, nameUnavailableLabel) => {
        if (!row) { return ''; }
        const name = row.displayName || nameUnavailableLabel || '';
        const parts = [name, row.positionName || row.positionCode, row.organizationUnitName || row.organizationUnitCode];
        return parts.filter((part) => part && String(part).trim().length > 0).join(' — ');
    };

    /*
     * ── THE WAY OUT OF AN EMPTY PICKER (2026-09-02) ──────────────────────────────────────────────────────
     *
     * The empty state is a disabled `<option>`, and an option renders TEXT and nothing else — no anchor can
     * live inside one. So the sentence stays in the control and the way out is drawn beside it, as a
     * `.form-text`, which is the carrier `taskAssigneeExcluded` already uses to explain a SHORT list (BL-072).
     * One vocabulary for "why this picker does not offer what you expected".
     *
     * Built as NODES, never as an HTML string: the label is translated text and the href is a route, and
     * string-building is how either becomes markup.
     *
     * ⚠ REMOVED AGAIN THE MOMENT THE LIST FILLS. A "go and create positions" link left standing over a working
     * picker describes a state that has passed, which is the same kind of lie as the empty one that said
     * nothing at all.
     */
    const EMPTY_HINT_MARKER = 'data-task-empty-hint';

    const clearEmptyPickerHint = (selectEl) => {
        const field = selectEl.closest ? selectEl.closest('.diten-field') : null;
        const scope = (field && field.parentElement) || selectEl.parentElement;
        scope?.querySelectorAll?.(`[${EMPTY_HINT_MARKER}]`).forEach((node) => node.remove());
    };

    const renderEmptyPickerHint = (selectEl, text) => {
        clearEmptyPickerHint(selectEl);
        if (!text.emptyActionHref || !text.emptyActionLabel) { return; }

        // Anchored to the FIELD WRAPPER, not the control: select2 replaces the control's neighbourhood, and a
        // hint inserted next to a hidden <select> would land inside the part select2 owns.
        const field = selectEl.closest ? selectEl.closest('.diten-field') : null;
        const anchorNode = field || selectEl;
        if (!anchorNode.parentElement) { return; }

        const hint = global.document.createElement('div');
        hint.className = 'form-text';
        hint.setAttribute(EMPTY_HINT_MARKER, 'true');

        const link = global.document.createElement('a');
        link.setAttribute('href', text.emptyActionHref);
        link.textContent = text.emptyActionLabel;
        hint.appendChild(link);

        anchorNode.parentElement.insertBefore(hint, anchorNode.nextSibling);
    };

    /*
     * Fill a person <select>. An empty list is a REAL state — nobody in the tenant holds a position — and gets an
     * explanation rather than a silently empty dropdown the user cannot interpret.
     */
    const renderPersonOptions = (selectEl, rows, labels, options) => {
        if (!selectEl) { return; }
        const text = labels || {};
        const multiple = !!(options && options.multiple);
        selectEl.innerHTML = '';

        if (!rows || rows.length === 0) {
            const empty = global.document.createElement('option');
            empty.value = '';
            empty.textContent = text.empty || '';
            empty.disabled = true;
            empty.selected = true;
            selectEl.appendChild(empty);
            selectEl.disabled = true;
            renderEmptyPickerHint(selectEl, text);
            return;
        }

        selectEl.disabled = false;
        clearEmptyPickerHint(selectEl);
        // A MULTI-select gets no placeholder at all: in a list box a placeholder is just another selectable
        // line, and selecting it would post an empty identity among the real ones.
        applyPlaceholder(selectEl, text.placeholder, multiple);

        /*
         * Grouped by ORGANIZATION UNIT, and the company is deliberately absent from the heading.
         *
         * ⚠ MEASURED: AssignablePersonDto carries `LegalEntityId` (BL-057) and NO legal entity NAME — Platform
         * has none to give. Company names live in MDM (MOD-0220) and reach the browser through a proxy the
         * document module uses. Printing the id would be the GUID-on-screen defect (BL-049) and inventing a
         * name is not an option, so the heading says the unit. That also answers the question actually asked:
         * a "factory" IS an organization unit. If companies must head the groups later, the name has to come
         * from where names live, which is a cross-service resolver and its own piece of work.
         */
        fillGroupedOptions(
            selectEl,
            rows,
            'person',
            (row) => row.userId,
            (row) => formatPersonLabel(row, text.nameUnavailable));
    };

    /*
     * A row-shaped select2 result/selection template — the same dispatch Tasks/form.js's `pickerRowTemplate`
     * used to own, generalised over a caller-supplied row-kind table so a picker that is not "person" (Tasks'
     * own 'seat'/position picker, kept local to that file) can share the same three-case dispatch:
     *   - a GROUP heading (`children`) → returned as a STRING, so select2's own escaping applies to a label
     *     that is also tenant data;
     *   - a line with no row behind it (a search hint, say) → its plain text, unchanged;
     *   - a real row → the built node.
     * `rowBuilders` is `{ [kind]: { result, selection } }`; a caller with only person rows can pass
     * `{ person: { result: personOptionNode, selection: personSelectionNode } }`.
     */
    const pickerRowTemplate = (kind, labels, mode, rowBuilders) => {
        const builders = (rowBuilders || { person: { result: personOptionNode, selection: personSelectionNode } })[kind];
        const build = builders && (mode === 'selection' ? builders.selection : builders.result);
        return (data) => {
            if (!data) { return ''; }
            const row = data.element && data.element.ditenRow;
            if (!row || !build) { return data.text; }
            return build(row, labels);
        };
    };

    /*
     * select2 4.0.13 wires DropdownSearch only for single selects; composed here it also opens for a MULTI
     * select's dropdown — otherwise a tenant with dozens of eligible people has no way to find one by typing.
     * Built once and cached: Decorate() creates a class, and a fresh one per init would discard select2's own
     * event wiring on re-init. One shared instance for the whole page, same as each of its three prior copies
     * memoized for their own page.
     */
    let searchableDropdownAdapter = null;
    const buildSearchableDropdownAdapter = () => {
        if (searchableDropdownAdapter) { return searchableDropdownAdapter; }
        const amd = global.jQuery?.fn?.select2?.amd;
        if (!amd?.require) { return undefined; }
        try {
            const Dropdown = amd.require('select2/dropdown');
            const DropdownSearch = amd.require('select2/dropdown/search');
            const AttachBody = amd.require('select2/dropdown/attachBody');
            const Utils = amd.require('select2/utils');
            searchableDropdownAdapter = Utils.Decorate(Utils.Decorate(Dropdown, DropdownSearch), AttachBody);
            return searchableDropdownAdapter;
        } catch (e) {
            global.console?.warn?.('[DitenPersonPicker] dropdown search adapter unavailable; falling back to the default.', e);
            return undefined;
        }
    };

    global.DitenPersonPicker = {
        personInitials,
        joinParts,
        rowNode,
        initialsAvatar,
        personOptionNode,
        personSelectionNode,
        groupLabels,
        fillGroupedOptions,
        applyPlaceholder,
        formatPersonLabel,
        renderPersonOptions,
        pickerRowTemplate,
        buildSearchableDropdownAdapter
    };
})(typeof window !== 'undefined' ? window : globalThis);
