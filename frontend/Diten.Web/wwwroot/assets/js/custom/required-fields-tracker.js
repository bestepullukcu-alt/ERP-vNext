/**
 * Required Fields Tracker
 * Dynamically scans the page for required form inputs and displays a progress badge.
 */
document.addEventListener("DOMContentLoaded", function () {
    const forms = document.querySelectorAll("form");
    if (forms.length === 0) return;

    let requiredElements = [];

    // Find all required fields based on data-val-required or associated label with text-danger *
    forms.forEach(form => {
        const inputs = form.querySelectorAll("input, select, textarea");
        inputs.forEach(input => {
            // Check HTML5 required attribute or ASP.NET data-val-required
            let isRequired = input.hasAttribute("required") || input.hasAttribute("data-val-required");

            // Check if label has <span class="text-danger">*</span>
            if (!isRequired && input.id) {
                const label = document.querySelector(`label[for="${input.id}"]`);
                if (label && label.querySelector('.text-danger')) {
                    isRequired = true;
                }
            }

            // Optional: Skip hidden inputs or specific types
            if (isRequired && input.type !== 'hidden') {
                requiredElements.push(input);
            }
        });
    });

    if (requiredElements.length === 0) return;

    // Create the badge UI
    const trackerId = "global-required-tracker";
    let badge = document.getElementById(trackerId);

    if (!badge) {
        // Find the submit button or action area to place the tracker next to it
        const submitBtn = document.querySelector('button[type="submit"]') || document.querySelector('.btn-primary');

        if (submitBtn) {
            const actionContainer = submitBtn.parentNode;

            const wrapper = document.createElement("div");
            wrapper.className = "d-flex align-items-center me-4";

            // Adjust tracker look to blend with buttons
            wrapper.innerHTML = `
                <span id="${trackerId}" class="badge bg-label-danger fs-6 px-3 py-2" style="opacity: 0.9; transition: all 0.3s ease;">
                    <i class="bx bx-check-shield me-1"></i> <span class="tracker-text">0 / ${requiredElements.length}</span>
                </span>
            `;

            // Prepend it so it sits to the left of the Cancel/Save buttons
            actionContainer.insertBefore(wrapper, actionContainer.firstChild);
            badge = document.getElementById(trackerId);
        }
    }

    function updateProgress() {
        let filledCount = 0;
        requiredElements.forEach(el => {
            if (el.tagName === "SELECT") {
                // Handle Select2 or native select
                if ($(el).hasClass('select2-hidden-accessible')) {
                    const val = $(el).val();
                    if (val && val !== "") filledCount++;
                } else if (el.value.trim() !== "") {
                    filledCount++;
                }
            } else if (el.type === "radio" || el.type === "checkbox") {
                // If it's a group, checking if at least one is checked should be handled differently,
                // but for simple required checkbox:
                if (el.checked) filledCount++;
            } else {
                if (el.value.trim() !== "") filledCount++;
            }
        });

        const total = requiredElements.length;
        if (badge) {
            const textSpan = badge.querySelector('.tracker-text');
            // Using window.RequiredProgressText from Layout if available, otherwise fallback
            const template = window.RequiredProgressText || "{0} / {1}";
            textSpan.textContent = template.replace("{0}", filledCount).replace("{1}", total);

            if (filledCount === total) {
                badge.classList.remove('bg-label-danger');
                badge.classList.remove('bg-label-warning');
                badge.classList.add('bg-label-success');
            } else if (filledCount > 0) {
                badge.classList.remove('bg-label-danger');
                badge.classList.remove('bg-label-success');
                badge.classList.add('bg-label-warning');
            } else {
                badge.classList.add('bg-label-danger');
                badge.classList.remove('bg-label-warning');
                badge.classList.remove('bg-label-success');
            }
        }
    }

    // Attach events
    requiredElements.forEach(el => {
        el.addEventListener("input", updateProgress);
        el.addEventListener("change", updateProgress);

        // Select2 specific event
        if (el.tagName === "SELECT" && $(el).hasClass('select2-hidden-accessible')) {
            $(el).on('select2:select select2:unselect', function () {
                updateProgress();
            });
        }
    });

    // Initial check
    updateProgress();
});
