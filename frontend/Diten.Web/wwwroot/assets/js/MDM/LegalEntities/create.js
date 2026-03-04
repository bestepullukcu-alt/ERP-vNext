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
                    dropdownParent: $el.parent()
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

    function initFormValidation() {
        const form = document.getElementById('formCreateLegalEntity');
        if (form) {
            form.addEventListener('submit', function (e) {
                if (!form.checkValidity()) {
                    e.preventDefault();
                    e.stopPropagation();
                }
                form.classList.add('was-validated');
            }, false);
        }
    }

    function init() {
        initSelect2();
        initFlatpickr();
        initFormValidation();
    }

    return { init };
})();

document.addEventListener('DOMContentLoaded', function () {
    LegalEntityCreateManager.init();
});
