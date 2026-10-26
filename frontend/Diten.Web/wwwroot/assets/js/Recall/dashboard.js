/**
 * MOD-0177 R6 — Hazırlık Paneli (pack §8.9, mockup "isDash"). One door: /Recall/data/dashboard (getDashboard). Every figure is the server's;
 * this file only lays them out: the frequency band, five KPI cards, the drill chart (ApexCharts, the repo's vendored library) with the
 * target line, the open cases table. Quantities are shown per SKU + unit, never summed across units. Rows open nothing (R7).
 */
'use strict';

/** Pure: the five cards' texts from the getDashboard answer (no DOM; the Node test runs it against THIS file). */
var RecallDashboardView = (function () {
    const sumByUnit = (rows) => {
        const out = new Map();
        (rows || []).forEach((r) => {
            const key = `${r.skuId}|${r.baseUomId}`;
            out.set(key, { baseUomId: r.baseUomId, quantity: (out.get(key)?.quantity || 0) + Number(r.quantity || 0) });
        });
        return [...out.values()];
    };

    const cards = (panel, t, fmt) => {
        const open = panel.openCases || { total: 0, byClass: {} };
        const by = open.byClass || {};
        const last = panel.lastDrill;
        const cov = panel.lotCoverage || {};
        const gaps = panel.traceGaps || { count: 0, movements: [] };
        const returned = panel.returnedOpenCases || [];
        const units = sumByUnit(returned);
        return {
            open: { value: String(open.total ?? 0), sub: t('Recall.Kpi.OpenSub', by['1'] ?? 0, by['2'] ?? 0, by['3'] ?? 0) },
            lastDrill: last
                ? { value: fmt.duration(last.durationMinutes), badge: last.onTarget ? 'OnTarget' : 'OverTarget', sub: t('Recall.Kpi.LastDrillSub', last.number, last.targetHours) }
                : { value: t('Recall.Kpi.NoDrill'), badge: null, sub: '' },
            coverage: cov.total
                ? { value: fmt.percent(cov.percent), sub: t('Recall.Kpi.CoverageSub', cov.windowDays, fmt.qty(cov.withLot), fmt.qty(cov.total)) }
                : { value: '—', sub: t('Recall.Kpi.CoverageNone', cov.windowDays ?? 90) },
            gaps: {
                value: String(gaps.count ?? 0),
                sub: (gaps.movements || []).length ? gaps.movements.slice(0, 3).map((m) => m.number || m.transactionId).join(', ') : t('Recall.Kpi.GapsSub', gaps.windowDays ?? 90)
            },
            returned: units.length
                ? { value: units.map((u) => `${fmt.qty(u.quantity)} ${u.baseUomId}`).join(' · '), sub: returned.map((r) => `${r.number}: ${fmt.qty(r.quantity)} ${r.baseUomId}`).join(' · ') }
                : { value: t('Recall.Kpi.ReturnedNone'), sub: '' }
        };
    };

    /** The frequency band: null = no band; otherwise its title and detail. */
    const band = (warning, t, fmt) => {
        if (!warning?.warning) return null;
        return warning.lastCompletedAtUtc
            ? { title: t('Recall.Freq.Band', warning.warningDays), detail: t('Recall.Freq.Detail', fmt.date(warning.lastCompletedAtUtc), warning.daysSinceLastCompleted) }
            : { title: t('Recall.Freq.Band', warning.warningDays), detail: t('Recall.Freq.None') };
    };

    return { cards, band, sumByUnit };
})();

if (typeof window !== 'undefined') window.RecallDashboardView = RecallDashboardView;

(function () {
    if (typeof document === 'undefined' || !window.RecallUi) return;
    const ui = window.RecallUi;
    const { t, esc, byId } = ui;
    const lang = document.documentElement.lang || undefined;
    const fmt = {
        duration: ui.duration,
        qty: ui.qty,
        date: ui.date,
        percent: (p) => p == null ? '—' : new Intl.NumberFormat(lang, { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(Number(p) / 100)
    };

    const kpi = (name, card) => {
        const value = document.querySelector(`[data-kpi="${name}"]`);
        const sub = document.querySelector(`[data-kpi-sub="${name}"]`);
        const badge = document.querySelector(`[data-kpi-badge="${name}"]`);
        if (value) value.textContent = card.value;
        if (sub) sub.textContent = card.sub || '';
        if (badge) {
            badge.innerHTML = card.badge
                ? `<span class="badge ${card.badge === 'OnTarget' ? 'bg-label-success' : 'bg-label-danger'}">${esc(t('Recall.Badge.' + card.badge))}</span>`
                : '';
        }
    };

    const chart = (drillChart) => {
        const box = byId('rc-chart');
        const runs = drillChart?.runs || [];
        byId('rc-chart-target').textContent = t('Recall.Chart.Target', drillChart?.targetHours ?? '');
        byId('rc-chart-empty').classList.toggle('d-none', runs.length > 0);
        box.classList.toggle('d-none', runs.length === 0);
        if (!runs.length || typeof window.ApexCharts !== 'function') return;
        const hours = runs.map((r) => Math.round(r.durationMinutes / 6) / 10);
        const target = Number(drillChart.targetHours);
        new window.ApexCharts(box, {
            chart: { type: 'bar', height: 240, toolbar: { show: false }, fontFamily: 'inherit' },
            series: [{ name: t('Recall.Chart.Hours'), data: hours }],
            plotOptions: { bar: { distributed: true, borderRadius: 4, columnWidth: '55%' } },
            colors: runs.map((r) => (r.onTarget ? '#696cff' : '#ff3e1d')),
            legend: { show: false },
            dataLabels: { enabled: true, formatter: (v) => fmt.qty(v), style: { fontSize: '11px' } },
            xaxis: { categories: runs.map((r) => fmt.date(r.endedAtUtc)), labels: { rotate: -45 } },
            yaxis: { min: 0, max: Math.max(target * 1.5, ...hours) || 1, title: { text: t('Recall.Chart.Hours') } },
            annotations: { yaxis: [{ y: target, borderColor: '#ff3e1d', strokeDashArray: 4, label: { text: t('Recall.Chart.Target', target), style: { color: '#fff', background: '#ff3e1d' } } }] },
            tooltip: { y: { formatter: (v, o) => `${runs[o.dataPointIndex].number} · ${runs[o.dataPointIndex].lot?.lotNumber || ''} · ${fmt.duration(runs[o.dataPointIndex].durationMinutes)}` } }
        }).render();
    };

    const quantities = (list) => (list || []).map((q) => `<span dir="ltr">${esc(fmt.qty(q.quantity))} <small class="text-muted">${esc(q.baseUomId)}</small></span>`).join('<br>') || '—';
    const progress = (list) => (list || []).map((p) => {
        const pct = Math.max(0, Math.min(100, Number(p.percent) || 0));
        return `<div class="d-flex align-items-center gap-2" style="min-width:110px"><div class="progress flex-grow-1" style="height:6px">`
            + `<div class="progress-bar bg-success" role="progressbar" style="width:${pct}%" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100"></div></div>`
            + `<small dir="ltr">${esc(fmt.qty(pct))}%</small></div>`;
    }).join('') || '—';

    const table = (rows) => {
        const body = byId('rc-open-cases').querySelector('tbody');
        if (!rows.length) {
            body.innerHTML = `<tr><td colspan="7" class="text-muted">${esc(t('Recall.OpenCases.Empty'))}</td></tr>`;
            return;
        }
        body.innerHTML = rows.map((c) => `<tr>
            <td class="font-monospace">${esc(c.number)}</td>
            <td>${ui.productCell(c, c.rootLot?.lotNumber)}</td>
            <td>${ui.classBadge(c.class)}</td>
            <td>${esc(fmt.date(c.openedAtUtc))}</td>
            <td class="text-end">${quantities(c.affectedQuantity)}</td>
            <td>${progress(c.progress)}</td>
            <td>${ui.statusBadge(c.status)}</td></tr>`).join('');
    };

    const init = async () => {
        const reply = await ui.get('/Recall/data/dashboard');
        if (!reply.ok) {
            ui.showError(byId('rc-error'), reply);
            byId('rc-open-cases').querySelector('tbody').innerHTML = '';
            return;
        }
        const panel = reply.json?.panel || {};
        byId('rc-labels-unavailable').classList.toggle('d-none', reply.json?.labelsAvailable !== false);
        byId('rc-subtitle').textContent = t('Recall.Dashboard.Subtitle', fmt.date(panel.asOfUtc), panel.drillChart?.targetHours ?? '');
        const b = RecallDashboardView.band(panel.frequencyWarning, t, fmt);
        const bandBox = byId('rc-freq-band');
        bandBox.classList.toggle('d-none', !b);
        bandBox.classList.toggle('d-flex', !!b);
        if (b) {
            byId('rc-freq-title').textContent = b.title;
            byId('rc-freq-detail').textContent = b.detail;
        }
        const cards = RecallDashboardView.cards(panel, t, fmt);
        Object.entries(cards).forEach(([name, card]) => kpi(name, card));
        chart(panel.drillChart);
        table(panel.openCaseRows || []);
    };

    document.addEventListener('DOMContentLoaded', init);
})();
