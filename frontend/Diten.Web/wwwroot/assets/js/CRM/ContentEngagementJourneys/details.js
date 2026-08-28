/**
 * MOD-0162-FU05 ContentEngagementJourney Details — publish (separate endpoint + SoD), new-version (stage-id remap on
 * the server) and archive actions via the same-origin proxy. Nothing here advances a stage or reports progress.
 */
(function (window, document) {
    'use strict';
    const L = window.ContentEngagementJourneysL10n || window.L10n || {};
    const page = document.getElementById('contentEngagementJourneyDetailsPage');
    if (!page) return;
    const journeyId = page.dataset.journeyId;
    const endpoint = '/CRM/ContentEngagementJourneys/api';
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const toast = (m, t) => window.showToast?.(m, t || 'info');

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data;
    };

    document.getElementById('publishJourney')?.addEventListener('click', () => {
        window.showConfirm?.(L.PublishConfirm || L.AreYouSure, async () => {
            try {
                await envelope(await fetch(`${endpoint}/journeys/${journeyId}/publish`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                toast(L.RecordUpdated || 'Published', 'success');
                window.location.reload();
            } catch (e) { toast(e.message || L.ErrorState, 'error'); }
        }, { type: 'success' });
    });

    document.getElementById('newVersionJourney')?.addEventListener('click', () => {
        window.showConfirm?.(L.NewVersionConfirm || L.AreYouSure, async () => {
            try {
                const id = await envelope(await fetch(`${endpoint}/journeys/${journeyId}/new-version`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify({}) }));
                toast(L.RecordCreated || 'Created', 'success');
                window.location.href = `/CRM/ContentEngagementJourneys/Edit/${id}`;
            } catch (e) { toast(e.message || L.ErrorState, 'error'); }
        }, { type: 'info' });
    });

    document.getElementById('archiveJourney')?.addEventListener('click', e => {
        const name = e.currentTarget.dataset.name;
        window.showConfirm?.(L.ArchiveJourneyConfirm || L.AreYouSure, async () => {
            try {
                await envelope(await fetch(`${endpoint}/journeys/${journeyId}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                toast(L.RecordArchived || 'Archived', 'success');
                window.location.href = '/CRM/ContentEngagementJourneys';
            } catch (err) { toast(err.message || L.ErrorState, 'error'); }
        }, { entityName: name, type: 'warning', confirmButtonText: L.ArchiveJourney });
    });
})(window, document);
