(() => {
    const STORAGE_KEY = "ig.settings.v1";
    const defaults = {
        annualCarryingRate: 24,
        riskThresholdDays: 60,
        currency: "USD"
    };

    const normalizeSettings = (input) => {
        const annualRate = Number(input?.annualCarryingRate);
        const riskDays = Number(input?.riskThresholdDays);
        const currency = typeof input?.currency === "string" && input.currency ? input.currency : defaults.currency;
        return {
            annualCarryingRate: Number.isFinite(annualRate) ? Math.max(0, annualRate) : defaults.annualCarryingRate,
            riskThresholdDays: Number.isFinite(riskDays) ? Math.max(0, riskDays) : defaults.riskThresholdDays,
            currency
        };
    };

    const loadSettings = () => {
        try {
            const saved = localStorage.getItem(STORAGE_KEY);
            if (!saved) return { ...defaults };
            return normalizeSettings(JSON.parse(saved));
        } catch {
            return { ...defaults };
        }
    };

    const saveSettings = (settings) => {
        const normalized = normalizeSettings(settings);
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(normalized));
        } catch {
            // localStorage unavailable; ignore
        }
        return normalized;
    };

    const applySettingsToDom = (settings) => {
        const normalized = normalizeSettings(settings);
        const rateLabel = document.getElementById("ig-carrying-rate-label");
        if (rateLabel) {
            const monthlyRate = normalized.annualCarryingRate / 12;
            rateLabel.textContent = `Monthly Carrying Cost (${monthlyRate.toFixed(1)}%)`;
        }

        document.querySelectorAll('[data-format="currency"]').forEach((cell) => {
            cell.setAttribute("data-currency", normalized.currency);
        });

        if (typeof window.igFormatNumberCells === "function") {
            window.igFormatNumberCells();
        }
        if (typeof window.igApplyRiskHighlight === "function") {
            window.igApplyRiskHighlight(normalized);
        }
    };

    const openModal = () => {
        const modalEl = document.getElementById("ig-settings-modal");
        if (!modalEl || !window.bootstrap?.Modal) return;
        window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
    };

    window.igSettings = {
        storageKey: STORAGE_KEY,
        defaults,
        normalizeSettings,
        loadSettings,
        saveSettings,
        applySettingsToDom,
        openModal
    };
})();
