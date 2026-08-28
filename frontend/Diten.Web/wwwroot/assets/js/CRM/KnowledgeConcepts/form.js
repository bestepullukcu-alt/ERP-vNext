/**
 * MOD-0162-FU03 Concept Node form — Compact create/edit interactions:
 *  - Subject → ConceptType cascade (types carry SubjectId as option data-group)
 *  - ExternalRefType = global-product → a VISIBLE Select2 picker over the MDM selector (same-origin proxy), styled
 *    like every other picker on this form; any other type → the free-text ExternalRefId box. 404/403 → the picker
 *    renders disabled with the reason (AC-UI-2: never a silent empty list).
 *  - flatpickr on effective-window dates.
 *
 * NOTE: Create.cshtml / Edit.cshtml do NOT load the _IndexL10n bridge, so window.L10n is empty here. Every user-facing
 * string on this form is therefore rendered localized by Razor and read back from a data-* attribute — never from L.
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;
    const L = window.ConceptL10n || window.L10n || {};

    document.addEventListener('DOMContentLoaded', () => {
        if (window.flatpickr) document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
        if ($ && $.fn.select2) $('.select2').each(function () { $(this).select2({ width: '100%' }); });

        setupSubjectTypeCascade();
        setupGlobalProductPicker();
    });

    function setupSubjectTypeCascade() {
        const subject = document.getElementById('conceptSubjectId');
        const type = document.getElementById('conceptTypeId');
        if (!subject || !type) return;
        const allOptions = Array.from(type.options).map(o => ({ value: o.value, text: o.textContent, group: o.getAttribute('data-group') || '' }));
        const current = type.getAttribute('data-current') || '';

        const rebuild = () => {
            const subjectId = subject.value;
            const keep = allOptions.filter(o => !o.value || !o.group || o.group === subjectId);
            type.innerHTML = keep.map(o => `<option value="${o.value}"${o.value === current ? ' selected' : ''}>${o.text}</option>`).join('');
            if ($ && $.fn.select2) $(type).trigger('change');
        };
        // Only cascade on create (subject editable); on edit subject/type are fixed.
        if (!subject.hasAttribute('readonly')) {
            subject.addEventListener('change', rebuild);
            if ($ && $.fn.select2) $(subject).on('change', rebuild);
        }
    }

    // ExternalRefType = global-product → a visible Select2 picker over the MDM selector. Any other type → the
    // free-text id box. Exactly one is on screen at a time.
    //
    // The free-text input is the single persisted control (name="ExternalRefId"): it always stays in the DOM and is
    // only HIDDEN while the picker drives it, so the stored value posts either way. The picker has no name attribute
    // and never posts — it mirrors its selection into the input. The form contract stays ExternalRefId-only.
    function setupGlobalProductPicker() {
        const typeSel = document.getElementById('conceptExternalRefType');
        const refInput = document.getElementById('conceptExternalRefId');
        const picker = document.getElementById('conceptGlobalProductId');
        const note = document.getElementById('conceptGlobalProductPickerNote');
        if (!typeSel || !refInput || !picker) return;

        const refLabel = document.getElementById('conceptExternalRefIdLabel');
        const pickerLabel = document.getElementById('conceptGlobalProductLabel');
        const refHint = document.getElementById('conceptExternalRefIdHint');
        const pickerHint = document.getElementById('conceptGlobalProductHint');
        // Toggle the WRAPPER, not the <select>: Select2 hides the select and renders its own sibling container.
        const pickerWrap = document.getElementById('conceptGlobalProductWrap') || picker;

        // Every message is rendered localized by Razor: this page has no window.L10n bridge.
        const msg = {
            unavailable: picker.dataset.msgUnavailable || '',
            GlobalProductEndpointMissing: picker.dataset.msgEndpointMissing || '',
            GlobalProductPermissionMissing: picker.dataset.msgPermissionMissing || '',
            GlobalProductPickerUnavailable: picker.dataset.msgUnavailable || ''
        };
        const serverDisabledReason = picker.dataset.pickerDisabledReason || '';
        const pickerUrl = picker.dataset.pickerUrl;

        const showNote = text => { if (note) { note.textContent = text || ''; note.classList.toggle('d-none', !text); } };
        const setHidden = (el, hidden) => el && el.classList.toggle('d-none', !!hidden);
        const isGlobalProduct = () => (typeSel.value || '').trim() === 'global-product';

        let select2Ready = false;
        const disablePicker = reason => {
            picker.disabled = true;
            if ($ && $(picker).hasClass('select2-hidden-accessible')) $(picker).trigger('change.select2');
            showNote(reason);
        };

        // Select2 is initialised the first time the picker is actually visible: initialising it inside a d-none
        // container measures the control as zero-width. width:'100%' (not 'element') is what keeps it the same size
        // as the Subject / Concept Type / Status pickers next to it — matching them is the whole point of this fix.
        const ensureSelect2 = () => {
            if (select2Ready || !($ && $.fn.select2)) return;
            select2Ready = true;
            $(picker).select2({
                // Same shape as initWidgets() uses for every other picker on this form: a position-relative wrapper
                // as dropdownParent plus width:'100%'. That is what makes this control look like its siblings.
                dropdownParent: $(pickerWrap),
                dropdownCssClass: 'concept-global-product-dropdown',
                width: '100%',
                placeholder: picker.dataset.placeholder || '',
                allowClear: true,
                minimumInputLength: 0,
                language: {
                    searching: () => picker.dataset.msgSearching || '',
                    noResults: () => picker.dataset.msgNoResults || ''
                },
                ajax: {
                    url: pickerUrl,
                    dataType: 'json',
                    delay: 250,
                    // Same-origin proxy: jQuery sends the session cookie and the MVC proxy attaches the token
                    // server-side. The browser never builds an Authorization header.
                    // The MDM selector is a paged SEARCH endpoint (max pageSize 100), so the list is narrowed by
                    // typing rather than by scrolling a full dump.
                    data: params => ({ search: params.term || '', pageNumber: 1, pageSize: 100 }),
                    processResults: body => {
                        if (body && body.disabled) {
                            disablePicker(msg[body.reason] || msg.unavailable);
                            return { results: [] };
                        }
                        showNote('');
                        return { results: (body && body.options ? body.options : []).map(o => ({ id: o.value, text: o.label })) };
                    },
                    // Select2 aborts the in-flight request when a new search starts, so the transport must return the
                    // jqXHR itself (a .then() chain has no .abort()).
                    transport: (params, success, failure) => {
                        const request = $.ajax(params);
                        request.then(success);
                        request.fail(xhr => { disablePicker(msg.unavailable); failure(xhr); });
                        return request;
                    }
                }
            });
            // Mirror the selection onto the one persisted field.
            $(picker).on('change', () => { refInput.value = picker.value || ''; });
        };

        let booted = false;
        const apply = () => {
            const global = isGlobalProduct();

            setHidden(refInput, global);
            setHidden(refLabel, global);
            setHidden(refHint, global);
            setHidden(pickerWrap, !global);
            setHidden(pickerLabel, !global);
            setHidden(pickerHint, !global);

            if (!global) {
                // Leaving global-product does NOT wipe the value: the user sees the raw id in the now-visible box and
                // decides what it should be. Clearing it here would be a silent data change.
                showNote('');
                return;
            }

            // Switching INTO global-product from another type: whatever was in the box is by definition not a global
            // product id, so both controls are cleared. This is an explicit user action, not a silent edit — and it
            // must not happen on first render, where the server pre-resolved a genuine stored product.
            if (booted && picker.value !== refInput.value) {
                refInput.value = '';
                picker.value = '';
                if ($ && $(picker).hasClass('select2-hidden-accessible')) $(picker).trigger('change.select2');
            }

            ensureSelect2();

            // AC-UI-2: the server already probed the selector. 404 / 403 / unreachable → disabled + reason, never a
            // silent empty list. The stored value still posts, because the hidden input is hidden, not disabled.
            if (serverDisabledReason) { disablePicker(serverDisabledReason); return; }
            picker.disabled = false;
            showNote('');
        };

        typeSel.addEventListener('change', apply);
        if ($ && $.fn.select2) $(typeSel).on('change', apply);
        apply();
        booted = true;
    }
})(window, document);
