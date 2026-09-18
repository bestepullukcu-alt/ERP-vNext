/**
 * SCMM-14-UI (CAND-CAP-0011) ContentScope form — create/edit interactions:
 *  - flatpickr on the usage-period dates
 *  - Select2 TAG inputs (.scope-tags) for product / market / audience references (opaque, sector-neutral config strings)
 *  - Archive button → same-origin proxy POST, then back to the list
 *
 * The form page has no window.L10n bridge — every user-facing string is Razor-localized and read from a data-* attribute.
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;

    document.addEventListener('DOMContentLoaded', () => {
        if (window.flatpickr) document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
        if ($ && $.fn.select2) {
            $('.scope-tags').each(function () {
                $(this).select2({ width: '100%', tags: true, tokenSeparators: [','], placeholder: this.getAttribute('data-placeholder') || '' });
            });
        }

        const btn = document.getElementById('btnArchiveScope');
        if (!btn) return;
        btn.addEventListener('click', event => {
            event.preventDefault();
            const run = async () => {
                btn.disabled = true;
                try {
                    const res = await fetch(`/CRM/ContentScopes/api/content-scopes/${btn.dataset.id}/archive`,
                        { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' } });
                    const body = await res.json().catch(() => ({}));
                    if (!res.ok) throw new Error((body.errors || [btn.dataset.fail]).join(' · '));
                    window.showToast?.(btn.dataset.ok, 'success');
                    window.location.href = '/CRM/ContentScopes';
                } catch (err) {
                    btn.disabled = false;
                    window.showToast?.(err.message || btn.dataset.fail, 'error');
                }
            };
            if (window.showConfirm) { window.showConfirm(btn.dataset.confirm, run, { type: 'warning' }); }
            else if (window.confirm(btn.dataset.confirm)) { run(); }
        });
    });
})(window, document);
