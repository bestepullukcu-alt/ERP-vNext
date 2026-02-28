/**
 * Legal Entities Create Script
 */

'use strict';

document.addEventListener('DOMContentLoaded', function (e) {
    (function () {
        // Initialize Select2
        var select2 = $('.select2');
        if (select2.length) {
            select2.each(function () {
                var $this = $(this);
                $this.wrap('<div class="position-relative"></div>').select2({
                    placeholder: 'Select',
                    dropdownParent: $this.parent()
                });
            });
        }

        // Initialize Flatpickr
        const flatpickrDate = document.querySelectorAll('.flatpickr-date');
        if (flatpickrDate) {
            flatpickrDate.forEach(function (element) {
                element.flatpickr({
                    monthSelectorType: 'static'
                });
            });
        }

        // Bootstrap Form Validation Enable
        const formCreateLegalEntity = document.getElementById('formCreateLegalEntity');
        if (formCreateLegalEntity) {
            formCreateLegalEntity.addEventListener('submit', function (event) {
                if (!formCreateLegalEntity.checkValidity()) {
                    event.preventDefault();
                    event.stopPropagation();
                }
                formCreateLegalEntity.classList.add('was-validated');
            }, false);
        }

    })();
});
