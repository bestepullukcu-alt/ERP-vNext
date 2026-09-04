'use strict';

/*
 * MOD-0024 task-template detail — resolves the ONE thing the payload cannot say for itself.
 *
 * The template carries a legal-entity ID and no name, and a GUID on a detail page is not an answer to "which
 * company?". The name is fetched from the same MDM lookup the form uses, so the two screens cannot disagree.
 *
 * An id the lookup does not know still renders as itself: a company that has since been removed is something the
 * reader needs to SEE rather than have quietly replaced with a blank.
 */
(function () {
    document.addEventListener('DOMContentLoaded', async function () {
        const host = document.querySelector('[data-template-legalentity-name]');
        if (!host) { return; }

        const id = host.getAttribute('data-legal-entity-id');
        // No id means EVERY company, and the server-rendered wording already says so — there is nothing to
        // resolve and nothing to overwrite.
        if (!id) { return; }

        try {
            const response = await fetch('/Tasks/api/legal-entities', {
                credentials: 'include',
                headers: window.DitenDataTable?.getAuthHeaders?.() || {}
            });
            if (!response.ok) { host.textContent = id; return; }

            const payload = await response.json();
            const rows = payload?.data ?? payload?.Data ?? payload;
            if (!Array.isArray(rows)) { host.textContent = id; return; }

            const match = rows.find((row) =>
                String(row.legalEntityId || row.LegalEntityId || row.id || row.Id) === String(id));
            if (!match) { host.textContent = id; return; }

            const code = match.code || match.Code;
            const name = match.displayName || match.DisplayName || match.legalName || match.LegalName;
            host.textContent = code ? `${code} — ${name || ''}` : (name || id);
        } catch (error) {
            // The id stays on screen rather than a spinner that never resolves.
            console.error('[TaskTemplateDetails] Legal entity lookup failed.', error);
            host.textContent = id;
        }
    });
})();
