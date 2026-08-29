(function (window, document) {
    'use strict';
    const form = document.getElementById('productForm');
    if (!form) return;

    const L = window.ProductL10n || {};

    const reindexRows = () => {
        document.querySelectorAll('.external-reference-row').forEach((row, index) => {
            row.dataset.index = String(index);
            row.querySelectorAll('[name]').forEach(input => {
                input.name = input.name.replace(/ExternalReferences\[\d+\]/, `ExternalReferences[${index}]`);
                if (input.id) input.id = input.id.replace(/ExternalReferences_\d+__/, `ExternalReferences_${index}__`);
            });
        });
    };

    document.getElementById('addExternalReference')?.addEventListener('click', () => {
        const host = document.getElementById('externalReferencesHost');
        const source = host?.querySelector('.external-reference-row');
        if (!host || !source) return;
        const row = source.cloneNode(true);
        row.querySelectorAll('input').forEach(input => {
            if (input.type === 'checkbox') input.checked = false;
            else input.value = '';
        });
        host.appendChild(row);
        reindexRows();
    });

    document.addEventListener('click', event => {
        const remove = event.target.closest('.remove-external-reference');
        if (!remove) return;
        const rows = document.querySelectorAll('.external-reference-row');
        const row = remove.closest('.external-reference-row');
        if (rows.length === 1) row?.querySelectorAll('input').forEach(input => input.type === 'checkbox' ? input.checked = false : input.value = '');
        else row?.remove();
        reindexRows();
    });

    const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    form.addEventListener('submit', event => {
        const from = document.getElementById('EffectiveFrom')?.value;
        const to = document.getElementById('EffectiveTo')?.value;
        if (from && to && new Date(to) < new Date(from)) {
            event.preventDefault();
            window.showToast?.(L.EffectiveToBeforeEffectiveFrom || 'Effective To cannot be earlier than Effective From.', 'error');
            return;
        }

        // Early feedback only — the server re-validates and owns the final answer.
        const raw = (document.getElementById('IndicationRefsRaw')?.value || '').trim();
        if (raw) {
            const invalid = raw.split(/[\s,;]+/).filter(Boolean).some(x => !GUID.test(x));
            if (invalid) {
                event.preventDefault();
                window.showToast?.(L.IndicationRefsInvalid || 'Indication references must be valid GUIDs.', 'error');
            }
        }
    });
})(window, document);
