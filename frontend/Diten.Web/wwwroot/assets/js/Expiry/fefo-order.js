/**
 * MOD-0176 F4 — FEFO Sırası (pack §8.2, KF-F). READ-ONLY: /Expiry/data/fefo-order → getFefoOrder (the service's order — the posting
 * hook's FefoOrderRule; this page sorts nothing). Eligible lots with the allocation of the requested quantity, the lots left out with
 * their reasons. No reservation button exists (D-1).
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc, qty } = ui;
    let skus = [];

    const loadSkus = async (warehouseId, preselect) => {
        const select = ui.byId('ex-order-sku');
        select.innerHTML = '<option value=""></option>';
        select.disabled = true;
        skus = [];
        if (!warehouseId) return;
        const reply = await ui.get(`/Expiry/lookups/skus?warehouseId=${encodeURIComponent(warehouseId)}`);
        if (!reply.ok) { ui.showError(ui.byId('ex-error'), reply); return; }
        skus = reply.json?.items || [];
        if (!skus.length) { select.add(new Option(t('Expiry.Order.NoProducts'), '')); return; }
        skus.forEach((s, i) => select.add(new Option(s.text || s.skuId, String(i))));
        select.disabled = false;
        const index = preselect ? skus.findIndex((s) => s.skuId === preselect.skuId && s.skuLevel === preselect.skuLevel) : -1;
        if (index >= 0) select.value = String(index);
    };

    const render = (order) => {
        const eligible = order.eligible || [];
        const requested = order.requestedQuantity;
        ui.byId('ex-order-hint').classList.add('d-none');
        ui.byId('ex-order-result').classList.remove('d-none');
        ui.byId('ex-order-allocation').innerHTML = requested != null
            ? `<span>${esc(t('Expiry.Order.ForQuantity', qty(requested)))}</span>`
              + eligible.filter((e) => Number(e.allocated) > 0).map((e) => `<span class="badge bg-label-primary font-monospace">${esc(e.lotNumber)} · ${esc(qty(e.allocated))}</span>`).join('')
            : '';
        const available = eligible.reduce((sum, e) => sum + Number(e.available), 0);
        ui.byId('ex-order-shortfall').textContent = Number(order.shortfall) > 0 ? t('Expiry.Order.Shortfall', qty(available), qty(order.shortfall)) : '';
        const today = new Date(new Date().toISOString().slice(0, 10) + 'T00:00:00Z');
        const daysLeft = (e) => e.expiryDate ? Math.round((new Date(e.expiryDate + 'T00:00:00Z') - today) / 86400000) : null;
        ui.byId('ex-order-eligible').querySelector('tbody').innerHTML = eligible.length
            ? eligible.map((e) => {
                const allocated = Number(e.allocated) > 0;
                return `<tr class="${allocated ? 'table-primary' : ''}"><td><span class="badge rounded-pill ${allocated ? 'bg-primary' : 'bg-label-primary'}">${esc(e.rank)}</span></td>`
                    + `<td class="font-monospace">${esc(e.lotNumber)}</td><td>${esc(ui.date(e.expiryDate))}</td><td class="text-end" dir="ltr">${esc(daysLeft(e) ?? '—')}</td>`
                    + `<td class="text-end" dir="ltr">${esc(qty(e.available))}</td><td class="text-end" dir="ltr">${allocated ? esc(qty(e.allocated)) : '—'}</td>`
                    + `<td>${ui.severityBadge(e.severity)}</td></tr>`;
            }).join('')
            : `<tr><td colspan="7" class="text-muted">${esc(t('Expiry.Order.EligibleEmpty'))}</td></tr>`;
        ui.byId('ex-order-basis').textContent = t('Expiry.Order.Basis', qty(order.skuAvailable));

        const excluded = order.excluded || [];
        ui.byId('ex-order-excluded-card').classList.remove('d-none');
        ui.byId('ex-order-excluded').innerHTML = excluded.length
            ? excluded.map((x) => {
                const link = x.reason === 'EXPIRED'
                    ? `<a class="small" href="/Expiry/Expired">${esc(t('Expiry.Order.SeeExpired'))}</a>`
                    : ['BLOCKED', 'QUARANTINED', 'HELD', 'BELOW_MIN_REMAINING', 'RETEST_DUE'].includes(x.reason) ? `<a class="small" href="/Quarantine">${esc(t('Expiry.Order.SeeQuarantine'))}</a>` : '';
                return `<li class="list-group-item d-flex flex-wrap align-items-center gap-2"><span class="font-monospace fw-medium">${esc(x.lotNumber)}</span>`
                    + `<small class="text-muted">${esc(t('Expiry.Col.Expiry'))} ${esc(ui.date(x.expiryDate))}</small><small dir="ltr">${esc(qty(x.onHand))}</small>`
                    + `${ui.reasonBadge(x.reason)}<span class="ms-auto">${link}</span></li>`;
            }).join('')
            : `<li class="list-group-item text-muted">${esc(t('Expiry.Order.ExcludedEmpty'))}</li>`;
    };

    const calculate = async (event) => {
        event?.preventDefault();
        ui.hideError(ui.byId('ex-error'));
        const warehouseId = ui.byId('ex-order-warehouse').value;
        const sku = skus[Number(ui.byId('ex-order-sku').value)];
        if (!warehouseId || !sku || ui.byId('ex-order-sku').value === '') return;
        const params = new URLSearchParams({ warehouseId, itemId: sku.itemId, skuId: sku.skuId, skuLevel: sku.skuLevel });
        const quantity = ui.byId('ex-order-quantity').value.trim().replace(',', '.');
        if (quantity) params.set('quantity', quantity);
        const reply = await ui.get(`/Expiry/data/fefo-order?${params}`);
        if (!reply.ok) { ui.showError(ui.byId('ex-error'), reply); return; }
        render(reply.json?.order || {});
    };

    document.addEventListener('DOMContentLoaded', async () => {
        const warehouse = ui.byId('ex-order-warehouse');
        await ui.loadWarehouses(warehouse);
        warehouse.addEventListener('change', () => loadSkus(warehouse.value));
        ui.byId('ex-order-form').addEventListener('submit', calculate);
        // "FEFO sırasını gör" from a list row: /Expiry/FefoOrder?warehouseId=…&itemId=…&skuId=…&skuLevel=…
        const query = new URLSearchParams(window.location.search);
        if (query.get('warehouseId')) {
            warehouse.value = query.get('warehouseId');
            await loadSkus(warehouse.value, { skuId: query.get('skuId'), skuLevel: query.get('skuLevel') });
            if (ui.byId('ex-order-sku').value !== '') await calculate();
        }
    });
})();
