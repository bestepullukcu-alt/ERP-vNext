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

    // WP-ST-DETAIL-2 — the "Sürüm geçmişi" panel. On the Detay page it fetches the play's lineage (newest-first, from the
    // DETAIL-1 /versions read) and lists each version: vN + a status badge + a date + a "geçerli" marker on the current
    // one. READ-ONLY and independent of the data-action flow above; a failed load shows a short message, never invents a
    // version. Only Bootstrap utility classes are used, so no extra stylesheet is needed on the Detay page.
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const versionStatus = status => {
        const s = String(status || '').toLowerCase();
        if (s === 'active' || s === 'published') return { label: L.VerStatusActive || s, tone: 'bg-label-success' };
        if (s === 'archived') return { label: L.VerStatusArchived || s, tone: 'bg-label-secondary' };
        return { label: L.VerStatusDraft || s, tone: 'bg-label-secondary' };
    };
    const versionDate = value => {
        if (!value) return '';
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10);
    };

    const loadVersionHistory = async host => {
        const id = host.dataset.id;
        if (!id) { host.textContent = L.ErrorState || ''; return; }
        host.textContent = L.Loading || '';
        try {
            const data = await envelope(await fetch(`${endpoint}/templates/${id}/versions`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            const versions = data?.versions || data?.Versions || [];
            if (versions.length === 0) { host.textContent = L.EmptyState || ''; return; }
            // The endpoint already sorts newest-first (TemplateVersion descending); the panel renders it as received.
            host.innerHTML = versions.map(v => {
                const st = versionStatus(v.templateStatus ?? v.TemplateStatus);
                const date = versionDate(v.activatedAt ?? v.ActivatedAt ?? v.createdAt ?? v.CreatedAt);
                const isCurrent = (v.isCurrent ?? v.IsCurrent) === true;
                const ver = v.templateVersion ?? v.TemplateVersion;
                return `
                <div class="d-flex align-items-center gap-2 py-1${isCurrent ? ' fw-semibold' : ''}">
                    <span class="text-nowrap">v${esc(ver)}</span>
                    <span class="badge ${st.tone} rounded-pill">${esc(st.label)}</span>
                    <span class="text-muted ms-auto">${esc(date)}</span>
                    ${isCurrent ? `<span class="badge bg-label-primary rounded-pill">${esc(L.CurrentVersion || '')}</span>` : ''}
                </div>`;
            }).join('');
        } catch (error) {
            host.textContent = error.message || L.ErrorState || '';
        }
    };

    const versionHost = document.getElementById('stVersionHistory');
    if (versionHost) loadVersionHistory(versionHost);
})(window, document);
