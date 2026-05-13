/**
 * Enhanced Unified Form Tracker
 * Per-form: scans each form on the page and renders a dedicated required/error badge.
 */
document.addEventListener("DOMContentLoaded", function () {
    const forms = document.querySelectorAll("form:not([data-no-tracker])");
    if (forms.length === 0) return;

    const cssEscape = (value) => {
        if (window.CSS && typeof window.CSS.escape === "function") {
            return window.CSS.escape(value);
        }
        return String(value).replace(/["\\]/g, "\\$&");
    };

    const findSubmitButtonForForm = (form) => {
        if (!form) return null;
        if (form.id) {
            const escapedId = cssEscape(form.id);
            const externalSubmit = document.querySelector(
                `button[type="submit"][form="${escapedId}"], input[type="submit"][form="${escapedId}"]`
            );
            if (externalSubmit) return externalSubmit;
        }
        return form.querySelector('button[type="submit"], input[type="submit"], .btn-primary');
    };

    const isVisibleInForm = (el, form) => {
        let node = el;
        while (node && node !== form) {
            if (node.classList && node.classList.contains('d-none')) return false;
            node = node.parentElement;
        }
        return true;
    };

    const collectFormInputs = (form) => {
        const required = [];
        const tracked = [];
        const inputs = form.querySelectorAll("input, select, textarea");
        inputs.forEach(input => {
            if (input.type === 'hidden') return;
            if (!isVisibleInForm(input, form)) return;

            const hasHtmlRequired = input.hasAttribute("required");
            const hasValidationRequired = input.hasAttribute("data-val-required");

            let labelHasAsterisk = false;
            if (input.id) {
                const label = document.querySelector(`label[for="${cssEscape(input.id)}"]`);
                if (label && label.querySelector('.text-danger:not(.js-partner-asterisk)')) {
                    labelHasAsterisk = true;
                }
            }

            const shouldTrackRequired = (input.type === "checkbox" || input.type === "radio")
                ? (hasHtmlRequired || labelHasAsterisk)
                : (hasHtmlRequired || hasValidationRequired || labelHasAsterisk);

            if (shouldTrackRequired) required.push(input);
            tracked.push(input);
        });
        return { required, tracked };
    };

    let trackerSeq = 0;

    const initTrackerForForm = (form) => {
        const { required, tracked } = collectFormInputs(form);
        if (tracked.length === 0) return;

        const targetSelector = form.dataset?.requiredTrackerTarget;
        const targetContainer = targetSelector ? document.querySelector(targetSelector) : null;
        const submitBtn = findSubmitButtonForForm(form);
        const actionContainer = targetContainer || (submitBtn ? submitBtn.parentNode : null);

        // No place to render and no required fields to surface — skip silently.
        if (!actionContainer) return;
        if (!targetContainer && required.length === 0) return;

        const trackerId = `unified-tracker-${++trackerSeq}`;
        const wrapper = document.createElement("div");
        wrapper.className = form.dataset?.requiredTrackerWrapperClass || "d-flex align-items-center me-4";

        const badgeClass = form.dataset?.requiredTrackerBadgeClass || "badge bg-label-danger fs-6 px-3 py-2 d-flex align-items-center";

        wrapper.innerHTML = `
            <span id="${trackerId}" class="${badgeClass}" style="opacity: 0.9; transition: all 0.3s ease; cursor: default;">
                <div class="required-part d-flex align-items-center">
                    <i class="bx bx-check-shield me-1 lh-1"></i> <span class="required-text">0 / 0</span>
                </div>
                <div class="error-part d-none d-flex align-items-center ms-2 ps-2 border-start" style="border-color: rgba(0,0,0,0.1) !important;">
                    <i class="bx bx-error-circle me-1 lh-1"></i> <span class="error-text">0</span>
                </div>
            </span>
        `;

        actionContainer.insertBefore(wrapper, actionContainer.firstChild);
        const badge = document.getElementById(trackerId);

        const updateProgress = () => {
            if (!badge) return;

            // Re-scan visible fields each time to handle dynamic show/hide
            const { required: currentRequired, tracked: currentTracked } = collectFormInputs(form);

            let filledCount = 0;
            currentRequired.forEach(el => {
                let isFilled = false;
                if (el.tagName === "SELECT") {
                    const $el = $(el);
                    if ($el.hasClass('select2-hidden-accessible')) {
                        const val = $el.val();
                        isFilled = Array.isArray(val)
                            ? val.length > 0
                            : (val !== null && String(val).trim() !== "");
                    } else {
                        isFilled = el.value.trim() !== "";
                    }
                } else if (el.type === "radio" || el.type === "checkbox") {
                    isFilled = el.checked;
                } else {
                    isFilled = el.value.trim() !== "";
                }
                if (isFilled) filledCount++;
            });

            let errorCount = 0;
            currentTracked.forEach(el => {
                const isInvalidNative = el.value.trim() !== "" && !el.checkValidity();
                const hasValidationErrorClass = el.classList.contains('input-validation-error');
                if (isInvalidNative || hasValidationErrorClass) errorCount++;
            });

            const totalRequired = currentRequired.length;
            const requiredTextSpan = badge.querySelector('.required-text');
            const errorTextSpan = badge.querySelector('.error-text');
            const errorPart = badge.querySelector('.error-part');

            let reqTemplate = window.RequiredProgressText || (window.L10n && window.L10n.RequiredProgressText);
            if (typeof reqTemplate !== 'string' || (!reqTemplate.includes("{0}") && !reqTemplate.includes("{{0}}"))) {
                reqTemplate = "Required: {0} / {1}";
            } else {
                reqTemplate = reqTemplate.replace("{{0}}", "{0}").replace("{{1}}", "{1}");
            }

            let errTemplate = window.ValidationErrorsText || (window.L10n && window.L10n.ValidationErrorsText);
            if (typeof errTemplate !== 'string' || (!errTemplate.includes("{0}") && !errTemplate.includes("{{0}}"))) {
                errTemplate = "Errors: {0}";
            } else {
                errTemplate = errTemplate.replace("{{0}}", "{0}");
            }

            requiredTextSpan.textContent = reqTemplate.replace("{0}", filledCount).replace("{1}", totalRequired);

            if (errorCount > 0) {
                errorTextSpan.textContent = errTemplate.replace("{0}", errorCount);
                errorPart.classList.remove('d-none');
                badge.classList.remove('bg-label-success');
                if (filledCount === totalRequired) {
                    badge.classList.add('bg-label-warning');
                    badge.classList.remove('bg-label-danger');
                } else {
                    badge.classList.add('bg-label-danger');
                    badge.classList.remove('bg-label-warning');
                }
            } else {
                errorPart.classList.add('d-none');
                if (filledCount === totalRequired) {
                    badge.classList.remove('bg-label-danger', 'bg-label-warning');
                    badge.classList.add('bg-label-success');
                } else if (filledCount > 0) {
                    badge.classList.remove('bg-label-danger', 'bg-label-success');
                    badge.classList.add('bg-label-warning');
                } else {
                    badge.classList.add('bg-label-danger');
                    badge.classList.remove('bg-label-warning', 'bg-label-success');
                }
            }
        };

        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.type === "attributes" && mutation.attributeName === "class") {
                    updateProgress();
                }
            });
        });

        tracked.forEach(el => {
            el.addEventListener("input", updateProgress);
            el.addEventListener("change", updateProgress);
            el.addEventListener("focusout", updateProgress);
            observer.observe(el, { attributes: true, attributeFilter: ["class"] });

            if (el.tagName === "SELECT" && $(el).hasClass('select2-hidden-accessible')) {
                $(el).on('select2:select select2:unselect select2:clear change.select2', updateProgress);
            }
        });

        updateProgress();
    };

    forms.forEach(initTrackerForForm);
});
