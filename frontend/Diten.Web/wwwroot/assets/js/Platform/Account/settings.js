'use strict';

(function () {
    const endpoint = '/Platform/Account/api/me';
    const l10n = window.PlatformAccountL10n || {};
    const ui = window.PlatformAccountUi;

    let currentAccount = null;

    const setInput = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.value = value ?? '';
    };

    const readErrors = async (response) => {
        try {
            const payload = await response.json();
            if (Array.isArray(payload.errors) && payload.errors.length > 0) {
                return payload.errors.join('\n');
            }
            if (payload.detail) return payload.detail;
            if (payload.message) return payload.message;
        } catch {
            // Response body is optional.
        }
        return l10n.gatewayError || 'Gateway request failed.';
    };

    const populate = (account) => {
        currentAccount = account;
        ui.fillSummary(account);
        setInput('account-display-name', account.displayName);
        setInput('account-email', account.email);
        setInput('account-username', account.userName);
        setInput('account-status', ui.humanize(account.status));
        setInput('account-actor-type', ui.humanize(account.actorType));
        setInput('account-roles', Array.isArray(account.roles) ? account.roles.map(ui.humanize).join(', ') : '');
        setInput('account-version', account.version ?? 0);
    };

    const load = async () => {
        try {
            const response = await fetch(endpoint, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) {
                throw new Error(`Settings load failed: ${response.status}`);
            }
            const payload = await response.json();
            populate(ui.unwrap(payload));
        } catch (error) {
            console.error('[PlatformAccount] Settings load failed.', error);
            ui.notify(l10n.unableToLoad || 'Account settings could not be loaded.', 'error');
        }
    };

    const save = async (event) => {
        event.preventDefault();
        const form = event.currentTarget;
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }

        const button = document.getElementById('account-save-button');
        const payload = {
            displayName: document.getElementById('account-display-name')?.value || '',
            version: Number.parseInt(document.getElementById('account-version')?.value || '0', 10)
        };

        try {
            if (button) button.disabled = true;
            const response = await fetch(endpoint, {
                method: 'PUT',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok && response.status !== 204) {
                throw new Error(await readErrors(response));
            }

            ui.notify(l10n.profileSaved || 'Profile saved.', 'success');
            await load();
        } catch (error) {
            console.error('[PlatformAccount] Settings save failed.', error);
            ui.notify(error.message || l10n.gatewayError || 'Gateway request failed.', 'error');
            if (currentAccount) populate(currentAccount);
        } finally {
            if (button) button.disabled = false;
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        document.getElementById('platform-account-settings-form')?.addEventListener('submit', save);
        load();
    });
})();
