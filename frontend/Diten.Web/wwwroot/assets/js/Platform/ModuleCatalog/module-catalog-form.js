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

    // MC-3a — module code is a canonical slug: lowercase + PermissionKeyParser rule [a-z][a-z0-9-]*.
    const MODULE_CODE_SLUG_RE = /^[a-z][a-z0-9-]*$/;

    // Map machine-readable backend error codes to localized messages (mirrors the
    // server-side LocalizeGatewayError switch; used if errors ever surface client-side).
    const localizeError = (code) => {
        switch (code) {
            case 'MODULE_MANAGED_BY_CODE':
                return L.ModuleManagedByCode || code;
            case 'MODULE_CODE_IN_USE':
                return L.ModuleCodeInUse || code;
            default:
                return code;
        }
    };

    // Lowercase-normalize the module code as the user types and validate the slug
    // format. Invalid input marks the field is-invalid and blocks submit. Readonly
    // (edit mode) is left untouched — module code is HARD/immutable once created.
    const handleModuleCodeInput = () => {
        if (!inputModuleCode || isEdit) return;
        const normalized = (inputModuleCode.value || '').toLowerCase();
        if (inputModuleCode.value !== normalized) {
            const caret = inputModuleCode.selectionStart;
            inputModuleCode.value = normalized;
            try { inputModuleCode.setSelectionRange(caret, caret); } catch (error) { /* noop */ }
        }
        const value = inputModuleCode.value.trim();
        const valid = value.length > 0 && MODULE_CODE_SLUG_RE.test(value);
        inputModuleCode.classList.toggle('is-invalid', !valid && value.length > 0);
        inputModuleCode.classList.toggle('is-valid', valid);
        if (!valid) {
            inputModuleCode.setCustomValidity('invalid-module-code');
        } else {
            inputModuleCode.setCustomValidity('');
        }
        toggleSaveButton();
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
            if ($this.hasClass('select2-hidden-accessible')) $this.select2('destroy');

            const options = {
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select',
                width: 'element',
                placeholder: $this.data('placeholder') || ''
            };

            if (isStatus) {
                options.templateResult = formatStatus;
                options.templateSelection = formatStatus;
                options.minimumResultsForSearch = Infinity;
            }

            $this.select2(options);
        });

        function formatStatus(state) {
            if (!state.id) return state.text;
            let colorClass = 'text-muted';
            switch (state.id) {
                case 'Active': colorClass = 'text-success'; break;
                case 'Inactive': colorClass = 'text-warning'; break;
                case 'Deprecated': colorClass = 'text-danger'; break;
                case 'Draft': colorClass = 'text-secondary'; break;
                case 'Beta': colorClass = 'text-info'; break;
                case 'Preview': colorClass = 'text-primary'; break;
            }
            return $('<span class="' + colorClass + ' fw-medium"><i class="bx bxs-circle me-1 small"></i>' + state.text + '</span>');
        }
    };

    const unwrapLookupRows = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.Data)) return payload.Data;
        return [];
    };

    const normalizeLookupOption = (item) => {
        if (typeof item === 'string') {
            return { value: item, text: item };
        }
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

            // Module code: lowercase-normalize + slug validation (create mode only;
            // edit mode the field is readonly/immutable).
            if (inputModuleCode && !isEdit) {
                inputModuleCode.addEventListener('input', handleModuleCodeInput);
                inputModuleCode.addEventListener('blur', handleModuleCodeInput);
            }

            // FIX-MODULE-ICON — live boxicons preview: mirror the typed class onto the input-group prefix <i>.
            const iconInput = document.getElementById('moduleIconInput');
            const iconPreview = document.getElementById('moduleIconPreview');
            if (iconInput && iconPreview) {
                const syncIconPreview = () => {
                    const value = (iconInput.value || '').trim();
                    // Keep only a valid boxicons class; fall back to bx-box while empty/invalid so the badge never breaks.
                    const cls = /^bxs?-[a-z0-9-]+$/.test(value) ? value : 'bx-box';
                    iconPreview.className = 'bx ' + cls;
                };
                iconInput.addEventListener('input', syncIconPreview);
                syncIconPreview();
            }

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
