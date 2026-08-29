(function (window, document) {
    'use strict';
    const form = document.getElementById('brandForm');
    if (!form) return;

    const L = window.BrandL10n || {};

    // MVC collection binding needs contiguous indexes; a removed middle row would otherwise truncate the list.
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
        // Keep one blank row so the operator always has somewhere to type.
        if (rows.length === 1) row?.querySelectorAll('input').forEach(input => input.type === 'checkbox' ? input.checked = false : input.value = '');
        else row?.remove();
        reindexRows();
    });

    form.addEventListener('submit', event => {
        const from = document.getElementById('EffectiveFrom')?.value;
        const to = document.getElementById('EffectiveTo')?.value;
        if (from && to && new Date(to) < new Date(from)) {
            event.preventDefault();
            window.showToast?.(L.EffectiveToBeforeEffectiveFrom || 'Effective To cannot be earlier than Effective From.', 'error');
        }
    });
})(window, document);
