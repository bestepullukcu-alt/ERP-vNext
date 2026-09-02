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

    /*
     * ══ AN ACKNOWLEDGEMENT THAT SURVIVES A NAVIGATION ═════════════════════════════════════════════════════
     *
     * The client-side twin of `handleTempDataToasts` above, and it sits here for the same reason that one
     * does: a page that saves and then LEAVES has nowhere to raise a toast — the page raising it is about to
     * be replaced. The server solves that with TempData; a form that navigates in the browser needs the same
     * hand-over on the browser's side.
     *
     * ⚠ WHY IT IS NOT A MODAL. /Tasks/Create used to hold the reader in a dialog so the acknowledgement would
     * be seen. That works, and it is the wrong instrument: a modal exists to ASK, and "created" asks nothing.
     * The reader is stopped mid-flow to confirm something they did on purpose.
     *
     * ⚠ WHY IT IS NOT SIX PRIVATE KEYS. Six Organization screens already do this, each with its own
     * sessionStorage key flushed on its own list page (`p-toast`, `ou-toast`, `a-toast`, …). That works only
     * while a form knows its single destination. The task form does not: it lands on the Task Center, or on
     * any local returnUrl it was handed. So the flush lives where EVERY tenant page passes, and the key has
     * exactly one owner — this file — which both writers and readers go through.
     *
     * ⚠ SPENT ON READ, always. A message left behind would re-announce a save on the next reload, which is a
     * page claiming something happened that did not.
     */
    var PENDING_TOAST_KEY = 'diten-pending-toast';

    function handOverToast(message, type) {
        if (!message) return;
        try {
            window.sessionStorage.setItem(
                PENDING_TOAST_KEY, JSON.stringify({ message: message, type: type || 'success' }));
        } catch (error) {
            // Private mode, or storage full. The save itself succeeded; losing its announcement is the least
            // bad outcome and is not worth breaking the navigation over.
            console.warn('[BackboneShell] the save acknowledgement could not be handed over:', error);
        }
    }

    function flushPendingToast() {
        var raw = null;
        try {
            raw = window.sessionStorage.getItem(PENDING_TOAST_KEY);
            window.sessionStorage.removeItem(PENDING_TOAST_KEY);
        } catch (error) {
            return;
        }
        if (!raw || !window.showToast) return;

        try {
            var pending = JSON.parse(raw);
            if (pending && pending.message) {
                window.showToast(pending.message, pending.type || 'success');
            }
        } catch (error) {
            console.warn('[BackboneShell] a handed-over toast could not be read:', error);
        }
    }

    function init() {
        hideSandboxMenuItems();
        initUserDisplay();
        bindLanguagePersistence();
        syncRequiredFieldTemplates();
        ensureConfirmFallback();
        handleTempDataToasts();
        flushPendingToast();
    }

    return {
        init: init,
        // The ONE way a page hands an acknowledgement across a navigation — see handOverToast.
        handOverToast: handOverToast
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    window.BackboneShell.init();
});
