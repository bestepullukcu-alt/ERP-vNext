'use strict';

// MOD-0141 Requisition Create/Edit form init. Shared by Create.cshtml and Edit.cshtml so the views carry no
// inline script. Adds repeatable RequisitionLine rows (itemId/skuId/quantity/uomId/needBy). Item/SKU/UoM ids are
// free-typed opaque references (MOD-0290/MOD-0048 consume) validated server-side (fail-closed). No hardcoded gateway
// port in browser JS.
document.addEventListener('DOMContentLoaded', function () {
    // Repeatable RequisitionLine rows. Names are reindexed contiguously (Lines[0..n]) on every add/remove so the
    // MVC model binder receives a dense list.
    var body = document.getElementById('requisitionLinesBody');
    if (!body) return;
    var prefix = body.getAttribute('data-line-prefix') || 'Lines';

    var reindex = function () {
        var rows = body.querySelectorAll('.requisition-line-row');
        rows.forEach(function (row, i) {
            row.querySelectorAll('.requisition-line-input').forEach(function (input) {
                var field = input.getAttribute('data-line-field');
                input.setAttribute('name', prefix + '[' + i + '].' + field);
                input.setAttribute('id', prefix + '_' + i + '__' + field);
            });
            var removeBtn = row.querySelector('.requisition-line-remove');
            if (removeBtn) removeBtn.disabled = rows.length <= 1;
        });
    };

    var addRow = function () {
        var rows = body.querySelectorAll('.requisition-line-row');
        var template = rows[rows.length - 1];
        if (!template) return;
        var clone = template.cloneNode(true);
        clone.querySelectorAll('.requisition-line-input').forEach(function (input) { input.value = ''; });
        body.appendChild(clone);
        reindex();
    };

    document.getElementById('btnAddLine')?.addEventListener('click', addRow);
    body.addEventListener('click', function (e) {
        var btn = e.target.closest('.requisition-line-remove');
        if (!btn) return;
        var rows = body.querySelectorAll('.requisition-line-row');
        if (rows.length <= 1) return;
        btn.closest('.requisition-line-row')?.remove();
        reindex();
    });

    reindex();
});
