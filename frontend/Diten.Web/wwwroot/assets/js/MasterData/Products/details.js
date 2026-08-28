(function (window, document) {
    'use strict';
    const page = window.ProductPage;
    if (!page) return;

    const endpoint = '/MasterData/Products/api';
    const L = window.ProductL10n || {};

    const getAuthHeaders = () => ({ Accept: 'application/json' });

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };

    // Archive uses POST /archive — never HTTP DELETE. Archiving never removes Campaign / Knowledge /
    // Frequency references to this product; it only closes it to new writes.
    document.getElementById('archiveProduct')?.addEventListener('click', () => {
        window.showConfirm?.(L.ArchiveProductConfirm, async () => {
            try {
                await envelope(await fetch(`${endpoint}/${page.productId}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                window.showToast?.(L.RecordArchived, 'success');
                window.location.reload();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type: 'warning', confirmButtonText: L.ArchiveProduct });
    });
})(window, document);
