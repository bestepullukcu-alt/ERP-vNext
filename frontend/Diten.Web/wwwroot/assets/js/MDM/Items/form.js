'use strict';

const ItemFormPage = (function () {
    const formEl = document.getElementById('formItem');
    const payloadEl = document.getElementById('item-form-data');
    if (!formEl || !payloadEl) {
        return { init: function () { } };
    }

    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const itemTypeEl = document.getElementById('ItemTypeId');
    const categoryEl = document.getElementById('CategoryId');
    const variantModelEl = document.getElementById('VariantModelId');
    const serviceItemEl = document.getElementById('ServiceItem');
    const attributeHost = document.getElementById('itemAttributesEditor');
    const variantHost = document.getElementById('itemVariantsEditor');
    const attributeTemplateCountEl = document.getElementById('attributeTemplateCount');
    const variantHintEl = document.getElementById('variantAxisHint');
    const attributeValuesJsonEl = document.getElementById('AttributeValuesJson');
    const variantsJsonEl = document.getElementById('VariantsJson');

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(';').shift();
        }

        return null;
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (error) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const payload = JSON.parse(payloadEl.textContent || '{}');
    const L = Object.assign({
        noAttributeTemplateSelected: '',
        noVariantAxisDefined: '',
        noVariantsDefined: '',
        variantLabel: 'Variant',
        remove: 'Remove',
        code: 'Code',
        name: 'Name',
        active: 'Active'
    }, payload.l10n || {});
    let templates = Array.isArray(payload.variantTemplates) ? payload.variantTemplates : [];
    let attributeValues = Array.isArray(payload.attributeValues) ? payload.attributeValues : [];
    let variants = Array.isArray(payload.variants) ? payload.variants : [];

    const initSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) {
            return;
        }

        $(formEl).find('.select2').each(function () {
            $(this).select2({
                width: '100%'
            });
        });
    };

    const syncServiceItemWithType = () => {
        const selectedOption = itemTypeEl?.selectedOptions?.[0];
        const itemTypeName = selectedOption?.textContent?.trim()?.toLowerCase() || '';
        if (serviceItemEl) {
            serviceItemEl.checked = itemTypeName === 'service item';
        }
    };

    const filterDependentOptions = () => {
        const selectedItemTypeId = itemTypeEl?.value || '';
        if (categoryEl) {
            Array.from(categoryEl.options).forEach((option) => {
                if (!option.value) {
                    option.hidden = false;
                    return;
                }

                const matches = !selectedItemTypeId || option.dataset.itemTypeId === selectedItemTypeId;
                option.hidden = !matches;
            });

            if (categoryEl.selectedOptions[0]?.hidden) {
                categoryEl.value = '';
            }
        }

        if (variantModelEl) {
            Array.from(variantModelEl.options).forEach((option) => {
                if (!option.value) {
                    option.hidden = false;
                    return;
                }

                const matches = !selectedItemTypeId || option.dataset.itemTypeId === selectedItemTypeId;
                option.hidden = !matches;
            });

            if (variantModelEl.selectedOptions[0]?.hidden) {
                variantModelEl.value = '';
            }
        }
    };

    const renderAttributes = () => {
        const itemTemplates = templates.filter((template) => !template.isVariantAxis);
        attributeHost.innerHTML = '';
        attributeTemplateCountEl.textContent = String(itemTemplates.length);

        if (!itemTemplates.length) {
            attributeHost.innerHTML = `<p class="text-muted mb-0">${L.noAttributeTemplateSelected}</p>`;
            return;
        }

        itemTemplates.forEach((template) => {
            const existing = attributeValues.find((value) => value.attributeDefinitionId === template.attributeDefinitionId);
            const row = document.createElement('div');
            row.className = 'border rounded p-3';
            row.innerHTML = `
                <label class="form-label d-flex justify-content-between">
                    <span>${template.attributeName}</span>
                    ${template.isRequired ? '<span class="text-danger">*</span>' : ''}
                </label>
                <input type="text" class="form-control js-item-attribute"
                       data-attribute-definition-id="${template.attributeDefinitionId}"
                       value="${existing?.value ?? ''}">
            `;
            attributeHost.appendChild(row);
        });
    };

    const buildVariantTemplateInputs = (variant, variantIndex) => {
        const variantTemplates = templates.filter((template) => template.isVariantAxis);
        if (!variantTemplates.length) {
            return `<p class="text-muted mb-0">${L.noVariantAxisDefined}</p>`;
        }

        return variantTemplates.map((template) => {
            const existing = (variant.attributeValues || []).find((value) => value.attributeDefinitionId === template.attributeDefinitionId);
            return `
                <div class="col-md-6">
                    <label class="form-label d-flex justify-content-between">
                        <span>${template.attributeName}</span>
                        ${template.isRequired ? '<span class="text-danger">*</span>' : ''}
                    </label>
                    <input type="text" class="form-control js-variant-attribute"
                           data-variant-index="${variantIndex}"
                           data-attribute-definition-id="${template.attributeDefinitionId}"
                           value="${existing?.value ?? ''}">
                </div>
            `;
        }).join('');
    };

    const renderVariants = () => {
        variantHost.innerHTML = '';
        const variantTemplates = templates.filter((template) => template.isVariantAxis);
        variantHintEl.classList.toggle('d-none', variantTemplates.length === 0);

        if (!variants.length) {
            variantHost.innerHTML = `<div class="text-muted">${L.noVariantsDefined}</div>`;
            return;
        }

        variants.forEach((variant, index) => {
            const card = document.createElement('div');
            card.className = 'border rounded p-3';
            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-center mb-3">
                    <h6 class="mb-0">${L.variantLabel} ${index + 1}</h6>
                    <button type="button" class="btn btn-sm btn-label-danger js-remove-variant" data-variant-index="${index}">${L.remove}</button>
                </div>
                <div class="row g-3">
                    <div class="col-md-4">
                        <label class="form-label">${L.code}</label>
                        <input type="text" class="form-control js-variant-code" data-variant-index="${index}" value="${variant.code ?? ''}">
                    </div>
                    <div class="col-md-5">
                        <label class="form-label">${L.name}</label>
                        <input type="text" class="form-control js-variant-name" data-variant-index="${index}" value="${variant.name ?? ''}">
                    </div>
                    <div class="col-md-3 d-flex align-items-end">
                        <div class="form-check form-switch">
                            <input type="checkbox" class="form-check-input js-variant-active" data-variant-index="${index}" ${variant.isActive !== false ? 'checked' : ''}>
                            <label class="form-check-label">${L.active}</label>
                        </div>
                    </div>
                    ${buildVariantTemplateInputs(variant, index)}
                </div>
            `;
            variantHost.appendChild(card);
        });
    };

    const collectState = () => {
        attributeValues = Array.from(formEl.querySelectorAll('.js-item-attribute'))
            .map((input) => ({
                attributeDefinitionId: input.dataset.attributeDefinitionId,
                value: input.value.trim()
            }))
            .filter((value) => value.value);

        variants = Array.from(formEl.querySelectorAll('.js-variant-code'))
            .map((input) => {
                const index = input.dataset.variantIndex;
                const name = formEl.querySelector(`.js-variant-name[data-variant-index="${index}"]`)?.value?.trim() || '';
                const isActive = !!formEl.querySelector(`.js-variant-active[data-variant-index="${index}"]`)?.checked;
                const attributePayload = Array.from(formEl.querySelectorAll(`.js-variant-attribute[data-variant-index="${index}"]`))
                    .map((attributeInput) => ({
                        attributeDefinitionId: attributeInput.dataset.attributeDefinitionId,
                        value: attributeInput.value.trim()
                    }))
                    .filter((value) => value.value);

                return {
                    code: input.value.trim(),
                    name: name,
                    isActive: isActive,
                    attributeValues: attributePayload
                };
            })
            .filter((variant) => variant.code || variant.name || variant.attributeValues.length);

        attributeValuesJsonEl.value = JSON.stringify(attributeValues);
        variantsJsonEl.value = JSON.stringify(variants);
    };

    const addVariant = () => {
        variants.push({ code: '', name: '', isActive: true, attributeValues: [] });
        renderVariants();
    };

    const removeVariant = (index) => {
        variants = variants.filter((_, currentIndex) => currentIndex !== index);
        renderVariants();
    };

    const loadTemplates = async () => {
        const variantModelId = variantModelEl?.value;
        if (!variantModelId) {
            templates = [];
            attributeValues = [];
            variants = [];
            renderAttributes();
            renderVariants();
            return;
        }

        try {
            const response = await fetch(`${apiUrl}/api/item-variant-models/${variantModelId}`, {
                method: 'GET',
                headers: getAuthHeaders()
            });

            if (!response.ok) {
                throw new Error('Variant model request failed.');
            }

            const variantModel = await response.json();
            templates = Array.isArray(variantModel.attributes) ? variantModel.attributes : [];
        } catch (error) {
            console.error('Variant model templates could not be loaded.', error);
            templates = [];
        }

        renderAttributes();
        renderVariants();
    };

    const bindEvents = () => {
        itemTypeEl?.addEventListener('change', () => {
            filterDependentOptions();
            syncServiceItemWithType();
        });

        variantModelEl?.addEventListener('change', () => {
            loadTemplates();
        });

        document.getElementById('btnAddVariant')?.addEventListener('click', addVariant);

        variantHost.addEventListener('click', (event) => {
            const removeBtn = event.target.closest('.js-remove-variant');
            if (!removeBtn) {
                return;
            }

            removeVariant(Number(removeBtn.dataset.variantIndex));
        });

        formEl.addEventListener('submit', () => {
            collectState();
        });
    };

    return {
        init: function () {
            initSelect2();
            filterDependentOptions();
            syncServiceItemWithType();
            renderAttributes();
            renderVariants();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => ItemFormPage.init());
