'use strict';

/*
 * MOD-0022 E5W2 — İmza anlamları (pack §11.3, KS-12; mockup v2 tab 6 + "MODAL · İMZA ANLAMI"). Page:
 * Views/ESignature/Settings/Meanings.cshtml.
 *
 * THE LIST IS A COMPONENT (BL-440): createList, CLIENT mode on GET /ESignature/api/meanings (listMeanings — unpaged, a
 * handful per tenant). Filters: status (active / inactive), usage (used / unused) — matched in the browser.
 *
 *   Yeni anlam / Düzenle → the modal: POST meanings { code, names{7}, description } | PUT meanings/{code} { names, description }
 *                          (the code is the path: it never changes; once a signature used it the field is locked — isLocked)
 *   Aktif toggle         → POST meanings/{code}/activate | deactivate (409 ESIGN_MEANING_IN_USE while a policy row uses it)
 * Without esign.meanings.manage: no New, no toggle, the row action is "Görüntüle" (the modal read-only, no Save button).
 */
(function (global) {
    const E = global.ESign;
    const P = global.ESignPages;
    const t = E.t;
    const esc = E.escapeHtml;
    const $ = (id) => document.getElementById(id);
    const LANGS = ['tr', 'en', 'fr', 'es', 'zh', 'ar', 'ru'];
    const tableEl = document.querySelector('.datatables-esign-meanings');
    const canManage = $('esignMeaningsRoot')?.getAttribute('data-can-manage') === 'true' && P.can('esign.meanings.manage');
    let list = null;
    let editing = null; // the meaning being edited, null = new

    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi', matches: (row, selected) => !selected.length || selected.includes(row.isActive ? 'active' : 'inactive') },
        { id: 'filterLocked', key: 'usage', kind: 'single', matches: (row, value) => !value || (value === 'used') === !!row.isLocked }
    ];

    const filled = (names) => LANGS.filter((l) => names && String(names[l] || '').trim()).length;
    const reload = () => list?.dt?.ajax?.reload(null, false);

    // ── the modal ────────────────────────────────────────────────────────────────────────────────────────────────
    function openModal(meaning) {
        editing = meaning || null;
        const ro = !canManage;
        $('esignMeaningModalTitle').textContent = !meaning ? t('ESign.Meanings.New') : (ro ? t('ESign.Meanings.View', meaning.code) : t('ESign.Meanings.Edit', meaning.code));
        $('esignMeaningError').classList.add('d-none');
        const code = $('esignMeaningCode');
        code.value = meaning ? meaning.code : '';
        code.readOnly = !!meaning; // the code is the path of the update — it never changes
        code.disabled = ro;
        code.classList.remove('is-invalid');
        $('esignMeaningCodeLocked').classList.toggle('d-none', !(meaning && meaning.isLocked));
        $('esignMeaningDescription').value = (meaning && meaning.description) || '';
        $('esignMeaningDescription').disabled = ro;
        LANGS.forEach((l) => {
            const input = $(`esignMeaningName-${l}`);
            input.value = (meaning && meaning.names && meaning.names[l]) || '';
            input.disabled = ro;
            input.classList.remove('is-invalid');
        });
        const warn = $('esignMeaningUsageWarn');
        const used = meaning && meaning.usageCount > 0;
        warn.classList.toggle('d-none', !used || ro);
        if (used) warn.querySelector('span').textContent = t('ESign.Meanings.UsageWarn', meaning.usageCount);
        $('esignMeaningCancel').textContent = ro ? t('ESign.Dialog.Close') : t('ESign.Dialog.Cancel');
        global.bootstrap.Modal.getOrCreateInstance($('esignMeaningModal')).show();
    }

    function formError(text) {
        const box = $('esignMeaningError');
        box.textContent = text || '';
        box.classList.toggle('d-none', !text);
    }

    async function submit(e) {
        e.preventDefault();
        if (!canManage) return;
        formError('');
        const names = {};
        let missing = false;
        LANGS.forEach((l) => {
            const input = $(`esignMeaningName-${l}`);
            names[l] = input.value.trim();
            input.classList.toggle('is-invalid', !names[l]);
            if (!names[l]) missing = true;
        });
        const code = $('esignMeaningCode').value.trim().toUpperCase();
        const codeOk = /^[A-Z][A-Z0-9_]{1,31}$/.test(code);
        $('esignMeaningCode').classList.toggle('is-invalid', !codeOk);
        document.querySelector('[data-esign-error="code"]').textContent = codeOk ? '' : t('ESign.Meanings.CodeFormat');
        if (missing || !codeOk) { formError(t('ESign.Meanings.FixFields')); return; }
        const description = $('esignMeaningDescription').value.trim() || null;
        const save = $('esignMeaningSave');
        save.disabled = true;
        try {
            const r = editing
                ? await E.call('PUT', `${P.API}/meanings/${encodeURIComponent(editing.code)}`, { names, description })
                : await E.call('POST', `${P.API}/meanings`, { code, names, description });
            if (!r.ok) {
                const errorCode = E.codeOf(r);
                if (errorCode === 'ESIGN_MEANING_LANGUAGE_MISSING') {
                    ((r.json && r.json.errors) || []).forEach((x) => {
                        const lang = String(x).startsWith('missing:') ? String(x).slice(8) : null;
                        if (lang && $(`esignMeaningName-${lang}`)) $(`esignMeaningName-${lang}`).classList.add('is-invalid');
                    });
                }
                formError(E.message(errorCode));
                return;
            }
            global.bootstrap.Modal.getOrCreateInstance($('esignMeaningModal')).hide();
            P.toast(t(editing ? 'ESign.Meanings.Updated' : 'ESign.Meanings.Created', code));
            reload();
        } finally {
            save.disabled = false;
        }
    }

    async function toggle(row) {
        if (!canManage) return;
        const activate = !row.isActive;
        const ok = await P.confirm({
            title: t(activate ? 'ESign.Meanings.Activate.Title' : 'ESign.Meanings.Deactivate.Title', row.code),
            subtext: t(activate ? 'ESign.Meanings.Activate.Sub' : 'ESign.Meanings.Deactivate.Sub'),
            confirmText: t(activate ? 'ESign.Meanings.Activate' : 'ESign.Meanings.Deactivate'),
            type: activate ? 'success' : 'warning'
        });
        if (!ok) return;
        const r = await E.call('POST', `${P.API}/meanings/${encodeURIComponent(row.code)}/${activate ? 'activate' : 'deactivate'}`);
        if (!r.ok) { P.failed(r); return; }
        reload();
    }

    async function init() {
        if (!tableEl) return;
        list = await global.DitenDataTable.createList({
            tableEl,
            dataMode: 'client',
            ajax: { url: `${P.API}/meanings`, type: 'GET' },
            actions: { onRowAction: { edit: ({ row }) => openModal(row), view: ({ row }) => openModal(row) } },
            toolbar: { addNewText: canManage ? t('ESign.Meanings.New') : '', onAddNew: () => openModal(null), colvisColumns: [2, 3, 4, 5] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: {
                moduleKey: 'ESignature',
                pageKey: 'Meanings',
                saveViewColumnIndexes: [1, 2, 3, 4, 5],
                defaultVisibleColumnIndexes: [1, 2, 3, 4, 5],
                baseOrder: [[1, 'asc']]
            },
            config: {
                order: [[1, 'asc']],
                initComplete: function () {
                    try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ }
                },
                columns: [
                    { data: 'code', name: 'control' },
                    { data: 'code', name: 'code' },
                    { data: 'names', name: 'name' },
                    { data: 'description', name: 'description', defaultContent: '' },
                    { data: 'usageCount', name: 'usage' },
                    { data: 'isActive', name: 'status' },
                    { data: 'code', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (d, type, row) => `<span class="fw-medium text-heading font-monospace">${esc(d)}</span>${row.isLocked ? ` <i class="bx bx-lock-alt text-muted" title="${esc(t('ESign.Meanings.CodeLocked'))}"></i>` : ''}` },
                    {
                        targets: 2,
                        render: (d, type) => {
                            const n = d || {};
                            if (type !== 'display') return `${n.tr || ''} ${n.en || ''}`;
                            const count = filled(n);
                            return `<div class="fw-medium">${esc(n[P.lang] || n.tr || '')}</div><small class="text-muted">${esc(n.en || '')} · <span class="${count === 7 ? 'text-success' : 'text-danger'}">${esc(t('ESign.Meanings.LangCount', count))}</span></small>`;
                        }
                    },
                    { targets: 3, render: (d) => `<small class="text-muted">${esc(d || '')}</small>` },
                    { targets: 4, render: (d, type) => (type === 'display' ? esc(d ? t('ESign.Meanings.UsedIn', d) : t('ESign.Meanings.Unused')) : Number(d || 0)) },
                    {
                        targets: 5,
                        render: (d, type, row) => {
                            if (type !== 'display') return d ? 'active' : 'inactive';
                            const label = esc(t(d ? 'ESign.Meanings.Active' : 'ESign.Meanings.Inactive'));
                            return canManage
                                ? `<div class="form-check form-switch mb-0"><input class="form-check-input" type="checkbox" role="switch" data-esign-toggle="${esc(row.code)}"${d ? ' checked' : ''} aria-label="${label}"><span class="small ms-1">${label}</span></div>`
                                : `<span class="badge ${d ? 'bg-label-success' : 'bg-label-secondary'}">${label}</span>`;
                        }
                    },
                    {
                        targets: -1,
                        title: t('ESign.Common.Actions'),
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (d, type, row) => {
                            const attrs = (text) => ({ 'data-id': row.code, 'data-json': JSON.stringify(row), title: text, 'aria-label': text });
                            return global.DitenDataTable.renderActions([
                                { key: 'edit', visible: canManage, icon: 'bx bx-edit', text: t('ESign.Meanings.EditAction'), attrs: attrs(t('ESign.Meanings.EditAction')) },
                                { key: 'view', visible: !canManage, icon: 'bx bx-show', text: t('ESign.Meanings.ViewAction'), attrs: attrs(t('ESign.Meanings.ViewAction')) }
                            ]);
                        }
                    }
                ]
            }
        });
        tableEl.addEventListener('click', (e) => {
            const sw = e.target.closest('[data-esign-toggle]');
            if (!sw) return;
            e.preventDefault();
            const row = list.dt.rows().data().toArray().find((x) => x.code === sw.getAttribute('data-esign-toggle'));
            if (row) toggle(row);
        });
        $('esignMeaningForm').addEventListener('submit', submit);
    }

    init();
})(window);
