'use strict';

/*
 * MOD-0024 task-template Create/Edit form (BL-054).
 *
 * Three jobs, all of them about the form telling the truth:
 *   · the pickers are filled from the SAME lookups the neighbouring screens use, so a template cannot name a
 *     checklist, a position or a company those screens could not;
 *   · an EMPTY checklist list is EXPLAINED rather than left to be read as a broken control — that is the exact
 *     defect this slice closes one level up, and repeating it here would be the same mistake indented;
 *   · only the relevant assignment picker is shown, and the irrelevant one is CLEARED — a stale hidden value is
 *     an identity the server would receive without anybody having chosen it, and it refuses that outright.
 */
(function () {
    const api = '/Tasks/api';

    const fetchJson = async (url) => {
        try {
            const response = await fetch(url, {
                credentials: 'include',
                headers: window.DitenDataTable?.getAuthHeaders?.() || {}
            });
            if (!response.ok) { return []; }
            const payload = await response.json();
            const rows = payload?.data ?? payload?.Data ?? payload;
            return Array.isArray(rows) ? rows : [];
        } catch (error) {
            console.error('[TaskTemplateForm] Lookup failed.', url, error);
            return [];
        }
    };

    /**
     * Fills a <select>, keeping whatever the model already selected.
     *
     * The selected value is re-applied AFTER the options exist: on Edit the markup carries the saved id but the
     * option it points at has not been created yet, so without this the picker silently comes up empty and a
     * save would blank a binding nobody touched.
     */
    const fill = (select, rows, toOption, placeholder) => {
        if (!select) { return; }
        const current = select.getAttribute('data-selected') || select.value || '';
        select.innerHTML = '';
        if (placeholder !== undefined) {
            const blank = document.createElement('option');
            blank.value = '';
            blank.textContent = placeholder;
            select.appendChild(blank);
        }
        rows.forEach((row) => {
            const option = toOption(row);
            if (!option?.value) { return; }
            const el = document.createElement('option');
            el.value = option.value;
            el.textContent = option.text;
            select.appendChild(el);
        });
        if (current) { select.value = current; }
        if (window.jQuery && window.jQuery(select).hasClass('select2-hidden-accessible')) {
            window.jQuery(select).trigger('change.select2');
        }
    };

    const codeName = (code, name) => (code ? `${code} — ${name || ''}` : (name || ''));

    const positionText = (row) =>
        [row.positionName, row.organizationUnitName].filter(Boolean).join(' — ') || row.positionCode || '';

    /**
     * Shows the pool picker only when the default IS a pool, and clears it otherwise.
     *
     * Clearing is the load-bearing half here, more than on the sibling screen: the server REFUSES a position
     * sent with a non-pool default rather than dropping it, so a stale hidden value would surface as a save that
     * fails for no reason the user can see.
     */
    const syncAssignmentVisibility = () => {
        const target = document.querySelector('[data-template-target]');
        const poolBlock = document.querySelector('[data-template-pool]');
        if (!target || !poolBlock) { return; }

        const isPool = target.value === 'PositionPool';
        poolBlock.classList.toggle('d-none', !isPool);

        if (isPool) { return; }
        const position = document.querySelector('[data-template-position]');
        if (!position) { return; }
        position.value = '';
        position.removeAttribute('data-selected');
        if (window.jQuery && window.jQuery(position).hasClass('select2-hidden-accessible')) {
            window.jQuery(position).trigger('change.select2');
        }
    };

    document.addEventListener('DOMContentLoaded', async function () {
        // Remember what the model chose before select2 rewrites the DOM around these elements.
        document
            .querySelectorAll('[data-template-checklist], [data-template-position], [data-template-legalentity]')
            .forEach((el) => { if (el.value) { el.setAttribute('data-selected', el.value); } });

        const select2Elements = window.jQuery ? window.jQuery('.select2') : null;
        if (select2Elements && select2Elements.length) {
            select2Elements.each(function () {
                const $this = window.jQuery(this);
                $this.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $this.parent() });
            });
        }

        const [checklists, positions, legalEntities] = await Promise.all([
            fetchJson(`${api}/checklist-template-lookup`),
            fetchJson(`${api}/assignable-positions`),
            fetchJson(`${api}/legal-entities`)
        ]);

        const checklistSelect = document.querySelector('[data-template-checklist]');
        fill(checklistSelect, checklists,
            (row) => ({
                value: row.id,
                // The step count travels with the option so the reader can tell a real gate from a stub before
                // binding it — the name alone hides that completely.
                text: row.itemCount ? `${row.name} (${row.itemCount})` : row.name
            }),
            checklistSelect?.querySelector('option[value=""]')?.textContent || '');

        /*
         * ⚠ THE EMPTY LIST IS NAMED, not left blank.
         *
         * An empty picker and a picker whose endpoint failed look identical, and the person filling this form
         * has no way to tell them apart — which is exactly how the recurrence rule's own template picker sat
         * live and useless for months. Here the screen says which of the two it is, and offers the way out.
         */
        document.querySelector('[data-template-checklist-empty]')
            ?.classList.toggle('d-none', checklists.length > 0);

        fill(document.querySelector('[data-template-position]'), positions,
            (row) => ({ value: row.positionId, text: positionText(row) }), '');

        const legalEntitySelect = document.querySelector('[data-template-legalentity]');
        fill(legalEntitySelect, legalEntities,
            (row) => ({
                value: row.legalEntityId || row.LegalEntityId || row.id || row.Id,
                text: codeName(
                    row.code || row.Code,
                    row.displayName || row.DisplayName || row.legalName || row.LegalName)
            }),
            // "Every company" — the blank option's own wording, so the widest scope reads as a CHOICE rather
            // than as an unanswered field.
            legalEntitySelect?.querySelector('option[value=""]')?.textContent || '');

        const target = document.querySelector('[data-template-target]');
        if (target) {
            target.addEventListener('change', syncAssignmentVisibility);
            if (window.jQuery) { window.jQuery(target).on('select2:select', syncAssignmentVisibility); }
        }
        syncAssignmentVisibility();
    });
})();
