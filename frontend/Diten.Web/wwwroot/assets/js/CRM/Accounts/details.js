/**
 * MOD-0150 FU04 — Account 360 Details: Related Accounts (Account↔Account relationships).
 *  • Table: DataTable v2 via the shared Crm360Section factory (inline filter, Save View, colReorder, export/colvis).
 *    No checkbox column: relationships are never bulk-acted on.
 *  • Create / Edit: Golden Slim canvas (#offcanvasRelationship) — AJAX submit, no page navigation.
 *  • End: same canvas shape (#offcanvasRelationshipEnd). An End never deletes; it sets Status=ended + ValidTo.
 *
 * Rows come from the server-rendered #related-accounts-payload projection, so the section needs no extra endpoint.
 * Edit prefill reads the relationship itself (the projection carries no CrossCountryReason).
 */
'use strict';

(function () {
    const tableEl = document.getElementById('dt-account-relationships');
    const payloadEl = document.getElementById('related-accounts-payload');
    if (!tableEl || !payloadEl) return;

    let ctx = {};
    try {
        ctx = JSON.parse(payloadEl.textContent || '{}');
    } catch (error) {
        console.error('[AccountDetails] Related accounts payload could not be parsed.', error);
        return;
    }

    const accountId = ctx.accountId;
    const canManage = !!ctx.canManage;
    const rows = Array.isArray(ctx.rows) ? ctx.rows : [];

    let L = window.L10n || {};
    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    let editingId = null;
    let endingId = null;

    const esc = (v) => (v === null || v === undefined || v === ''
        ? ''
        : String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])));
    const dash = (v) => (v === null || v === undefined || String(v).trim() === '' ? '-' : esc(v));
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const authHeaders = (includeJson) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const tokenOf = (formId) =>
        document.querySelector(`#${formId} input[name="__RequestVerificationToken"]`)?.value || '';

    // "ended"/"inactive" rows are historical: they stay visible (downstream sales/visit context) but are not editable.
    const isClosed = (status) => ['ended', 'inactive'].includes(String(status || '').toLowerCase());
    const statusBadgeClass = (status) => ({
        active: 'bg-label-success',
        pending: 'bg-label-warning',
        inactive: 'bg-label-secondary',
        ended: 'bg-label-secondary'
    }[String(status || '').toLowerCase()] || 'bg-label-secondary');

    // ─── Row rendering ───────────────────────────────────────────────────────
    const renderActions = (row) => {
        if (!canManage) return '';
        if (!row.isSource) return `<span class="text-muted small">${esc(L.ManagedFromSource)}</span>`;
        if (isClosed(row.status)) return '';

        return window.DitenDataTable?.renderActions?.([
            {
                key: 'edit',
                icon: 'bx bx-edit',
                className: 'btn-text-secondary',
                attrs: { 'data-row-action': 'edit', 'data-id': row.relationshipId, title: L.EditRelationship, 'aria-label': L.EditRelationship }
            },
            {
                key: 'end',
                icon: 'bx bx-time-five',
                text: L.EndRelationship,
                attrs: { 'data-row-action': 'end', 'data-id': row.relationshipId, title: L.EndRelationship, 'aria-label': L.EndRelationship }
            }
        ]) || '';
    };

    const renderRow = (row) => {
        const closed = isClosed(row.status);
        const label = row.effectiveLabelCode ? `<span class="badge bg-label-info">${esc(row.effectiveLabelCode)}</span>` : '-';
        const direction = row.displayDirection ? `<span class="badge bg-label-secondary">${esc(row.displayDirection)}</span>` : '-';
        const status = row.status ? `<span class="badge ${statusBadgeClass(row.status)}">${esc(row.status)}</span>` : '-';

        return `<tr class="${closed ? 'text-muted' : ''}">
            <td></td>
            <td><a href="/CRM/Accounts/Details/${esc(row.relatedAccountId)}" title="${esc(L.ViewAccount)}">${esc(row.relatedAccountName || '-')}</a></td>
            <td>${dash(row.relatedAccountCode)}</td>
            <td>${dash(row.relatedAccountType)}</td>
            <td>${label}</td>
            <td>${direction}</td>
            <td data-order="${esc(row.status)}">${status}</td>
            <td class="text-nowrap" data-order="${esc(row.validFrom || '')}">${dash(row.validFrom)}</td>
            <td class="text-nowrap" data-order="${esc(row.validTo || '')}">${dash(row.validTo)}</td>
            <td class="text-truncate" style="max-width:220px" title="${esc(row.notes)}">${dash(row.notes)}</td>
            <td class="cell-fit text-end text-nowrap">${renderActions(row)}</td>
        </tr>`;
    };

    // ─── Golden Slim create/edit canvas ──────────────────────────────────────
    const offcanvasEl = document.getElementById('offcanvasRelationship');
    const getOffcanvas = () =>
        (offcanvasEl && window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl) : null);

    const select2Ids = ['relTargetAccountId', 'relRelationshipType', 'relStatus'];

    const initOffcanvasSelect2 = () => {
        if (!window.jQuery || !$.fn.select2 || !offcanvasEl) return;
        select2Ids.forEach((id) => {
            const $el = $(`#${id}`);
            if (!$el.length) return;
            if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
            $el.select2({ dropdownParent: $(offcanvasEl), placeholder: $el.data('placeholder') || '', width: '100%' });
        });
    };

    const setSelect = (id, value) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = value || '';
        if (window.jQuery && $(`#${id}`).hasClass('select2-hidden-accessible')) {
            $(`#${id}`).val(value || '').trigger('change');
        }
    };

    const setTargetAccountLocked = (locked) => {
        const target = document.getElementById('relTargetAccountId');
        if (target) target.disabled = locked;
        if (window.jQuery) $('#relTargetAccountId').prop('disabled', locked).trigger('change.select2');
    };

    const resetForm = () => {
        const form = document.getElementById('formRelationship');
        if (!form) return;
        form.classList.remove('was-validated');
        document.getElementById('formRelationshipAlert')?.classList.add('d-none');
        const idEl = document.getElementById('relRelationshipId');
        if (idEl) idEl.value = '';
        ['relValidFrom', 'relValidTo', 'relCrossCountryReason', 'relNotes'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) el.value = '';
        });
        select2Ids.forEach((id) => setSelect(id, ''));
        setTargetAccountLocked(false);
    };

    const setCanvasTitle = (text) => {
        const label = document.getElementById('offcanvasRelationshipLabel');
        if (label) label.textContent = text || '';
    };

    const openCreate = () => {
        if (!offcanvasEl) return;
        editingId = null;
        resetForm();
        setCanvasTitle(L.AddRelatedAccount);
        getOffcanvas()?.show();
    };

    const openEdit = async (relationshipId) => {
        if (!offcanvasEl || !relationshipId) return;
        editingId = relationshipId;
        resetForm();
        setCanvasTitle(L.EditRelationship);

        try {
            const res = await fetch(`/CRM/Accounts/${accountId}/Relationships/${relationshipId}/Json`, {
                credentials: 'same-origin',
                headers: authHeaders()
            });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Relationship load failed.');

            const d = json.data;
            document.getElementById('relRelationshipId').value = d.id || '';
            setSelect('relTargetAccountId', d.targetAccountId);
            setSelect('relRelationshipType', d.relationshipType);
            setSelect('relStatus', d.status);
            document.getElementById('relValidFrom').value = d.validFrom || '';
            document.getElementById('relValidTo').value = d.validTo || '';
            document.getElementById('relNotes').value = d.notes || '';
        } catch (error) {
            console.error('[AccountDetails] Failed to load relationship for edit.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }

        // The backend never re-points an existing relationship to another account.
        setTargetAccountLocked(true);
        getOffcanvas()?.show();
    };

    const showErrors = (alertId, errors) => {
        const alertEl = document.getElementById(alertId);
        if (!alertEl) return;
        const list = Array.isArray(errors) && errors.length ? errors : [L.FormValidationError || L.ErrorOccurred];
        alertEl.innerHTML = list.map((e) => `<div>${esc(e)}</div>`).join('');
        alertEl.classList.remove('d-none');
    };

    const submitForm = async () => {
        const form = document.getElementById('formRelationship');
        if (!form) return;

        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showErrors('formRelationshipAlert', [L.FormValidationError || L.ErrorOccurred]);
            return;
        }

        const formData = new FormData(form);
        // A disabled select is not serialized; the ViewModel still requires TargetAccountId on edit.
        if (editingId && !formData.get('TargetAccountId')) {
            formData.set('TargetAccountId', document.getElementById('relTargetAccountId')?.value || '');
        }

        const url = editingId
            ? `/CRM/Accounts/${accountId}/Relationships/${editingId}/Edit`
            : `/CRM/Accounts/${accountId}/Relationships/Add`;

        const saveBtn = document.getElementById('btnSaveRelationship');
        if (saveBtn) saveBtn.disabled = true;

        try {
            const res = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': tokenOf('formRelationship'),
                    ...authHeaders()
                },
                body: formData
            });
            const json = await res.json();

            if (json.success) {
                getOffcanvas()?.hide();
                // The projection (effective label, direction, inverse rows) is backend-derived — reload to stay truthful.
                window.location.assign(`/CRM/Accounts/Details/${accountId}`);
            } else {
                showErrors('formRelationshipAlert', json.errors);
            }
        } catch (error) {
            console.error('[AccountDetails] Relationship submit failed.', error);
            showErrors('formRelationshipAlert', [L.ErrorOccurred]);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    // ─── End (historical close) canvas ───────────────────────────────────────
    const endOffcanvasEl = document.getElementById('offcanvasRelationshipEnd');
    const getEndOffcanvas = () =>
        (endOffcanvasEl && window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(endOffcanvasEl) : null);

    const todayIso = () => {
        const now = new Date();
        return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    };

    const resetEndForm = () => {
        const form = document.getElementById('formRelationshipEnd');
        if (!form) return;
        form.classList.remove('was-validated');
        document.getElementById('formRelationshipEndAlert')?.classList.add('d-none');
        const notes = document.getElementById('endNotes');
        if (notes) notes.value = '';
        const date = document.getElementById('endDate');
        if (date) date.value = todayIso();
        ['endTargetAccountName', 'endRelationshipType', 'endValidFrom'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) el.textContent = '-';
        });
    };

    const openEnd = (relationshipId) => {
        if (!endOffcanvasEl || !relationshipId) return;
        // The summary comes from the projection row already on screen — no extra round-trip for a read-only recap.
        const row = rows.find((r) => String(r.relationshipId) === String(relationshipId));
        if (!row) return;

        endingId = relationshipId;
        resetEndForm();
        const setText = (id, value) => {
            const el = document.getElementById(id);
            if (el) el.textContent = normalizeString(value) || '-';
        };
        setText('endTargetAccountName', row.relatedAccountName);
        setText('endRelationshipType', row.relationshipType || row.effectiveLabelCode);
        setText('endValidFrom', row.validFrom);
        getEndOffcanvas()?.show();
    };

    const submitEndForm = async () => {
        const form = document.getElementById('formRelationshipEnd');
        if (!form || !endingId) return;

        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showErrors('formRelationshipEndAlert', [L.FormValidationError || L.ErrorOccurred]);
            return;
        }

        const confirmBtn = document.getElementById('btnConfirmEndRelationship');
        if (confirmBtn) confirmBtn.disabled = true;

        try {
            const res = await fetch(`/CRM/Accounts/${accountId}/Relationships/${endingId}/End`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': tokenOf('formRelationshipEnd'),
                    ...authHeaders()
                },
                body: new FormData(form)
            });
            const json = await res.json();

            if (json.success) {
                getEndOffcanvas()?.hide();
                window.location.assign(`/CRM/Accounts/Details/${accountId}`);
            } else {
                showErrors('formRelationshipEndAlert', json.errors);
            }
        } catch (error) {
            console.error('[AccountDetails] End relationship submit failed.', error);
            showErrors('formRelationshipEndAlert', [L.ErrorOccurred]);
        } finally {
            if (confirmBtn) confirmBtn.disabled = false;
        }
    };

    // ─── Boot ────────────────────────────────────────────────────────────────
    const init = async () => {
        syncL10n();

        const section = await window.Crm360Section.create({
            tableEl,
            bodyEl: document.getElementById('relatedAccountsBody'),
            rows,
            renderRow,
            totalColumnCount: 11,
            saveViewColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9],
            baseOrder: [[1, 'asc']],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1 },
                { targets: 10, responsivePriority: 2, orderable: false, searchable: false }
            ],
            pageKey: 'AccountRelationships',
            filters: [
                { selectId: 'filterRelationshipType', pick: (r) => r.effectiveLabelCode },
                { selectId: 'filterRelationshipStatus', pick: (r) => r.status },
                { selectId: 'filterRelatedAccountType', pick: (r) => r.relatedAccountType }
            ],
            filterHostId: 'inlineFilterHost',
            filterCollapseId: 'inlineFilterCollapse',
            applyButtonId: 'btnFilterApply',
            resetButtonId: 'btnFilterReset',
            skeletonSelector: '#skeleton-loader',
            addNewText: canManage ? L.AddRelatedAccount : null,
            onAddNew: openCreate,
            rowActions: canManage ? {
                edit: ({ id }) => { if (id) openEdit(String(id)); },
                end: ({ id }) => { if (id) openEnd(String(id)); }
            } : null,
            l10n: () => L
        });
        if (!section) console.warn("[AccountDetails] Related accounts table could not be initialised.");

        initOffcanvasSelect2();
        document.getElementById('btnSaveRelationship')?.addEventListener('click', submitForm);
        document.getElementById('btnConfirmEndRelationship')?.addEventListener('click', submitEndForm);
        offcanvasEl?.addEventListener('hidden.bs.offcanvas', () => { editingId = null; resetForm(); });
        endOffcanvasEl?.addEventListener('hidden.bs.offcanvas', () => { endingId = null; resetEndForm(); });

        // Legacy Add / Edit / End routes redirect here with ?relationship=… or ?endRelationship=… — re-open the canvas.
        if (canManage) {
            const query = new URLSearchParams(window.location.search);
            const requested = query.get('relationship');
            const requestedEnd = query.get('endRelationship');
            if (requested === 'new') openCreate();
            else if (requested) openEdit(requested);
            else if (requestedEnd) openEnd(requestedEnd);
        }
    };

    void init();
})();
