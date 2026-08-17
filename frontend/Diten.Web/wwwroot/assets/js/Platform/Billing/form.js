'use strict';

(function () {
    const endpoint = '/Platform/Billing/api';
    const selectedPlanId = document.querySelector('[data-billing-plan-select]')?.dataset?.selectedPlanId || '';

    const loadPlans = async () => {
        const select = document.querySelector('[data-billing-plan-select]');
        if (!select) return;
        try {
            const response = await fetch(`${endpoint}/plans`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) return;
            const payload = await response.json();
            const plans = payload.data || payload.Data || [];
            plans
                .filter((plan) => (plan.status || plan.Status) === 'Active')
                .forEach((plan) => {
                    const id = plan.id || plan.Id;
                    const option = document.createElement('option');
                    option.value = id;
                    option.textContent = `${plan.name || plan.Name} v${plan.planVersion || plan.PlanVersion} - ${Number(plan.amount || plan.Amount || 0).toFixed(2)} ${plan.currency || plan.Currency}`;
                    if (String(id).toLowerCase() === String(selectedPlanId).toLowerCase()) option.selected = true;
                    select.appendChild(option);
                });
        } catch (error) {
            console.error('[Billing Form] Failed to load plans.', error);
        }
    };

    const initSelect2 = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('.select2').each(function () {
            const $this = $(this);
            if ($this.hasClass('select2-hidden-accessible')) $this.select2('destroy');
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent()
            });
        });
    };

    document.addEventListener('DOMContentLoaded', async () => {
        await loadPlans();
        initSelect2();
    });
})();
