(() => {
    const ready = (fn) => {
        if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", fn);
        else fn();
    };

    const createSkeleton = () => {
        const div = document.createElement("div");
        div.className = "mg-empty";
        div.textContent = "Loading governance data...";
        return div;
    };

    const groupSearchResults = (query, modules, subdomains) => {
        const q = String(query || "").trim().toLowerCase();
        if (!q) return { modules: [], subdomains: [], objects: [] };
        const moduleHits = modules.filter((m) => m.toLowerCase().includes(q));
        const subdomainHits = subdomains.filter((s) => s.toLowerCase().includes(q));
        return {
            modules: moduleHits,
            subdomains: subdomainHits,
            objects: [...moduleHits, ...subdomainHits].slice(0, 6)
        };
    };

    ready(() => {
        const globalStateHost = document.getElementById("mg-global-state");
        if (globalStateHost) {
            globalStateHost.classList.remove("d-none");
            globalStateHost.textContent = "Loading governance widgets...";
        }

        if (globalStateHost && !window.ManagementGovernanceData) {
            globalStateHost.classList.remove("d-none");
            globalStateHost.textContent = "Governance adapter not available. Rendering static shell fallback.";
        } else if (window.ManagementGovernanceData) {
            Promise.all([
                window.ManagementGovernanceData.useManagementGovernanceSummary(),
                window.ManagementGovernanceData.useGovernanceWorkQueue(),
                window.ManagementGovernanceData.useGovernanceCadence(),
                window.ManagementGovernanceData.useRecentGovernanceActivity()
            ]).then(() => {
                if (!globalStateHost) return;
                globalStateHost.classList.add("d-none");
                globalStateHost.textContent = "";
            }).catch(() => {
                if (!globalStateHost) return;
                globalStateHost.classList.remove("d-none");
                globalStateHost.textContent = "Failed to load governance widgets. Retry or use static shell.";
            });
        }

        const queueButtons = document.querySelectorAll("[data-mg-queue-mode]");
        const table = document.getElementById("mg-queue-table");
        const kanban = document.getElementById("mg-queue-kanban");
        queueButtons.forEach((btn) => {
            btn.addEventListener("click", () => {
                queueButtons.forEach((b) => b.classList.remove("active"));
                btn.classList.add("active");
                const mode = btn.getAttribute("data-mg-queue-mode");
                if (!table || !kanban) return;
                table.classList.toggle("d-none", mode !== "table");
                kanban.classList.toggle("d-none", mode !== "kanban");
            });
        });

        const input = document.getElementById("mg-search-input");
        const button = document.getElementById("mg-search-button");
        const output = document.getElementById("mg-search-results");
        if (input && button && output) {
            const moduleNames = (window.mgSearchSource?.modules && Array.isArray(window.mgSearchSource.modules))
                ? window.mgSearchSource.modules
                : [...document.querySelectorAll(".mg-module-card h3")].map((x) => x.textContent || "");
            const subdomainNames = (window.mgSearchSource?.subdomains && Array.isArray(window.mgSearchSource.subdomains))
                ? window.mgSearchSource.subdomains
                : [...document.querySelectorAll("article h3.h6")].map((x) => x.textContent || "");
            const render = () => {
                const query = input.value;
                output.innerHTML = "";
                if (!query.trim()) {
                    output.appendChild(createSkeleton());
                    output.firstChild.textContent = "Type to search subdomains, modules, and governance objects.";
                    return;
                }

                const grouped = groupSearchResults(query, moduleNames, subdomainNames);
                if (!grouped.objects.length) {
                    output.appendChild(createSkeleton());
                    output.firstChild.textContent = "No results. Try subdomain name, module owner, or action type.";
                    return;
                }

                const section = (title, values) => {
                    const wrap = document.createElement("div");
                    wrap.className = "mb-3";
                    const h = document.createElement("div");
                    h.className = "small text-uppercase text-muted mb-1";
                    h.textContent = title;
                    wrap.appendChild(h);
                    values.forEach((v) => {
                        const item = document.createElement("div");
                        item.className = "small";
                        item.textContent = v;
                        wrap.appendChild(item);
                    });
                    return wrap;
                };

                output.appendChild(section("Modules", grouped.modules));
                output.appendChild(section("Subdomains", grouped.subdomains));
            };

            button.addEventListener("click", render);
            input.addEventListener("keydown", (e) => {
                if (e.key === "Enter") render();
            });
            render();
        }

        const filterStatus = document.getElementById("mg-filter-status");
        const filterOwner = document.getElementById("mg-filter-owner");
        const filterRisk = document.getElementById("mg-filter-risk");
        const subdomainCards = [...document.querySelectorAll(".mg-card article, article.mg-card")];
        let emptyState = document.getElementById("mg-filter-empty-state");
        if (!emptyState) {
            emptyState = document.createElement("div");
            emptyState.id = "mg-filter-empty-state";
            emptyState.className = "mg-empty d-none mt-2";
            emptyState.textContent = "No items match current filters.";
            const host = document.querySelector(".container-xxl");
            if (host) host.appendChild(emptyState);
        }
        const applyFilters = () => {
            const status = filterStatus?.value?.trim() || "";
            const owner = filterOwner?.value?.trim() || "";
            const risk = filterRisk?.value?.trim() || "";
            subdomainCards.forEach((card) => {
                const text = card.textContent || "";
                const okStatus = !status || text.includes(status);
                const okOwner = !owner || text.includes(owner);
                const okRisk = !risk || text.includes(risk);
                card.classList.toggle("d-none", !(okStatus && okOwner && okRisk));
            });
            const visibleCount = subdomainCards.filter((x) => !x.classList.contains("d-none")).length;
            emptyState.classList.toggle("d-none", visibleCount > 0);
        };
        filterStatus?.addEventListener("change", applyFilters);
        filterOwner?.addEventListener("change", applyFilters);
        filterRisk?.addEventListener("change", applyFilters);
    });
})();
