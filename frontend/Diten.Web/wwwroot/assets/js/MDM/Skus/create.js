/**
 * Skus – Create/Edit Page Script
 * JS-002: Module Pattern (IIFE)
 */
'use strict';

const SkusFormManager = (function () {
    const initSelect2 = () => {
        const select2Elements = $('.select2');
        if (!select2Elements.length) return;

        select2Elements.each(function () {
            const $el = $(this);
            $el.wrap('<div class="position-relative"></div>').select2({
                placeholder: $el.find('option[value=""]').text() || '',
                dropdownParent: $el.parent()
            });
        });
    };

    const init = () => {
        initSelect2();
        
        // Form validation feedback
        const form = document.getElementById('formSkus');
        if (form) {
            form.addEventListener('submit', function (event) {
                if (!form.checkValidity()) {
                    event.preventDefault();
                    event.stopPropagation();
                }
                form.classList.add('was-validated');
            }, false);
        }
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => SkusFormManager.init());
