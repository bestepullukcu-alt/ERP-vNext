/**
 * MOD-0176 F4 — FEFO Paneli (pack §8.8, §11.3). One door: /Expiry/data/dashboard (getDashboard + the company horizons + the last-30-days
 * below-minimum count when readable). Cards, the 12-month chart (AVAILABLE vs held, stacked), the 10 nearest lots. No figure is computed
 * here — every number is the service's.
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc, qty } = ui;
    const MONTHS_FORMAT = new Intl.DateTimeFormat(document.documentElement.lang || undefined, { month: 'short', year: '2-digit', timeZone: 'UTC' });

    const set = (attr, name, html, href) => {
        const el = document.querySelector(`[data-${attr}="${name}"]`);
        if (el) el.innerHTML = html;
        if (href !== undefined) {
            const link = document.querySelector(`[data-kpi-link="${name}"]`);
            if (link) { if (href) link.setAttribute('href', href); else { link.removeAttribute('href'); link.classList.add('pe-none'); } }
        }
    };

    const render = (data) => {
        const panel = data.panel || {};
        const k = panel.kpis || {};
        const policy = data.policy;
        const warehouse = ui.byId('ex-filter-warehouse')?.value || '';
        const withWh = (url) => warehouse ? `${url}${url.includes('?') ? '&' : '?'}warehouseId=${encodeURIComponent(warehouse)}` : url;
        ui.byId('ex-subtitle').textContent = policy
            ? t('Expiry.Dashboard.Subtitle', ui.date(panel.asOf), policy.approachingDays, policy.criticalDays)
            : t('Expiry.Dashboard.SubtitleToday', ui.date(panel.asOf));
        ui.byId('ex-labels-unavailable')?.classList.toggle('d-none', data.labelsAvailable !== false);

        set('kpi-label', 'expired', esc(t('Expiry.Kpi.Expired')));
        set('kpi', 'expired', esc(t('Expiry.Kpi.Lots', qty(k.expiredOnHand?.lots ?? 0))), withWh('/Expiry/Expired'));
        set('kpi-sub', 'expired', esc(t('Expiry.Kpi.Quantity', qty(k.expiredOnHand?.quantity ?? 0))));

        const horizon = (name, days, plain) => {
            set('kpi-label', name, esc(days ? t('Expiry.Kpi.Critical', days) : t(plain)));
            set('kpi', name, esc(t('Expiry.Kpi.Lots', qty(k[name]?.lots ?? 0))), withWh(`/Expiry/Approaching${days ? `?horizonDays=${days}` : ''}`));
            set('kpi-sub', name, esc(t('Expiry.Kpi.AvailableHeld', qty(k[name]?.available ?? 0), qty(k[name]?.held ?? 0))));
        };
        horizon('critical', policy?.criticalDays, 'Expiry.Kpi.CriticalPlain');
        horizon('approaching', policy?.approachingDays, 'Expiry.Kpi.ApproachingPlain');

        const belowCard = document.querySelector('[data-kpi-card="belowMin"]');
        if (data.belowMinLast30 == null) belowCard?.classList.add('d-none');
        else {
            set('kpi-label', 'belowMin', esc(t('Expiry.Kpi.BelowMin')));
            set('kpi', 'belowMin', esc(t('Expiry.Kpi.Lots', qty(data.belowMinLast30))), null);
        }

        set('kpi-label', 'exceptions', esc(t('Expiry.Kpi.OpenExceptions')));
        set('kpi', 'exceptions', esc(qty(k.openExceptions ?? 0)), null);
        set('kpi-sub', 'exceptions', k.lateExceptions
            ? `<span class="text-danger">${esc(t('Expiry.Kpi.LateSome', qty(k.lateExceptions)))}</span>`
            : esc(t('Expiry.Kpi.LateNone')) + (k.avgResolutionHours != null ? ` · ${esc(t('Expiry.Kpi.AvgResolution', qty(k.avgResolutionHours)))}` : ''));

        const period = ui.byId('ex-filter-period')?.value || '30';
        set('kpi-label', 'compliance', esc(t('Expiry.Kpi.Compliance', period)));
        set('kpi', 'compliance', k.fefoCompliancePct == null ? '—' : `<span dir="ltr">%${esc(qty(k.fefoCompliancePct))}</span>`, null);
        set('kpi-sub', 'compliance', k.fefoIssues
            ? esc(t('Expiry.Kpi.ComplianceSub', qty(k.fefoIssues - k.fefoBypasses), qty(k.fefoIssues)))
            : esc(t('Expiry.Kpi.ComplianceNone')));

        // chart: stacked bars, the service's 12 months
        const months = panel.expiryByMonth || [];
        const max = Math.max(1, ...months.map((m) => Number(m.available) + Number(m.held)));
        ui.byId('ex-chart').innerHTML = months.map((m) => {
            const total = Number(m.available) + Number(m.held);
            const label = MONTHS_FORMAT.format(new Date(`${m.month}-01T00:00:00Z`));
            const title = `${label}: ${t('Expiry.Chart.Available')} ${qty(m.available)} · ${t('Expiry.Chart.Held')} ${qty(m.held)}`;
            return `<div class="d-flex flex-column align-items-center justify-content-end flex-fill h-100" title="${esc(title)}">`
                + `<small class="text-muted mb-1" dir="ltr">${total ? esc(qty(total)) : ''}</small>`
                + `<div class="w-100 d-flex flex-column justify-content-end" style="height:${Math.round((total / max) * 160)}px;max-width:36px">`
                + `<div class="bg-info rounded-top" style="height:${total ? (Number(m.held) / total) * 100 : 0}%"></div>`
                + `<div class="bg-primary ${Number(m.held) ? '' : 'rounded-top'}" style="height:${total ? (Number(m.available) / total) * 100 : 0}%"></div></div>`
                + `<small class="text-muted mt-1 text-nowrap">${esc(label)}</small></div>`;
        }).join('');

        // top risk
        ui.byId('ex-toprisk-sub').textContent = policy ? t('Expiry.TopRisk.Subtitle', policy.approachingDays) : '';
        const rows = panel.topRisk || [];
        ui.byId('ex-toprisk').querySelector('tbody').innerHTML = rows.length
            ? rows.map((r) => `<tr><td>${ui.productCell(r)}</td><td>${ui.lotLink(r)}</td><td>${esc(r.warehouseLabel || '')}</td>`
                + `<td class="text-end" dir="ltr">${esc(qty(r.daysToExpiry))}</td><td class="text-end">${ui.amount(Number(r.onHand?.available) + Number(r.onHand?.held), r.onHand)}</td>`
                + `<td>${ui.statusCell(r)}</td><td>${ui.rowActions(r, '/Quarantine')}</td></tr>`).join('')
            : `<tr><td colspan="7" class="text-muted">${esc(t('Expiry.TopRisk.Empty'))}</td></tr>`;
    };

    const load = async () => {
        const params = new URLSearchParams();
        const warehouse = ui.byId('ex-filter-warehouse')?.value;
        if (warehouse) params.set('warehouseId', warehouse);
        params.set('periodDays', ui.byId('ex-filter-period')?.value || '30');
        const reply = await ui.get(`/Expiry/data/dashboard?${params}`);
        if (!reply.ok) {
            ui.showError(ui.byId('ex-error'), reply);
            return;
        }
        ui.hideError(ui.byId('ex-error'));
        render(reply.json || {});
    };

    document.addEventListener('DOMContentLoaded', async () => {
        await ui.loadWarehouses(ui.byId('ex-filter-warehouse'));
        ui.byId('ex-filter-warehouse')?.addEventListener('change', load);
        ui.byId('ex-filter-period')?.addEventListener('change', load);
        await load();
    });
})();
