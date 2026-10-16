/**
 * MOD-0177 R6 — İzleme page (pack §8.2, §11.3, mockup "isTrace"). Lot search (TRACE) + direction → /Recall/data/trace; the tree and the table
 * are drawn from ONE node list (RecallTraceView). "CSV indir" (RECALL_TRACE_EXPORT) downloads the service's file; "Vaka aç"
 * (RECALL_CASE_OPEN) opens a real Draft case (D-22). Recent lots are kept in this browser only (D-23).
 */
'use strict';

(function () {
    if (typeof document === 'undefined' || !window.RecallUi || !window.RecallTraceView) return;
    const ui = window.RecallUi;
    const view = window.RecallTraceView;
    const { t, esc, byId } = ui;
    const RECENT_KEY = 'recall.trace.recent';
    let last = null;          // { rootLotId, lotNumber, direction, trace }
    let rows = [];
    const collapsed = new Set();

    const direction = () => document.querySelector('input[name="rc-direction"]:checked')?.value || 'Both';
    const lotSelect = () => byId('rc-trace-lot');

    // ── recent lots (per-viewer convenience, browser only) ─────────────────────────────────────────────────────
    const recent = () => {
        try { return JSON.parse(window.localStorage.getItem(RECENT_KEY) || '[]').filter((x) => x && x.id && x.text).slice(0, 5); } catch (error) { return []; }
    };
    const remember = (id, text) => {
        try { window.localStorage.setItem(RECENT_KEY, JSON.stringify([{ id, text }, ...recent().filter((x) => x.id !== id)].slice(0, 5))); } catch (error) { /* no storage */ }
    };
    const drawRecent = () => {
        const list = recent();
        byId('rc-recent').classList.toggle('d-none', list.length === 0);
        byId('rc-recent-list').innerHTML = list.map((x) => `<button type="button" class="btn btn-xs btn-label-secondary font-monospace" data-recent-lot="${esc(x.id)}" data-recent-text="${esc(x.text)}">${esc(x.text)}</button>`).join('');
    };
    const choose = (id, text) => {
        const select = lotSelect();
        if (![...select.options].some((o) => o.value === id)) select.add(new Option(text, id, true, true));
        select.value = id;
        if (window.jQuery?.fn?.select2) window.jQuery(select).val(id).trigger('change');
    };

    // ── drawing ─────────────────────────────────────────────────────────────────────────────────────────────────
    const badge = (b) => `<span class="badge bg-label-${b.tone}">${esc(t(b.key) || b.raw || b.key)}</span>`;
    const amount = (q) => q ? `<span dir="ltr">${esc(ui.qty(q.value))} <small class="text-muted">${esc(q.unit)}</small></span>` : '';
    const ICON = { RawMaterialLot: 'bx-vial', FinishedGoodLot: 'bx-capsule', Lot: 'bx-package', Input: 'bx-log-in-circle', Stock: 'bx-buildings',
        Shipment: 'bx-package', Customer: 'bx-store', Gap: 'bx-error', Disposal: 'bx-trash' };

    const drawTree = () => {
        const box = byId('rc-trace-tree');
        const hidden = (r) => r.ancestors.some((a) => collapsed.has(a));
        box.innerHTML = rows.filter((r) => !hidden(r)).map((r) => `
            <div class="list-group-item d-flex flex-wrap align-items-center gap-2 ${r.gap ? 'list-group-item-danger' : ''}" data-node="${esc(r.key)}" style="padding-inline-start:${1 + r.depth * 1.5}rem">
                ${r.hasChildren ? `<button type="button" class="btn btn-icon btn-sm btn-text-secondary" data-toggle-node="${esc(r.key)}" aria-label="${esc(r.kindText)}"><i class="bx ${collapsed.has(r.key) ? 'bx-chevron-right' : 'bx-chevron-down'}"></i></button>` : '<span style="width:2rem"></span>'}
                <i class="bx ${ICON[r.kind] || 'bx-circle'} text-muted"></i>
                <div class="d-flex flex-column" style="flex:1 1 10rem;min-width:0">
                    <small class="text-muted">${esc(r.kindText)}</small>
                    <span class="fw-medium text-heading text-truncate">${esc(r.title)}</span>
                    ${r.sub ? `<small class="text-muted text-truncate">${esc(r.sub)}</small>` : ''}
                </div>
                ${r.place && r.kind === 'Shipment' ? `<small class="text-muted text-break">${esc(r.place)}</small>` : ''}
                <span class="text-end">${amount(r.quantity)}</span>
                <span class="d-flex flex-wrap gap-1">${r.badges.map(badge).join('')}</span>
            </div>`).join('');
    };

    const drawTable = () => {
        byId('rc-trace-table').querySelector('tbody').innerHTML = view.tableRows(last.trace, t).map((r) => `
            <tr class="${r.gap ? 'table-danger' : ''}" data-node="${esc(r.key)}">
                <td>${esc(r.kindText)}${r.badges.length ? ' ' + r.badges.map(badge).join(' ') : ''}</td>
                <td>${esc(r.product)}</td>
                <td class="font-monospace">${esc(r.lot)}</td>
                <td>${esc(r.place)}</td>
                <td class="text-end">${amount(r.quantity)}</td>
                <td class="font-monospace">${esc(r.ref)}</td>
                <td>${esc(ui.date(r.date))}</td>
            </tr>`).join('');
    };

    const draw = () => {
        const s = view.summary(last.trace);
        byId('rc-trace-summary').textContent = t('Recall.Trace.Summary', s.finishedGoodLots, s.stockLines, s.shipments, s.gaps);
        byId('rc-trace-duration').textContent = t('Recall.Trace.Duration', ui.qty(Math.round(s.seconds * 10) / 10));
        const gapBand = byId('rc-trace-gapband');
        gapBand.classList.toggle('d-none', s.gaps === 0);
        gapBand.textContent = s.gaps ? t('Recall.Trace.GapBand', s.gaps) : '';
        byId('rc-trace-truncated').classList.toggle('d-none', !s.truncated);
        const other = byId('rc-trace-othercompany');
        other.classList.toggle('d-none', s.otherCompany === 0);
        other.textContent = s.otherCompany ? t('Recall.Trace.OtherCompany', ui.qty(s.otherCompany)) : '';
        const root = (last.trace.nodes || []).find((n) => view.isLot(n) && n.lot?.lotId === last.rootLotId);
        byId('rc-trace-product').value = root?.itemLabel || '—';

        const actions = [];
        if (ui.can('RECALL_CASE_OPEN') && byId('rc-open-case-modal')) {
            actions.push(`<button type="button" class="btn btn-sm btn-primary" id="rc-open-case"><i class="bx bx-error-alt me-1"></i>${esc(t('Recall.Trace.OpenCase'))}</button>`);
        }
        if (ui.can('RECALL_TRACE_EXPORT')) {
            const href = `/Recall/export/trace?${new URLSearchParams({ rootLotId: last.rootLotId, direction: last.direction })}`;
            actions.push(`<a class="btn btn-sm btn-label-secondary" id="rc-trace-csv" href="${esc(href)}"><i class="bx bx-download me-1"></i>${esc(t('Recall.Trace.Csv'))}</a>`);
        }
        byId('rc-trace-actions').innerHTML = actions.join('');
        byId('rc-open-case')?.addEventListener('click', openCase);

        rows = view.treeRows(last.trace, t);
        collapsed.clear();
        drawTree();
        drawTable();
        byId('rc-trace-result').classList.remove('d-none');
        byId('rc-trace-empty').classList.add('d-none');
    };

    const run = async (event) => {
        event?.preventDefault();
        const rootLotId = lotSelect().value;
        if (!rootLotId) return;
        let lotNumber = lotSelect().selectedOptions?.[0]?.textContent || rootLotId;
        ui.hideError(byId('rc-error'));
        byId('rc-trace-busy').classList.remove('d-none');
        byId('rc-trace-run').disabled = true;
        const reply = await ui.get(`/Recall/data/trace?${new URLSearchParams({ rootLotId, direction: direction() })}`);
        byId('rc-trace-busy').classList.add('d-none');
        byId('rc-trace-run').disabled = false;
        if (!reply.ok) {
            ui.showError(byId('rc-error'), reply);
            return;
        }
        byId('rc-labels-unavailable').classList.toggle('d-none', reply.json?.labelsAvailable !== false);
        const trace = reply.json?.trace || { nodes: [] };
        // R7c: opened by ?lot={id} the picker knows only the id — the root node names the lot; the option and the recent list take its number
        const rootNumber = (trace.nodes || []).find((n) => view.isLot(n) && n.lot?.lotId === rootLotId)?.lot?.lotNumber;
        if (rootNumber && rootNumber !== lotNumber) { lotNumber = rootNumber; const select = lotSelect(); [...select.options].filter((o) => o.value === rootLotId).forEach((o) => o.remove()); select.add(new Option(rootNumber, rootLotId, true, true)); if (window.jQuery?.fn?.select2) window.jQuery(select).trigger('change.select2'); }
        last = { rootLotId, lotNumber, direction: direction(), trace };
        remember(rootLotId, lotNumber);
        drawRecent();
        draw();
    };

    // ── "Vaka aç" (D-22: a real Draft case) ──────────────────────────────────────────────────────────────────────
    let openKey = null;
    const openCase = () => {
        const modal = byId('rc-open-case-modal');
        if (!modal || !last) return;
        openKey = ui.newKey();
        byId('rc-open-case-form').reset();
        byId('rc-open-case-intro').textContent = t('Recall.OpenCase.Intro', last.lotNumber);
        ui.hideError(byId('rc-open-case-error'));
        modal.querySelector('[data-err="reason"]').textContent = '';
        window.bootstrap.Modal.getOrCreateInstance(modal).show();
    };
    const submitCase = async (event) => {
        event.preventDefault();
        const reason = byId('rc-open-case-reason').value.trim();
        const err = byId('rc-open-case-modal').querySelector('[data-err="reason"]');
        err.textContent = reason.length < 10 ? t('Recall.Common.ReasonMin') : '';
        if (reason.length < 10) return;
        const button = byId('rc-open-case-submit');
        button.disabled = true;
        const body = { idempotencyKey: openKey, rootLotId: last.rootLotId, direction: last.direction, class: Number(byId('rc-open-case-class').value),
            level: byId('rc-open-case-level').value, reason };
        const reference = byId('rc-open-case-reference').value.trim();
        if (reference) body.reasonReference = reference;
        const reply = await ui.post('/Recall/data/cases', body);
        button.disabled = false;
        if (!reply.ok) {
            ui.showError(byId('rc-open-case-error'), reply);
            return;
        }
        window.bootstrap.Modal.getOrCreateInstance(byId('rc-open-case-modal')).hide();
        window.showToast?.(t('Recall.OpenCase.Done', reply.json?.number || ''), 'success');
    };

    const initLotPicker = () => {
        const select = lotSelect();
        if (window.jQuery?.fn?.select2) {
            window.jQuery(select).select2({
                placeholder: select.dataset.placeholder || '',
                allowClear: true,
                width: '100%',
                ajax: {
                    url: '/Recall/lookups/lots',
                    dataType: 'json',
                    delay: 250,
                    data: (params) => ({ q: params.term || '' }),
                    processResults: (data) => ({ results: (data?.items || []).map((i) => ({ id: i.value, text: i.text || i.value })) })
                }
            });
        }
    };

    const init = () => {
        initLotPicker();
        drawRecent();
        // R7c: "İzle" from the 0174 lot badge and "Tam izleme (geri çağırma)" from /LotTrace open here with ?lot={lotId} — the lot is chosen and
        // the trace runs (its number comes back with the trace; until then the id stands in). Anything else in ?lot is ignored.
        const asked = new URLSearchParams(window.location.search).get('lot');
        if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(asked || '')) { choose(asked, asked); run(); }
        byId('rc-trace-form').addEventListener('submit', run);
        byId('rc-recent-list').addEventListener('click', (event) => {
            const button = event.target.closest('[data-recent-lot]');
            if (button) choose(button.dataset.recentLot, button.dataset.recentText);
        });
        byId('rc-trace-tree').addEventListener('click', (event) => {
            const toggle = event.target.closest('[data-toggle-node]');
            if (!toggle) return;
            const key = toggle.dataset.toggleNode;
            if (collapsed.has(key)) collapsed.delete(key); else collapsed.add(key);
            drawTree();
        });
        byId('rc-expand-all').addEventListener('click', () => { collapsed.clear(); drawTree(); });
        document.querySelectorAll('input[name="rc-view"]').forEach((input) => input.addEventListener('change', () => {
            const table = input.value === 'table' && input.checked;
            byId('rc-trace-tree').classList.toggle('d-none', table);
            byId('rc-trace-table-wrap').classList.toggle('d-none', !table);
            byId('rc-expand-all').classList.toggle('d-none', table);
        }));
        byId('rc-open-case-form')?.addEventListener('submit', submitCase);
    };

    document.addEventListener('DOMContentLoaded', init);
})();
