/**
 * Enhanced Unified Form Tracker
 * Dynamically scans the page for required form inputs and validation errors.
 */
document.addEventListener("DOMContentLoaded", function () {
    const forms = document.querySelectorAll("form:not([data-no-tracker])");
    if (forms.length === 0) return;

    let requiredElements = [];
    let allTrackedElements = [];

    // Initialize tracking for all forms
    forms.forEach(form => {
        const inputs = form.querySelectorAll("input, select, textarea");
        inputs.forEach(input => {
            const isRequired = input.hasAttribute("required") || input.hasAttribute("data-val-required");

            // Check if label has asterisk
            let labelHasAsterisk = false;
            if (input.id) {
                const label = document.querySelector(`label[for="${input.id}"]`);
                if (label && label.querySelector('.text-danger')) {
                    labelHasAsterisk = true;
                }
            }

            if (isRequired || labelHasAsterisk) {
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

    // Create the badge UI
    const trackerId = "global-unified-tracker";
    let badge = document.getElementById(trackerId);

    if (!badge) {
        const submitBtn = document.querySelector('button[type="submit"]') || document.querySelector('.btn-primary');
        if (submitBtn) {
            const actionContainer = submitBtn.parentNode;
            const wrapper = document.createElement("div");
            wrapper.className = "d-flex align-items-center me-4";

            wrapper.innerHTML = `
                <span id="${trackerId}" class="badge bg-label-danger fs-6 px-3 py-2 d-flex align-items-center" style="opacity: 0.9; transition: all 0.3s ease; cursor: default;">
                    <div class="required-part me-2 border-end pe-2 d-flex align-items-center" style="border-color: rgba(0,0,0,0.1) !important;">
                        <i class="bx bx-check-shield me-1 lh-1"></i> <span class="required-text">0 / 0</span>
                    </div>
                    <div class="error-part d-none d-flex align-items-center">
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
                    isFilled = val && val !== "";
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
            // Check native validity (handles type="email", pattern, etc.)
            // We only count errors if the field has been touched or the form was submitted
            // However, for real-time feedback, we check if it has a value AND is invalid
            if (el.value.trim() !== "" && !el.checkValidity()) {
                errorCount++;
            }
        });

        // 3. Update UI
        const totalRequired = requiredElements.length;
        const requiredTextSpan = badge.querySelector('.required-text');
        const errorTextSpan = badge.querySelector('.error-text');
        const errorPart = badge.querySelector('.error-part');

        // Localization templates
        const reqTemplate = window.RequiredProgressText || "Required: {0} / {1}";
        const errTemplate = window.ValidationErrorsText || "Errors: {0}";

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

    // Attach events to all tracked elements for errors, and required elements for progress
    allTrackedElements.forEach(el => {
        el.addEventListener("input", updateProgress);
        el.addEventListener("change", updateProgress);

        if (el.tagName === "SELECT" && $(el).hasClass('select2-hidden-accessible')) {
            $(el).on('select2:select select2:unselect', function () {
                updateProgress();
            });
        }
    });

    // Initial check
    updateProgress();
});
