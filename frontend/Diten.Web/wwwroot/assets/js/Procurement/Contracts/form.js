'use strict';

// MOD-0144 Contracting Create/Edit form init. Shared by Create.cshtml and Edit.cshtml so the views carry no
// inline script. Select2 setup mirrors the shipped Sourcing form.js; adds repeatable ClauseRef rows and the
// supplier lookup (MOD-0140 consume). No hardcoded gateway port in browser JS (same-origin lookup proxy).
document.addEventListener('DOMContentLoaded', function () {
    /*
     * SELECT2 — placeholder is DECLARED so the theme greys it like every other hint. Supplier is a single
     * select fed by the lookup proxy; evidence refs is a free-typed tag list validated as opaque strings.
     */
    var select2Elements = $('.select2');
    if (select2Elements.length) {
        select2Elements.each(function () {
            var $this = $(this);
            var isTags = $this.attr('id') === 'evidenceRefs';
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent(),
                tags: isTags,
                placeholder: $this.data('placeholder') || $this.find('option[value=""]').text() || ''
            });
        });
    }

    // Supplier options (MOD-0140 consume) — populated from the same-origin lookup proxy, preserving any
    // pre-selected id (edit mode). Unknown supplier ids are rejected server-side (fail-closed).
    var $supplier = $('#supplierId');
    if ($supplier.length) {
        fetch('/Contracts/lookups', { method: 'GET', credentials: 'same-origin' })
            .then(function (res) { return res.ok ? res.json() : null; })
            .then(function (data) {
                if (!data || !Array.isArray(data.suppliers)) return;
                var selected = $supplier.val();
                var existing = new Set(($supplier.find('option').toArray()).map(function (o) { return o.value; }));
                data.suppliers.forEach(function (item) {
                    if (!item || !item.value || existing.has(item.value)) return;
                    $supplier.append(new Option(item.text || item.value, item.value, false, false));
                });
                $supplier.val(selected).trigger('change');
            })
            .catch(function (error) { console.error('[Contracts Lookup] Failed.', error); });
    }

    // Repeatable ClauseRef rows. Names are reindexed contiguously (Clauses[0..n]) on every add/remove so the
    // MVC model binder receives a dense list. The deviation checkbox posts only when checked (Deviation=false otherwise).
    var body = document.getElementById('clauseRefsBody');
    if (!body) return;
    var prefix = body.getAttribute('data-line-prefix') || 'Clauses';

    var reindex = function () {
        var rows = body.querySelectorAll('.clause-ref-row');
        rows.forEach(function (row, i) {
            row.querySelectorAll('.clause-ref-input, .clause-ref-deviation').forEach(function (input) {
                var field = input.getAttribute('data-line-field');
                input.setAttribute('name', prefix + '[' + i + '].' + field);
                input.setAttribute('id', prefix + '_' + i + '__' + field);
            });
            var removeBtn = row.querySelector('.clause-ref-remove');
            if (removeBtn) removeBtn.disabled = rows.length <= 1;
        });
    };

    var addRow = function () {
        var rows = body.querySelectorAll('.clause-ref-row');
        var template = rows[rows.length - 1];
        if (!template) return;
        var clone = template.cloneNode(true);
        clone.querySelectorAll('.clause-ref-input').forEach(function (input) { input.value = ''; });
        clone.querySelectorAll('.clause-ref-deviation').forEach(function (input) { input.checked = false; });
        body.appendChild(clone);
        reindex();
    };

    document.getElementById('btnAddClause')?.addEventListener('click', addRow);
    body.addEventListener('click', function (e) {
        var btn = e.target.closest('.clause-ref-remove');
        if (!btn) return;
        var rows = body.querySelectorAll('.clause-ref-row');
        if (rows.length <= 1) return;
        btn.closest('.clause-ref-row')?.remove();
        reindex();
    });

    reindex();
});
