'use strict';

// MOD-0145 Sourcing Create/Edit form init. Shared by Create.cshtml and Edit.cshtml so the views carry no
// inline script. Select2 setup mirrors the shipped Suppliers form.js; adds repeatable RfxLine rows and the
// invited-supplier lookup (MOD-0140 consume). No hardcoded gateway port in browser JS (same-origin lookup proxy).
document.addEventListener('DOMContentLoaded', function () {
    /*
     * SELECT2 — placeholder is DECLARED so the theme greys it like every other hint. The option text stays
     * localized in the resx behind the markup.
     */
    var select2Elements = $('.select2');
    if (select2Elements.length) {
        select2Elements.each(function () {
            var $this = $(this);
            var isTags = $this.attr('id') === 'invitedSupplierIds';
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent(),
                tags: isTags,
                placeholder: $this.data('placeholder') || $this.find('option[value=""]').text() || ''
            });
        });
    }

    // Invited supplier options (MOD-0140 consume) — populated from the same-origin lookup proxy, preserving
    // any pre-selected ids (edit mode). Free-typed ids are allowed (tags) and validated server-side (fail-closed).
    var $invited = $('#invitedSupplierIds');
    if ($invited.length) {
        fetch('/Sourcing/lookups', { method: 'GET', credentials: 'same-origin' })
            .then(function (res) { return res.ok ? res.json() : null; })
            .then(function (data) {
                if (!data || !Array.isArray(data.suppliers)) return;
                var selected = $invited.val() || [];
                var existing = new Set(($invited.find('option').toArray()).map(function (o) { return o.value; }));
                data.suppliers.forEach(function (item) {
                    if (!item || !item.value || existing.has(item.value)) return;
                    $invited.append(new Option(item.text || item.value, item.value, false, false));
                });
                $invited.val(selected).trigger('change');
            })
            .catch(function (error) { console.error('[Sourcing Lookup] Failed.', error); });
    }

    // Repeatable RfxLine rows. Names are reindexed contiguously (Lines[0..n]) on every add/remove so the
    // MVC model binder receives a dense list.
    var body = document.getElementById('rfxLinesBody');
    if (!body) return;
    var prefix = body.getAttribute('data-line-prefix') || 'Lines';

    var reindex = function () {
        var rows = body.querySelectorAll('.rfx-line-row');
        rows.forEach(function (row, i) {
            row.querySelectorAll('.rfx-line-input').forEach(function (input) {
                var field = input.getAttribute('data-line-field');
                input.setAttribute('name', prefix + '[' + i + '].' + field);
                input.setAttribute('id', prefix + '_' + i + '__' + field);
            });
            var removeBtn = row.querySelector('.rfx-line-remove');
            if (removeBtn) removeBtn.disabled = rows.length <= 1;
        });
    };

    var addRow = function () {
        var rows = body.querySelectorAll('.rfx-line-row');
        var template = rows[rows.length - 1];
        if (!template) return;
        var clone = template.cloneNode(true);
        clone.querySelectorAll('.rfx-line-input').forEach(function (input) { input.value = ''; });
        body.appendChild(clone);
        reindex();
    };

    document.getElementById('btnAddLine')?.addEventListener('click', addRow);
    body.addEventListener('click', function (e) {
        var btn = e.target.closest('.rfx-line-remove');
        if (!btn) return;
        var rows = body.querySelectorAll('.rfx-line-row');
        if (rows.length <= 1) return;
        btn.closest('.rfx-line-row')?.remove();
        reindex();
    });

    reindex();
});
