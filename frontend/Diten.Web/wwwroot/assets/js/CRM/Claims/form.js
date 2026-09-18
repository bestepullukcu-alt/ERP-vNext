/**
 * SCMM-12-UI (CAND-CAP-0011) Claim form — Compact create/edit interactions:
 *  - flatpickr on the effective-window dates
 *  - Select2 TAG inputs (.claim-tags) for the free-string lists: qualifiers, evidence refs, applicability
 *    product/market/audience refs — the user types values (opaque, sector-neutral) and each becomes a posted option
 *  - Select2 picker (.claim-picker) for ComponentRefs — server-rendered KnowledgeContent options (by-id reuse)
 *  - Approve / Archive buttons → same-origin proxy POST, then back to the list
 *
 * NOTE: Create.cshtml / Edit.cshtml do NOT load the _IndexL10n bridge, so window.L10n is empty here. Every user-facing
 * string is rendered localized by Razor and read back from a data-* attribute — never from L.
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;

    document.addEventListener('DOMContentLoaded', () => {
        if (window.flatpickr) document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));

        if ($ && $.fn.select2) {
            // Disabled controls (an approved / archived governed body) are shown read-only; their values still post via
            // the hidden mirrors rendered server-side, so Select2 here is display-only for them.
            $('.claim-tags').each(function () {
                $(this).select2({ width: '100%', tags: true, tokenSeparators: [','], placeholder: this.getAttribute('data-placeholder') || '' });
            });
            $('.claim-picker').each(function () {
                $(this).select2({ width: '100%', placeholder: this.getAttribute('data-placeholder') || '', allowClear: true });
            });
        }

        wireLifecycleButton('btnApproveClaim', id => `/CRM/Claims/api/claims/${id}/approve`);
        wireLifecycleButton('btnArchiveClaim', id => `/CRM/Claims/api/claims/${id}/archive`);
    });

    // Approve / Archive: confirm → same-origin proxy POST → toast → back to the list (the row state has changed).
    function wireLifecycleButton(buttonId, pathOf) {
        const btn = document.getElementById(buttonId);
        if (!btn) return;
        btn.addEventListener('click', event => {
            event.preventDefault();
            const id = btn.dataset.id;
            const run = async () => {
                btn.disabled = true;
                try {
                    const res = await fetch(pathOf(id), { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' } });
                    const body = await res.json().catch(() => ({}));
                    if (!res.ok) throw new Error((body.errors || [btn.dataset.fail]).join(' · '));
                    window.showToast?.(btn.dataset.ok, 'success');
                    window.location.href = '/CRM/Claims';
                } catch (err) {
                    btn.disabled = false;
                    window.showToast?.(err.message || btn.dataset.fail, 'error');
                }
            };
            if (window.showConfirm) {
                window.showConfirm(btn.dataset.confirm, run, { type: buttonId === 'btnArchiveClaim' ? 'warning' : 'success' });
            } else if (window.confirm(btn.dataset.confirm)) {
                run();
            }
        });
    }
})(window, document);
