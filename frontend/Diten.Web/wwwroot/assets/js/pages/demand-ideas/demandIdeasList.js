(() => {
    const PAGE_SIZE = 10;
    let allRows = [];
    let apiBaseResolved = "";
    let listUrlResolved = "/api/v1/demand-ideas";

    const notify = (message, kind = "success") => {
        if (window.Notiflix?.Notify) {
            if (kind === "error") Notiflix.Notify.failure(message);
            else if (kind === "warning") Notiflix.Notify.warning(message);
            else Notiflix.Notify.success(message);
            return;
        }
        if (window.Swal) {
            Swal.fire({
                title: message,
                icon: kind === "error" ? "error" : kind === "warning" ? "warning" : "success",
                timer: 2400,
                showConfirmButton: false,
                toast: true,
                position: "top-end"
            });
            return;
        }
        // eslint-disable-next-line no-alert
        alert(message);
    };

    const apiUrl = (path) => {
        const p = path.startsWith("/") ? path : `/${path}`;
        if (apiBaseResolved) return `${apiBaseResolved}/api/v1${p}`;
        return `/api/v1${p}`;
    };

    const fetchJson = async (url, options) => {
        const res = await fetch(url, options);
        const text = await res.text();
        let data = null;
        try {
            data = text ? JSON.parse(text) : null;
        } catch {
            data = text;
        }
        return { res, data };
    };

    const getApiFailure = (res, data, fallbackMessage) => {
        const firstError = data && typeof data === "object" && data.errors && typeof data.errors === "object"
            ? Object.values(data.errors).flat().find((x) => typeof x === "string" && x)
            : null;
        if (firstError) return firstError;
        if (data && typeof data === "object" && typeof data.message === "string" && data.message) return data.message;
        return fallbackMessage || `Request failed (${res.status})`;
    };

    const initials = (name) => {
        const n = String(name || "").trim();
        if (!n) return "?";
        const p = n.split(/\s+/).filter(Boolean);
        if (p.length === 1) return p[0].slice(0, 2).toUpperCase();
        return (p[0][0] + p[p.length - 1][0]).toUpperCase();
    };

    const mapApiToLegacyRow = (x) => {
        const owner = x.ownerName || x.requestor || "";
        const sponsor = x.sponsor || "";
        return {
            id: x.id,
            recordNumber: x.recordNumber,
            title: x.title || "—",
            classification: x.category || "—",
            requestType: x.requestType || "—",
            priority: x.priority || "Medium",
            status: x.status || "Draft",
            ownerName: owner || "—",
            ownerInitials: initials(owner),
            sponsorName: sponsor || "—",
            sponsorInitials: sponsor ? initials(sponsor) : "—",
            businessUnit: x.businessUnit || "—",
            tags: x.tags || [],
            submittedAt: x.createdAt,
            dueDate: x.reviewDueDate,
            reviewDueDate: x.reviewDueDate,
            relatedIdeaIds: Array.isArray(x.relatedIdeaIds) ? x.relatedIdeaIds : [],
            requestorName: x.requestor || owner,
            stakeholders: [],
            canTransfer: false
        };
    };
    const mapDtoToUpsertPayload = (x) => ({
        title: x.title || null,
        problemStatement: x.problemStatement || null,
        expectedOutcome: x.expectedOutcome || null,
        requestType: x.requestType || null,
        strategicAlignment: x.strategicAlignment || null,
        businessUnit: x.businessUnit || null,
        requestor: x.requestor || null,
        sponsor: x.sponsor || null,
        ownerName: x.ownerName || x.requestor || null,
        proposedScope: x.proposedScope || null,
        outOfScope: x.outOfScope || null,
        assumptions: x.assumptions || null,
        constraints: x.constraints || null,
        category: x.category || null,
        demandSource: x.demandSource || null,
        priority: x.priority || null,
        complianceImpact: x.complianceImpact || null,
        estimatedComplexity: x.estimatedComplexity || null,
        riskSensitivity: x.riskSensitivity || null,
        supportingLinks: Array.isArray(x.supportingLinks) ? x.supportingLinks : [],
        notes: x.notes || null,
        tags: Array.isArray(x.tags) ? x.tags : [],
        strategicThemeKeys: Array.isArray(x.strategicThemeKeys) ? x.strategicThemeKeys : [],
        relatedIdeaIds: Array.isArray(x.relatedIdeaIds) ? x.relatedIdeaIds : [],
        reviewDueDate: x.reviewDueDate || null,
        attachments: Array.isArray(x.attachments)
            ? x.attachments.map((a) => ({
                id: a.id || "",
                fileName: a.fileName || "",
                contentType: a.contentType || "",
                sizeBytes: a.sizeBytes || 0,
                storageKey: a.storageKey || ""
            }))
            : []
    });
    let filters = window.DemandIdeaFilters.parseUrl();
    let sortKey = "submitted";
    let sortDir = "desc";
    let page = 1;
    const el = {
        q: document.getElementById("demand-search-q"),
        quickStatus: document.getElementById("demand-quick-status"),
        quickPriority: document.getElementById("demand-quick-priority"),
        moreCount: document.getElementById("demand-more-filters-count"),
        chips: document.getElementById("demand-filter-chips"),
        clearAll: document.getElementById("demand-clear-filters"),
        resultText: document.getElementById("demand-result-count"),
        tbody: document.getElementById("demand-table-body"),
        pager: document.getElementById("demand-pagination"),
        applyModal: document.getElementById("demand-apply-filters"),
        modalForm: document.getElementById("demand-more-filters-form")
    };
    const activeFilters = () => {
        const f = { ...filters, q: el.q?.value ?? filters.q };
        if (el.quickStatus?.value) f.statuses = [el.quickStatus.value];
        if (el.quickPriority?.value) f.priorities = [el.quickPriority.value];
        return f;
    };
    const getFiltered = () => allRows.filter((r) => window.DemandIdeaFilters.matchesRow(r, activeFilters()));
    const readModalIntoFilters = () => {
        if (!el.modalForm) return;
        const fd = new FormData(el.modalForm);
        const gm = (n) => fd.getAll(n).filter(Boolean);
        filters.statuses = gm("statuses"); filters.priorities = gm("priorities"); filters.requestTypes = gm("requestTypes");
        filters.categories = gm("categories"); filters.businessUnits = gm("businessUnits"); filters.strategicThemes = gm("strategicThemes");
        filters.owners = gm("owners"); filters.sponsors = gm("sponsors"); filters.requestors = gm("requestors"); filters.stakeholders = gm("stakeholders"); filters.reviewers = gm("reviewers");
        filters.complianceImpact = gm("complianceImpact"); filters.complexity = gm("complexity"); filters.riskSensitivity = gm("riskSensitivity");
        filters.submittedFrom = fd.get("submittedFrom") || ""; filters.submittedTo = fd.get("submittedTo") || "";
        filters.reviewDueFrom = fd.get("reviewDueFrom") || ""; filters.reviewDueTo = fd.get("reviewDueTo") || "";
        filters.transferred = fd.get("transferred") || ""; filters.linkedProjectId = fd.get("linkedProjectId") || ""; filters.linkedInitiativeId = fd.get("linkedInitiativeId") || "";
        filters.hasAttachments = fd.get("hasAttachments") || ""; filters.hasComments = fd.get("hasComments") || ""; filters.tags = gm("tags");
        filters.demandSources = gm("demandSources"); filters.transferTargetTypes = gm("transferTargetTypes");
        filters.transferDateFrom = fd.get("transferDateFrom") || ""; filters.transferDateTo = fd.get("transferDateTo") || "";
        filters.portfolioLinkStatus = gm("portfolioLinkStatus"); filters.criticality = gm("criticality"); filters.slaOverdue = fd.get("slaOverdue") || "";
        filters.dueFrom = fd.get("dueFrom") || ""; filters.dueTo = fd.get("dueTo") || "";
        filters.hasSupportingLinks = fd.get("hasSupportingLinks") || ""; filters.hasDuplicatesFlagged = fd.get("hasDuplicatesFlagged") || ""; filters.hasRelatedIdeas = fd.get("hasRelatedIdeas") || "";
        filters.createdBy = fd.get("createdBy") || ""; filters.createdFrom = fd.get("createdFrom") || ""; filters.createdTo = fd.get("createdTo") || "";
        filters.updatedFrom = fd.get("updatedFrom") || ""; filters.updatedTo = fd.get("updatedTo") || "";
        filters.lastActivityFrom = fd.get("lastActivityFrom") || ""; filters.lastActivityTo = fd.get("lastActivityTo") || "";
        filters.recordId = fd.get("recordId") || ""; filters.hasReviewerAssigned = fd.get("hasReviewerAssigned") || "";
    };
    const writeModalFromFilters = () => {
        if (!el.modalForm) return;
        el.modalForm.reset();
        const sm = (n, v) => {
            const s = el.modalForm.querySelector(`[name="${n}"]`);
            if (!s || !v?.length || !s.multiple) return;
            Array.from(s.options).forEach((o) => { o.selected = v.includes(o.value); });
        };
        sm("statuses", filters.statuses); sm("priorities", filters.priorities); sm("requestTypes", filters.requestTypes);
        sm("categories", filters.categories); sm("businessUnits", filters.businessUnits); sm("strategicThemes", filters.strategicThemes);
        sm("owners", filters.owners); sm("sponsors", filters.sponsors); sm("requestors", filters.requestors); sm("stakeholders", filters.stakeholders); sm("reviewers", filters.reviewers);
        sm("complianceImpact", filters.complianceImpact); sm("complexity", filters.complexity); sm("riskSensitivity", filters.riskSensitivity);
        const sv = (n, v) => { const i = el.modalForm.querySelector(`[name="${n}"]`); if (i) i.value = v || ""; };
        sv("submittedFrom", filters.submittedFrom); sv("submittedTo", filters.submittedTo); sv("reviewDueFrom", filters.reviewDueFrom); sv("reviewDueTo", filters.reviewDueTo);
        sv("transferred", filters.transferred); sv("linkedProjectId", filters.linkedProjectId); sv("linkedInitiativeId", filters.linkedInitiativeId);
        sv("hasAttachments", filters.hasAttachments); sv("hasComments", filters.hasComments); sm("tags", filters.tags);
        sm("demandSources", filters.demandSources); sm("transferTargetTypes", filters.transferTargetTypes);
        sv("transferDateFrom", filters.transferDateFrom); sv("transferDateTo", filters.transferDateTo); sm("portfolioLinkStatus", filters.portfolioLinkStatus);
        sm("criticality", filters.criticality); sv("slaOverdue", filters.slaOverdue); sv("dueFrom", filters.dueFrom); sv("dueTo", filters.dueTo);
        sv("hasSupportingLinks", filters.hasSupportingLinks); sv("hasDuplicatesFlagged", filters.hasDuplicatesFlagged); sv("hasRelatedIdeas", filters.hasRelatedIdeas);
        sv("createdBy", filters.createdBy); sv("createdFrom", filters.createdFrom); sv("createdTo", filters.createdTo);
        sv("updatedFrom", filters.updatedFrom); sv("updatedTo", filters.updatedTo); sv("lastActivityFrom", filters.lastActivityFrom); sv("lastActivityTo", filters.lastActivityTo);
        sv("recordId", filters.recordId); sv("hasReviewerAssigned", filters.hasReviewerAssigned);
    };
    const syncQuick = () => {
        if (el.quickStatus) { el.quickStatus.value = filters.statuses.length === 1 ? filters.statuses[0] : ""; }
        if (el.quickPriority) { el.quickPriority.value = filters.priorities.length === 1 ? filters.priorities[0] : ""; }
    };
    const buildChips = () => {
        const f = activeFilters();
        const p = [];
        const pm = (label, arr, key) => (arr || []).forEach((v) => p.push({ label: `${label}: ${v}`, clear: () => { filters[key] = filters[key].filter((x) => x !== v); if (key === "statuses" && el.quickStatus?.value === v) el.quickStatus.value = ""; if (key === "priorities" && el.quickPriority?.value === v) el.quickPriority.value = ""; syncQuick(); } }));
        if (f.q) p.push({ label: `Search: ${f.q}`, clear: () => (filters.q = "") });
        pm("Status", f.statuses, "statuses"); pm("Priority", f.priorities, "priorities");
        pm("BU", filters.businessUnits, "businessUnits"); pm("Type", filters.requestTypes, "requestTypes"); pm("Tag", filters.tags, "tags");
        if (filters.transferred) p.push({ label: `Transferred: ${filters.transferred}`, clear: () => (filters.transferred = "") });
        return p;
    };
    const renderChips = () => {
        if (!el.chips) return;
        const parts = buildChips();
        el.chips.innerHTML = parts.map((x, i) => `<span class="badge bg-label-primary text-primary me-1 mb-1 rounded-pill px-2 py-1">${x.label}<button type="button" class="btn btn-sm btn-link text-primary p-0 lh-1 demand-chip-remove" data-i="${i}">&times;</button></span>`).join("");
        el.chips.querySelectorAll(".demand-chip-remove").forEach((b) => b.addEventListener("click", () => {
            const i = +b.getAttribute("data-i"); if (parts[i]) { parts[i].clear(); syncQuick(); applyFilters(false); }
        }));
    };
    const updateSortHeaders = () => {
        document.querySelectorAll("#demand-ideas-table thead .demand-sortable").forEach((th) => {
            const key = th.getAttribute("data-sort-key");
            const icon = th.querySelector(".demand-sort-icon");
            if (!icon) return;
            icon.className = `bx demand-sort-icon ms-1 ${key === sortKey ? (sortDir === "asc" ? "bx-up-arrow-alt" : "bx-down-arrow-alt") + " text-primary" : "bx-sort-alt-2 text-muted"}`;
        });
    };
    const renderPager = (pages, total) => {
        if (!el.pager) return;
        el.pager.innerHTML = `<span class="small text-muted">${total} total</span><div class="d-flex align-items-center gap-2">
            <button type="button" class="btn btn-sm btn-outline-secondary" ${page <= 1 ? "disabled" : ""} data-p="prev">Previous</button>
            <span class="small text-muted">Page ${page} of ${pages}</span>
            <button type="button" class="btn btn-sm btn-outline-secondary" ${page >= pages ? "disabled" : ""} data-p="next">Next</button></div>`;
        el.pager.querySelector('[data-p="prev"]')?.addEventListener("click", () => { page = Math.max(1, page - 1); renderTable(); });
        el.pager.querySelector('[data-p="next"]')?.addEventListener("click", () => { page = Math.min(pages, page + 1); renderTable(); });
    };
    const renderTable = () => {
        let rows = getFiltered();
        rows = window.DemandIdeaTable.sortRows(rows, sortKey, sortDir);
        const total = rows.length;
        const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
        if (page > pages) page = pages;
        const slice = window.DemandIdeaTable.paginate(rows, page, PAGE_SIZE);
        if (el.tbody) {
            el.tbody.innerHTML = slice.map((r) => window.DemandIdeaTable.renderRow(r)).join("");
            window.DemandIdeaRowActions.wireRowActions(el.tbody);
        }
        if (el.resultText) {
            if (!total) el.resultText.textContent = "No ideas match the current filters";
            else if (pages === 1) el.resultText.textContent = `Showing ${slice.length} of ${total} ideas`;
            else el.resultText.textContent = `Showing ${(page - 1) * PAGE_SIZE + 1}–${(page - 1) * PAGE_SIZE + slice.length} of ${total} ideas`;
        }
        renderPager(pages, total); renderChips(); if (el.moreCount) el.moreCount.textContent = String(window.DemandIdeaFilters.countActive(filters));
        updateSortHeaders();
    };
    const applyFilters = (push) => {
        filters.q = el.q?.value || "";
        if (el.quickStatus?.value) filters.statuses = [el.quickStatus.value];
        if (el.quickPriority?.value) filters.priorities = [el.quickPriority.value];
        if (push) window.DemandIdeaFilters.pushUrl(activeFilters());
        page = 1; renderTable();
    };
    document.addEventListener("DOMContentLoaded", async () => {
        if (!document.getElementById("demand-ideas-table")) return;
        const wrap = document.querySelector(".demand-ideas-list");
        const apiBase = (wrap?.dataset.apiBase || "").replace(/\/$/, "");
        apiBaseResolved = apiBase;
        listUrlResolved = apiBase ? `${apiBase}/api/v1/demand-ideas` : "/api/v1/demand-ideas";
        try {
            const res = await fetch(listUrlResolved);
            if (res.ok) {
                const raw = await res.json();
                allRows = Array.isArray(raw) ? raw.map(mapApiToLegacyRow) : [];
            }
        } catch (e) {
            console.error(e);
        }
        if (!allRows.length) allRows = window.__DEMAND_IDEAS_ROWS__ || [];
        filters = window.DemandIdeaFilters.parseUrl();
        if (el.q) el.q.value = filters.q;
        if (filters.statuses[0] && el.quickStatus) el.quickStatus.value = filters.statuses[0];
        if (filters.priorities[0] && el.quickPriority) el.quickPriority.value = filters.priorities[0];
        el.q?.addEventListener("input", () => applyFilters(true));
        el.quickStatus?.addEventListener("change", () => { filters.statuses = el.quickStatus.value ? [el.quickStatus.value] : []; applyFilters(true); });
        el.quickPriority?.addEventListener("change", () => { filters.priorities = el.quickPriority.value ? [el.quickPriority.value] : []; applyFilters(true); });
        el.clearAll?.addEventListener("click", () => {
            filters = window.DemandIdeaFilters.defaultFilters(); if (el.q) el.q.value = ""; if (el.quickStatus) el.quickStatus.value = ""; if (el.quickPriority) el.quickPriority.value = "";
            writeModalFromFilters(); window.DemandIdeaFilters.pushUrl(filters); applyFilters(false);
        });
        el.applyModal?.addEventListener("click", () => {
            readModalIntoFilters(); if (el.quickStatus?.value) filters.statuses = [el.quickStatus.value];
            if (el.quickPriority?.value) filters.priorities = [el.quickPriority.value]; filters.q = el.q?.value || "";
            window.DemandIdeaFilters.pushUrl(filters); window.bootstrap?.Modal.getInstance(document.getElementById("demand-more-filters-modal"))?.hide();
            page = 1; renderTable();
        });
        document.getElementById("demand-more-filters-btn")?.addEventListener("click", () => {
            writeModalFromFilters();
            document.querySelectorAll(".demand-fp").forEach((inp) => { if (window.flatpickr && !inp._df) inp._df = window.flatpickr(inp, { dateFormat: "Y-m-d", allowInput: true }); });
        });
        document.querySelectorAll("#demand-ideas-table thead .demand-sortable").forEach((th) => {
            th.addEventListener("click", () => {
                const k = th.getAttribute("data-sort-key");
                if (sortKey === k) sortDir = sortDir === "asc" ? "desc" : "asc"; else { sortKey = k; sortDir = "asc"; }
                document.querySelectorAll("#demand-ideas-table thead .demand-sortable").forEach((h) => h.classList.remove("text-primary"));
                th.classList.add("text-primary"); renderTable();
            });
        });
        document.querySelector("#demand-ideas-table thead .demand-sortable[data-sort-key=\"submitted\"]")?.classList.add("text-primary");
        applyFilters(false);
        document.addEventListener("demand:deleted", (e) => {
            allRows = allRows.filter((r) => r.id !== e.detail.id);
            applyFilters(true);
            notify("Removed from current list view.", "warning");
        });
        document.addEventListener("demand:transfer", () => {
            notify("Transfer to PPM is not available yet.", "warning");
        });
        document.addEventListener("demand:moreAction", async (e) => {
            const id = e?.detail?.id;
            const action = e?.detail?.action;
            if (!id || !action) return;
            if (action === "duplicate") {
                try {
                    const original = await fetchJson(apiUrl(`/demand-ideas/${encodeURIComponent(id)}`));
                    if (!original.res.ok || !original.data) {
                        notify("Could not load item for duplication.", "error");
                        return;
                    }
                    const payload = mapDtoToUpsertPayload(original.data);
                    const created = await fetchJson(apiUrl("/demand-ideas"), {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify(payload)
                    });
                    if (!created.res.ok || !created.data?.id) {
                        notify(getApiFailure(created.res, created.data, "Duplicate failed."), "error");
                        return;
                    }
                    allRows.unshift(mapApiToLegacyRow(created.data));
                    applyFilters(false);
                    notify("Duplicate created.");
                    window.location.href = `/DemandIdeas/Capture?id=${encodeURIComponent(created.data.id)}`;
                } catch {
                    notify("Duplicate failed.", "error");
                }
                return;
            }
            if (action === "changeStatus") {
                const row = allRows.find((x) => x.id === id);
                if (!row) return;
                if (row.status !== "Draft") {
                    notify("Only Draft items can be submitted.", "warning");
                    return;
                }
                try {
                    const res = await fetchJson(apiUrl(`/demand-ideas/${encodeURIComponent(id)}/submit`), {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: "{}"
                    });
                    if (!res.res.ok || !res.data) {
                        notify(getApiFailure(res.res, res.data, "Status change failed."), "error");
                        return;
                    }
                    const idx = allRows.findIndex((x) => x.id === id);
                    if (idx >= 0) allRows[idx] = mapApiToLegacyRow(res.data);
                    applyFilters(false);
                    notify("Status updated to Submitted.");
                } catch {
                    notify("Status change failed.", "error");
                }
                return;
            }
            if (action === "assignReviewer") {
                notify("Assign Reviewer is not available yet.", "warning");
                return;
            }
            if (action === "transferPpm") {
                notify("Transfer to PPM is not available yet.", "warning");
                return;
            }
            if (action === "archive") {
                allRows = allRows.filter((r) => r.id !== id);
                applyFilters(false);
                notify("Archived from current list view.", "warning");
            }
        });
        document.getElementById("demand-export-btn")?.addEventListener("click", () => {
            const blob = new Blob([JSON.stringify(allRows, null, 2)], { type: "application/json" });
            const a = document.createElement("a"); a.href = URL.createObjectURL(blob); a.download = "demand-ideas-export.json"; a.click(); URL.revokeObjectURL(a.href);
        });
    });
})();
