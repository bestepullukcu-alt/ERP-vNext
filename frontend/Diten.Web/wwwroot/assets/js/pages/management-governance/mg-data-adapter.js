(() => {
    const safe = (v, fallback) => (v == null ? fallback : v);

    function useManagementGovernanceSummary() {
        const cards = [...document.querySelectorAll("[data-page] .h4")].map((el) => el.textContent?.trim()).filter(Boolean);
        return Promise.resolve({ cards });
    }

    function useSubdomainSummary(slug) {
        return Promise.resolve({ slug: safe(slug, "") });
    }

    function useGovernanceWorkQueue() {
        const rows = document.querySelectorAll("#mg-queue-table tbody tr");
        return Promise.resolve({ count: rows.length });
    }

    function useGovernanceCadence() {
        const entries = document.querySelectorAll(".card .h6");
        return Promise.resolve({ count: entries.length });
    }

    function useGovernanceSearch() {
        return Promise.resolve({ connected: true });
    }

    function useRecentGovernanceActivity() {
        const items = document.querySelectorAll(".card .badge");
        return Promise.resolve({ markers: items.length });
    }

    window.ManagementGovernanceData = {
        useManagementGovernanceSummary,
        useSubdomainSummary,
        useGovernanceWorkQueue,
        useGovernanceCadence,
        useGovernanceSearch,
        useRecentGovernanceActivity
    };
})();
