/**
 * MOD-0178 DL-3 — Yerleştirme Görevleri (PUTAWAYTASKS) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, search, paging and export come from DitenDataTable.createList. This
 * file writes only the screen's own business: columns, badges, the QuickView (confirmations + timeline), the confirm panel, the create /
 * edit form, the reason and assign dialogs.
 *
 * SERVER MODE, PROXY PROFILE (pack §11.2): '/Warehouse/PutawayTasks/api' translates the factory's wire for listPutawayTasks; the browser
 * never calls the inventory service (5067), /api/warehouse or the Gateway.
 *
 * THE STATE RULES ARE THE SERVICE'S (§8.15, AC-39 / AC-43): every button comes from the task's allowedActions / disabledActions through
 * PutawayRules (rules.js) — a key the reader lacks hides the button, a state that forbids it disables it WITH its reason (7 languages).
 */
'use strict';

const PutawayTasksList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-putawaytasks');
    const rules = window.PutawayRules;
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const endpoint = '/Warehouse/PutawayTasks/api';
    const bulkEndpoint = '/Warehouse/PutawayTasks/bulk'; // the bulk "Ata" (bulkAssignPutawayTasks)
    const hasSelection = !!document.querySelector('.datatables-putawaytasks .dt-checkboxes-select-all');
    const canAssign = can('warehouse.putaway-tasks.update') && !!document.getElementById('bulkActionBar');

    const token = () => document.querySelector('#putawayActionToken input[name="__RequestVerificationToken"], #formPutawayTask input[name="__RequestVerificationToken"]')?.value || '';
    const offcanvas = (id) => (byId(id) ? window.bootstrap?.Offcanvas.getOrCreateInstance(byId(id)) : null);
    const modal = (id) => (byId(id) ? window.bootstrap?.Modal.getOrCreateInstance(byId(id)) : null);

    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const post = async (url, body) => {
        const headers = Object.assign({}, getAuthHeaders(true), { RequestVerificationToken: token() });
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const failure = (answer) => answer?.json?.error?.message || W('Warehouse.Error.Generic');
    const showAlert = (id, text) => {
        const box = byId(id);
        if (!box) return;
        box.textContent = text || '';
        box.classList.toggle('d-none', !text);
    };
    const fieldError = (id, text) => { const el = byId(id); if (el) el.textContent = text || ''; };
    const reload = () => list?.dt?.ajax?.reload(null, false);
    const person = (p) => (p ? (p.name || W('UnknownUser')) : '');
    const quantity = (row) => `${row.confirmed ?? '0'} / ${row.planned ?? '0'} ${row.uom ?? ''}`.trim();
    const codeOf = (label) => String(label ?? '').split(' · ')[0];
    const fmtDate = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString(window.CurrentLanguage || undefined);
    };
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');

    const badge = (b) => (b ? `<span class="badge ${b.cls}">${esc(W(b.word))}</span>` : '');
    const statusBadge = (status) => badge(rules.statusBadge(status));
    const flagBadge = (flag) => badge(rules.flagBadge(flag));

    // ─── Filters ─────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' },
        { id: 'filterAssignee', key: 'assigneeUserId', kind: 'text' },
        { id: 'filterPriority', key: 'priority', kind: 'single' }
    ];

    const pickerNotice = (available) => {
        const notice = byId('putaway-picker-unavailable');
        if (!notice) return;
        notice.textContent = available ? '' : W('Warehouse.Error.ServiceUnavailable');
        notice.classList.toggle('d-none', !!available);
    };

    const ajaxSelect = (el, url, extra = {}) => {
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = window.jQuery(el);
        $el.select2(Object.assign({
            placeholder: $el.data('placeholder') || '',
            allowClear: true,
            width: '100%',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url,
                dataType: 'json',
                delay: 250,
                data: (params) => ({ q: params.term || '' }),
                processResults: (data) => {
                    pickerNotice(data?.available !== false);
                    return { results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                },
                error: (xhr) => { if (xhr?.statusText !== 'abort') pickerNotice(false); }
            }
        }, extra));
    };

    const loadLookupOptions = async () => {
        const filterExtra = { dropdownParent: window.jQuery?.(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', width: 'element' };
        ajaxSelect(byId('filterWarehouse'), '/Warehouse/PutawayTasks/warehouses', filterExtra);
        ajaxSelect(byId('filterAssignee'), '/Warehouse/PutawayTasks/people', filterExtra);
    };

    const onResponse = (json) => {
        byId('putaway-list-error')?.classList.add('d-none');
        if (json && json.namesAvailable === false) {
            const box = byId('putaway-list-error');
            if (box) { box.textContent = W('NamesUnavailable'); box.classList.remove('d-none', 'alert-danger'); box.classList.add('alert-warning'); }
        }
    };

    // The list's own failure surface: the proxy's localized sentence, never a technical toast (UAS-001 §5 / §6).
    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        const box = byId('putaway-list-error');
        if (box) {
            box.textContent = message || W('Warehouse.Error.ServiceUnavailable');
            box.classList.remove('d-none', 'alert-warning');
            box.classList.add('alert-danger');
        }
    };

    // ─── Menus (AC-39 / AC-43) ───────────────────────────────────────────────
    const ActionIcon = {
        Confirm: 'bx bx-check-circle', Update: 'bx bx-edit', Claim: 'bx bx-user-plus', CloseShort: 'bx bx-stop-circle', Cancel: 'bx bx-x-circle',
        Retry: 'bx bx-refresh', MarkReturned: 'bx bx-undo', ResolveIntegrity: 'bx bx-shield-x'
    };

    /** A menu entry: enabled → its handler key; disabled → no handler, the button word and the reason as its text (AC-43). */
    const menuEntry = (state, rowJson) => {
        const word = W(rules.ActionWord[state.action]);
        if (state.enabled) {
            return { key: state.action, className: state.dangerous ? 'text-danger' : '', icon: ActionIcon[state.action], text: word, attrs: { 'data-json': rowJson, title: word, 'aria-label': word } };
        }
        const reason = W(rules.reasonWord(state.reasonCode));
        return {
            key: `disabled-${state.action}`, className: 'disabled text-muted', icon: ActionIcon[state.action], text: reason ? `${word} — ${reason}` : word,
            attrs: { 'aria-disabled': 'true', title: reason, 'data-disabled-reason': state.reasonCode || '' }
        };
    };

    const renderAction = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        return window.DitenDataTable.renderActions([
            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-json': rowJson, title: L().QuickView, 'aria-label': L().QuickView } },
            ...rules.menu(full, can, rules.RowMenu).map((state) => menuEntry(state, rowJson))
        ]);
    };

    // ─── QuickView ───────────────────────────────────────────────────────────
    let current = null; // { task, etag }

    const setText = (id, value) => { const el = byId(id); if (el) el.textContent = value == null || value === '' ? '-' : String(value); };

    const fillHead = (task) => {
        setText('offcanvasDetailsPreviewLabel', task.number);
        setText('oc-subtitle', [task.sku, task.lotNumber].filter(Boolean).join(' · '));
        const status = rules.statusBadge(task.status);
        const statusEl = byId('oc-status');
        if (statusEl) { statusEl.className = `badge ${status.cls}`; statusEl.textContent = W(status.word); }
        const flag = rules.flagBadge(task.integrationFlag);
        const flagEl = byId('oc-flag');
        if (flagEl) {
            flagEl.className = `badge ${flag ? flag.cls : 'd-none'}`;
            flagEl.textContent = flag ? W(flag.word) : '';
            flagEl.dataset.whFlag = task.integrationFlag || 'None';
        }
        setText('oc-sku', task.sku);
        setText('oc-lot', task.lotNumber);
        setText('oc-warehouse', task.warehouse);
        setText('oc-from', task.fromLocation);
        setText('oc-to', task.toLocation);
        setText('oc-planned', `${task.planned ?? ''} ${task.uom ?? ''}`.trim());
        setText('oc-confirmed', task.confirmed);
        setText('oc-pending', task.pending);
        setText('oc-remaining', task.remaining);
        setText('oc-assignee', person(task.assignee));
        setText('oc-priority', task.priority ? W(`Warehouse.Priority.${task.priority}`) : '');
        setText('oc-note', task.note || task.shortCloseReason || task.cancelReason);
    };

    const ResolutionWord = {
        SupersededByNewConfirmation: 'Warehouse.Resolution.SupersededByNewConfirmation', ReturnedToSource: 'Warehouse.Resolution.ReturnedToSource',
        ResolvedOwnMovement: 'Warehouse.Resolution.ResolvedPosted', ReturnedToRefused: 'Warehouse.Resolution.ResolvedRefused'
    };

    const fillConfirmations = (confirmations) => {
        const host = byId('oc-confirmations');
        if (!host) return;
        host.innerHTML = (confirmations || []).map((c) => {
            const inFlight = c.status === 'Pending' && c.inFlightUntilUtc && new Date(c.inFlightUntilUtc) > new Date();
            const status = inFlight ? W('Warehouse.Status.InFlight') : W(`Warehouse.ConfirmationStatus.${c.status}`);
            const resolution = ResolutionWord[c.integrityResolution] || ResolutionWord[c.resolution];
            const lines = [
                `${esc(c.quantity)} → ${esc(c.toLocation)}`,
                c.transactionNumber ? esc(format(W('ConfirmationMovement'), c.transactionNumber)) : '',
                c.status === 'Pending' ? esc(format(W('ConfirmationAttempts'), c.attempts)) : '',
                c.status === 'Pending' && c.lastAttemptAtUtc ? esc(format(W('ConfirmationLastAttempt'), fmtDate(c.lastAttemptAtUtc))) : '',
                c.errorCode ? esc(W(`Warehouse.Error.${c.errorCode}`) || W('Warehouse.Error.Generic')) : '',
                resolution ? esc(W(resolution)) : '',
                c.confirmedBy ? esc(format(W('ConfirmationBy'), person(c.confirmedBy), fmtDate(c.confirmedAtUtc))) : ''
            ].filter(Boolean);
            return `<div class="border rounded p-3" data-confirmation-seq="${esc(c.seq)}">`
                + `<div class="d-flex justify-content-between align-items-center mb-1"><strong>${esc(format(W('ConfirmationSeq'), c.seq))}</strong>`
                + `<span class="badge bg-label-secondary" data-confirmation-status="${esc(c.status)}">${esc(status)}</span></div>`
                + lines.map((line) => `<div class="small">${line}</div>`).join('') + '</div>';
        }).join('');
        byId('oc-confirmations-empty')?.classList.toggle('d-none', (confirmations || []).length > 0);
    };

    const fillTimeline = (timeline, available) => {
        const host = byId('oc-timeline');
        if (!host) return;
        host.innerHTML = (timeline || []).map((e) => `<li class="timeline-item timeline-item-transparent" data-event-kind="${esc(e.kind)}">`
            + '<span class="timeline-point timeline-point-primary"></span><div class="timeline-event"><div class="timeline-header mb-1">'
            + `<h6 class="mb-0">${esc(W(`Warehouse.Event.${e.kind}`))}</h6><small class="text-muted">${esc(fmtDate(e.atUtc))}</small></div>`
            + `<small class="text-muted">${esc(e.system ? W('SystemActor') : person(e.actor))}</small></div></li>`).join('');
        byId('oc-timeline-unavailable')?.classList.toggle('d-none', available !== false);
    };

    const fillActions = (task) => {
        const host = byId('oc-actions');
        if (!host) return;
        host.innerHTML = rules.menu(task, can, rules.QuickViewMenu).map((state) => {
            const word = W(rules.ActionWord[state.action]);
            const reason = state.enabled ? '' : W(rules.reasonWord(state.reasonCode));
            const cls = state.dangerous ? 'btn-label-danger' : state.action === 'Confirm' || state.action === 'Retry' ? 'btn-primary' : 'btn-label-secondary';
            return `<div class="d-flex flex-column align-items-stretch"><button type="button" class="btn ${cls} wh-touch" data-oc-action="${state.action}"`
                + `${state.enabled ? '' : ' disabled aria-disabled="true"'} title="${esc(reason)}">${esc(word)}</button>`
                + (reason ? `<small class="text-muted wh-menu-reason" data-oc-reason="${esc(state.reasonCode)}">${esc(reason)}</small>` : '') + '</div>';
        }).join('');
    };

    const populateDetailsOffcanvas = async (row) => {
        current = { task: row, etag: row.etag };
        showAlert('oc-error', '');
        fillHead(row);
        fillConfirmations([]);
        fillTimeline([], true);
        fillActions(row);
        const answer = await getJson(`/Warehouse/PutawayTasks/task/${encodeURIComponent(row.id)}`);
        if (!answer.ok) { showAlert('oc-error', failure(answer)); return; }
        current = { task: answer.json.task, etag: answer.json.etag };
        fillHead(current.task);
        fillConfirmations(answer.json.confirmations);
        fillTimeline(answer.json.timeline, answer.json.timelineAvailable);
        fillActions(current.task);
    };

    // ─── Bin field: keyboard + barcode, exact code only (AC-44) ──────────────
    const bindBinField = (prefix, warehouseOf) => {
        const input = byId(`${prefix}BinCode`);
        if (!input) return;
        const idEl = byId(`${prefix}BinId`);
        const labelEl = byId(`${prefix}BinLabel`);
        const options = byId(`${prefix}BinOptions`);
        let timer = null;

        const clear = () => { if (idEl) idEl.value = ''; if (labelEl) labelEl.textContent = ''; };

        input.resolve = async () => {
            const code = input.value.trim();
            fieldError(`${prefix}BinError`, '');
            clear();
            if (!code) return { ok: true, empty: true };
            const answer = await getJson(`/Warehouse/PutawayTasks/bins?${new URLSearchParams({ warehouseId: warehouseOf() || '', code }).toString()}`);
            const verdict = rules.binVerdict(code, answer.ok ? answer.json : null);
            if (!verdict.ok) { fieldError(`${prefix}BinError`, W(verdict.error)); return verdict; }
            if (idEl) idEl.value = verdict.id;
            if (labelEl) labelEl.textContent = verdict.label;
            input.dispatchEvent(new CustomEvent('wh:bin', { detail: verdict }));
            return verdict;
        };

        input.addEventListener('keydown', (event) => {
            if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); input.resolve(); } // a barcode reader ends with Enter
        });
        input.addEventListener('change', () => input.resolve());
        input.addEventListener('input', () => {
            clear();
            window.clearTimeout(timer);
            const term = input.value.trim();
            if (term.length < 2 || !options) return;
            timer = window.setTimeout(async () => {
                const answer = await getJson(`/Warehouse/PutawayTasks/bins?${new URLSearchParams({ warehouseId: warehouseOf() || '', q: term }).toString()}`);
                options.innerHTML = (answer.json?.items || []).map((o) => `<option value="${esc(codeOf(o.text))}">${esc(o.text)}</option>`).join('');
            }, 250);
        });
    };

    const setBin = (prefix, id, label) => {
        const input = byId(`${prefix}BinCode`);
        if (input) input.value = label ? codeOf(label) : '';
        if (byId(`${prefix}BinId`)) byId(`${prefix}BinId`).value = id || '';
        if (byId(`${prefix}BinLabel`)) byId(`${prefix}BinLabel`).textContent = label || '';
        fieldError(`${prefix}BinError`, '');
    };

    /** The bin to send: '' when the field is empty; null when a typed code did not resolve (nothing is sent — AC-44). */
    const binToSend = async (prefix) => {
        const input = byId(`${prefix}BinCode`);
        if (!input || !input.value.trim()) return '';
        if (byId(`${prefix}BinId`)?.value) return byId(`${prefix}BinId`).value;
        const verdict = await input.resolve();
        return verdict.ok ? verdict.id : null;
    };

    const bindSteppers = (root) => {
        root?.querySelectorAll('[data-wh-step]').forEach((button) => {
            button.addEventListener('click', () => {
                const target = byId(button.dataset.whFor || 'confirmQuantity');
                if (!target) return;
                target.value = rules.step(target.value, Number(button.dataset.whStep), target.dataset.max);
                target.dispatchEvent(new Event('input'));
            });
        });
    };

    // ─── Confirm panel (§8.3, AC-44 / AC-45) ─────────────────────────────────
    let confirmState = null; // { task, etag, key }

    const openConfirm = (task, etag) => {
        if (!byId('offcanvasConfirm')) return;
        confirmState = { task, etag: etag || task.etag, key: newKey() };
        showAlert('confirm-alert', '');
        setText('confirm-subtitle', [task.number, task.sku, task.lotNumber].filter(Boolean).join(' · '));
        const qty = byId('confirmQuantity');
        qty.value = task.remaining || '';
        qty.dataset.max = task.remaining || '';
        qty.readOnly = !!task.serialControlled; // a serial SKU is whole quantity only (§8.16)
        byId('formConfirm')?.querySelectorAll('[data-wh-step]').forEach((b) => { b.disabled = !!task.serialControlled; });
        setText('confirm-remaining', format(W('RemainingHint'), task.remaining, task.uom));
        fieldError('confirmQuantityError', '');
        setBin('confirm', task.toLocationId, task.toLocation);
        byId('confirmDeviation').value = '';
        byId('confirmDeviationBlock')?.classList.add('d-none');
        const retry = rules.actionState(task, 'Retry', can);
        byId('btnConfirmRetry')?.classList.toggle('d-none', !(retry.visible && retry.enabled && task.pendingSeq));
        const confirm = rules.actionState(task, 'Confirm', can);
        const submit = byId('btnConfirmSubmit');
        if (submit) {
            submit.disabled = !confirm.enabled;
            submit.title = confirm.enabled ? '' : W(rules.reasonWord(confirm.reasonCode));
        }
        offcanvas('offcanvasDetailsPreview')?.hide();
        offcanvas('offcanvasConfirm')?.show();
    };

    const refreshDeviation = () => {
        if (!confirmState) return;
        const show = rules.needsDeviation(confirmState.task.toLocationId, byId('confirmBinId')?.value);
        byId('confirmDeviationBlock')?.classList.toggle('d-none', !show);
    };

    const submitConfirm = async () => {
        if (!confirmState) return;
        showAlert('confirm-alert', '');
        const qty = byId('confirmQuantity').value.trim();
        if (!rules.isQuantity(qty)) { fieldError('confirmQuantityError', W('ValidationQuantity')); return; }
        fieldError('confirmQuantityError', '');
        const to = await binToSend('confirm');
        if (!to) { if (to === '') fieldError('confirmBinError', W('BinNotFound')); return; }
        const deviation = byId('confirmDeviation').value.trim();
        if (rules.needsDeviation(confirmState.task.toLocationId, to) && !deviation) { showAlert('confirm-alert', W('DeviationRequired')); return; }
        const button = byId('btnConfirmSubmit');
        button.disabled = true;
        try {
            const answer = await post(`/Warehouse/PutawayTasks/confirm/${encodeURIComponent(confirmState.task.id)}`, {
                idempotencyKey: confirmState.key, ifMatch: confirmState.etag, quantity: qty, toLocationId: to, deviationReason: deviation || null
            });
            if (!answer.ok) { showAlert('confirm-alert', failure(answer)); return; }
            offcanvas('offcanvasConfirm')?.hide();
            window.showToast?.(answer.json?.message || '', 'success');
            reload();
        } finally {
            button.disabled = false;
        }
    };

    // ─── Key-only writes: Retry, Claim ───────────────────────────────────────
    const retry = async (task) => {
        if (!task?.pendingSeq) return;
        const answer = await post(`/Warehouse/PutawayTasks/retry/${encodeURIComponent(task.id)}/${task.pendingSeq}`, { idempotencyKey: newKey() });
        if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
        offcanvas('offcanvasConfirm')?.hide();
        window.showToast?.(answer.json?.message || '', 'success');
        reload();
    };

    const claim = (task, etag) => {
        window.showConfirm?.(L().AreYouSure, async () => {
            const answer = await post(`/Warehouse/PutawayTasks/claim/${encodeURIComponent(task.id)}`, { idempotencyKey: newKey(), ifMatch: etag || task.etag });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            window.showToast?.(answer.json?.message || '', 'success');
            reload();
        }, { entityName: task.number, confirmButtonText: W('Warehouse.Action.Claim') });
    };

    // ─── Reason dialog: Returned, ResolveIntegrity, CloseShort, Cancel ───────
    const ReasonSpec = {
        MarkReturned: { path: (t) => `returned/${t.id}/${t.refusedSeq}`, intro: 'Warehouse.Action.Returned.Confirm', needs: (t) => !!t.refusedSeq },
        ResolveIntegrity: { path: (t) => `resolve/${t.id}/${t.integritySeq}`, intro: 'Warehouse.Action.ResolveIntegrity.Confirm', label: 'Warehouse.Action.ResolveIntegrity.Reason', needs: (t) => !!t.integritySeq },
        CloseShort: { path: (t) => `close-short/${t.id}`, intro: 'Warehouse.Action.CloseShort.Confirm', needs: () => true },
        Cancel: { path: (t) => `cancel/${t.id}`, intro: 'Warehouse.Action.Cancel.Confirm', needs: () => true }
    };
    let reasonState = null;

    const openReason = (action, task, etag) => {
        const spec = ReasonSpec[action];
        if (!spec || !spec.needs(task) || !byId('modalPutawayReason')) return;
        reasonState = { action, task, etag: etag || task.etag, key: newKey() };
        setText('modalPutawayReasonLabel', W(rules.ActionWord[action]));
        setText('reason-intro', `${task.number} — ${W(spec.intro)}`);
        const label = document.querySelector('label[for="putawayReason"]');
        if (label && spec.label) label.firstChild.textContent = `${W(spec.label)} `;
        byId('putawayReason').value = '';
        showAlert('reason-alert', '');
        const submit = byId('btnReasonSubmit');
        submit.textContent = W(rules.ActionWord[action]);
        submit.className = `btn ${action === 'Cancel' || action === 'ResolveIntegrity' ? 'btn-danger' : 'btn-primary'}`;
        offcanvas('offcanvasDetailsPreview')?.hide();
        modal('modalPutawayReason')?.show();
    };

    const submitReason = async () => {
        if (!reasonState) return;
        const reason = byId('putawayReason').value.trim();
        if (reason.length < 10) { showAlert('reason-alert', W('ReasonRequired')); return; }
        const button = byId('btnReasonSubmit');
        button.disabled = true;
        try {
            const answer = await post(`/Warehouse/PutawayTasks/${ReasonSpec[reasonState.action].path(reasonState.task)}`, { idempotencyKey: reasonState.key, ifMatch: reasonState.etag, reason });
            if (!answer.ok) { showAlert('reason-alert', failure(answer)); return; }
            modal('modalPutawayReason')?.hide();
            window.showToast?.(answer.json?.message || '', 'success');
            reload();
        } finally {
            button.disabled = false;
        }
    };

    // ─── Create / edit form (§9: 6 fields) ───────────────────────────────────
    let formState = null; // { mode, task, etag, key, appliedSuggestion }

    const candidateTemplate = (item) => {
        if (!item.id || (!item.locked && !item.unknownConditions)) return item.text;
        const span = document.createElement('span');
        span.className = item.locked ? 'wh-locked-option' : 'wh-unknown-option';
        span.innerHTML = item.locked
            ? `<i class="bx bx-lock-alt" aria-hidden="true"></i>${esc(item.text)}`
            : `<i class="bx bx-error" aria-hidden="true"></i>${esc(item.text)}`;
        span.title = item.locked ? W('LockedSource') : W('Warehouse.Candidate.StorageConditionsUnknown');
        return span;
    };

    const initFormPickers = () => {
        if (!window.jQuery?.fn?.select2) return;
        const parent = window.jQuery('#offcanvasCreateEdit');
        const candidate = byId('putawayCandidate');
        if (candidate && !window.jQuery(candidate).hasClass('select2-hidden-accessible')) {
            window.jQuery(candidate).select2({
                dropdownParent: parent,
                placeholder: window.jQuery(candidate).data('placeholder') || '',
                width: '100%',
                language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
                templateResult: candidateTemplate,
                ajax: {
                    url: '/Warehouse/PutawayTasks/candidates',
                    dataType: 'json',
                    delay: 250,
                    data: (params) => ({ q: params.term || '' }),
                    processResults: (data) => ({
                        results: (data?.items || []).map((c) => Object.assign(rules.candidateOption(c, W), { warehouseId: c.warehouseId, openQuantity: c.openQuantity, uom: c.uom }))
                    })
                }
            }).on('select2:select', (event) => {
                const data = event.params.data;
                byId('putawayWarehouseId').value = data.warehouseId || '';
                byId('putawayQuantity').value = data.openQuantity || '';
                byId('putawayQuantity').dataset.max = data.openQuantity || '';
                byId('putawayCandidateHint').textContent = format(W('RemainingHint'), data.openQuantity, data.uom);
                fieldError('putawayCandidateError', '');
                // §11.3a: an unknown storage condition closes the bin and "Kaydet" and says why; the row stays selectable (not hidden)
                if (rules.applyConditionGate(byId, !!data.unknownConditions, W)) setBin('putaway', '', '');
            });
        }

        const assignee = byId('putawayAssignee');
        if (assignee && !window.jQuery(assignee).hasClass('select2-hidden-accessible')) ajaxSelect(assignee, '/Warehouse/PutawayTasks/people', { dropdownParent: parent });
    };

    const setAssignee = (p) => {
        const el = byId('putawayAssignee');
        if (!el) return;
        el.innerHTML = '<option value=""></option>';
        if (p?.userId) el.add(new Option(p.name || W('UnknownUser'), p.userId, true, true));
        if (window.jQuery) window.jQuery(el).trigger('change');
    };

    const showSuggestion = (task) => {
        const s = rules.suggestion(task);
        byId('putawaySuggestion')?.classList.toggle('d-none', !s);
        setText('putawaySuggestionText', s ? s.label : '');
    };

    const openCreate = () => {
        if (!can('warehouse.putaway-tasks.create') || !byId('offcanvasCreateEdit')) return;
        formState = { mode: 'create', task: null, etag: null, key: newKey(), appliedSuggestion: null };
        showAlert('formPutawayTaskAlert', '');
        setText('offcanvasCreateEditLabel', W('FormTitleCreate'));
        byId('putawaySourceBlock').classList.remove('d-none');
        byId('putawaySourceReadonly').classList.add('d-none');
        if (window.jQuery) window.jQuery('#putawayCandidate').val(null).trigger('change');
        byId('putawayCandidateHint').textContent = '';
        byId('putawayWarehouseId').value = '';
        byId('putawayQuantity').value = '';
        byId('putawayQuantity').readOnly = false;
        setBin('putaway', '', '');
        setAssignee(null);
        byId('putawayPriority').value = 'Normal';
        byId('putawayNote').value = '';
        byId('putawaySuggestion')?.classList.add('d-none'); // a new task has no suggestion yet — the service computes it on create (§8.6)
        rules.applyConditionGate(byId, false, W);
        ['putawayCandidateError', 'putawayQuantityError', 'putawayBinError'].forEach((id) => fieldError(id, ''));
        offcanvas('offcanvasCreateEdit')?.show();
    };

    const openEdit = async (id) => {
        if (!can('warehouse.putaway-tasks.update') || !byId('offcanvasCreateEdit')) return;
        const answer = await getJson(`/Warehouse/PutawayTasks/task/${encodeURIComponent(id)}`);
        if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
        const task = answer.json.task;
        formState = { mode: 'edit', task, etag: answer.json.etag, key: newKey(), appliedSuggestion: task.targetSource === 'SuggestionApplied' ? task.toLocationId : null };
        showAlert('formPutawayTaskAlert', '');
        setText('offcanvasCreateEditLabel', W('FormTitleEdit'));
        byId('putawaySourceBlock').classList.add('d-none');
        byId('putawaySourceReadonly').classList.remove('d-none');
        setText('putawaySourceText', [task.fromLocation, task.sku, task.lotNumber].filter(Boolean).join(' · '));
        byId('putawayWarehouseId').value = task.warehouseId || '';
        byId('putawayQuantity').value = task.planned || '';
        byId('putawayQuantity').readOnly = !!task.serialControlled; // whole quantity only (§8.16)
        setBin('putaway', task.toLocationId, task.toLocation);
        setAssignee(task.assignee);
        byId('putawayPriority').value = task.priority || 'Normal';
        byId('putawayNote').value = task.note || '';
        showSuggestion(task);
        rules.applyConditionGate(byId, false, W); // the task carries no product conditions (draft §3.1): the service answers 422 on save if unknown
        ['putawayQuantityError', 'putawayBinError'].forEach((fid) => fieldError(fid, ''));
        offcanvas('offcanvasDetailsPreview')?.hide();
        offcanvas('offcanvasCreateEdit')?.show();
    };

    // "Uygula" (§8.6, AC-42): the reader takes the suggestion; nothing is filled before this click.
    const applySuggestion = () => {
        const s = rules.suggestion(formState?.task);
        if (!s) return;
        setBin('putaway', s.id, s.label);
        formState.appliedSuggestion = s.id;
        byId('putawaySuggestion')?.classList.add('d-none');
    };

    const submitForm = async () => {
        if (!formState || byId('btnSavePutawayTask')?.disabled) return; // closed by §11.3a (unknown storage condition): nothing is sent
        showAlert('formPutawayTaskAlert', '');
        const qty = byId('putawayQuantity').value.trim();
        if (!rules.isQuantity(qty)) { fieldError('putawayQuantityError', W('ValidationQuantity')); return; }
        fieldError('putawayQuantityError', '');
        const to = await binToSend('putaway');
        if (to === null) return; // a typed code that is not a bin: field error already shown, nothing is sent
        const assignee = byId('putawayAssignee')?.value || '';
        const priority = byId('putawayPriority').value;
        const note = byId('putawayNote').value.trim();
        const source = rules.targetSource(formState.appliedSuggestion, to);
        let answer;

        if (formState.mode === 'create') {
            const candidate = byId('putawayCandidate')?.value;
            if (!candidate) { fieldError('putawayCandidateError', W('ValidationSource')); return; }
            answer = await post('/Warehouse/PutawayTasks/create', {
                idempotencyKey: formState.key, candidateKey: candidate, plannedQuantity: qty, toLocationId: to || null, targetSource: source,
                assigneeUserId: assignee || null, priority, note: note || null
            });
        } else {
            const t = formState.task;
            const changes = {};
            if (qty !== String(t.planned ?? '')) changes.plannedQuantity = qty;
            if ((to || null) !== (t.toLocationId || null)) { changes.toLocationId = to || null; changes.targetSource = source; }
            if ((assignee || null) !== (t.assignee?.userId || null)) changes.assigneeUserId = assignee || null;
            if (priority !== t.priority) changes.priority = priority;
            if ((note || null) !== (t.note || null)) changes.note = note || null;
            if (!Object.keys(changes).length) { showAlert('formPutawayTaskAlert', W('NoChanges')); return; }
            answer = await post(`/Warehouse/PutawayTasks/edit/${encodeURIComponent(t.id)}`, { idempotencyKey: formState.key, ifMatch: formState.etag, changes });
        }

        if (!answer.ok) { showAlert('formPutawayTaskAlert', failure(answer)); return; }
        offcanvas('offcanvasCreateEdit')?.hide();
        window.showToast?.(answer.json?.message || '', 'success');
        reload();
    };

    // ─── Bulk "Ata" (Q-13 A) ─────────────────────────────────────────────────
    let assignState = null;

    const openAssign = ({ ids }) => {
        if (!ids?.length || !byId('modalPutawayAssign')) return;
        const rows = (list?.dt?.rows?.().data?.().toArray?.() || []).filter((r) => ids.includes(r.id));
        assignState = { items: rows.map((r) => ({ id: r.id, version: r.version, number: r.number })), key: newKey() };
        showAlert('assign-alert', '');
        byId('assign-results').innerHTML = '';
        if (window.jQuery) window.jQuery('#assignUser').val(null).trigger('change');
        modal('modalPutawayAssign')?.show();
    };

    const submitAssign = async () => {
        if (!assignState) return;
        const user = byId('assignUser')?.value;
        if (!user) { showAlert('assign-alert', W('AssigneeRequired')); return; }
        const answer = await post(bulkEndpoint, {
            idempotencyKey: assignState.key, assigneeUserId: user, items: assignState.items.map((i) => ({ id: i.id, version: i.version }))
        });
        if (!answer.ok) { showAlert('assign-alert', failure(answer)); return; }
        const numbers = Object.fromEntries(assignState.items.map((i) => [i.id, i.number]));
        byId('assign-results').innerHTML = (answer.json.results || []).map((r) => `<li data-assign-outcome="${esc(r.outcome)}">${esc(numbers[r.id] || r.id)} — ${esc(W(`Warehouse.BulkOutcome.${r.outcome}`))}</li>`).join('');
        window.showToast?.(format(W('AssignResults'), answer.json.assigned, assignState.items.length), answer.json.assigned ? 'success' : 'warning');
        reload();
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const rowActionHandlers = {
        Confirm: ({ row }) => row && openConfirm(row),
        Update: ({ row }) => row?.id && openEdit(row.id),
        Claim: ({ row }) => row && claim(row),
        CloseShort: ({ row }) => row && openReason('CloseShort', row),
        Cancel: ({ row }) => row && openReason('Cancel', row)
    };

    const initDataTable = async () => {
        if (!tableEl) return;
        const base = hasSelection ? 2 : 1; // control [+ selection]
        const columns = [{ data: 'id', name: 'control' }];
        if (hasSelection) columns.push({ data: 'id', name: 'checkbox' });
        columns.push(
            { data: 'number', name: 'number' },
            { data: 'status', name: 'status' },
            { data: 'integrationFlag', name: 'flag' },
            { data: 'sku', name: 'product' },
            { data: 'lotNumber', name: 'lot' },
            { data: 'fromLocation', name: 'from' },
            { data: 'toLocation', name: 'to' },
            { data: 'planned', name: 'quantity' },
            { data: 'assignee', name: 'assignee' },
            { data: 'priority', name: 'priority' },
            { data: 'id', name: 'action' }
        );
        const visible = Array.from({ length: 10 }, (_, i) => base + i);
        const columnDefs = [
            { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
            { targets: base, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data)}</span>` },
            { targets: base + 1, render: (data) => statusBadge(data) },
            { targets: base + 2, render: (data) => flagBadge(data) },
            { targets: base + 3, render: (data) => esc(data) },
            { targets: base + 4, render: (data) => esc(data) },
            { targets: base + 5, render: (data) => `<small>${esc(data)}</small>` },
            { targets: base + 6, render: (data) => `<small>${esc(data)}</small>` },
            { targets: base + 7, render: (data, type, full) => esc(quantity(full)) },
            { targets: base + 8, render: (data) => esc(person(data)) },
            { targets: base + 9, render: (data) => esc(data ? W(`Warehouse.Priority.${data}`) : '') },
            { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
        ];
        if (hasSelection) {
            columnDefs.splice(1, 0, { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(data)}">` });
        }

        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET', error: onListError },
            onResponse,
            export: { mode: 'server', url: '/Warehouse/PutawayTasks/export', fileName: 'putaway-tasks' },
            bulk: canAssign ? {
                bulkBarSelector: '#bulkActionBar', bulkCountSelector: '#bulkSelectedCount', bulkActionSelector: '[data-bulk-action]', checkboxSelector: '.dt-checkboxes',
                clearSelectionSelector: '#btnClearSelection', selectAllSelector: '.dt-checkboxes-select-all', onBulkAction: { assign: openAssign }
            } : undefined,
            actions: { onRowAction: rowActionHandlers },
            // "Yeni görev" only for a holder of warehouse.putaway-tasks.create (absent otherwise — UAS-001 §6).
            toolbar: { addNewText: can('warehouse.putaway-tasks.create') ? (L().AddNew || '') : '', onAddNew: openCreate, colvisColumns: visible },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'PutawayTasks', saveViewColumnIndexes: visible, defaultVisibleColumnIndexes: visible, baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: { ordering: false, searching: true, columns, columnDefs }
        });
    };

    const bindEvents = () => {
        bindBinField('confirm', () => confirmState?.task?.warehouseId);
        bindBinField('putaway', () => byId('putawayWarehouseId')?.value);
        byId('confirmBinCode')?.addEventListener('wh:bin', refreshDeviation);
        bindSteppers(byId('formConfirm'));
        bindSteppers(byId('formPutawayTask'));
        byId('btnConfirmSubmit')?.addEventListener('click', submitConfirm);
        byId('btnConfirmRetry')?.addEventListener('click', () => retry(confirmState?.task));
        byId('btnReasonSubmit')?.addEventListener('click', submitReason);
        byId('btnSavePutawayTask')?.addEventListener('click', submitForm);
        byId('btnApplySuggestion')?.addEventListener('click', applySuggestion);
        byId('btnAssignSubmit')?.addEventListener('click', submitAssign);
        byId('offcanvasCreateEdit')?.addEventListener('show.bs.offcanvas', () => window.setTimeout(initFormPickers, 50));
        byId('modalPutawayAssign')?.addEventListener('shown.bs.modal', () => {
            const el = byId('assignUser');
            if (el && window.jQuery && !window.jQuery(el).hasClass('select2-hidden-accessible')) ajaxSelect(el, '/Warehouse/PutawayTasks/people', { dropdownParent: window.jQuery('#modalPutawayAssign') });
        });
        // QuickView buttons — delegated on the panel; each opens the same flow as the row menu, with the panel's fresh ETag.
        byId('oc-actions')?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-oc-action]');
            if (!button || button.disabled || !current) return;
            const { task, etag } = current;
            const action = button.dataset.ocAction;
            if (action === 'Confirm') openConfirm(task, etag);
            else if (action === 'Retry') retry(task);
            else if (action === 'Update') openEdit(task.id);
            else if (action === 'Claim') claim(task, etag);
            else openReason(action, task, etag);
        });
    };

    return {
        init: function () {
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => PutawayTasksList.init());
