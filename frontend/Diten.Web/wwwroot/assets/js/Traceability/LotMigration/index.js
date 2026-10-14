/**
 * MOD-0174 P4 — Elle Girilmiş Lotları Aktarma (pack §11.5, K-D, CT-D6). One row per SKU + typed lot text; the decisions
 * live in THIS page until "Aktarımı Onayla" (ASSUMPTION-0174-11) sends every one of them in ONE commit with the group
 * count (a list that changed meanwhile → MIGRATION_STALE, reload). The page decides nothing the service would not: it
 * suggests what the service suggested (ManualLotMatcher) and draws; the commit is checked again by the MVC and the service.
 *
 * Row actions (pack §14, each drawn only with its key): APPLYSUGGESTION · LINKEXISTINGLOT · NEWLOTFROMTEXT · MARKTYPO ·
 * CHANGEDECISION; toolbar: APPLYALLSUGGESTIONS · CONFIRMMIGRATION (disabled while a row is pending). Dialogs go through
 * window.showConfirm (no raw SweetAlert). The browser talks only to same-origin /LotMigration.
 */
'use strict';

window.LotMigrationPage = (function () {
    const ui = window.TraceUi;
    const t = ui.t;
    const esc = ui.esc;
    const byId = (id) => document.getElementById(id);
    const tableEl = byId('dt-lotmigration');

    const state = {
        groups: [],
        byId: new Map(),
        decisions: new Map(),
        tab: 'all',
        completed: null,
        key: (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`).toString(),
        pick: null,
        newLotFor: null
    };
    let list = null;

    // ── decisions (page state) ─────────────────────────────────────────────────────────────────────────

    /// The decision a suggestion stands for; null when it cannot be applied as is (a new lot whose text is no lot number).
    const suggestionDecision = (g) => {
        const s = g.suggestion || {};
        if ((s.kind === 'ExactLot' || s.kind === 'SimilarLot') && s.lotId) {
            return { kind: 'LinkExisting', lotId: s.lotId, label: s.lotNumber || s.lotId, typo: s.kind === 'SimilarLot' };
        }
        if (s.kind === 'SimilarGroup' && s.groupId && state.byId.has(s.groupId)) {
            return { kind: 'Typo', targetGroupId: s.groupId, label: s.groupText || '' };
        }
        if (g.normalizedValid) {
            return { kind: 'NewLot', newLot: { lotNumber: g.normalized }, label: g.normalized };
        }
        return null;
    };

    const suggestionText = (g) => {
        const s = g.suggestion || {};
        switch (s.kind) {
            case 'ExactLot': return s.note === 'case' ? t('Suggest.CaseOnly', s.lotNumber || '') : t('Suggest.Exact', s.lotNumber || '');
            case 'SimilarLot': return t('Suggest.Similar', s.lotNumber || '');
            case 'SimilarGroup': return t('Suggest.SimilarRow', s.groupText || '');
            default: return g.normalizedValid ? t('Suggest.NewDraft', g.normalized) : t('Suggest.None');
        }
    };

    const decisionBadge = (g) => {
        const d = state.decisions.get(g.groupId);
        if (!d) return `<span class="badge bg-label-warning">${esc(t('Decision.Pending'))}</span>`;
        if (d.kind === 'NewLot') return `<span class="badge bg-label-info">${esc(t('Decision.New', d.newLot.lotNumber))}</span>`;
        if (d.kind === 'Typo' || d.typo) return `<span class="badge bg-label-primary">${esc(t('Decision.Typo', d.label))}</span>`;
        return `<span class="badge bg-label-success">${esc(t('Decision.Linked', d.label))}</span>`;
    };

    /// Where a row ends after its typo chain (≤ 3 steps, same as the service): an existing lot id or the owner of a new lot.
    const finalTarget = (groupId) => {
        let current = groupId;
        for (let hop = 0; hop <= 3; hop++) {
            const d = state.decisions.get(current);
            if (!d) return null;
            if (d.kind === 'LinkExisting') return `lot:${d.lotId}`;
            if (d.kind === 'NewLot') return `new:${current}`;
            current = d.targetGroupId;
        }
        return null;
    };

    // ── KPI, tabs, toolbar ─────────────────────────────────────────────────────────────────────────────

    const refresh = () => {
        const moves = (rows) => rows.reduce((sum, g) => sum + (g.transactionCount || 0), 0);
        const matched = state.groups.filter((g) => state.decisions.has(g.groupId));
        const pending = state.groups.filter((g) => !state.decisions.has(g.groupId));
        const put = (name, rows) => {
            const n = document.querySelector(`[data-kpi="${name}"]`);
            if (n) n.textContent = ui.qty(rows.length);
            const m = document.querySelector(`[data-kpi-moves="${name}"]`);
            if (m) m.textContent = t('Kpi.Moves', ui.qty(moves(rows)));
        };
        put('total', state.groups);
        put('matched', matched);
        put('pending', pending);

        const readOnly = !!state.completed;
        const applyAll = byId('btnApplyAll');
        if (applyAll) applyAll.disabled = readOnly || !ui.can('APPLYALLSUGGESTIONS') || pending.every((g) => !suggestionDecision(g));
        const confirm = byId('btnConfirmMigration');
        if (confirm) confirm.disabled = readOnly || !ui.can('CONFIRMMIGRATION') || pending.length > 0 || state.groups.length === 0;
        list?.dt?.rows().invalidate('data').draw(false);
    };

    const bindTabs = () => {
        document.querySelectorAll('#migration-tabs [data-tab]').forEach((button) => button.addEventListener('click', () => {
            state.tab = button.dataset.tab;
            document.querySelectorAll('#migration-tabs [data-tab]').forEach((b) => b.classList.toggle('active', b === button));
            list?.dt?.draw();
        }));
        // The tab is a row filter of THIS table only (page state, not a server query).
        window.jQuery?.fn?.dataTable?.ext?.search?.push((settings, data, index, row) => {
            if (settings.nTable !== tableEl || state.tab === 'all') return true;
            const decided = state.decisions.has(row?.groupId);
            return state.tab === 'done' ? decided : !decided;
        });
    };

    const showError = (reply) => ui.showAlert(byId('migration-error'), ui.errorText(reply), reply?.json?.error?.correlationId);

    // ── row actions ────────────────────────────────────────────────────────────────────────────────────

    const decide = (groupId, decision) => {
        if (decision) state.decisions.set(groupId, decision);
        else state.decisions.delete(groupId);
        refresh();
    };

    const openPick = async (g, typo) => {
        state.pick = { group: g, typo };
        byId('modalMigrationPickTitle').textContent = typo ? t('Pick.TitleTypo') : t('Pick.TitleLink');
        byId('migrationPickTyped').textContent = g.typedText;
        byId('migrationPickTypoNote').classList.toggle('d-none', !typo);
        const select = byId('migrationPickTarget');
        select.innerHTML = '<option value=""></option>';
        const reply = await ui.getJson(`/LotMigration/lots?${new URLSearchParams({ sku: g.sku })}`);
        const lots = reply.json?.items || [];
        if (typo) {
            const rows = state.groups.filter((o) => o.sku === g.sku && o.groupId !== g.groupId);
            if (rows.length) {
                const group = document.createElement('optgroup');
                group.label = t('Pick.Rows');
                rows.forEach((o) => group.append(new Option(o.typedText, `row:${o.groupId}`)));
                select.append(group);
            }
        }
        if (lots.length) {
            const group = document.createElement('optgroup');
            group.label = t('Pick.Lots');
            lots.forEach((l) => group.append(new Option([l.lotNumber, ui.day(l.expiryDate), l.status ? t('Status.' + l.status) : ''].filter(Boolean).join(' · '), `lot:${l.value}|${l.lotNumber}`)));
            select.append(group);
        }
        byId('migrationPickNoLots').classList.toggle('d-none', select.options.length > 1);
        byId('btnMigrationPickApply').disabled = true;
        window.bootstrap?.Modal.getOrCreateInstance(byId('modalMigrationPick')).show();
    };

    const applyPick = () => {
        const value = byId('migrationPickTarget').value;
        const { group, typo } = state.pick || {};
        if (!value || !group) return;
        if (value.startsWith('row:')) {
            const target = state.byId.get(value.slice(4));
            decide(group.groupId, { kind: 'Typo', targetGroupId: target.groupId, label: target.typedText });
        } else {
            const [lotId, lotNumber] = value.slice(4).split('|');
            decide(group.groupId, { kind: 'LinkExisting', lotId, label: lotNumber, typo: !!typo });
        }
        window.bootstrap?.Modal.getOrCreateInstance(byId('modalMigrationPick')).hide();
    };

    const NEW_LOT_FIELDS = {
        lotNumber: 'migrationLotNumber', manufacturerLot: 'migrationManufacturerLot', supplierLot: 'migrationSupplierLot',
        manufacturingDate: 'migrationManufacturingDate', expiryDate: 'migrationExpiryDate', retestDate: 'migrationRetestDate',
        countryOfOrigin: 'migrationCountry', coaReference: 'migrationCoa'
    };

    const openNewLot = (g) => {
        state.newLotFor = g;
        const current = state.decisions.get(g.groupId);
        const values = current?.kind === 'NewLot' ? current.newLot : { lotNumber: g.normalizedValid ? g.normalized : g.typedText.trim() };
        Object.entries(NEW_LOT_FIELDS).forEach(([field, id]) => { byId(id).value = values[field] || ''; });
        document.querySelectorAll('[data-newlot-error]').forEach((e) => { e.textContent = ''; });
        byId('migrationNewLotSku').textContent = [g.product, g.skuLabel].filter(Boolean).join(' · ');
        byId('migrationNewLotPrefilled').classList.toggle('d-none', !g.normalizedValid);
        window.bootstrap?.Modal.getOrCreateInstance(byId('modalMigrationNewLot')).show();
    };

    const applyNewLot = async () => {
        const g = state.newLotFor;
        if (!g) return;
        const body = {};
        Object.entries(NEW_LOT_FIELDS).forEach(([field, id]) => { body[field] = byId(id).value || null; });
        const reply = await ui.postJson('/LotMigration/validate-lot', body, ui.antiforgery(byId('migration-antiforgery')));
        document.querySelectorAll('[data-newlot-error]').forEach((e) => { e.textContent = ''; });
        if (!reply.ok) {
            const fields = reply.json?.fields || {};
            Object.entries(fields).forEach(([field, message]) => {
                const box = document.querySelector(`[data-newlot-error="${field}"]`);
                if (box) box.textContent = message;
            });
            if (!Object.keys(fields).length) showError(reply);
            return;
        }
        body.lotNumber = reply.json.lotNumber;
        decide(g.groupId, { kind: 'NewLot', newLot: body, label: body.lotNumber });
        window.bootstrap?.Modal.getOrCreateInstance(byId('modalMigrationNewLot')).hide();
    };

    const rowActionHandlers = {
        APPLYSUGGESTION: ({ row }) => { const g = state.byId.get(row?.groupId); if (g) decide(g.groupId, suggestionDecision(g)); },
        LINKEXISTINGLOT: ({ row }) => { const g = state.byId.get(row?.groupId); if (g) openPick(g, false); },
        MARKTYPO: ({ row }) => { const g = state.byId.get(row?.groupId); if (g) openPick(g, true); },
        NEWLOTFROMTEXT: ({ row }) => { const g = state.byId.get(row?.groupId); if (g) openNewLot(g); },
        CHANGEDECISION: ({ row }) => { if (row?.groupId) decide(row.groupId, null); }
    };

    const renderAction = (full) => {
        if (state.completed) return '';
        const g = state.byId.get(full.groupId) || full;
        const decided = state.decisions.has(g.groupId);
        const attrs = (text) => ({ 'data-id': g.groupId, 'data-json': JSON.stringify({ groupId: g.groupId }), title: text, 'aria-label': text });
        return window.DitenDataTable.renderActions([
            { key: 'APPLYSUGGESTION', visible: ui.can('APPLYSUGGESTION') && !decided && !!suggestionDecision(g), icon: 'bx bx-check', text: t('Action.APPLYSUGGESTION'), attrs: attrs(t('Action.APPLYSUGGESTION')) },
            { key: 'LINKEXISTINGLOT', visible: ui.can('LINKEXISTINGLOT') && !decided, icon: 'bx bx-link', text: t('Action.LINKEXISTINGLOT'), attrs: attrs(t('Action.LINKEXISTINGLOT')) },
            { key: 'NEWLOTFROMTEXT', visible: ui.can('NEWLOTFROMTEXT') && !decided, icon: 'bx bx-plus-circle', text: t('Action.NEWLOTFROMTEXT'), attrs: attrs(t('Action.NEWLOTFROMTEXT')) },
            { key: 'MARKTYPO', visible: ui.can('MARKTYPO') && !decided, icon: 'bx bx-git-merge', text: t('Action.MARKTYPO'), attrs: attrs(t('Action.MARKTYPO')) },
            { key: 'CHANGEDECISION', visible: ui.can('CHANGEDECISION') && decided, icon: 'bx bx-undo', text: t('Action.CHANGEDECISION'), attrs: attrs(t('Action.CHANGEDECISION')) }
        ]);
    };

    // ── toolbar: apply all, confirm ────────────────────────────────────────────────────────────────────

    const applyAll = () => {
        state.groups.filter((g) => !state.decisions.has(g.groupId)).forEach((g) => {
            const decision = suggestionDecision(g);
            if (decision) state.decisions.set(g.groupId, decision);
        });
        refresh();
    };

    const confirmMigration = () => {
        const moves = state.groups.reduce((sum, g) => sum + (g.transactionCount || 0), 0);
        const targets = new Set(state.groups.map((g) => finalTarget(g.groupId)).filter(Boolean));
        const created = [...targets].filter((x) => x.startsWith('new:')).length;
        const existing = targets.size - created;
        const subtext = `<p class="mb-2">${esc(t('Confirm.Count', ui.qty(moves), ui.qty(targets.size)))}</p>`
            + `<p class="mb-2 small">${esc(t('Confirm.Existing', ui.qty(existing)))} · ${esc(t('Confirm.New', ui.qty(created)))}</p>`
            + `<div class="alert alert-danger small py-2 mb-0">${esc(t('Confirm.Danger'))}</div>`;

        window.showConfirm?.(t('Confirm.Title'), async (reason) => {
            const decisions = state.groups.map((g) => {
                const d = state.decisions.get(g.groupId);
                if (d.kind === 'LinkExisting') return { groupId: g.groupId, kind: d.kind, lotId: d.lotId };
                if (d.kind === 'Typo') return { groupId: g.groupId, kind: d.kind, targetGroupId: d.targetGroupId };
                return { groupId: g.groupId, kind: d.kind, newLot: d.newLot };
            });
            const reply = await ui.postJson('/LotMigration/commit', {
                idempotencyKey: state.key,
                reason: (reason || '').trim(),
                expectedGroupCount: state.groups.length,
                decisions
            }, ui.antiforgery(byId('migration-antiforgery')));
            if (reply.ok && reply.json?.success) {
                window.showToast?.(t('Toast.Done'), 'success');
                await load();
                return;
            }
            showError(reply);
            if (reply.json?.error?.code === 'MIGRATION_STALE') {
                state.key = (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`).toString();
                await load();
            }
        }, {
            type: 'danger',
            subtext,
            confirmButtonText: t('Confirm.Button'),
            cancelButtonText: t('Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Confirm.Reason'),
            inputAttributes: { maxlength: 500 },
            inputValidator: (value) => ((value || '').trim().length < 10 ? t('Validation.ReasonMin') : null)
        });
    };

    // ── the list ───────────────────────────────────────────────────────────────────────────────────────

    const showCompleted = (done) => {
        state.completed = done || null;
        const box = byId('migration-done');
        if (!box) return;
        box.classList.toggle('d-none', !done);
        byId('migration-banner')?.classList.toggle('d-none', !!done);
        if (done) {
            box.querySelector('[data-done-text]').textContent = t('Banner.Done', ui.dateTime(done.completedAt),
                ui.qty(done.transactionsLinked || 0), ui.qty(done.lotsLinked || 0), ui.qty(done.lotsCreated || 0));
        }
    };

    const receive = (json) => {
        const items = json?.data?.items || [];
        state.groups = items;
        state.byId = new Map(items.map((g) => [g.groupId, g]));
        [...state.decisions.keys()].filter((id) => !state.byId.has(id)).forEach((id) => state.decisions.delete(id));
        showCompleted(json?.completed);
        byId('migration-error')?.classList.add('d-none');
        setTimeout(refresh, 0);
        return items;
    };

    const load = async () => list?.dt?.ajax.reload(null, false);

    const initDataTable = async () => {
        if (!tableEl) return;
        list = await window.DitenDataTable.createList({
            tableEl: tableEl,
            dataMode: 'client',
            ajax: {
                url: '/LotMigration/groups',
                type: 'GET',
                dataSrc: receive,
                error: (xhr) => showError({ status: xhr?.status || 0, json: (() => { try { return JSON.parse(xhr?.responseText || '{}'); } catch (e) { return null; } })() })
            },
            actions: { onRowAction: rowActionHandlers },
            toolbar: { addNewText: '', colvisColumns: [1, 2, 3, 4, 5, 6] },
            savedView: { moduleKey: 'Traceability', pageKey: 'LotMigration', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6], baseOrder: [[1, 'asc']] },
            config: {
                ordering: true,
                searching: true,
                pageLength: 25,
                initComplete: function () { try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ } },
                columns: [
                    { data: 'id', name: 'control' }, { data: 'product', name: 'product' }, { data: 'typedText', name: 'typedText' },
                    { data: 'transactionCount', name: 'transactionCount' }, { data: 'onHand', name: 'onHand' },
                    { data: 'suggestion', name: 'suggestion' }, { data: 'groupId', name: 'decision' }, { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type, full) => `${esc(data || '')}<div class="small text-muted">${esc(full.skuLabel || '')}</div>` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data || '')}</span>` },
                    { targets: 3, render: (data) => esc(ui.qty(data || 0)) },
                    { targets: 4, render: (data, type, full) => `${esc(ui.qty(data || 0))} ${esc(full.baseUomId || '')}` },
                    { targets: 5, orderable: false, render: (data, type, full) => esc(suggestionText(full)) },
                    { targets: 6, orderable: false, render: (data, type, full) => decisionBadge(full) },
                    { targets: -1, title: t('Col.Action'), searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    const bind = () => {
        bindTabs();
        byId('btnApplyAll')?.addEventListener('click', applyAll);
        byId('btnConfirmMigration')?.addEventListener('click', confirmMigration);
        byId('btnMigrationPickApply')?.addEventListener('click', applyPick);
        byId('migrationPickTarget')?.addEventListener('change', () => { byId('btnMigrationPickApply').disabled = !byId('migrationPickTarget').value; });
        byId('btnMigrationNewLotApply')?.addEventListener('click', applyNewLot);
    };

    return {
        init: () => { bind(); initDataTable(); },
        state
    };
})();

document.addEventListener('DOMContentLoaded', () => window.LotMigrationPage.init());
