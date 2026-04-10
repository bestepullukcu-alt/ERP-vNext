/**
 * Composition Management - Form JS
 * Handles dynamic component rows and composition-specific validation.
 */
'use strict';

$(function () {
    const apiBaseUrl = window.ApiBaseUrl || '';
    const form = $('#formComposition');
    const componentsContainer = $('#componentsContainer');
    const templateRow = $('#templateComponentRow').html();
    const btnAddComponent = $('#btnAddComponent');
    const validationMessage = $('#componentsValidationMessage');
    const defaultUnitId = form.data('default-unit-id') || '';
    const ingredientPlaceholder = form.data('ingredient-placeholder') || 'Search ingredient...';
    const componentsRequiredMessage = form.data('components-required-message') || 'At least one component is required.';
    const componentsValidationMessage = form.data('components-validation-message') || 'Please complete each component row before saving.';
    const componentIngredientMessage = form.data('component-ingredient-message') || 'Ingredient selection is required.';
    const componentQuantityMessage = form.data('component-quantity-message') || 'Quantity must be greater than zero.';
    const componentUnitMessage = form.data('component-unit-message') || 'Component unit is required.';
    const duplicateIngredientMessage = form.data('duplicate-ingredient-message') || 'Duplicate ingredients are not allowed.';

    $('.select2').each(function () {
        initStandardSelect2($(this));
    });

    initializeExistingRows();
    
    btnAddComponent.on('click', function () {
        addComponentRow();
    });

    componentsContainer.on('click', '.btn-remove-component', function () {
        $(this).closest('.component-row').remove();
        reIndexRows();
        clearComponentValidation();
    });

    componentsContainer.on('change input', 'select, input', function () {
        clearFieldValidation($(this));
        clearComponentValidation();
    });

    form.on('submit', function (event) {
        if (!validateComponents()) {
            event.preventDefault();
        }
    });

    function initStandardSelect2(element) {
        if (!element.parent().hasClass('position-relative')) {
            element.wrap('<div class="position-relative"></div>');
        }

        element.select2({
            placeholder: element.attr('placeholder') || 'Select...',
            dropdownParent: element.parent()
        });
    }

    function initComponentSelect2(row) {
        const ingredientSelect = row.find('.select2-component');
        if (!ingredientSelect.parent().hasClass('position-relative')) {
            ingredientSelect.wrap('<div class="position-relative"></div>');
        }

        ingredientSelect.select2({
            placeholder: ingredientPlaceholder,
            dropdownParent: ingredientSelect.parent(),
            ajax: {
                url: `${apiBaseUrl}/api/items`,
                type: 'GET',
                dataType: 'json',
                delay: 250,
                headers: {
                    Authorization: `Bearer ${getCookie('access_token')}`,
                    'X-Tenant-Id': getTenantId()
                },
                data(params) {
                    return {
                        search: params.term,
                        page: params.page || 1
                    };
                },
                processResults(data) {
                    return {
                        results: (data.data || []).map((item) => ({
                            id: item.id,
                            text: `${item.name} (${item.code})`
                        }))
                    };
                },
                cache: true
            },
            minimumInputLength: 1
        });

        const unitSelect = row.find('.select2-simple');
        if (!unitSelect.parent().hasClass('position-relative')) {
            unitSelect.wrap('<div class="position-relative"></div>');
        }

        unitSelect.select2({
            dropdownParent: unitSelect.parent()
        });
    }

    function initializeExistingRows() {
        componentsContainer.find('.component-row').each(function () {
            initComponentSelect2($(this));
        });
    }

    function addComponentRow() {
        const index = componentsContainer.find('.component-row').length;
        const sequence = index + 1;
        const newRow = $(templateRow.replace(/{index}/g, index).replace(/{sequence}/g, sequence));
        componentsContainer.append(newRow);
        initComponentSelect2(newRow);

        if (defaultUnitId) {
            newRow.find('select[name$=".UnitId"]').val(defaultUnitId).trigger('change');
        }
    }

    function ensureAtLeastOneRow() {
        if (componentsContainer.find('.component-row').length === 0) {
            addComponentRow();
        }
    }

    function reIndexRows() {
        componentsContainer.find('.component-row').each(function (index) {
            const row = $(this);
            const sequence = index + 1;
            row.attr('data-index', index);
            
            // Update visual sequence
            row.find('.row-sequence').text(sequence);
            
            // Update sequence input value
            row.find('.row-sequence-input').val(sequence);
            
            row.find('[name]').each(function () {
                const input = $(this);
                const name = input.attr('name');
                input.attr('name', name.replace(/\[\d+\]/, `[${index}]`));
            });
        });
    }

    function validateComponents() {
        clearComponentValidation();

        const rows = componentsContainer.find('.component-row');
        if (rows.length === 0) {
            showComponentValidation(componentsRequiredMessage);
            return false;
        }

        let isValid = true;
        const selectedIngredients = new Set();
        let hasDuplicate = false;

        rows.each(function () {
            const row = $(this);
            const ingredientField = row.find('select[name$=".ComponentId"]');
            const quantityField = row.find('input[name$=".Quantity"]');
            const unitField = row.find('select[name$=".UnitId"]');
            const quantityValue = Number.parseFloat(quantityField.val());
            const ingredientId = ingredientField.val();

            if (!ingredientId) {
                markInvalid(ingredientField, componentIngredientMessage);
                isValid = false;
            } else if (selectedIngredients.has(ingredientId)) {
                markInvalid(ingredientField, duplicateIngredientMessage);
                isValid = false;
                hasDuplicate = true;
            } else {
                selectedIngredients.add(ingredientId);
            }

            if (!Number.isFinite(quantityValue) || quantityValue <= 0) {
                markInvalid(quantityField, componentQuantityMessage);
                isValid = false;
            }

            if (!unitField.val()) {
                markInvalid(unitField, componentUnitMessage);
                isValid = false;
            }
        });

        if (!isValid) {
            const finalMessage = hasDuplicate ? duplicateIngredientMessage : componentsValidationMessage;
            showComponentValidation(finalMessage);
        }

        return isValid;
    }

    function markInvalid(field, message) {
        field.addClass('is-invalid');

        const invalidFeedback = field.siblings('.invalid-feedback');
        if (invalidFeedback.length) {
            invalidFeedback.text(message);
            return;
        }

        field.after(`<div class="invalid-feedback d-block">${message}</div>`);
    }

    function clearFieldValidation(field) {
        field.removeClass('is-invalid');
        field.siblings('.invalid-feedback.d-block').remove();
    }

    function showComponentValidation(message) {
        validationMessage.text(message).removeClass('d-none');
    }

    function clearComponentValidation() {
        validationMessage.addClass('d-none').text('');
    }

    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(';').shift();
        }

        return '';
    }

    function getTenantId() {
        return window.currentTenantId || '00000000-0000-0000-0000-000000000001';
    }
});
