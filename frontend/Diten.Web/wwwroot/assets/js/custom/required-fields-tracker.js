/**
 * Enhanced Unified Form Tracker
 * Dynamically scans the page for required form inputs and validation errors.
 */
document.addEventListener("DOMContentLoaded", function () {
    const forms = document.querySelectorAll("form:not([data-no-tracker])");
    if (forms.length === 0) return;

    let requiredElements = [];
    let allTrackedElements = [];

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

    // Initialize tracking for all forms
    forms.forEach(form => {
        const inputs = form.querySelectorAll("input, select, textarea");
        inputs.forEach(input => {
            const hasHtmlRequired = input.hasAttribute("required");
            const hasValidationRequired = input.hasAttribute("data-val-required");

            // Check if label has asterisk
            let labelHasAsterisk = false;
            if (input.id) {
                const label = document.querySelector(`label[for="${input.id}"]`);
                if (label && label.querySelector('.text-danger')) {
                    labelHasAsterisk = true;
                }
            }

            const shouldTrackRequired = (input.type === "checkbox" || input.type === "radio")
                ? (hasHtmlRequired || labelHasAsterisk)
                : (hasHtmlRequired || hasValidationRequired || labelHasAsterisk);

            if (shouldTrackRequired) {
                if (input.type !== 'hidden') {
                    requiredElements.push(input);
                }
            }

            // Track all non-hidden inputs for validation errors (even if not required)
            if (input.type !== 'hidden') {
                allTrackedElements.push(input);
            }
        });
    });

    if (allTrackedElements.length === 0) return;

    const trackerForm = Array.from(forms).find(form =>
        requiredElements.some(el => el.form === form) ||
        allTrackedElements.some(el => el.form === form)
    );

    // Create the badge UI
    const trackerId = "global-unified-tracker";
    let badge = document.getElementById(trackerId);

    if (!badge) {
        const submitBtn = findSubmitButtonForForm(trackerForm);
        if (submitBtn) {
            const targetSelector = trackerForm?.dataset?.requiredTrackerTarget;
            const targetContainer = targetSelector ? document.querySelector(targetSelector) : null;
            const actionContainer = targetContainer || submitBtn.parentNode;
            const wrapper = document.createElement("div");
            wrapper.className = trackerForm?.dataset?.requiredTrackerWrapperClass || "d-flex align-items-center me-4";

            const badgeClass = trackerForm?.dataset?.requiredTrackerBadgeClass || "badge bg-label-danger fs-6 px-3 py-2 d-flex align-items-center";

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
            badge = document.getElementById(trackerId);
        }
    }

    function updateProgress() {
        if (!badge) return;

        // 1. Calculate Required Progress
        let filledCount = 0;
        requiredElements.forEach(el => {
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

        // 2. Count Validation Errors
        let errorCount = 0;
        allTrackedElements.forEach(el => {
            // Check native validity AND jQuery Validation class
            const isInvalidNative = el.value.trim() !== "" && !el.checkValidity();
            const hasValidationErrorClass = el.classList.contains('input-validation-error');

            if (isInvalidNative || hasValidationErrorClass) {
                errorCount++;
            }
        });

        // 3. Update UI
        const totalRequired = requiredElements.length;
        const requiredTextSpan = badge.querySelector('.required-text');
        const errorTextSpan = badge.querySelector('.error-text');
        const errorPart = badge.querySelector('.error-part');

        // Localization templates with safe fallbacks
        let reqTemplate = window.RequiredProgressText || (window.L10n && window.L10n.RequiredProgressText);
        if (typeof reqTemplate !== 'string' || (!reqTemplate.includes("{0}") && !reqTemplate.includes("{{0}}"))) {
            reqTemplate = "Required: {0} / {1}";
        } else {
            // Support both standard {0} and some double-curly variants if present
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
            // If there's an error, it's always at least warning/danger
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
            // No errors logic
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
    }

    // Watch for class changes (specifically for 'input-validation-error')
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            if (mutation.type === "attributes" && mutation.attributeName === "class") {
                updateProgress();
            }
        });
    });

    // Attach events to all tracked elements
    allTrackedElements.forEach(el => {
        el.addEventListener("input", updateProgress);
        el.addEventListener("change", updateProgress);
        el.addEventListener("focusout", updateProgress); // Catch validation on blur

        // Start observing for class changes
        observer.observe(el, { attributes: true, attributeFilter: ["class"] });

        if (el.tagName === "SELECT" && $(el).hasClass('select2-hidden-accessible')) {
            $(el).on('select2:select select2:unselect select2:clear change.select2', function () {
                updateProgress();
            });
        }
    });

    // Initial check
    updateProgress();
});
