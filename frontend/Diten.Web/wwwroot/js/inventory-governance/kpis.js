(() => {
    const safeNumber = (value) => {
        const numeric = Number(value);
        return Number.isFinite(numeric) ? numeric : 0;
    };

    const sum = (rows, selector) => rows.reduce((acc, row) => acc + safeNumber(selector(row)), 0);

    const formatDate = (date) => {
        if (!(date instanceof Date) || Number.isNaN(date.valueOf())) return "--";
        const year = date.getFullYear();
        const month = `${date.getMonth() + 1}`.padStart(2, "0");
        const day = `${date.getDate()}`.padStart(2, "0");
        return `${year}-${month}-${day}`;
    };

    const trendClass = (value) => {
        if (typeof value !== "string") return "ig-pill--neutral";
        if (value.trim().startsWith("-")) return "ig-pill--negative";
        if (value.trim().startsWith("+")) return "ig-pill--positive";
        return "ig-pill--neutral";
    };

    const computeKpis = (rows, settings) => {
        const allRows = Array.isArray(rows) ? rows : [];
        const annualRate = safeNumber(settings?.annualCarryingRate) / 100;
        const monthlyRate = annualRate / 12;
        const totalStockValue = sum(allRows, (row) => row.stockValue);
        const totalItems = allRows.length;
        const atRiskRows = allRows.filter((row) => ["AtRisk", "Critical"].includes(row.status));
        const itemsAtRiskCount = atRiskRows.length;
        const avgDoh = totalItems ? sum(allRows, (row) => row.daysOnHand) / totalItems : 0;
        const holdingCost = totalStockValue * monthlyRate;

        const statusTotals = {
            Healthy: sum(allRows.filter((row) => row.status === "Healthy"), (row) => row.stockValue),
            Monitor: sum(allRows.filter((row) => row.status === "Monitor"), (row) => row.stockValue),
            AtRisk: sum(allRows.filter((row) => row.status === "AtRisk"), (row) => row.stockValue),
            Critical: sum(allRows.filter((row) => row.status === "Critical"), (row) => row.stockValue)
        };

        const riskThreshold = safeNumber(settings?.riskThresholdDays);
        const expiryRows = allRows.filter((row) => row.daysToExpiry > 0 && row.daysToExpiry <= riskThreshold);
        const expiryValue = sum(expiryRows, (row) => row.stockValue);
        const projectedStockouts = allRows.filter((row) => row.status === "Critical").length;
        const excessAbovePolicy = sum(allRows.filter((row) => row.daysOnHand > 90), (row) => row.stockValue);
        const totalSales = sum(allRows, (row) => row.avgSalesMonthly);
        const gmroi = totalStockValue > 0 ? totalSales / totalStockValue : 0;

        const earliestDate = allRows.reduce((min, row) => {
            if (!row.asOfDate) return min;
            if (!min || row.asOfDate < min) return row.asOfDate;
            return min;
        }, null);
        const latestDate = allRows.reduce((max, row) => {
            if (!row.asOfDate) return max;
            if (!max || row.asOfDate > max) return row.asOfDate;
            return max;
        }, null);

        const cashReleaseValue = sum(atRiskRows, (row) => row.stockValue);
        const cashReleaseTarget = Math.max(cashReleaseValue * 1.15, 200000);
        const expiryDueRows = allRows.filter((row) => row.daysToExpiry > 0 && row.daysToExpiry <= 7);
        const exposureValue = sum(expiryDueRows, (row) => row.stockValue);

        return {
            lastUpdated: latestDate,
            execSummary: {
                itemsAtRiskCount,
                avgDoh,
                dohTarget: 55,
                holdingCost,
                totalStockValue
            },
            statusMix: {
                totals: statusTotals,
                totalItems
            },
            diagnostic: {
                cashReleasedPotential: totalStockValue * 0.1,
                expiryValue,
                projectedStockouts,
                excessAbovePolicy,
                gmroi
            },
            topKpis: {
                cashReleaseValue,
                cashReleaseTarget,
                expiryLotsDue: expiryDueRows.length,
                expiryExposure: exposureValue,
                stockoutPending: projectedStockouts,
                earliestWeek: earliestDate ? `W${String(getWeekNumber(earliestDate)).padStart(2, "0")}` : "--"
            }
        };
    };

    const getWeekNumber = (date) => {
        const target = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
        const dayNumber = target.getUTCDay() || 7;
        target.setUTCDate(target.getUTCDate() + 4 - dayNumber);
        const yearStart = new Date(Date.UTC(target.getUTCFullYear(), 0, 1));
        return Math.ceil((((target - yearStart) / 86400000) + 1) / 7);
    };

    const renderKpis = (kpis) => {
        if (!kpis) return;
        const formatCurrency = window.igFormatCurrency ?? ((value, currency) => value ?? "--");
        const formatInt = window.igFormatInt ?? ((value) => value ?? "--");
        const currency = window.igState?.settings?.currency ?? "USD";

        const lastUpdatedEl = document.getElementById("ig-last-updated");
        if (lastUpdatedEl) {
            lastUpdatedEl.textContent = `Last updated: ${formatDate(kpis.lastUpdated)}`;
        }

        const execGrid = document.getElementById("ig-exec-summary-grid");
        if (execGrid) {
            const avgDoh = kpis.execSummary.avgDoh;
            const avgLabel = `${formatInt(avgDoh.toFixed ? avgDoh.toFixed(0) : avgDoh)} days`;
            execGrid.innerHTML = `
                <div class="col">
                    <div class="ig-card ig-card--accent-red h-100" data-testid="ig-card-items-at-risk">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Items at Risk</div>
                                <div class="ig-kpi-value">${formatInt(kpis.execSummary.itemsAtRiskCount)}</div>
                            </div>
                            <div class="ig-icon-chip">!</div>
                        </div>
                        <div class="ig-kpi-sub">Target: &lt;1 item</div>
                        <div class="ig-pill ig-pill--negative">+0.0% MoM</div>
                        <a class="ig-card__link" href="javascript:void(0)">Review risk list</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-amber h-100" data-testid="ig-card-avg-doh">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Avg DoH vs Target</div>
                                <div class="ig-kpi-value">${avgLabel}</div>
                            </div>
                            <div class="ig-icon-chip">%</div>
                        </div>
                        <div class="ig-kpi-sub">Target: ${kpis.execSummary.dohTarget} days</div>
                        <div class="ig-pill ig-pill--neutral">0.0% MoM</div>
                        <a class="ig-card__link" href="javascript:void(0)">View DoH drivers</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-blue h-100" data-testid="ig-card-holding-cost">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Holding Cost</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.execSummary.holdingCost, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">$</div>
                        </div>
                        <div class="ig-kpi-sub">Monthly carrying cost</div>
                        <div class="ig-pill ig-pill--positive">+0.0% MoM</div>
                        <a class="ig-card__link" href="javascript:void(0)">Open cost detail</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-gray h-100" data-testid="ig-card-total-stock">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Total Stock Value</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.execSummary.totalStockValue, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">S</div>
                        </div>
                        <div class="ig-kpi-sub">Across all exceptions</div>
                        <div class="ig-pill ig-pill--positive">+0.0% MoM</div>
                        <a class="ig-card__link" href="javascript:void(0)">View portfolio</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-gray ig-card--settings h-100" role="button" data-ig-action="open-settings" data-testid="ig-card-settings">
                        <div class="ig-card__settings-icon">S</div>
                        <div class="ig-card__settings-label">Settings</div>
                        <div class="ig-kpi-sub">Update policy inputs</div>
                    </div>
                </div>
            `;
        }

        const statusGrid = document.getElementById("ig-status-mix-grid");
        if (statusGrid) {
            const total = Object.values(kpis.statusMix.totals).reduce((acc, val) => acc + safeNumber(val), 0);
            const pct = (value) => (total ? `${((value / total) * 100).toFixed(1)}%` : "0.0%");
            statusGrid.innerHTML = `
                <div class="col">
                    <div class="ig-card ig-card--accent-green h-100" data-testid="ig-card-status-healthy">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Healthy</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.statusMix.totals.Healthy, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">V</div>
                        </div>
                        <div class="ig-kpi-sub">${pct(kpis.statusMix.totals.Healthy)} of total</div>
                        <div class="ig-pill ig-pill--positive">Stable</div>
                        <a class="ig-card__link" href="javascript:void(0)">View items</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-blue h-100" data-testid="ig-card-status-monitor">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Monitor</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.statusMix.totals.Monitor, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">O</div>
                        </div>
                        <div class="ig-kpi-sub">${pct(kpis.statusMix.totals.Monitor)} of total</div>
                        <div class="ig-pill ig-pill--neutral">Flat</div>
                        <a class="ig-card__link" href="javascript:void(0)">Open monitor list</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-amber h-100" data-testid="ig-card-status-atrisk">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">At Risk</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.statusMix.totals.AtRisk, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">!</div>
                        </div>
                        <div class="ig-kpi-sub">${pct(kpis.statusMix.totals.AtRisk)} of total</div>
                        <div class="ig-pill ig-pill--negative">Rising</div>
                        <a class="ig-card__link" href="javascript:void(0)">Review risks</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-red h-100" data-testid="ig-card-status-critical">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Critical</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.statusMix.totals.Critical, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">!</div>
                        </div>
                        <div class="ig-kpi-sub">${pct(kpis.statusMix.totals.Critical)} of total</div>
                        <div class="ig-pill ig-pill--negative">Escalate</div>
                        <a class="ig-card__link" href="javascript:void(0)">Open critical list</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-gray h-100" data-testid="ig-card-status-totalitems">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Total Items</div>
                                <div class="ig-kpi-value">${formatInt(kpis.statusMix.totalItems)}</div>
                            </div>
                            <div class="ig-icon-chip">#</div>
                        </div>
                        <div class="ig-kpi-sub">100% of total</div>
                        <div class="ig-pill ig-pill--neutral">All statuses</div>
                        <a class="ig-card__link" href="javascript:void(0)">View totals</a>
                    </div>
                </div>
            `;
        }

        const diagnosticGrid = document.getElementById("ig-diagnostic-grid");
        if (diagnosticGrid) {
            diagnosticGrid.innerHTML = `
                <div class="col">
                    <div class="ig-card ig-card--accent-blue h-100" data-testid="ig-card-diag-cash">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Cash Released Potential</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.diagnostic.cashReleasedPotential, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">$</div>
                        </div>
                        <div class="ig-kpi-sub">Potential release from risk</div>
                        <div class="ig-pill ${trendClass("-0.0%") }">-0.0% MoM</div>
                        <a class="ig-card__link" href="javascript:void(0)">View opportunities</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-amber h-100" data-testid="ig-card-diag-expiry">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Expiry Value</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.diagnostic.expiryValue, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">!</div>
                        </div>
                        <div class="ig-kpi-sub">Within risk threshold</div>
                        <div class="ig-pill ig-pill--neutral">Week over week</div>
                        <a class="ig-card__link" href="javascript:void(0)">Review expiring lots</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-red h-100" data-testid="ig-card-diag-stockouts">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Projected Stockouts (8w)</div>
                                <div class="ig-kpi-value">${formatInt(kpis.diagnostic.projectedStockouts)}</div>
                            </div>
                            <div class="ig-icon-chip">!</div>
                        </div>
                        <div class="ig-kpi-sub">Based on current demand</div>
                        <div class="ig-pill ig-pill--negative">Elevated</div>
                        <a class="ig-card__link" href="javascript:void(0)">Open mitigations</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-gray h-100" data-testid="ig-card-diag-excesspolicy">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">Excess Above Policy</div>
                                <div class="ig-kpi-value">${formatCurrency(kpis.diagnostic.excessAbovePolicy, currency)}</div>
                            </div>
                            <div class="ig-icon-chip">S</div>
                        </div>
                        <div class="ig-kpi-sub">Above policy maximums</div>
                        <div class="ig-pill ig-pill--neutral">Policy bound</div>
                        <a class="ig-card__link" href="javascript:void(0)">View excess</a>
                    </div>
                </div>
                <div class="col">
                    <div class="ig-card ig-card--accent-green h-100" data-testid="ig-card-diag-gmroi">
                        <div class="ig-card__top">
                            <div>
                                <div class="ig-kpi-label">GMROI</div>
                                <div class="ig-kpi-value">${kpis.diagnostic.gmroi.toFixed(2)}x</div>
                            </div>
                            <div class="ig-icon-chip">%</div>
                        </div>
                        <div class="ig-kpi-sub">Portfolio weighted average</div>
                        <div class="ig-pill ig-pill--positive">Improving</div>
                        <a class="ig-card__link" href="javascript:void(0)">View drivers</a>
                    </div>
                </div>
            `;
        }

        const cashCard = document.querySelector('[data-testid="ig-kpi-cash"]');
        const expiryCard = document.querySelector('[data-testid="ig-kpi-expiry"]');
        const stockoutCard = document.querySelector('[data-testid="ig-kpi-stockout"]');

        if (cashCard) {
            const primary = cashCard.querySelector("[data-ig-kpi-primary]");
            const secondary = cashCard.querySelector("[data-ig-kpi-secondary]");
            if (primary) primary.textContent = formatCurrency(kpis.topKpis.cashReleaseValue, currency);
            if (secondary) {
                secondary.textContent = `${formatCurrency(kpis.topKpis.cashReleaseValue, currency)} / ${formatCurrency(kpis.topKpis.cashReleaseTarget, currency)}`;
            }
        }
        if (expiryCard) {
            const primary = expiryCard.querySelector("[data-ig-kpi-primary]");
            const secondary = expiryCard.querySelector("[data-ig-kpi-secondary]");
            if (primary) primary.textContent = `${formatInt(kpis.topKpis.expiryLotsDue)} lots`;
            if (secondary) secondary.textContent = `${formatCurrency(kpis.topKpis.expiryExposure, currency)} exposure`;
        }
        if (stockoutCard) {
            const primary = stockoutCard.querySelector("[data-ig-kpi-primary]");
            const secondary = stockoutCard.querySelector("[data-ig-kpi-secondary]");
            if (primary) primary.textContent = `${formatInt(kpis.topKpis.stockoutPending)} items`;
            if (secondary) secondary.textContent = `Earliest ${kpis.topKpis.earliestWeek}`;
        }
    };

    window.igKpis = {
        computeKpis,
        renderKpis
    };
})();
