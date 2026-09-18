'use strict';

// MOD-0143 Invoice Capture & 3-Way Match Create/Edit form init. Shared by Create.cshtml and Edit.cshtml so the
// views carry no inline script. Adds repeatable InvoiceLine rows (itemId/quantity/unitPrice/lineAmount/poLineId).
// Money values are Decimal strings (float YASAK). No hardcoded gateway port in browser JS.
document.addEventListener('DOMContentLoaded', function () {
    // Repeatable InvoiceLine rows. Names are reindexed contiguously (Lines[0..n]) on every add/remove so the
    // MVC model binder receives a dense list.
    var body = document.getElementById('invoiceLinesBody');
    if (!body) return;
    var prefix = body.getAttribute('data-line-prefix') || 'Lines';

    var reindex = function () {
        var rows = body.querySelectorAll('.invoice-line-row');
        rows.forEach(function (row, i) {
            row.querySelectorAll('.invoice-line-input').forEach(function (input) {
                var field = input.getAttribute('data-line-field');
                input.setAttribute('name', prefix + '[' + i + '].' + field);
                input.setAttribute('id', prefix + '_' + i + '__' + field);
            });
            var removeBtn = row.querySelector('.invoice-line-remove');
            if (removeBtn) removeBtn.disabled = rows.length <= 1;
        });
    };

    var addRow = function () {
        var rows = body.querySelectorAll('.invoice-line-row');
        var template = rows[rows.length - 1];
        if (!template) return;
        var clone = template.cloneNode(true);
        clone.querySelectorAll('.invoice-line-input').forEach(function (input) {
            if (input.tagName === 'SELECT') { input.selectedIndex = 0; }
            else { input.value = ''; }
        });
        body.appendChild(clone);
        reindex();
    };

    document.getElementById('btnAddLine')?.addEventListener('click', addRow);
    body.addEventListener('click', function (e) {
        var btn = e.target.closest('.invoice-line-remove');
        if (!btn) return;
        var rows = body.querySelectorAll('.invoice-line-row');
        if (rows.length <= 1) return;
        btn.closest('.invoice-line-row')?.remove();
        reindex();
    });

    reindex();
});
