/**
 * MOD-0174 P3 — İzlenebilirlik (pack §11.3): one same-origin call (/LotTrace/data/{lotId}) draws the flow — backward 2
 * levels, the root, forward 2 levels — the per-unit totals (never summed across units) and the stat cards. Clicking a
 * lot card re-roots the flow (the address follows). Not a DataTable.
 */
'use strict';

const LotTrace = (function () {
    const ui = window.TraceUi;
    const t = ui.t;
    const esc = ui.esc;
    const root = document.getElementById('lottrace');
    const picker = document.getElementById('lotTraceLot');
    const byId = (id) => document.getElementById(id);

    const NODE_LABEL = { receipt: 'Node.Receipt', opening: 'Node.Opening', shipment: 'Node.Shipment', consumed: 'Node.Consumed', lot: 'Node.Used' };

    const card = (node) => {
        const isLot = node.kind === 'lot' && node.lotId;
        const head = `<small class="text-muted d-block">${esc(t(NODE_LABEL[node.kind] || 'Node.Used'))}</small>`;
        const title = `<span class="fw-medium font-monospace">${esc(node.title || '')}</span>`;
        const sub = [node.status ? ui.statusBadge(node.status) : '', node.subtitle ? esc(ui.date(node.subtitle)) : '', node.reference && !isLot ? esc(node.reference) : '']
            .filter(Boolean).join(' · ');
        return isLot
            ? `<button type="button" class="btn btn-outline-secondary text-start p-2" data-reroot="${esc(node.lotId)}">${head}${title}<div class="small">${sub}</div></button>`
            : `<div class="border rounded p-2">${head}${title}<div class="small">${sub}</div></div>`;
    };

    const column = (slot, nodes, emptyKey) => {
        const box = byId(`lottrace-${slot}`)?.querySelector('[data-nodes]');
        if (!box) return;
        box.innerHTML = nodes.length ? nodes.map(card).join('') : `<small class="text-muted">${esc(t(emptyKey))}</small>`;
    };

    const load = async (lotId, push) => {
        byId('lottrace-error')?.classList.add('d-none');
        if (!lotId) { byId('lottrace-body')?.classList.add('d-none'); byId('lottrace-empty')?.classList.remove('d-none'); return; }
        const reply = await ui.getJson(`/LotTrace/data/${encodeURIComponent(lotId)}`);
        if (!reply.ok) {
            const box = byId('lottrace-error');
            if (box) { box.textContent = ui.errorText(reply); box.classList.remove('d-none'); }
            return;
        }
        const d = reply.json;
        byId('lottrace-empty')?.classList.add('d-none');
        byId('lottrace-body')?.classList.remove('d-none');
        const set = (name, html) => { const n = document.querySelector(`#lottrace-root [data-root="${name}"]`); if (n) n.innerHTML = html; };
        set('lotNumber', esc(d.root.lotNumber));
        set('product', esc(d.root.product || ''));
        set('status', ui.statusBadge(d.root.status));
        set('expiry', esc(ui.date(d.root.expiryDate) || '—'));
        set('onHand', esc(d.root.onHand == null ? '0' : `${ui.qty(d.root.onHand)} ${d.root.baseUomId || ''}`.trim()));
        const detail = document.querySelector('#lottrace-root [data-root="detail"]');
        if (detail) detail.href = ui.lotUrl(d.root.lotId);

        column('back2', d.back2 || [], 'Back.None');
        column('back1', d.back1 || [], 'Back.None');
        column('fwd1', d.forward1 || [], 'Fwd.None');
        column('fwd2', d.forward2 || [], 'Fwd.None');

        // Per unit, never summed across units (pack §11.3).
        const totals = d.totals || [];
        document.querySelector('#lottrace-total [data-totals]').innerHTML = totals.length
            ? totals.map((x) => `<div class="fs-5 fw-medium" dir="ltr">${esc(t('Total.PerUnit', ui.qty(x.affected), x.baseUomId, ui.qty(x.onHand), ui.qty(x.shipped)))}</div>`).join('')
            : '<span class="text-muted">0</span>';
        const stat = (name, text) => { const n = document.querySelector(`#lottrace-stats [data-stat="${name}"]`); if (n) n.textContent = text; };
        stat('onHand', totals.map((x) => `${ui.qty(x.onHand)} ${x.baseUomId}`).join(' · ') || '0');
        stat('shipped', totals.map((x) => `${ui.qty(x.shipped)} ${x.baseUomId}`).join(' · ') || '0');
        stat('lots', String(d.affectedLots || 1));

        if (picker && picker.value !== d.root.lotId) {
            if (!Array.from(picker.options).some((o) => o.value === d.root.lotId)) picker.add(new Option(d.root.lotNumber, d.root.lotId));
            picker.value = d.root.lotId;
            if (window.jQuery) $(picker).trigger('change.select2');
        }
        if (push) window.history.pushState({ lotId }, '', `/LotTrace?${new URLSearchParams({ lotId })}`);
    };

    const bind = () => {
        if (picker && window.jQuery?.fn?.select2) {
            $(picker).select2({
                width: '100%', placeholder: picker.dataset.placeholder || '',
                language: { searching: () => t('Searching'), noResults: () => t('NoResults') },
                ajax: { url: '/LotTrace/lookups/lots', dataType: 'json', delay: 250, data: (p) => ({ q: p.term || '' }),
                    processResults: (dd) => ({ results: (dd?.items || []).map((o) => ({ id: o.value, text: o.text })) }) }
            });
            $(picker).on('select2:select', () => load(picker.value, true));
        }
        root.addEventListener('click', (event) => {
            const target = event.target.closest('[data-reroot]');
            if (target) load(target.dataset.reroot, true);
        });
        window.addEventListener('popstate', () => load(new URLSearchParams(window.location.search).get('lotId') || '', false));
    };

    return {
        init: () => {
            if (!root) return;
            bind();
            load(root.dataset.lotId || '', false);
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => LotTrace.init());
