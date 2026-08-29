/**
 * MOD-0167-FU04 Strategy Template Details — the three lifecycle actions and nothing else.
 * There is no apply/generate button here and there never will be: applying a play to a period is MOD-0155.
 */
(function (window, document) {
    'use strict';
    const endpoint = '/CRM/StrategyTemplates/api';
    const L = window.StrategyTemplatesL10n || window.L10n || {};

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    const post = async (url, successKey, redirectTo) => {
        try {
            const data = await envelope(await fetch(url, {
                method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            window.showToast?.(successKey, 'success');
            window.location.href = typeof redirectTo === 'function' ? redirectTo(data) : redirectTo;
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        }
    };

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-action]');
        if (!button) return;
        const id = button.dataset.id;
        if (!id) return;

        if (button.dataset.action === 'activate') {
            event.preventDefault();
            window.showConfirm?.(L.ActivateStrategyTemplateConfirm,
                () => post(`${endpoint}/templates/${id}/activate`, L.RecordActivated, `/CRM/StrategyTemplates/Details/${id}`),
                { type: 'question', confirmButtonText: L.ActivateStrategyTemplate });
            return;
        }

        if (button.dataset.action === 'archive') {
            event.preventDefault();
            window.showConfirm?.(L.ArchiveStrategyTemplateConfirm,
                () => post(`${endpoint}/templates/${id}/archive`, L.RecordArchived, '/CRM/StrategyTemplates'),
                { type: 'warning', confirmButtonText: L.ArchiveStrategyTemplate });
            return;
        }

        if (button.dataset.action === 'new-version') {
            event.preventDefault();
            window.showConfirm?.(L.NewVersionConfirm,
                () => post(`${endpoint}/templates/${id}/new-version`, L.RecordCreated,
                    created => created ? `/CRM/StrategyTemplates/Edit/${created}` : '/CRM/StrategyTemplates'),
                { type: 'question', confirmButtonText: L.NewVersion });
        }
    });
})(window, document);
