(function (window, document) {
    'use strict';
    const page = document.getElementById('knowledgeDetailsPage');
    if (!page) return;
    const L = window.KnowledgeL10n || {};
    const contentId = page.dataset.contentId;
    const button = document.getElementById('archiveContent');
    if (!button) return;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    button.addEventListener('click', () => {
        window.showConfirm?.(L.ArchiveContentConfirm, async () => {
            try {
                await envelope(await fetch(`/CRM/Knowledge/api/contents/${contentId}/archive`, {
                    method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                }));
                window.showToast?.(L.RecordArchived, 'success');
                window.location.reload();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { entityName: button.dataset.name, type: 'warning', confirmButtonText: L.ArchiveContent });
    });
})(window, document);
