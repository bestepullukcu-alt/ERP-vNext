/**
 * Module Catalog Form (Create/Edit) logic
 */
'use strict';

const ModuleCatalogForm = (function () {
    const form = document.getElementById('moduleCatalogForm');
    const btnSave = document.getElementById('btnSaveModule');
    const inputModuleCode = document.getElementById('ModuleCode');
    const previewModuleCode = document.getElementById('moduleCodePreview');
    const previewSpan = previewModuleCode?.querySelector('span');
    const L = window.L10n || {};

    const normalizeCode = (value) => {
        return (value || '')
            .toUpperCase()
            .replace(/\s+/g, '-')
            .replace(/[^A-Z0-9-]/g, '')
            .replace(/-+/g, '-');
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

    const initTooltips = () => {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    };

    const bindEvents = () => {
        if (inputModuleCode) {
            inputModuleCode.addEventListener('input', function () {
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
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ModuleCatalogForm.init());
