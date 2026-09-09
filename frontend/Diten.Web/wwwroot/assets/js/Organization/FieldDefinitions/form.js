'use strict';

/*
 * MOD-0288-FU04 — the field-definition create/edit form.
 *
 * Imitates `Tasks/FieldDefinitions/form.js`; shares no file with it (§2).
 *
 * ⚠ ONE THING FROM THE PRECEDENT IS DELIBERATELY NOT COPIED. It re-selects a stored value with an exact
 * comparison —
 *
 *     Array.prototype.some.call(select.options, function (o) { return o.value === previous; })
 *
 * — which is sound only while the server's casing and the option's casing never differ. FU03 shipped the
 * version of that assumption that did differ: a `titleCase()` helper turned `GroupFunction` into
 * "Groupfunction", nothing matched, the select fell back to empty, and the NEXT SAVE silently wrote
 * `Department`. Opening a unit and pressing Save changed its type. Two of the eight types on this form —
 * `MultilineText` and `SingleSelect` — are two words, so the same failure is twice as easy here.
 * `optionValueFor` below asks the select what it actually offers and matches case-insensitively.
 */
(function () {
    const form = document.getElementById('organizationFieldDefinitionForm');
    const detailsDeactivate = document.querySelector('[data-deactivate-definition]');
    const L = (typeof window !== 'undefined' && window.L10n) ? window.L10n : {};

    // Which constraint groups each type can carry. Mirrors FU02's ValidateConstraints: a max length on a
    // date, or options on a number, are refused there — so the form does not offer them.
    const CONSTRAINTS_BY_TYPE = {
        Text: ['length'],
        MultilineText: ['length'],
        Integer: ['range'],
        Decimal: ['range'],
        Boolean: [],
        Date: [],
        SingleSelect: ['options'],
        Reference: ['Reference']
    };

    /**
     * Resolve a value the server sent against the option values a select actually offers, case-insensitively.
     * Returns null when nothing matches, so a caller can leave the control alone rather than blanking it.
     */
    const optionValueFor = (select, raw) => {
        const value = String(raw ?? '').trim();
        if (!select || !value.length) return null;
        const match = Array.from(select.options).find((o) => o.value.toLowerCase() === value.toLowerCase());
        return match ? match.value : null;
    };

    // ── The type-dependent constraint groups ─────────────────────────────────────────────────────────────
    const applyConstraintVisibility = () => {
        const typeSelect = document.querySelector('[data-field-data-type]');
        if (!typeSelect) return;

        // ⚠ The select's OWN value, resolved through its options. Never a re-capitalised string.
        const resolved = optionValueFor(typeSelect, typeSelect.value) || 'Text';
        const groups = CONSTRAINTS_BY_TYPE[resolved] || [];

        document.querySelectorAll('[data-constraint-group]').forEach((element) => {
            const group = element.getAttribute('data-constraint-group');
            element.classList.toggle('d-none', groups.indexOf(group) === -1);
        });
    };

    // ── The SingleSelect option editor ───────────────────────────────────────────────────────────────────
    const optionsEditor = () => document.querySelector('[data-options-editor]');

    /*
     * Re-index every row's `name` after any add or remove. Model binding reads `Options[0]`, `Options[1]`…
     * and stops at the first gap — so removing the middle row without re-indexing silently drops every
     * choice after it, and the save looks like it worked.
     */
    const reindexOptions = () => {
        const editor = optionsEditor();
        if (!editor) return;
        editor.querySelectorAll('[data-option-row] input').forEach((input, index) => {
            input.name = `Options[${index}]`;
        });
    };

    const addOptionRow = (value) => {
        const editor = optionsEditor();
        if (!editor) return;
        const row = document.createElement('div');
        row.className = 'input-group mb-2';
        row.setAttribute('data-option-row', '');

        const input = document.createElement('input');
        input.className = 'form-control';
        input.maxLength = 120;
        input.placeholder = L.OptionPlaceholder || '';
        input.value = value || '';

        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'btn btn-label-danger';
        remove.setAttribute('data-option-remove', '');
        remove.textContent = L.OptionRemove || '';

        row.appendChild(input);
        row.appendChild(remove);
        editor.appendChild(row);
        reindexOptions();
    };

    const bindOptionsEditor = () => {
        document.querySelector('[data-option-add]')?.addEventListener('click', () => addOptionRow(''));
        optionsEditor()?.addEventListener('click', (event) => {
            if (!event.target.closest('[data-option-remove]')) return;
            event.target.closest('[data-option-row]')?.remove();
            reindexOptions();
        });
    };

    // ── Deactivation from the details page ───────────────────────────────────────────────────────────────
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const bindDeactivate = () => {
        if (!detailsDeactivate) return;
        detailsDeactivate.addEventListener('click', () => {
            const id = detailsDeactivate.getAttribute('data-definition-id');
            const version = detailsDeactivate.getAttribute('data-definition-version') || '0';
            if (!id) return;
            const run = async () => {
                try {
                    const response = await fetch(
                        `/Organization/FieldDefinitions/api/${encodeURIComponent(id)}/deactivate?expectedVersion=${encodeURIComponent(version)}`,
                        { method: 'POST', headers: getAuthHeaders() });
                    if (!response.ok) throw new Error('Deactivate failed.');
                    try { sessionStorage.setItem('ofd-toast', L.RecordDeactivated || ''); } catch { /* ignore */ }
                    window.location.href = '/Organization/FieldDefinitions';
                } catch (error) {
                    console.error('[OrganizationFieldDefinitions] Deactivate failed.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            };
            if (window.showConfirm) window.showConfirm(L.DeactivateConfirm || L.AreYouSure, run, { type: 'warning', confirmButtonText: L.Deactivate });
            else run();
        });
    };

    const initSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        jq('.select2').each(function () {
            const $this = jq(this);
            if ($this.hasClass('select2-hidden-accessible')) return;
            $this.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $this.parent() });
        });
    };

    const boot = () => {
        bindDeactivate();
        if (!form) return;

        // The stored type resolved through the option list before anything else reads the select.
        const typeSelect = document.querySelector('[data-field-data-type]');
        if (typeSelect) {
            const resolved = optionValueFor(typeSelect, typeSelect.value);
            if (resolved) {
                typeSelect.value = resolved;
            } else if (typeSelect.value) {
                // Loud, and it changes nothing: a type this build does not know stays as it is rather than
                // being reset to the first option and saved as something else.
                console.error(`[OrganizationFieldDefinitions] Unknown field type "${typeSelect.value}" — leaving the select untouched.`);
            }
            typeSelect.addEventListener('change', applyConstraintVisibility);
            if (window.jQuery) window.jQuery(typeSelect).on('change', applyConstraintVisibility);
        }

        bindOptionsEditor();
        reindexOptions();
        applyConstraintVisibility();
        initSelect2();
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})();
