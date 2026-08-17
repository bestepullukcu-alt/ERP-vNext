'use strict';

(function () {
    const endpoint = '/Platform/Account/api/me';
    const l10n = window.PlatformAccountL10n || {};
    const ui = window.PlatformAccountUi;

    const load = async () => {
        try {
            const response = await fetch(endpoint, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) {
                throw new Error(`Profile load failed: ${response.status}`);
            }
            const payload = await response.json();
            ui.fillSummary(ui.unwrap(payload));
        } catch (error) {
            console.error('[PlatformAccount] Profile load failed.', error);
            ui.notify(l10n.unableToLoad || 'Profile could not be loaded.', 'error');
        }
    };

    document.addEventListener('DOMContentLoaded', load);
})();
