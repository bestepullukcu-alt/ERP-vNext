/**
 * Legal Entities – Create Page Script
 * Follows JS-002: Module Pattern
 */

'use strict';

const LegalEntityCreateManager = (function () {

    function initSelect2() {
        const select2Elements = $('.select2');
        if (select2Elements.length) {
            select2Elements.each(function () {
                const $el = $(this);
                $el.wrap('<div class="position-relative"></div>').select2({
                    placeholder: $el.find('option[value=""]').text() || 'Select',
                    dropdownParent: $el.parent(),
                    allowClear: true
                });
            });
        }
    }

    function initFlatpickr() {
        const dateInputs = document.querySelectorAll('.flatpickr-date');
        if (dateInputs.length) {
            dateInputs.forEach(function (el) {
                el.flatpickr({
                    monthSelectorType: 'static'
                });
            });
        }
    }

    function initInputRestrictions() {
        // Phone mask: strictly digits, +, -, (), and space
        const phoneInputs = document.querySelectorAll('.phone-mask');
        phoneInputs.forEach(el => {
            el.addEventListener('input', function (e) {
                this.value = this.value.replace(/[^0-9+\-()\s]/g, '');
            });
        });

        // Numeric only: strictly digits
        const numericInputs = document.querySelectorAll('.numeric-only');
        numericInputs.forEach(el => {
            el.addEventListener('input', function (e) {
                this.value = this.value.replace(/[^0-9]/g, '');
            });
        });

        // Fiscal Year mask: digits and hyphens (01-01)
        const fiscalInputs = document.querySelectorAll('.fiscal-year-mask');
        fiscalInputs.forEach(el => {
            el.addEventListener('input', function (e) {
                this.value = this.value.replace(/[^0-9\-]/g, '');
            });
        });
    }

    function initFormValidation() {
        const form = document.getElementById('formCreateLegalEntity');
        if (form) {
            form.addEventListener('submit', function (e) {
                let isValid = form.checkValidity();

                // Clear all previous manual error messages
                $(form).find('.invalid-feedback').text('');

                if (!isValid) {
                    e.preventDefault();
                    e.stopPropagation();

                    // For each invalid element, try to find a localized message
                    $(form).find('input, select, textarea').each(function () {
                        if (!this.validity.valid) {
                            const $el = $(this);
                            const name = $el.attr('name');
                            const $feedback = $(`[data-valmsg-for="${name}"]`);

                            if ($feedback.length) {
                                let message = '';
                                // Priority: 1. Specific Data-Val message from model, 2. Native browser message
                                if (this.validity.valueMissing) {
                                    message = $el.attr('data-val-required');
                                } else if (this.validity.typeMismatch || this.validity.patternMismatch) {
                                    // Try to find any regex or type message from the model
                                    message = $el.attr('data-val-regex') ||
                                        $el.attr('data-val-email') ||
                                        $el.attr('data-val-url') ||
                                        $el.attr('data-val-phone');
                                }

                                // Fallback to native or generic if model translation not found
                                if (!message) message = this.validationMessage;

                                $feedback.text(message).show();
                            }
                        }
                    });
                }
                form.classList.add('was-validated');
            }, false);

            // Also handle individual input events to clear errors as user types
            $(form).on('input change', 'input, select, textarea', function () {
                if (this.checkValidity()) {
                    $(this).removeClass('is-invalid');
                    $(`[data-valmsg-for="${$(this).attr('name')}"]`).text('').hide();
                }
            });
        }
    }

    function init() {
        initSelect2();
        initFlatpickr();
        initInputRestrictions();
        initFormValidation();
    }

    return { init };
})();

document.addEventListener('DOMContentLoaded', function () {
    LegalEntityCreateManager.init();
});
