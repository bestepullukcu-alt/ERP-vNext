/**
 * SCMM-11-UI (CAND-CAP-0011) EligibilityPolicy form — create/edit interactions:
 *  - flatpickr on the effective-window dates
 *  - Select2 on Status + each condition row's Dimension / Match (single) and Values (free multi-tag; reference strings)
 *  - Condition builder: add/remove rows via the .Index model-binding convention
 *  - Archive button → same-origin proxy POST, then back to the list
 *
 * The form page has no window.L10n bridge — labels are Razor-localized; JS reads data-* where needed.
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;
    let counter = Date.now();

    function initRowWidgets(scope) {
        if (!$ || !$.fn.select2) return;
        $(scope).find('.cond-dim, .cond-match').each(function () {
            if ($(this).hasClass('select2-hidden-accessible')) return;
            $(this).select2({ width: '100%', minimumResultsForSearch: Infinity });
        });
        $(scope).find('.cond-values').each(function () {
            if ($(this).hasClass('select2-hidden-accessible')) return;
            $(this).select2({ width: '100%', tags: true, tokenSeparators: [','], placeholder: this.getAttribute('data-placeholder') || '' });
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        if (window.flatpickr) document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
        if ($ && $.fn.select2) {
            $('#policyForm .select2').each(function () { $(this).select2({ width: '100%' }); });
        }
        initRowWidgets(document.getElementById('conditionRows'));

        const addBtn = document.getElementById('btnAddCondition');
        const template = document.getElementById('conditionRowTemplate');
        const host = document.getElementById('conditionRows');
        if (addBtn && template && host) {
            addBtn.addEventListener('click', () => {
                const idx = ++counter;
                const html = template.innerHTML.replace(/__I__/g, String(idx));
                const wrap = document.createElement('div');
                wrap.innerHTML = html.trim();
                const row = wrap.firstElementChild;
                document.getElementById('conditionsEmpty')?.remove();
                host.appendChild(row);
                initRowWidgets(row);
            });
        }

        // Remove a condition row (event delegation; select2 widgets are destroyed with the row).
        host?.addEventListener('click', event => {
            const rm = event.target.closest('.cond-remove');
            if (!rm) return;
            event.preventDefault();
            rm.closest('.condition-row')?.remove();
        });

        // Archive.
        const archiveBtn = document.getElementById('btnArchivePolicy');
        if (archiveBtn) {
            archiveBtn.addEventListener('click', event => {
                event.preventDefault();
                const run = async () => {
                    archiveBtn.disabled = true;
                    try {
                        const res = await fetch(`/CRM/EligibilityPolicies/api/eligibility-policies/${archiveBtn.dataset.id}/archive`,
                            { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' } });
                        const body = await res.json().catch(() => ({}));
                        if (!res.ok) throw new Error((body.errors || [archiveBtn.dataset.fail]).join(' · '));
                        window.showToast?.(archiveBtn.dataset.ok, 'success');
                        window.location.href = '/CRM/EligibilityPolicies';
                    } catch (err) {
                        archiveBtn.disabled = false;
                        window.showToast?.(err.message || archiveBtn.dataset.fail, 'error');
                    }
                };
                if (window.showConfirm) { window.showConfirm(archiveBtn.dataset.confirm, run, { type: 'warning' }); }
                else if (window.confirm(archiveBtn.dataset.confirm)) { run(); }
            });
        }
    });
})(window, document);
