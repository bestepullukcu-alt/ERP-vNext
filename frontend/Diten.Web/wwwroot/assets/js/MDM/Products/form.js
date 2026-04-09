'use strict';

const ProductFormPage = (function () {
    const formEl = document.getElementById('formProduct');
    const productTypeEl = document.getElementById('ProductType');
    const categoryEl = document.getElementById('CategoryId');
    let categoryPlaceholder = '';
    let categoryOptions = [];

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

        if (categoryEl && window.jQuery) {
            categoryPlaceholder = $(categoryEl).find('option[value=""]').text() || '';
        }

        if (categoryEl) {
            categoryOptions = Array.from(categoryEl.options)
                .filter((option) => option.value)
                .map((option) => ({
                    value: option.value,
                    text: option.text,
                    productType: option.dataset.productType || ''
                }));
        }
    };

    const refreshCategorySelect2 = () => {
        if (!categoryEl || !window.jQuery || !$.fn.select2) {
            return;
        }

        const $category = $(categoryEl);
        if ($category.hasClass('select2-hidden-accessible')) {
            $category.select2('destroy');
        }

        $category.select2({
            placeholder: categoryPlaceholder || $category.find('option[value=""]').text() || '',
            dropdownParent: $category.parent(),
            width: '100%',
            allowClear: true
        });
    };

    const filterCategoryOptions = () => {
        const selectedProductType = productTypeEl?.value || '';
        if (!categoryEl) {
            return;
        }

        const currentValue = categoryEl.value;
        const filteredOptions = categoryOptions.filter((option) => option.productType === selectedProductType);

        categoryEl.innerHTML = '';
        categoryEl.append(new Option(categoryPlaceholder, ''));

        filteredOptions.forEach((option) => {
            const renderedOption = new Option(option.text, option.value);
            renderedOption.dataset.productType = option.productType;
            categoryEl.append(renderedOption);
        });

        const hasCurrentValue = filteredOptions.some((option) => option.value === currentValue);
        categoryEl.value = hasCurrentValue ? currentValue : '';

        categoryEl.disabled = !selectedProductType;
        if (window.jQuery) {
            $(categoryEl).prop('disabled', !selectedProductType);
            refreshCategorySelect2();
            $(categoryEl).trigger('change');
        }
    };

    const bindEvents = () => {
        productTypeEl?.addEventListener('change', filterCategoryOptions);
        if (productTypeEl && window.jQuery) {
            $(productTypeEl).on('change.product-form', filterCategoryOptions);
        }
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
