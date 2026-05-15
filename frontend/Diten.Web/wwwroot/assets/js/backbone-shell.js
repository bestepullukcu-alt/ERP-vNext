'use strict';

window.BackboneShell = (function () {
    function hideSandboxMenuItems() {
        window.setTimeout(function () {
            document.querySelectorAll('.menu-header, .menu-item').forEach(function (el) {
                var link = (el.querySelector('a')?.getAttribute('href') || '').toLowerCase();
                if (link.includes('themesandbox')) {
                    el.style.display = 'none';
                }
            });
        }, 100);
    }

    function initUserDisplay() {
        var user = window.CurrentUser || {};
        var nameEl = document.getElementById('navbar-user-name');
        var roleEl = document.getElementById('navbar-user-role');

        if (!nameEl || !roleEl) return;

        var displayName = user.displayName ||
            [user.firstName, user.lastName].filter(Boolean).join(' ') ||
            user.email ||
            nameEl.textContent;
        var role = Array.isArray(user.roles) && user.roles.length > 0
            ? user.roles[0]
            : (user.actorType || window.L10n?.User || 'User');
        var initials = user.initials || createInitials(displayName, user.email);

        nameEl.textContent = displayName;
        roleEl.textContent = humanize(role);
        document.querySelectorAll('[data-platform-account-avatar]').forEach(function (el) {
            el.textContent = initials;
        });
    }

    function createInitials(displayName, email) {
        var source = String(displayName || '').trim() ||
            String(email || '').split('@')[0].replace(/[._-]+/g, ' ');
        var initials = source
            .split(/\s+/)
            .filter(Boolean)
            .slice(0, 2)
            .map(function (part) { return part.charAt(0); })
            .join('')
            .toUpperCase();
        return initials || '?';
    }

    function humanize(value) {
        return String(value || '')
            .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
            .replace(/[_-]+/g, ' ');
    }

    function bindLanguagePersistence() {
        document.querySelectorAll('.dropdown-language .dropdown-item').forEach(function (item) {
            item.addEventListener('click', function () {
                var lang = this.getAttribute('data-language');
                if (!lang) return;
                document.cookie = '.AspNetCore.Culture=c=' + lang + '|uic=' + lang + ';path=/;max-age=31536000';
            });
        });
    }

    function syncRequiredFieldTemplates() {
        var l10n = window.L10n || {};
        window.RequiredProgressText = l10n.RequiredProgressText;
        window.ValidationErrorsText = l10n.ValidationErrorsText;
    }

    function ensureConfirmFallback() {
        if (typeof window.showConfirm !== 'undefined') return;

        window.showConfirm = function (_key, callback) {
            if (window.confirm(window.L10n?.AreYouSure || 'Are you sure?')) {
                callback?.();
            }
        };
    }

    function handleTempDataToasts() {
        var tempData = window.BackboneShellTempData || {};
        if (tempData.successMessage && window.showToast) {
            window.showToast(tempData.successMessage, 'success');
        }
        if (tempData.errorMessage && window.showToast) {
            window.showToast(tempData.errorMessage, 'error');
        }
    }

    function init() {
        hideSandboxMenuItems();
        initUserDisplay();
        bindLanguagePersistence();
        syncRequiredFieldTemplates();
        ensureConfirmFallback();
        handleTempDataToasts();
    }

    return {
        init: init
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    window.BackboneShell.init();
});
