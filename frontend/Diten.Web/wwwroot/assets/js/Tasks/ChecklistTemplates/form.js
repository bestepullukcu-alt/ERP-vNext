'use strict';

/*
 * MOD-0024 checklist-template Create/Edit form (BL-054).
 *
 * The step editor, and its one hard constraint: ASP.NET model binding reads `Items[N].Field`, and the indexes
 * must stay a gapless 0..n-1 sequence. Deleting row 1 of three without renumbering silently truncates the post
 * at the gap — the server then receives one item where the user could see two, and saves it without complaint.
 * That is why every add and every remove is followed by a full reindex rather than by a cheaper local edit.
 */
(function () {
    const rowsHost = () => document.querySelector('[data-checklist-rows]');

    /**
     * Rewrites every row's name/id so the indexes are 0..n-1 with no gaps.
     *
     * `data-valmsg-for` and `data-valmsg-replace` are rewritten too: unobtrusive validation keys its messages on
     * the field name, so a row whose input was renumbered but whose message anchor was not would show another
     * row's error beside it.
     */
    const reindex = () => {
        const host = rowsHost();
        if (!host) { return; }

        Array.from(host.querySelectorAll('[data-checklist-row]')).forEach((row, index) => {
            row.querySelectorAll('input, select, textarea, span').forEach((el) => {
                ['name', 'id', 'data-valmsg-for'].forEach((attribute) => {
                    const value = el.getAttribute(attribute);
                    if (!value) { return; }
                    el.setAttribute(attribute, value.replace(/Items[_\[]\d+[_\]]?/, (match) =>
                        match.replace(/\d+/, String(index))));
                });
            });
        });
    };

    const isBlank = (row) => {
        const values = Array.from(row.querySelectorAll('input[type="text"], input:not([type])'))
            .map((input) => (input.value || '').trim());
        return values.every((value) => value === '');
    };

    /**
     * Keeps exactly ONE blank row at the bottom.
     *
     * The editor must always offer somewhere to type — a step list with no empty row makes the user hunt for the
     * "add" button before they can begin. The server skips blank rows on save, so this costs nothing.
     */
    const ensureTrailingBlank = () => {
        const host = rowsHost();
        if (!host) { return; }

        const rows = Array.from(host.querySelectorAll('[data-checklist-row]'));
        if (rows.length === 0 || !isBlank(rows[rows.length - 1])) {
            addRow();
        }
    };

    const addRow = () => {
        const host = rowsHost();
        const rows = host ? Array.from(host.querySelectorAll('[data-checklist-row]')) : [];
        if (!host || rows.length === 0) { return; }

        const clone = rows[rows.length - 1].cloneNode(true);
        clone.querySelectorAll('input').forEach((input) => {
            if (input.type === 'checkbox') { input.checked = false; return; }
            // The hidden `false` companion of each checkbox must keep its value — clearing it would post nothing
            // for an unticked box, and the binder would then keep whatever was stored.
            if (input.type !== 'hidden') { input.value = ''; }
        });
        clone.querySelectorAll('select').forEach((select) => { select.selectedIndex = 0; });
        // A cloned validation message would arrive already showing the previous row's error.
        clone.querySelectorAll('.field-validation-error, .text-danger').forEach((el) => { el.textContent = ''; });

        host.appendChild(clone);
        reindex();
    };

    const removeRow = (row) => {
        const host = rowsHost();
        if (!host || !row) { return; }

        row.remove();
        reindex();
        // Removing the last row would leave nowhere to type; the editor puts one back rather than emptying.
        ensureTrailingBlank();
    };

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelector('[data-checklist-add]')?.addEventListener('click', (event) => {
            event.preventDefault();
            addRow();
        });

        // Delegated, because rows are created after this handler is bound.
        rowsHost()?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-checklist-remove]');
            if (!button) { return; }
            event.preventDefault();
            removeRow(button.closest('[data-checklist-row]'));
        });

        // Typing in the last row means it is no longer the spare one, so a new spare is offered.
        rowsHost()?.addEventListener('input', (event) => {
            const row = event.target.closest('[data-checklist-row]');
            const rows = Array.from(rowsHost().querySelectorAll('[data-checklist-row]'));
            if (row && row === rows[rows.length - 1]) { ensureTrailingBlank(); }
        });

        reindex();
        ensureTrailingBlank();
    });
})();
