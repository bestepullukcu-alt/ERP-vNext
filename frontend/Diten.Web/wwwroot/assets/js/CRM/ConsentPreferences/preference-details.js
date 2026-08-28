(function (window, document) {
    'use strict';
    const page = document.getElementById('preferenceDetailsPage');
    if (!page) return;
    const L = window.ConsentPreferenceL10n || {};
    const base = '/CRM/ConsentPreferences';
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };
    document.getElementById('archivePreference')?.addEventListener('click', event => {
        const id = event.currentTarget.dataset.id;
        window.showConfirm?.(L.ArchivePreferenceConfirm, async () => {
            try {
                await envelope(await fetch(`${base}/api/preferences/${id}/archive`, { method:'POST', credentials:'same-origin', headers:{ Accept:'application/json' } }));
                window.showToast?.(L.RecordArchived, 'success');
                window.location.href = `${base}/Preferences/${id}`;
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type:'warning', confirmButtonText:L.ArchivePreference });
    });
})(window, document);
