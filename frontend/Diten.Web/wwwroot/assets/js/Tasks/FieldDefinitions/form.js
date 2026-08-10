'use strict';

// TaskFieldDefinitions Create/Edit form init (flatpickr + Select2 + the option-source chooser).
// Shared by Create.cshtml and Edit.cshtml so the views carry no inline script.
(function () {
    /* ── The option-source chooser ────────────────────────────────────────────────────────────────────────────
     *
     * The source key used to be a free-text box. Typing "country" where the set is called "COUNTRY" saved a
     * definition that then never appeared on any form — the resolver refused the unknown source, and the task
     * form dropped the field rather than showing an empty picker. Both of those are the RIGHT behaviours and
     * both stay. What is removed here is the way to get into that state: a key that can only be chosen cannot be
     * mistyped.
     *
     * Nothing in this file knows the name of a single source. The kind is asked of the server, and the answer is
     * whatever the platform, the tenant's reference sets and the registered modules currently offer.
     */

    var l10n = (function () {
        var node = document.getElementById('taskfielddefinitions-form-l10n');
        if (!node) { return { sourceLabels: {} }; }
        try { return JSON.parse(node.textContent) || { sourceLabels: {} }; }
        catch (_) { return { sourceLabels: {} }; }
    })();

    // Our own sources ship a resource key and the seven resx files carry the words; a tenant's own reference set
    // carries its own name and has no key. Falling back to the label the server sent keeps a source that is
    // missing a translation READABLE rather than blank.
    function sourceLabel(source) {
        var key = source.labelResourceKey || source.LabelResourceKey;
        var translated = key ? l10n.sourceLabels[key] : null;
        return translated || source.label || source.Label || source.key || source.Key || '';
    }

    function option(value, text, disabled) {
        var element = document.createElement('option');
        element.value = value;
        element.textContent = text;
        if (disabled) { element.disabled = true; }
        return element;
    }

    async function loadSources(kind) {
        var select = document.querySelector('[data-options-source-key]');
        if (!select) { return; }

        // The value BEFORE the list is replaced. On the first load of an edit form this is the definition's
        // stored key, and re-selecting it is the difference between an edit that keeps its source and one that
        // silently clears it.
        var previous = select.getAttribute('data-selected') || select.value || '';
        select.innerHTML = '';

        if (!kind || kind === 'None') {
            // No kind, no key. Disabled rather than hidden: a control that vanishes reads as a bug, and the
            // administrator has to be able to see that the field simply has no source.
            select.appendChild(option('', l10n.selectSourcePlaceholder || '', true));
            select.disabled = true;
            select.value = '';
            return;
        }

        var result = await window.TasksApi.fieldOptionSources(kind);
        if (!result.ok) {
            select.appendChild(option('', l10n.loadFailed || '', true));
            select.disabled = true;
            window.console && window.console.warn(
                '[TaskFieldDefinitions] the option sources for kind "' + kind + '" could not be read (status '
                + result.status + (result.reasonCode ? ', ' + result.reasonCode : '') + ').');
            return;
        }

        var sources = Array.isArray(result.data) ? result.data : [];
        if (sources.length === 0) {
            // A real state, and it says so: this kind offers nothing in this tenant today.
            select.appendChild(option('', l10n.noSources || '', true));
            select.disabled = true;
            return;
        }

        select.disabled = false;
        select.appendChild(option('', l10n.selectSourcePlaceholder || ''));
        sources.forEach(function (source) {
            select.appendChild(option(source.key || source.Key, sourceLabel(source)));
        });

        /*
         * The old selection survives ONLY if the new kind still offers it. Carrying it across a kind change
         * would store a reference-set code under a platform-lookup kind — a pairing that saves and then never
         * resolves, which is the same disappearing field by another route.
         */
        var stillOffered = Array.prototype.some.call(
            select.options, function (o) { return o.value === previous; });
        select.value = stillOffered ? previous : '';
    }

    function wireSourceChooser() {
        var kindSelect = document.querySelector('select[name="OptionsSourceKind"]');
        var keySelect = document.querySelector('[data-options-source-key]');
        if (!kindSelect || !keySelect) { return; }

        // The stored key, kept aside before the first load empties the control.
        keySelect.setAttribute('data-selected', keySelect.getAttribute('data-selected') || keySelect.value || '');

        loadSources(kindSelect.value);

        // jQuery, because Select2 replaces the native control and its change does not reach a plain listener.
        $(kindSelect).on('change', function () {
            // A key belonging to the previous kind is not carried over — see loadSources.
            keySelect.setAttribute('data-selected', '');
            loadSources(kindSelect.value);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        // Flatpickr
        var flatpickrDate = document.querySelectorAll('.flatpickr-date');
        if (flatpickrDate) {
            flatpickrDate.forEach(function (element) {
                element.flatpickr({
                    monthSelectorType: 'static',
                    dateFormat: 'Y-m-d'
                });
            });
        }

        // Select2
        var select2Elements = $('.select2');
        if (select2Elements.length) {
            select2Elements.each(function () {
                var $this = $(this);
                $this.wrap('<div class="position-relative"></div>').select2({
                    dropdownParent: $this.parent()
                });
            });
        }

        wireSourceChooser();
    });
})();
