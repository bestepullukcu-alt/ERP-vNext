/**
 * MOD-0174 P3 — Lot Detayı (pack §11.3): one same-origin call (/Lots/data/{lotId}) fills the sections — Eldeki stok
 * (stockSummary, K-A), Lot bilgileri (11 fields incl. Takip and Kaynak), Stok dağılımı (+ the Blocked / Expired banner
 * "stoğun tamamı kullanılamaz"), Seri numaraları (first 8, serial-tracked only), Değişiklik geçmişi (paged, append-only).
 * The header buttons are server-drawn per key; the edit button shown is the one of the lot's status (K-C).
 */
'use strict';

const LotDetail = (function () {
    const ui = window.TraceUi;
    const t = ui.t;
    const esc = ui.esc;
    const root = document.getElementById('lot-detail');
    const lotId = root?.dataset.lotId;
    let historyPage = 1;
    let historyTotal = 0;
    const HISTORY_PAGE_SIZE = 10;

    const section = (id) => document.getElementById(id);
    const reveal = (box, selector) => {
        box.querySelectorAll('[data-skeleton]').forEach((s) => s.classList.add('d-none'));
        box.querySelector(selector)?.classList.remove('d-none');
    };

    const sourceText = (source) => {
        if (!source?.kind) return t('Info.None');
        const kind = { GoodsReceipt: 'Info.Source.GoodsReceipt', ProductionReceipt: 'Info.Source.Production', OpeningBalance: 'Info.Source.Opening', Manual: 'Info.Source.Manual', Migration: 'Info.Source.Migration' }[source.kind];
        return [t(kind), source.documentNumber, ui.date(source.date)].filter(Boolean).join(' · ');
    };

    const renderInfo = (data) => {
        const lot = data.lot;
        const fields = [
            ['Form.LotNumber', `<span class="font-monospace">${esc(lot.lotNumber)}</span>`],
            ['Form.Product', esc(lot.product || lot.itemId || '')],
            ['Form.Sku', esc(lot.sku || `${lot.skuLevel || ''} ${lot.skuId || ''}`.trim())],
            ['Form.ManufacturerLot', esc(lot.manufacturerLot || t('Info.None'))],
            ['Form.SupplierLot', esc(lot.supplierLot || t('Info.None'))],
            ['Form.ManufacturingDate', esc(ui.date(lot.manufacturingDate) || t('Info.None'))],
            ['Form.ExpiryDate', lot.expiryDate ? ui.expiryCell(lot.expiryDate, lot.expiryState) : esc(t('Info.None'))],
            ['Form.RetestDate', lot.retestDate ? ui.retestCell(lot.retestDate, lot.retestDue) : esc(t('Info.None'))],
            ['Form.Country', esc(lot.country || t('Info.None'))],
            ['Form.Coa', esc(lot.coaReference || t('Info.None'))],
            ['Info.Tracking', esc(t(lot.trackingMode === 'LotAndSerial' ? 'Tracking.LotAndSerial' : 'Tracking.Lot'))],
            ['Info.Source', esc(sourceText(data.source))]
        ];
        const dl = section('lot-info').querySelector('[data-fields]');
        dl.innerHTML = fields.map(([label, value]) => `<dt class="col-5 fw-medium text-heading">${esc(t(label))}</dt><dd class="col-7 text-break">${value}</dd>`).join('');
        reveal(section('lot-info'), '[data-fields]');
    };

    const renderHeader = (data) => {
        const lot = data.lot;
        document.getElementById('lot-title').textContent = lot.lotNumber || '';
        document.getElementById('lot-breadcrumb').textContent = lot.lotNumber || '';
        document.getElementById('lot-status-badge').innerHTML = ui.statusBadge(lot.status);
        const warnings = [];
        if (lot.expiryState === 'Expired' || lot.expiryState === 'ExpiringSoon') warnings.push(ui.expiryCell(lot.expiryDate, lot.expiryState));
        if (lot.retestDue) warnings.push(`<span class="badge bg-label-warning">${esc(t('Retest.Past'))}</span>`);
        document.getElementById('lot-warnings').innerHTML = warnings.join(' ');
        // K-C: show the edit button of the lot's status (both are server-drawn per key, hidden until the status is known).
        document.querySelectorAll('[data-trace-action="EDITLOT"],[data-trace-action="EDITRELEASEDLOT"]').forEach((a) => {
            a.classList.toggle('d-none', a.dataset.traceAction !== data.editAction);
        });
        if (lot.status === 'Expired') document.querySelector('[data-trace-action="CHANGELOTSTATUS"]')?.classList.add('d-none');
    };

    const renderStock = (data) => {
        const box = section('lot-stock');
        const onHand = section('lot-onhand');
        const value = onHand.querySelector('[data-value]');
        value.textContent = data.lot.onHand == null ? '0' : `${ui.qty(data.lot.onHand)} ${data.lot.baseUomId || ''}`.trim();
        reveal(onHand, '[data-value]');

        const rows = (data.stock?.rows || []).filter((r) => Number(r.onHand) !== 0);
        if (!data.stock || rows.length === 0) {
            reveal(box, '[data-empty]');
        } else {
            box.querySelector('tbody').innerHTML = rows.map((r) => `<tr><td>${esc(r.warehouse || '')}</td><td>${esc(r.location || '')}</td>`
                + `<td>${esc(t('StockStatus.' + r.stockStatus))}</td><td class="text-end" dir="ltr">${esc(ui.qty(r.onHand))} ${esc(r.baseUomId || '')}</td></tr>`).join('');
            reveal(box, '[data-table]');
        }
        // "Bloke lot ≠ bloke stok": the rows keep their stock status, the lot makes all of them unusable (CT-D1).
        const banner = document.getElementById('lot-unusable');
        const unusable = data.lot.status === 'Blocked' ? 'Detail.Unusable.Blocked' : data.lot.status === 'Expired' ? 'Detail.Unusable.Expired' : null;
        if (unusable) {
            banner.textContent = t(unusable, value.textContent);
            banner.classList.remove('d-none');
        }
    };

    const renderSerials = (data) => {
        if (!data.serials) return;
        const box = section('lot-serials');
        const list = box.querySelector('[data-list]');
        list.innerHTML = data.serials.items.map((s) => `<span class="badge bg-label-secondary font-monospace">${esc(s.serialNumber)}</span> ${ui.serialBadge(s.status)}`).join(' ');
        if (data.serials.total > data.serials.items.length) list.insertAdjacentHTML('beforeend', `<small class="text-muted align-self-center">${esc(t('Detail.SerialsMore', data.serials.total - data.serials.items.length))}</small>`);
        box.classList.remove('d-none');
    };

    const historyRow = (h) => {
        const who = h.source === 'system-expiry' ? t('History.System') : (h.actorDisplay || h.actorUserId || '');
        const change = h.field ? `${esc(h.oldValue ?? '—')} → ${esc(h.newValue ?? '—')}` : esc(t('History.Action.' + h.action));
        return `<tr><td class="text-nowrap">${esc(ui.dateTime(h.occurredAt))}</td><td>${esc(who)}</td><td>${esc(h.field || t('History.Action.' + h.action))}</td>`
            + `<td class="text-break">${change}</td><td class="text-break">${esc(h.reason || '')}</td></tr>`;
    };

    const renderHistory = (history) => {
        const box = section('lot-history');
        historyTotal = history?.total || 0;
        box.querySelector('tbody').innerHTML = (history?.items || []).map(historyRow).join('');
        reveal(box, '[data-table]');
        const pages = Math.max(1, Math.ceil(historyTotal / HISTORY_PAGE_SIZE));
        const pager = box.querySelector('[data-pager]');
        pager.classList.toggle('d-none', pages <= 1);
        pager.querySelector('[data-page-text]').textContent = t('History.Page', historyPage, pages);
        pager.querySelector('[data-page="prev"]').disabled = historyPage <= 1;
        pager.querySelector('[data-page="next"]').disabled = historyPage >= pages;
    };

    const loadHistory = async (page) => {
        const reply = await ui.getJson(`/Lots/history/${encodeURIComponent(lotId)}?page=${page}`);
        if (reply.ok) { historyPage = page; renderHistory(reply.json); }
    };

    const load = async () => {
        const reply = await ui.getJson(`/Lots/data/${encodeURIComponent(lotId)}`);
        if (!reply.ok) {
            ui.showAlert(document.getElementById('lot-detail-error'), ui.errorText(reply), reply.json?.error?.correlationId);
            document.querySelectorAll('#lot-detail [data-skeleton]').forEach((s) => s.classList.add('d-none'));
            return;
        }
        const data = reply.json;
        renderHeader(data);
        renderInfo(data);
        renderStock(data);
        renderSerials(data);
        historyPage = 1;
        renderHistory(data.history);
    };

    const bind = () => {
        const pager = section('lot-history')?.querySelector('[data-pager]');
        pager?.querySelector('[data-page="prev"]')?.addEventListener('click', () => loadHistory(historyPage - 1));
        pager?.querySelector('[data-page="next"]')?.addEventListener('click', () => loadHistory(historyPage + 1));
        document.querySelector('[data-trace-action="CHANGELOTSTATUS"]')?.addEventListener('click', () => window.LotStatus?.open(lotId, { onDone: load }));
    };

    return {
        init: () => {
            if (!lotId) return;
            bind();
            load();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => LotDetail.init());
