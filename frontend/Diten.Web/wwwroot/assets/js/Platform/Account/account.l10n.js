'use strict';

(function () {
    const payload = document.getElementById('account-l10n');
    const fallback = window.PlatformAccountL10n || {};

    if (payload) {
        try {
            window.PlatformAccountL10n = Object.assign({}, fallback, JSON.parse(payload.textContent || '{}'));
        } catch (error) {
            console.error('[PlatformAccount] Localization payload could not be parsed.', error);
            window.PlatformAccountL10n = fallback;
        }
    } else {
        window.PlatformAccountL10n = fallback;
    }

    const l10n = window.PlatformAccountL10n;

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');

    const humanize = (value) => {
        const text = String(value || '').trim();
        if (!text) return l10n.notAvailable || '--';
        return text
            .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
            .replace(/[_-]+/g, ' ');
    };

    const initials = (displayName, email) => {
        const source = String(displayName || '').trim() ||
            String(email || '').split('@')[0].replace(/[._-]+/g, ' ');
        const parts = source.split(/\s+/).filter(Boolean);
        const value = parts.slice(0, 2).map((part) => part.charAt(0)).join('').toUpperCase();
        return value || '?';
    };

    const formatDate = (value) => {
        if (!value) return l10n.notAvailable || '--';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return l10n.notAvailable || '--';
        return new Intl.DateTimeFormat(undefined, {
            dateStyle: 'medium',
            timeStyle: 'short'
        }).format(date);
    };

    const notify = (message, type) => {
        if (window.showToast) {
            window.showToast(message, type || 'info');
            return;
        }
        if (window.notyf) {
            type === 'error' ? window.notyf.error(message) : window.notyf.success(message);
        }
    };

    const unwrap = (payload) => payload && Object.prototype.hasOwnProperty.call(payload, 'data') ? payload.data : payload;

    const setHeader = (account) => {
        if (!account) return;
        const avatarText = initials(account.displayName, account.email);
        document.querySelectorAll('[data-platform-account-avatar]').forEach((el) => {
            el.textContent = avatarText;
        });
        const nameEl = document.getElementById('navbar-user-name');
        if (nameEl) nameEl.textContent = account.displayName || account.email || nameEl.textContent;
        const roleEl = document.getElementById('navbar-user-role');
        if (roleEl) roleEl.textContent = humanize(account.actorType);
        if (window.CurrentUser) {
            window.CurrentUser.displayName = account.displayName;
            window.CurrentUser.initials = avatarText;
        }
    };

    const setFieldText = (name, value) => {
        document.querySelectorAll(`[data-account-field="${name}"]`).forEach((el) => {
            el.textContent = value || l10n.notAvailable || '--';
        });
    };

    const renderRoles = (roles) => {
        const container = document.querySelector('[data-account-roles]');
        if (!container) return;
        const values = Array.isArray(roles) ? roles : [];
        if (values.length === 0) {
            container.innerHTML = `<span class="badge bg-label-secondary">${escapeHtml(l10n.notAvailable || '--')}</span>`;
            return;
        }
        container.innerHTML = values
            .map((role) => `<span class="badge bg-label-secondary">${escapeHtml(humanize(role))}</span>`)
            .join('');
    };

    const fillSummary = (account) => {
        if (!account) return;
        const avatar = initials(account.displayName, account.email);
        document.querySelectorAll('[data-account-avatar-lg]').forEach((el) => {
            el.textContent = avatar;
        });
        setFieldText('displayName', account.displayName);
        setFieldText('email', account.email);
        setFieldText('userName', account.userName);
        setFieldText('actorType', humanize(account.actorType));
        setFieldText('status', humanize(account.status));
        setFieldText('lastLoginAt', formatDate(account.lastLoginAtUtc || account.lastLoginAt));
        setFieldText('invitationStatus', humanize(account.invitationStatus));
        setFieldText('invitedAt', formatDate(account.invitedAtUtc || account.invitedAt));
        setFieldText('createdAt', formatDate(account.createdAt));
        setFieldText('updatedAt', formatDate(account.updatedAt));
        renderRoles(account.roles);
        setHeader(account);
    };

    window.PlatformAccountUi = {
        escapeHtml,
        fillSummary,
        formatDate,
        humanize,
        initials,
        notify,
        setHeader,
        unwrap
    };
})();
