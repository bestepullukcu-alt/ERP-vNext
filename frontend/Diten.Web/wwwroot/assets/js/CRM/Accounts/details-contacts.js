/**
 * MOD-0150 FU03 — Account 360 Details: Related Contacts (Account↔Contact links).
 *  • Table: DataTable v2 via the shared Crm360Section factory (inline filter, Save View, colReorder, export/colvis).
 *    No checkbox column: links are never bulk-acted on.
 *  • Create / Edit: Golden Slim canvas (#offcanvasContactLink) — AJAX submit, no page navigation.
 *  • End: same canvas shape (#offcanvasContactLinkEnd). An End never deletes; it sets Status=ended + ValidTo.
 *
 * Rows come from the server-rendered #related-contacts-payload projection. The canvas reads the link itself before
 * opening because the projection carries no validity window / CrossCountryReason.
 */
'use strict';

(function () {
    const tableEl = document.getElementById('dt-account-contacts');
    const payloadEl = document.getElementById('related-contacts-payload');
    if (!tableEl || !payloadEl) return;

    let ctx = {};
    try {
        ctx = JSON.parse(payloadEl.textContent || '{}');
    } catch (error) {
        console.error('[AccountDetails] Related contacts payload could not be parsed.', error);
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

    // "ended"/"inactive" links are historical: they stay visible but are not editable.
    const isClosed = (status) => ['ended', 'inactive'].includes(String(status || '').toLowerCase());

    // ─── Row rendering ───────────────────────────────────────────────────────
    const renderActions = (row) => {
        if (!canManage || isClosed(row.status)) return '';

        return window.DitenDataTable?.renderActions?.([
            {
                key: 'edit',
                icon: 'bx bx-edit',
                className: 'btn-text-secondary',
                attrs: { 'data-row-action': 'edit', 'data-id': row.linkId, title: L.EditContactLink, 'aria-label': L.EditContactLink }
            },
            {
                key: 'end',
                icon: 'bx bx-time-five',
                text: L.EndContactLink,
                attrs: { 'data-row-action': 'end', 'data-id': row.linkId, title: L.EndContactLink, 'aria-label': L.EndContactLink }
            }
        ]) || '';
    };

    const renderRow = (row) => {
        const closed = isClosed(row.status);
        const contactType = row.contactType ? `<span class="badge bg-label-info">${esc(row.contactType)}</span>` : '-';
        const primary = row.isPrimary ? `<span class="badge bg-label-primary">${esc(L.Primary)}</span>` : '-';
        const status = row.status
            ? `<span class="badge ${closed ? 'bg-label-secondary' : 'bg-label-success'}">${esc(row.status)}</span>`
            : '-';

        return `<tr class="${closed ? 'text-muted' : ''}">
            <td></td>
            <td><a href="/CRM/Contacts/Details/${esc(row.contactId)}" title="${esc(L.ViewContact)}">${esc(row.displayName || '-')}</a></td>
            <td>${contactType}</td>
            <td>${dash(row.roleCode)}</td>
            <td>${dash(row.reportsToName)}</td>
            <td data-order="${row.isPrimary ? '1' : '0'}">${primary}</td>
            <td class="text-nowrap">${dash(row.phone)}</td>
            <td class="text-truncate" style="max-width:220px" title="${esc(row.email)}">${dash(row.email)}</td>
            <td data-order="${esc(row.status)}">${status}</td>
            <td class="cell-fit text-end text-nowrap">${renderActions(row)}</td>
        </tr>`;
    };

    // ─── Golden Slim create/edit canvas ──────────────────────────────────────
    const offcanvasEl = document.getElementById('offcanvasContactLink');
    const getOffcanvas = () =>
        (offcanvasEl && window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl) : null);

    const select2Ids = ['linkContactId', 'linkRoleCode', 'linkReportsToContactId'];

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

    const setContactLocked = (locked) => {
        const el = document.getElementById('linkContactId');
        if (el) el.disabled = locked;
        if (window.jQuery) $('#linkContactId').prop('disabled', locked).trigger('change.select2');
    };

    // The manager list carries every already-linked contact; a contact may not report to itself (backend rejects it
    // with 400), so the edited contact's own option is removed while its canvas is open.
    const setReportsToExclusion = (contactId) => {
        const select = document.getElementById('linkReportsToContactId');
        if (!select) return;
        Array.from(select.options).forEach((opt) => {
            opt.hidden = !!contactId && opt.value === String(contactId);
            opt.disabled = opt.hidden;
        });
        if (window.jQuery && $('#linkReportsToContactId').hasClass('select2-hidden-accessible')) {
            $('#linkReportsToContactId').trigger('change.select2');
        }
    };

    const resetForm = () => {
        const form = document.getElementById('formContactLink');
        if (!form) return;
        form.classList.remove('was-validated');
        document.getElementById('formContactLinkAlert')?.classList.add('d-none');
        const idEl = document.getElementById('linkId');
        if (idEl) idEl.value = '';
        ['linkValidFrom', 'linkValidTo', 'linkCrossCountryReason', 'linkNotes'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) el.value = '';
        });
        const primary = document.getElementById('linkIsPrimary');
        if (primary) primary.checked = false;
        select2Ids.forEach((id) => setSelect(id, ''));
        setContactLocked(false);
        setReportsToExclusion(null);
    };

    const setCanvasTitle = (text) => {
        const label = document.getElementById('offcanvasContactLinkLabel');
        if (label) label.textContent = text || '';
    };

    const openCreate = () => {
        if (!offcanvasEl) return;
        editingId = null;
        resetForm();
        setCanvasTitle(L.AddContactLink);
        getOffcanvas()?.show();
    };

    const openEdit = async (linkId) => {
        if (!offcanvasEl || !linkId) return;
        editingId = linkId;
        resetForm();
        setCanvasTitle(L.EditContactLink);

        try {
            const res = await fetch(`/CRM/Accounts/${accountId}/Contacts/${linkId}/Json`, {
                credentials: 'same-origin',
                headers: authHeaders()
            });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Contact link load failed.');

            const d = json.data;
            document.getElementById('linkId').value = d.id || '';
            setSelect('linkContactId', d.contactId);
            setSelect('linkRoleCode', d.roleCode);
            setReportsToExclusion(d.contactId);
            setSelect('linkReportsToContactId', d.reportsToContactId);
            document.getElementById('linkIsPrimary').checked = !!d.isPrimary;
            document.getElementById('linkValidFrom').value = d.validFrom || '';
            document.getElementById('linkValidTo').value = d.validTo || '';
            document.getElementById('linkNotes').value = d.notes || '';
        } catch (error) {
            console.error('[AccountDetails] Failed to load contact link for edit.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }

        // The backend never re-points an existing link to another contact.
        setContactLocked(true);
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
        const form = document.getElementById('formContactLink');
        if (!form) return;

        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showErrors('formContactLinkAlert', [L.FormValidationError || L.ErrorOccurred]);
            return;
        }

        const formData = new FormData(form);
        // A disabled select is not serialized; the ViewModel still requires ContactId on edit.
        if (editingId && !formData.get('ContactId')) {
            formData.set('ContactId', document.getElementById('linkContactId')?.value || '');
        }

        const url = editingId
            ? `/CRM/Accounts/${accountId}/Contacts/${editingId}/Edit`
            : `/CRM/Accounts/${accountId}/Contacts/Add`;

        const saveBtn = document.getElementById('btnSaveContactLink');
        if (saveBtn) saveBtn.disabled = true;

        try {
            const res = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': tokenOf('formContactLink'),
                    ...authHeaders()
                },
                body: formData
            });
            const json = await res.json();

            if (json.success) {
                getOffcanvas()?.hide();
                // Primary flag and the in-account hierarchy are backend-derived — reload to stay truthful.
                window.location.assign(`/CRM/Accounts/Details/${accountId}`);
            } else {
                showErrors('formContactLinkAlert', json.errors);
            }
        } catch (error) {
            console.error('[AccountDetails] Contact link submit failed.', error);
            showErrors('formContactLinkAlert', [L.ErrorOccurred]);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    // ─── End (historical close) canvas ───────────────────────────────────────
    const endOffcanvasEl = document.getElementById('offcanvasContactLinkEnd');
    const getEndOffcanvas = () =>
        (endOffcanvasEl && window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(endOffcanvasEl) : null);

    const todayIso = () => {
        const now = new Date();
        return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    };

    const resetEndForm = () => {
        const form = document.getElementById('formContactLinkEnd');
        if (!form) return;
        form.classList.remove('was-validated');
        document.getElementById('formContactLinkEndAlert')?.classList.add('d-none');
        const notes = document.getElementById('endContactNotes');
        if (notes) notes.value = '';
        const date = document.getElementById('endContactDate');
        if (date) date.value = todayIso();
        ['endContactName', 'endContactRole', 'endContactValidFrom'].forEach((id) => {
            const el = document.getElementById(id);
            if (el) el.textContent = '-';
        });
    };

    const openEnd = async (linkId) => {
        if (!endOffcanvasEl || !linkId) return;
        const row = rows.find((r) => String(r.linkId) === String(linkId));
        if (!row) return;

        endingId = linkId;
        resetEndForm();
        const setText = (id, value) => {
            const el = document.getElementById(id);
            if (el) el.textContent = normalizeString(value) || '-';
        };
        setText('endContactName', row.displayName);
        setText('endContactRole', row.roleCode);
        getEndOffcanvas()?.show();

        // ValidFrom is not part of the projection; fill it in once the link detail arrives (the canvas is already usable).
        try {
            const res = await fetch(`/CRM/Accounts/${accountId}/Contacts/${linkId}/Json`, {
                credentials: 'same-origin',
                headers: authHeaders()
            });
            const json = await res.json();
            if (json.success && json.data && endingId === linkId) setText('endContactValidFrom', json.data.validFrom);
        } catch (error) {
            console.error('[AccountDetails] Contact link summary could not be loaded.', error);
        }
    };

    const submitEndForm = async () => {
        const form = document.getElementById('formContactLinkEnd');
        if (!form || !endingId) return;

        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showErrors('formContactLinkEndAlert', [L.FormValidationError || L.ErrorOccurred]);
            return;
        }

        const confirmBtn = document.getElementById('btnConfirmEndContactLink');
        if (confirmBtn) confirmBtn.disabled = true;

        try {
            const res = await fetch(`/CRM/Accounts/${accountId}/Contacts/${endingId}/End`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'RequestVerificationToken': tokenOf('formContactLinkEnd'),
                    ...authHeaders()
                },
                body: new FormData(form)
            });
            const json = await res.json();

            if (json.success) {
                getEndOffcanvas()?.hide();
                window.location.assign(`/CRM/Accounts/Details/${accountId}`);
            } else {
                showErrors('formContactLinkEndAlert', json.errors);
            }
        } catch (error) {
            console.error('[AccountDetails] End contact link submit failed.', error);
            showErrors('formContactLinkEndAlert', [L.ErrorOccurred]);
        } finally {
            if (confirmBtn) confirmBtn.disabled = false;
        }
    };

    // ─── Boot ────────────────────────────────────────────────────────────────
    const init = async () => {
        syncL10n();

        const section = await window.Crm360Section.create({
            tableEl,
            bodyEl: document.getElementById('relatedContactsBody'),
            rows,
            renderRow,
            totalColumnCount: 10,
            saveViewColumns: [1, 2, 3, 4, 5, 6, 7, 8],
            baseOrder: [[1, 'asc']],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1 },
                { targets: 9, responsivePriority: 2, orderable: false, searchable: false }
            ],
            pageKey: 'AccountContacts',
            filters: [
                { selectId: 'filterContactRole', pick: (r) => r.roleCode },
                { selectId: 'filterContactType', pick: (r) => r.contactType },
                { selectId: 'filterContactStatus', pick: (r) => r.status }
            ],
            filterHostId: 'inlineFilterHostContacts',
            filterCollapseId: 'inlineFilterCollapseContacts',
            applyButtonId: 'btnContactsFilterApply',
            resetButtonId: 'btnContactsFilterReset',
            skeletonSelector: '#skeleton-loader-contacts',
            addNewText: canManage ? L.AddContactLink : null,
            onAddNew: openCreate,
            rowActions: canManage ? {
                edit: ({ id }) => { if (id) openEdit(String(id)); },
                end: ({ id }) => { if (id) openEnd(String(id)); }
            } : null,
            l10n: () => L
        });
        if (!section) console.warn('[AccountDetails] Related contacts table could not be initialised.');

        initOffcanvasSelect2();
        document.getElementById('btnSaveContactLink')?.addEventListener('click', submitForm);
        document.getElementById('btnConfirmEndContactLink')?.addEventListener('click', submitEndForm);
        offcanvasEl?.addEventListener('hidden.bs.offcanvas', () => { editingId = null; resetForm(); });
        endOffcanvasEl?.addEventListener('hidden.bs.offcanvas', () => { endingId = null; resetEndForm(); });

        // Legacy Add / Edit / End routes redirect here with ?contactLink=… or ?endContactLink=… — re-open the canvas.
        if (canManage) {
            const query = new URLSearchParams(window.location.search);
            const requested = query.get('contactLink');
            const requestedEnd = query.get('endContactLink');
            if (requested === 'new') openCreate();
            else if (requested) openEdit(requested);
            else if (requestedEnd) openEnd(requestedEnd);
        }
    };

    void init();
})();
