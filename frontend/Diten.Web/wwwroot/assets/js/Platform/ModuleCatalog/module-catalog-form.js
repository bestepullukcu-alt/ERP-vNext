/**
 * Module Catalog Form (Create/Edit) logic
 */
'use strict';

const ModuleCatalogForm = (function () {
    const form = document.getElementById('moduleCatalogForm');
    const btnSave = document.getElementById('btnSaveModule');
    const inputModuleCode = document.getElementById('ModuleCode');
    const inputModuleName = document.getElementById('ModuleName');
    const inputDisplayName = document.getElementById('DisplayName');
    const previewModuleCode = document.getElementById('moduleCodePreview');
    const previewSpan = previewModuleCode?.querySelector('span');
    const L = window.L10n || {};
    const isEdit = inputModuleCode?.dataset?.isEdit === 'true';
    let moduleCodeManuallyEdited = Boolean(inputModuleCode?.value);

    const normalizeCode = (value) => {
        return (value || '')
            .toUpperCase()
            .replace(/[^A-Z0-9]+/g, '-')
            .replace(/-+/g, '-')
            .replace(/^-|-$/g, '')
            .slice(0, 80)
            .replace(/-$/g, '');
    };

    const updatePreview = () => {
        if (!inputModuleCode || !previewModuleCode || !previewSpan) return;
        const normalized = normalizeCode(inputModuleCode.value);
        if (normalized.length > 0) {
            previewSpan.textContent = normalized;
            previewModuleCode.classList.remove('d-none');
        } else {
            previewModuleCode.classList.add('d-none');
        }
    };

    const toggleSaveButton = () => {
        if (!form || !btnSave) return;
        
        const isValid = form.checkValidity();
        btnSave.disabled = !isValid;
    };

    const handleValidation = (element) => {
        if (!element) return;
        
        if (element.checkValidity()) {
            element.classList.remove('is-invalid');
            element.classList.add('is-valid');
        } else {
            element.classList.remove('is-valid');
            element.classList.add('is-invalid');
        }
    };

    const initSelect2 = () => {
        if (typeof jQuery === 'undefined' || !jQuery.fn.select2) return;

        $('.select2').each(function () {
            const $this = $(this);
            const isStatus = $this.attr('id') === 'Status';

            const options = {
                dropdownParent: $this.parent()
            };

            if (isStatus) {
                options.templateResult = formatStatus;
                options.templateSelection = formatStatus;
                options.minimumResultsForSearch = Infinity;
            }

            $this.wrap('<div class="position-relative"></div>').select2(options);
        });

        function formatStatus(state) {
            if (!state.id) return state.text;
            let colorClass = 'text-muted';
            switch (state.id) {
                case 'Active': colorClass = 'text-success'; break;
                case 'Inactive': colorClass = 'text-warning'; break;
                case 'Deprecated': colorClass = 'text-danger'; break;
                case 'Draft': colorClass = 'text-secondary'; break;
            }
            return $('<span class="' + colorClass + ' fw-medium"><i class="bx bxs-circle me-1 small"></i>' + state.text + '</span>');
        }
    };

    const syncGeneratedModuleCode = () => {
        if (!inputModuleCode || isEdit || moduleCodeManuallyEdited) return;

        const source = inputDisplayName?.value || inputModuleName?.value || '';
        const normalized = normalizeCode(source);
        if (inputModuleCode.value !== normalized) {
            inputModuleCode.value = normalized;
        }

        updatePreview();
        handleValidation(inputModuleCode);
        toggleSaveButton();
    };

    const unwrapLookupRows = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.Data)) return payload.Data;
        return [];
    };

    const normalizeLookupOption = (item) => {
        const value = item?.value ?? item?.Value ?? item?.code ?? item?.Code ?? item?.id ?? item?.Id ?? '';
        const text = item?.name ?? item?.Name ?? item?.text ?? item?.Text ?? value;
        return {
            value: String(value || ''),
            text: String(text || value || '')
        };
    };

    const populateLookupSelect = async (select) => {
        const lookupUrl = select?.dataset?.lookupUrl;
        if (!select || !lookupUrl) return;

        const selectedValue = select.dataset.selectedValue || select.value || '';
        try {
            const response = await fetch(lookupUrl);
            if (!response.ok) throw new Error(`Lookup request failed: ${response.status}`);

            const payload = await response.json();
            const rows = unwrapLookupRows(payload)
                .map(normalizeLookupOption)
                .filter(option => option.value && option.text);

            const placeholder = select.querySelector('option[value=""]')?.textContent || '';
            select.innerHTML = '';
            select.appendChild(new Option(placeholder, ''));
            rows.forEach(option => {
                select.appendChild(new Option(option.text, option.value, false, option.value === selectedValue));
            });

            if (selectedValue && !rows.some(option => option.value === selectedValue)) {
                select.appendChild(new Option(selectedValue, selectedValue, true, true));
            }

            if (typeof jQuery !== 'undefined') {
                jQuery(select).trigger('change.select2');
            }
        } catch (error) {
            console.error('[ModuleCatalogForm] Lookup load failed.', error);
            if (selectedValue && !select.querySelector(`option[value="${CSS.escape(selectedValue)}"]`)) {
                select.appendChild(new Option(selectedValue, selectedValue, true, true));
            }
            window.showToast?.(L.ErrorOccurred || 'Error occurred.', 'error');
        } finally {
            toggleSaveButton();
        }
    };

    const loadLookupOptions = async () => {
        const lookupSelects = form?.querySelectorAll('select[data-lookup-url]') || [];
        await Promise.all(Array.from(lookupSelects).map(populateLookupSelect));
    };

    const initTooltips = () => {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    };

    const bindEvents = () => {
        if (inputModuleCode) {
            inputModuleCode.addEventListener('input', function () {
                moduleCodeManuallyEdited = true;
                const normalized = normalizeCode(this.value);
                if (this.value !== normalized) {
                    this.value = normalized;
                }
                updatePreview();
                handleValidation(this);
                toggleSaveButton();
            });
            // Initial preview if editing
            updatePreview();
        }

        inputDisplayName?.addEventListener('input', syncGeneratedModuleCode);
        inputModuleName?.addEventListener('input', syncGeneratedModuleCode);

        if (form) {
            // Monitor all inputs for validation state
            const inputs = form.querySelectorAll('input:not(#ModuleCode), select, textarea');
            inputs.forEach(input => {
                input.addEventListener('input', () => {
                    handleValidation(input);
                    toggleSaveButton();
                });
                input.addEventListener('change', () => {
                    handleValidation(input);
                    toggleSaveButton();
                });
            });

            // Special handling for Select2
            $('.select2').on('change', function (e) {
                const el = e.target;
                const $selection = $(el).next('.select2-container').find('.select2-selection');
                
                if (el.checkValidity()) {
                    $selection.removeClass('is-invalid').addClass('is-valid');
                } else {
                    $selection.removeClass('is-valid').addClass('is-invalid');
                }
                toggleSaveButton();
            });

            // Prevent default form submission if invalid
            form.addEventListener('submit', function (e) {
                if (!form.checkValidity()) {
                    e.preventDefault();
                    e.stopPropagation();
                }
                form.classList.add('was-validated');
            }, false);

            // Initial check
            toggleSaveButton();
        }
    };

    const init = () => {
        initSelect2();
        initTooltips();
        bindEvents();
        loadLookupOptions();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ModuleCatalogForm.init());
