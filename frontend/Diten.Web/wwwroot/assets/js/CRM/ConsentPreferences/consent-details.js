(function (window, document) {
    'use strict';
    const page = document.getElementById('consentDetailsPage');
    if (!page) return;
    const L = window.ConsentPreferenceL10n || {};
    const base = '/CRM/ConsentPreferences';
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };
    document.getElementById('archiveConsent')?.addEventListener('click', event => {
        const id = event.currentTarget.dataset.id;
        window.showConfirm?.(L.ArchiveConsentConfirm, async () => {
            try {
                await envelope(await fetch(`${base}/api/consents/${id}/archive`, { method:'POST', credentials:'same-origin', headers:{ Accept:'application/json' } }));
                window.showToast?.(L.RecordArchived, 'success');
                window.location.href = `${base}/Consents/${id}`;
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type:'warning', confirmButtonText:L.ArchiveConsent });
    });
})(window, document);
