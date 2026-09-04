/**
 * MOD-0162-FU04 KnowledgePath Details — publish (D4), new-version (D5) and archive actions via the same-origin proxy.
 */
(function (window, document) {
    'use strict';
    const L = window.KnowledgePathsL10n || window.L10n || {};
    const page = document.getElementById('knowledgePathDetailsPage');
    if (!page) return;
    const pathId = page.dataset.pathId;
    const endpoint = '/CRM/KnowledgePaths/api';
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const toast = (m, t) => window.showToast?.(m, t || 'info');

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data;
    };

    document.getElementById('publishPath')?.addEventListener('click', () => {
        window.showConfirm?.(L.PublishConfirm || L.AreYouSure, async () => {
            try {
                await envelope(await fetch(`${endpoint}/paths/${pathId}/publish`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                toast(L.RecordUpdated || 'Published', 'success');
                window.location.reload();
            } catch (e) { toast(e.message || L.ErrorState, 'error'); }
        }, { type: 'success' });
    });

    document.getElementById('newVersionPath')?.addEventListener('click', () => {
        window.showConfirm?.(L.NewVersionConfirm || L.AreYouSure, async () => {
            try {
                const id = await envelope(await fetch(`${endpoint}/paths/${pathId}/new-version`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify({}) }));
                toast(L.RecordCreated || 'Created', 'success');
                window.location.href = `/CRM/KnowledgePaths/Edit/${id}`;
            } catch (e) { toast(e.message || L.ErrorState, 'error'); }
        }, { type: 'info' });
    });

    // Read-only Steps list: reveal / hide archived step cards.
    document.getElementById('showArchivedSteps')?.addEventListener('change', function () {
        document.querySelectorAll('.kp-step-archived').forEach(el => el.classList.toggle('d-none', !this.checked));
    });

    document.getElementById('archivePath')?.addEventListener('click', e => {
        const name = e.currentTarget.dataset.name;
        window.showConfirm?.(L.ArchivePathConfirm || L.AreYouSure, async () => {
            try {
                await envelope(await fetch(`${endpoint}/paths/${pathId}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                toast(L.RecordArchived || 'Archived', 'success');
                window.location.href = '/CRM/KnowledgePaths';
            } catch (err) { toast(err.message || L.ErrorState, 'error'); }
        }, { entityName: name, type: 'warning', confirmButtonText: L.ArchivePath });
    });
})(window, document);
