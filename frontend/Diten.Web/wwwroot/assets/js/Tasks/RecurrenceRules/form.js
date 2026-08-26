'use strict';

/*
 * MOD-0024 recurrence rule Create/Edit form (BL-052).
 *
 * Three jobs, all of them about the form telling the truth:
 *   · the pickers are filled from the SAME lookups the task form uses, so a rule cannot name somebody or some
 *     position the task form could not;
 *   · only the relevant assignment picker is shown, and the irrelevant one is CLEARED — a stale hidden value is
 *     an identity the server would receive without anybody having chosen it;
 *   · "myself" is never offered, and the option is never added by any code path here.
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
            console.error('[RecurrenceRuleForm] Lookup failed.', url, error);
            return [];
        }
    };

    /**
     * Fills a <select>, keeping whatever the model already selected.
     *
     * The selected value is re-applied AFTER the options exist: on Edit the markup carries the saved id but the
     * option it points at has not been created yet, so without this the picker silently comes up empty and a
     * save would blank an assignment nobody touched.
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

    const personText = (row) => {
        const name = row.displayName || window.L10n?.NotAvailable || '';
        // The position and its unit, for the same reason the lookup carries them: two "QA Specialist" holders in
        // different facilities are otherwise the same line twice.
        const where = [row.positionName, row.organizationUnitName].filter(Boolean).join(' — ');
        return where ? `${name} · ${where}` : name;
    };

    const positionText = (row) =>
        [row.positionName, row.organizationUnitName].filter(Boolean).join(' — ') || row.positionCode || '';

    /**
     * Shows the picker that matches the chosen target and CLEARS the other one.
     *
     * Clearing is the load-bearing half. A hidden-but-filled picker leaves a person id on a pool rule (or the
     * reverse) — the controller drops it, but the form would still be telling the reader something that is not
     * true about the rule they are saving.
     */
    const syncAssignmentVisibility = () => {
        const target = document.querySelector('[data-recurrence-target]');
        const personBlock = document.querySelector('[data-recurrence-person]');
        const poolBlock = document.querySelector('[data-recurrence-pool]');
        if (!target || !personBlock || !poolBlock) { return; }

        const isPool = target.value === 'PositionPool';
        personBlock.classList.toggle('d-none', isPool);
        poolBlock.classList.toggle('d-none', !isPool);

        const clear = (selector) => {
            const el = document.querySelector(selector);
            if (!el) { return; }
            el.value = '';
            el.removeAttribute('data-selected');
            if (window.jQuery && window.jQuery(el).hasClass('select2-hidden-accessible')) {
                window.jQuery(el).trigger('change.select2');
            }
        };
        clear(isPool ? '[data-recurrence-assignee]' : '[data-recurrence-position]');
    };

    document.addEventListener('DOMContentLoaded', async function () {
        // Remember what the model chose before select2 rewrites the DOM around these elements.
        document.querySelectorAll('[data-recurrence-assignee], [data-recurrence-position], [data-recurrence-template]')
            .forEach((el) => { if (el.value) { el.setAttribute('data-selected', el.value); } });

        const flatpickrDate = document.querySelectorAll('.flatpickr-date');
        flatpickrDate.forEach(function (element) {
            element.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d' });
        });

        const select2Elements = window.jQuery ? window.jQuery('.select2') : null;
        if (select2Elements && select2Elements.length) {
            select2Elements.each(function () {
                const $this = window.jQuery(this);
                $this.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $this.parent() });
            });
        }

        const [people, positions, templates] = await Promise.all([
            fetchJson(`${api}/assignable-people`),
            fetchJson(`${api}/assignable-positions`),
            fetchJson(`${api}/task-templates`)
        ]);

        fill(document.querySelector('[data-recurrence-assignee]'), people,
            (row) => ({ value: row.userId, text: personText(row) }), '');
        fill(document.querySelector('[data-recurrence-position]'), positions,
            (row) => ({ value: row.positionId, text: positionText(row) }), '');
        fill(document.querySelector('[data-recurrence-template]'), templates,
            (row) => ({ value: row.id, text: row.name }), window.L10n?.TemplateNone || '');

        const target = document.querySelector('[data-recurrence-target]');
        if (target) {
            target.addEventListener('change', syncAssignmentVisibility);
            if (window.jQuery) { window.jQuery(target).on('select2:select', syncAssignmentVisibility); }
        }
        syncAssignmentVisibility();
    });
})();
