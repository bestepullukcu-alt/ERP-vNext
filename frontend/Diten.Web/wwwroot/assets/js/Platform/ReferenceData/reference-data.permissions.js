'use strict';

(function () {
    const root = document.querySelector('[data-rd-permissions="true"]');
    const defaultReason = 'You do not have permission to perform this action.';
    const unavailableReason = 'This action is unavailable for the current state.';
    const retiredSetReason = 'This reference data set is retired. Changes are disabled.';
    let pageBlockReason = null;

    const readFlag = (capability) => {
        if (!root || !capability) return true;
        return String(root.dataset[capability] || '').toLowerCase() === 'true';
    };

    const normalize = (value) => String(value || '').trim().toLowerCase();
    const isRetiredSet = (setInfo) => normalize(setInfo?.status || setInfo?.Status) === 'retired';
    const setGlobalBlock = (blocked, reason) => {
        pageBlockReason = blocked ? (reason || unavailableReason) : null;
    };

    const setDisabled = (element, disabled, reason) => {
        if (!element) return;
        element.disabled = !!disabled;
        if (disabled) {
            element.setAttribute('title', reason || unavailableReason);
            element.setAttribute('aria-disabled', 'true');
            element.dataset.disabledReason = reason || unavailableReason;
        } else {
            element.removeAttribute('title');
            element.setAttribute('aria-disabled', 'false');
            delete element.dataset.disabledReason;
        }
    };

    const hide = (element) => {
        if (!element) return;
        element.classList.add('d-none');
        element.setAttribute('aria-hidden', 'true');
    };

    const show = (element) => {
        if (!element) return;
        element.classList.remove('d-none');
        element.removeAttribute('aria-hidden');
    };

    window.ReferenceDataPermissions = {
        can: readFlag,
        reason: () => defaultReason,
        apply: (element, capability, stateAllowed, stateReason) => {
            const permissionAllowed = readFlag(capability);
            const stateOk = stateAllowed !== false;
            const blocked = !!pageBlockReason;
            const reason = blocked ? pageBlockReason : permissionAllowed ? (stateReason || unavailableReason) : defaultReason;
            setDisabled(element, blocked || !(permissionAllowed && stateOk), reason);
            return !blocked && permissionAllowed && stateOk;
        },
        hideUnless: (element, capability) => {
            if (readFlag(capability)) {
                show(element);
                return true;
            }

            hide(element);
            return false;
        },
        guard: (capability, notify) => {
            if (pageBlockReason) {
                if (typeof notify === 'function') {
                    notify(pageBlockReason);
                }
                return false;
            }
            if (readFlag(capability)) return true;
            if (typeof notify === 'function') {
                notify(defaultReason);
            }
            return false;
        },
        setGlobalBlock,
        clearGlobalBlock: () => setGlobalBlock(false),
        blockReason: () => pageBlockReason,
        isBlocked: () => !!pageBlockReason,
        retiredSetReason,
        isRetiredSet,
        applySetState: (setInfo, notify) => {
            const retired = isRetiredSet(setInfo);
            setGlobalBlock(retired, retiredSetReason);
            if (retired && typeof notify === 'function') {
                notify(retiredSetReason);
            }
            return !retired;
        },
        setDisabled,
        hide,
        show
    };
})();
