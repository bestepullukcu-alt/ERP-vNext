'use strict';

(function () {
    const els = {
        error: document.getElementById('sp-error'),
        loading: document.getElementById('sp-loading'),
        empty: document.getElementById('sp-empty'),
        grid: document.getElementById('sp-grid'),
        kpiTotal: document.getElementById('kpi-total'),
        kpiActive: document.getElementById('kpi-active'),
        kpiTrial: document.getElementById('kpi-trial'),
        kpiPaid: document.getElementById('kpi-paid')
    };

    const setVisible = (el, visible) => {
        if (!el) return;
        el.classList.toggle('d-none', !visible);
    };

    const setText = (el, text) => {
        if (!el) return;
        el.textContent = text;
    };

    const showError = (message) => {
        setText(els.error, message || (window.L10n && window.L10n.ErrorOccurred) || 'Error');
        setVisible(els.error, true);
    };

    const safe = (value) => (value === null || value === undefined ? '' : String(value));

    const formatPrice = (amount, currency) => {
        if (amount === null || amount === undefined || amount === '') return '-';
        const numericAmount = Number(amount);
        const c = safe(currency).trim().toUpperCase();

        if (!Number.isNaN(numericAmount) && /^[A-Z]{3}$/.test(c)) {
            return new Intl.NumberFormat(undefined, {
                style: 'currency',
                currency: c,
                minimumFractionDigits: 0,
                maximumFractionDigits: 2
            }).format(numericAmount);
        }

        return c ? `${amount} ${c}` : `${amount}`;
    };

    const badge = (text, cls) => `<span class="badge ${cls} me-1">${text}</span>`;
    const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const renderPlanCard = (plan) => {
        const l10n = window.L10n || {};
        const isActive = !!plan.isActive;
        const headerBadges = [
            isActive ? badge(l10n.Active || 'Active', 'bg-label-success') : badge(l10n.Passive || 'Passive', 'bg-label-secondary'),
            plan.isDefault ? badge(l10n.Default || 'Default', 'bg-label-primary') : '',
            plan.isTrialPlan ? badge(l10n.Trial || 'Trial', 'bg-label-info') : ''
        ].filter(Boolean).join('');

        const features = Array.isArray(plan.includedFeatures) ? plan.includedFeatures.slice(0, 5) : [];
        const modules = Array.isArray(plan.includedModuleKeys) ? plan.includedModuleKeys.slice(0, 3).map((x) => `${l10n.Modules || 'Modules'}: ${x}`) : [];
        const quotas = plan.defaultQuotas ? Object.keys(plan.defaultQuotas).slice(0, 3).map((k) => `${k}: ${plan.defaultQuotas[k]}`) : [];
        const featureItems = features.concat(modules, quotas).slice(0, 7);
        const monthlyLabel = safe(l10n.Monthly || 'month').toLowerCase();
        const yearlyLabel = safe(l10n.Yearly || 'year').toLowerCase();

        const actions = `
            <a class="btn btn-icon btn-sm btn-label-secondary" href="/Platform/SubscriptionPlans/Edit/${plan.id}" title="${l10n.Edit || 'Edit'}" aria-label="${l10n.Edit || 'Edit'}">
                <i class="bx bx-edit"></i>
            </a>
            ${isActive
                ? `<button class="btn btn-icon btn-sm btn-label-secondary js-toggle" data-id="${plan.id}" data-action="deactivate" title="${l10n.Deactivate || 'Deactivate'}" aria-label="${l10n.Deactivate || 'Deactivate'}">
                       <i class="bx bx-block"></i>
                   </button>`
                : `<button class="btn btn-icon btn-sm btn-label-success js-toggle" data-id="${plan.id}" data-action="activate" title="${l10n.Activate || 'Activate'}" aria-label="${l10n.Activate || 'Activate'}">
                       <i class="bx bx-check"></i>
                   </button>`}
        `;

        return `
        <div class="col-12 col-md-6 col-xl-4">
            <div class="card h-100 border shadow-sm" data-plan-card>
                <div class="card-body d-flex flex-column p-4">
                    <div class="d-flex align-items-start justify-content-between gap-3 mb-4">
                        <div class="min-w-0">
                            <div class="d-flex flex-wrap align-items-center gap-2 mb-2">
                                <h5 class="mb-0 text-truncate">${safe(plan.name)}</h5>
                                ${headerBadges}
                            </div>
                            <small class="text-uppercase text-muted">${safe(plan.code)}</small>
                        </div>
                        <div class="d-flex align-items-center gap-1 flex-shrink-0">${actions}</div>
                    </div>
                    ${plan.description ? `<p class="text-muted mb-5">${safe(plan.description)}</p>` : `<p class="text-muted mb-5">&nbsp;</p>`}
                    <div class="mb-4">
                        <div class="d-flex align-items-baseline flex-wrap gap-1">
                            <span class="display-6 text-heading mb-0">${formatPrice(plan.priceMonthly, plan.currency)}</span>
                            <span class="text-muted">/${monthlyLabel}</span>
                        </div>
                        <div class="text-muted small">${formatPrice(plan.priceYearly, plan.currency)}/${yearlyLabel}</div>
                    </div>
                    ${plan.isTrialPlan ? `<div class="mb-4"><small class="text-muted">${l10n.TrialDurationDays || 'Trial Duration'}:</small> <span class="fw-semibold">${safe(plan.trialDurationDays || '-')}</span></div>` : ''}
                    <div class="border-top pt-4 mt-auto">
                        <small class="text-muted d-block mb-3">${l10n.Features || 'Features'}</small>
                        ${featureItems.length
                            ? `<ul class="list-unstyled mb-0">${featureItems.map((x) => `<li class="d-flex align-items-start gap-2 mb-2"><i class="bx bx-check text-success mt-1"></i><span>${safe(x)}</span></li>`).join('')}</ul>`
                            : '<div class="text-muted small">-</div>'}
                    </div>
                </div>
            </div>
        </div>`;
    };

    const bindPlanCardHover = () => {
        if (!els.grid) return;

        els.grid.querySelectorAll('[data-plan-card]').forEach((card) => {
            if (card.dataset.hoverBound === 'true') return;
            card.dataset.hoverBound = 'true';

            card.addEventListener('mouseenter', () => {
                card.classList.add('shadow-lg', 'z-1');
                card.classList.remove('shadow-sm');

                if (reduceMotion || !card.animate) return;
                if (card._spHoverAnimation) card._spHoverAnimation.cancel();
                card._spHoverAnimation = card.animate(
                    [{ transform: 'scale(1)' }, { transform: 'scale(1.02)' }],
                    { duration: 180, easing: 'ease-out', fill: 'forwards' }
                );
            });

            card.addEventListener('mouseleave', () => {
                card.classList.add('shadow-sm');
                card.classList.remove('shadow-lg', 'z-1');

                if (reduceMotion || !card.animate) return;
                if (card._spHoverAnimation) card._spHoverAnimation.cancel();
                card._spHoverAnimation = card.animate(
                    [{ transform: 'scale(1.02)' }, { transform: 'scale(1)' }],
                    { duration: 160, easing: 'ease-out', fill: 'forwards' }
                );
            });
        });
    };

    const loadSummary = async () => {
        const res = await fetch('/Platform/SubscriptionPlans/api/summary', { credentials: 'same-origin' });
        if (!res.ok) throw new Error('summary_failed');
        const json = await res.json();
        const data = json.data || json.Data || json;
        setText(els.kpiTotal, safe(data.totalPlans ?? data.TotalPlans ?? 0));
        setText(els.kpiActive, safe(data.activePlans ?? data.ActivePlans ?? 0));
        setText(els.kpiTrial, safe(data.trialPlans ?? data.TrialPlans ?? 0));
        setText(els.kpiPaid, safe(data.paidPlans ?? data.PaidPlans ?? 0));
    };

    const loadPlans = async () => {
        const res = await fetch('/Platform/SubscriptionPlans/api?page=1&pageSize=100&sort=sortOrder', { credentials: 'same-origin' });
        if (!res.ok) throw new Error('list_failed');
        const json = await res.json();
        const data = json.data || json.Data || json;
        const items = data.items || data.Items || [];
        return items;
    };

    const togglePlan = async (id, action) => {
        const url = action === 'activate'
            ? `/Platform/SubscriptionPlans/api/${id}/activate`
            : `/Platform/SubscriptionPlans/api/${id}/deactivate`;

        const res = await fetch(url, { method: 'POST', credentials: 'same-origin' });
        if (!res.ok) throw new Error('toggle_failed');
    };

    const bindActions = () => {
        document.addEventListener('click', async (e) => {
            const btn = e.target.closest('.js-toggle');
            if (!btn) return;
            e.preventDefault();

            const id = btn.getAttribute('data-id');
            const action = btn.getAttribute('data-action');
            if (!id || !action) return;

            btn.disabled = true;
            try {
                await togglePlan(id, action);
                await refresh();
            } catch {
                showError((window.L10n && window.L10n.GatewayError) || (window.L10n && window.L10n.ErrorOccurred) || 'Error');
            } finally {
                btn.disabled = false;
            }
        });
    };

    const refresh = async () => {
        setVisible(els.error, false);
        setVisible(els.loading, true);
        setVisible(els.empty, false);
        if (els.grid) els.grid.innerHTML = '';

        try {
            await loadSummary();
            const items = await loadPlans();
            if (!items.length) {
                setVisible(els.empty, true);
                return;
            }

            const html = items.map(renderPlanCard).join('');
            els.grid.innerHTML = html;
            bindPlanCardHover();
        } catch {
            showError((window.L10n && window.L10n.GatewayError) || (window.L10n && window.L10n.ErrorOccurred) || 'Error');
        } finally {
            setVisible(els.loading, false);
        }
    };

    bindActions();
    refresh();
})();
