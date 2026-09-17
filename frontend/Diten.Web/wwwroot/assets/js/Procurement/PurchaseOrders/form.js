'use strict';

// MOD-0141 Purchase Order Create/Edit form init. Shared by Create.cshtml and Edit.cshtml so the views carry no
// inline script. Select2 setup mirrors the shipped Sourcing form.js; adds repeatable PoLine rows and the supplier
// lookup (MOD-0140 consume). Item/SKU/UoM ids are free-typed opaque references validated server-side (fail-closed).
// No hardcoded gateway port in browser JS (same-origin lookup proxy).
document.addEventListener('DOMContentLoaded', function () {
    /*
     * SELECT2 — placeholder is DECLARED so the theme greys it like every other hint. The option text stays
     * localized in the resx behind the markup.
     */
    var select2Elements = $('.select2');
    if (select2Elements.length) {
        select2Elements.each(function () {
            var $this = $(this);
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent(),
                placeholder: $this.data('placeholder') || $this.find('option[value=""]').text() || ''
            });
        });
    }

    // Supplier options (MOD-0140 consume) — populated from the same-origin lookup proxy, preserving any pre-selected
    // id (edit mode). Validated server-side (fail-closed 404 for unknown supplier).
    var $supplier = $('#supplierId');
    if ($supplier.length) {
        fetch('/PurchaseOrders/lookups', { method: 'GET', credentials: 'same-origin' })
            .then(function (res) { return res.ok ? res.json() : null; })
            .then(function (data) {
                if (!data || !Array.isArray(data.suppliers)) return;
                var selected = $supplier.val() || '';
                var existing = new Set(($supplier.find('option').toArray()).map(function (o) { return o.value; }));
                data.suppliers.forEach(function (item) {
                    if (!item || !item.value || existing.has(item.value)) return;
                    $supplier.append(new Option(item.text || item.value, item.value, false, false));
                });
                $supplier.val(selected).trigger('change');
            })
            .catch(function (error) { console.error('[PurchaseOrders Lookup] Failed.', error); });
    }

    // Repeatable PoLine rows. Names are reindexed contiguously (Lines[0..n]) on every add/remove so the
    // MVC model binder receives a dense list.
    var body = document.getElementById('poLinesBody');
    if (!body) return;
    var prefix = body.getAttribute('data-line-prefix') || 'Lines';

    var reindex = function () {
        var rows = body.querySelectorAll('.po-line-row');
        rows.forEach(function (row, i) {
            row.querySelectorAll('.po-line-input').forEach(function (input) {
                var field = input.getAttribute('data-line-field');
                input.setAttribute('name', prefix + '[' + i + '].' + field);
                input.setAttribute('id', prefix + '_' + i + '__' + field);
            });
            var removeBtn = row.querySelector('.po-line-remove');
            if (removeBtn) removeBtn.disabled = rows.length <= 1;
        });
    };

    var addRow = function () {
        var rows = body.querySelectorAll('.po-line-row');
        var template = rows[rows.length - 1];
        if (!template) return;
        var clone = template.cloneNode(true);
        clone.querySelectorAll('.po-line-input').forEach(function (input) { input.value = ''; });
        body.appendChild(clone);
        reindex();
    };

    document.getElementById('btnAddLine')?.addEventListener('click', addRow);
    body.addEventListener('click', function (e) {
        var btn = e.target.closest('.po-line-remove');
        if (!btn) return;
        var rows = body.querySelectorAll('.po-line-row');
        if (rows.length <= 1) return;
        btn.closest('.po-line-row')?.remove();
        reindex();
    });

    reindex();
});
