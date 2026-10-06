'use strict';

// MOD-0193 BOM Create/Edit — repeatable component and routing-step rows, shared by Create.cshtml and Edit.cshtml so
// the views carry no inline script. Names are re-indexed densely (Components[0..n], Steps[0..n]) after every add or
// remove, so the MVC binder receives a contiguous list. The service validates everything again (fail-closed).
document.addEventListener('DOMContentLoaded', function () {
    const reindex = (body) => {
        const prefix = body.getAttribute('data-row-prefix');
        const rows = body.querySelectorAll('.bom-row');
        const allowEmpty = body.getAttribute('data-allow-empty') === 'true';
        rows.forEach((row, i) => {
            row.querySelectorAll('.bom-input').forEach((input) => {
                const field = input.getAttribute('data-field');
                input.setAttribute('name', `${prefix}[${i}].${field}`);
                input.setAttribute('id', `${prefix}_${i}__${field}`);
            });
            const remove = row.querySelector('.bom-remove-row');
            if (remove) remove.disabled = !allowEmpty && rows.length <= 1;
        });
    };

    const addRow = (body) => {
        const rows = body.querySelectorAll('.bom-row');
        const template = rows[rows.length - 1];
        if (!template) return;
        const clone = template.cloneNode(true);
        clone.querySelectorAll('.bom-input').forEach((input) => { input.value = ''; });
        // A new component line takes the next position in tens (10, 20, 30 …), as BOMs are usually numbered.
        const position = clone.querySelector('[data-field="Position"]');
        if (position) {
            const max = Array.from(body.querySelectorAll('[data-field="Position"]')).reduce((m, el) => Math.max(m, Number(el.value) || 0), 0);
            position.value = String(max + 10);
        }
        const step = clone.querySelector('[data-field="StepNo"]');
        if (step) {
            const max = Array.from(body.querySelectorAll('[data-field="StepNo"]')).reduce((m, el) => Math.max(m, Number(el.value) || 0), 0);
            step.value = String(max + 10);
        }
        body.appendChild(clone);
        reindex(body);
    };

    document.querySelectorAll('.bom-add-row').forEach((button) => {
        button.addEventListener('click', () => {
            const body = document.getElementById(button.getAttribute('data-target-body'));
            if (body) addRow(body);
        });
    });

    document.querySelectorAll('tbody[data-row-prefix]').forEach((body) => {
        body.addEventListener('click', (e) => {
            const button = e.target.closest('.bom-remove-row');
            if (!button || button.disabled) return;
            const rows = body.querySelectorAll('.bom-row');
            if (rows.length <= 1) {
                // The routing may be empty: clear the last row instead of removing the template.
                rows[0].querySelectorAll('.bom-input').forEach((input) => { input.value = ''; });
                return;
            }
            button.closest('.bom-row')?.remove();
            reindex(body);
        });
        reindex(body);
    });

    // ── Legal entity: MDM's referenceable lookup (the service proves the choice again on save) ──
    const formEl = document.getElementById('bomForm');
    const select = document.querySelector('select#LegalEntityId');
    const readOnlyName = document.getElementById('LegalEntityName');
    const hint = document.getElementById('LegalEntityHint');
    if (formEl && (select || readOnlyName)) {
        fetch(formEl.getAttribute('data-legal-entities-url'), { credentials: 'same-origin' })
            .then((res) => res.ok ? res.json() : Promise.reject(res))
            .then((items) => {
                const label = (e) => (e.code ? `${e.name} (${e.code})` : e.name);
                if (readOnlyName) {
                    const own = items.find((e) => e.id === readOnlyName.getAttribute('data-legal-entity-id'));
                    if (own) readOnlyName.value = label(own);
                    return;
                }
                items.forEach((e) => {
                    const option = document.createElement('option');
                    option.value = e.id;
                    option.textContent = label(e);
                    select.appendChild(option);
                });
                const wanted = select.getAttribute('data-selected') || new URLSearchParams(window.location.search).get('legalEntityId') || '';
                select.value = items.some((e) => e.id === wanted) ? wanted : (items.length === 1 ? items[0].id : '');
            })
            .catch(() => {
                if (hint) {
                    hint.textContent = formEl.getAttribute('data-unavailable-text') || '';
                    hint.classList.remove('text-muted');
                    hint.classList.add('text-danger');
                }
            });
    }

    // Client-side check before the round trip (MVP-6 recipe 3.8); the server stays authoritative.
    const form = document.getElementById('bomForm');
    form?.addEventListener('submit', (e) => {
        if (!form.checkValidity()) {
            e.preventDefault();
            e.stopPropagation();
        }
        form.classList.add('was-validated');
    });
});
