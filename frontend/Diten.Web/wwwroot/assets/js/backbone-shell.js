'use strict';

window.BackboneShell = (function () {
    function hideSandboxMenuItems() {
        window.setTimeout(function () {
            document.querySelectorAll('.menu-header, .menu-item').forEach(function (el) {
                var text = (el.textContent || '').toLowerCase();
                var link = (el.querySelector('a')?.getAttribute('href') || '').toLowerCase();
                if (text.includes('sandbox') || link.includes('themesandbox')) {
                    el.style.display = 'none';
                }
            });
        }, 100);
    }

    function initUserDisplay() {
        var user = window.CurrentUser || {};
        var nameEl = document.getElementById('navbar-user-name');
        var roleEl = document.getElementById('navbar-user-role');

        if (!user.firstName || !nameEl || !roleEl) return;

        nameEl.textContent = [user.firstName, user.lastName].filter(Boolean).join(' ');
        roleEl.textContent = Array.isArray(user.roles) && user.roles.length > 0 ? user.roles[0] : 'User';
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
            if (window.confirm('Are you sure?')) {
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
