'use strict';

const ProductFormPage = (function () {
    const formEl = document.getElementById('formProduct');
    const productTypeEl = document.getElementById('ProductType');
    const categoryEl = document.getElementById('CategoryId');

    if (!formEl) {
        return { init: function () { } };
    }

    const initSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) {
            return;
        }

        $(formEl).find('.select2').each(function () {
            const $el = $(this);
            if (!$el.parent().hasClass('position-relative')) {
                $el.wrap('<div class="position-relative"></div>');
            }

            $el.select2({
                placeholder: $el.find('option[value=""]').text() || '',
                dropdownParent: $el.parent(),
                width: '100%',
                allowClear: true
            });
        });
    };

    const filterCategoryOptions = () => {
        const selectedProductType = productTypeEl?.value || '';
        if (!categoryEl) {
            return;
        }

        Array.from(categoryEl.options).forEach((option) => {
            if (!option.value) {
                option.hidden = false;
                return;
            }

            const matches = !selectedProductType || option.dataset.productType === selectedProductType;
            option.hidden = !matches;
        });

        if (categoryEl.selectedOptions[0]?.hidden) {
            categoryEl.value = '';
            if (window.jQuery) {
                $(categoryEl).trigger('change');
            }
        }
    };

    const bindEvents = () => {
        productTypeEl?.addEventListener('change', filterCategoryOptions);
    };

    return {
        init: function () {
            initSelect2();
            filterCategoryOptions();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ProductFormPage.init());
