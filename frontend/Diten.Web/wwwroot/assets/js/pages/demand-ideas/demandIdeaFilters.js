(() => {
    const defaultFilters = () => ({
        q: "", statuses: [], priorities: [], requestTypes: [], categories: [], businessUnits: [],
        strategicThemes: [], owners: [], sponsors: [], requestors: [], stakeholders: [], reviewers: [],
        complianceImpact: [], complexity: [], riskSensitivity: [],
        submittedFrom: "", submittedTo: "", reviewDueFrom: "", reviewDueTo: "", transferred: "",
        linkedProjectId: "", linkedInitiativeId: "", hasAttachments: "", hasComments: "", tags: [],
        demandSources: [], transferTargetTypes: [], transferDateFrom: "", transferDateTo: "",
        portfolioLinkStatus: [], criticality: [], slaOverdue: "", dueFrom: "", dueTo: "",
        hasSupportingLinks: "", hasDuplicatesFlagged: "", hasRelatedIdeas: "",
        createdBy: "", createdFrom: "", createdTo: "", updatedFrom: "", updatedTo: "",
        lastActivityFrom: "", lastActivityTo: "", recordId: "", hasReviewerAssigned: ""
    });
    const parseUrl = () => {
        const p = new URLSearchParams(window.location.search);
        const f = defaultFilters();
        f.q = p.get("q") || "";
        ["statuses", "priorities", "requestTypes", "categories", "businessUnits", "strategicThemes", "owners", "sponsors", "requestors", "stakeholders", "reviewers", "complianceImpact", "complexity", "riskSensitivity", "tags", "demandSources", "transferTargetTypes", "portfolioLinkStatus", "criticality"].forEach((k) => { f[k] = p.getAll(k); });
        f.submittedFrom = p.get("submittedFrom") || ""; f.submittedTo = p.get("submittedTo") || "";
        f.reviewDueFrom = p.get("reviewDueFrom") || ""; f.reviewDueTo = p.get("reviewDueTo") || "";
        f.transferred = p.get("transferred") || ""; f.linkedProjectId = p.get("linkedProjectId") || "";
        f.linkedInitiativeId = p.get("linkedInitiativeId") || ""; f.hasAttachments = p.get("hasAttachments") || "";
        f.hasComments = p.get("hasComments") || ""; f.transferDateFrom = p.get("transferDateFrom") || "";
        f.transferDateTo = p.get("transferDateTo") || ""; f.slaOverdue = p.get("slaOverdue") || "";
        f.dueFrom = p.get("dueFrom") || ""; f.dueTo = p.get("dueTo") || "";
        f.hasSupportingLinks = p.get("hasSupportingLinks") || ""; f.hasDuplicatesFlagged = p.get("hasDuplicatesFlagged") || "";
        f.hasRelatedIdeas = p.get("hasRelatedIdeas") || ""; f.createdBy = p.get("createdBy") || "";
        f.createdFrom = p.get("createdFrom") || ""; f.createdTo = p.get("createdTo") || "";
        f.updatedFrom = p.get("updatedFrom") || ""; f.updatedTo = p.get("updatedTo") || "";
        f.lastActivityFrom = p.get("lastActivityFrom") || ""; f.lastActivityTo = p.get("lastActivityTo") || "";
        f.recordId = p.get("recordId") || ""; f.hasReviewerAssigned = p.get("hasReviewerAssigned") || "";
        return f;
    };
    const toUrlParams = (f) => {
        const p = new URLSearchParams();
        const m = (k, a) => (a || []).forEach((v) => { if (v) p.append(k, v); });
        if (f.q) p.set("q", f.q);
        m("statuses", f.statuses); m("priorities", f.priorities); m("requestTypes", f.requestTypes);
        m("categories", f.categories); m("businessUnits", f.businessUnits); m("strategicThemes", f.strategicThemes);
        m("owners", f.owners); m("sponsors", f.sponsors); m("requestors", f.requestors); m("stakeholders", f.stakeholders); m("reviewers", f.reviewers);
        m("complianceImpact", f.complianceImpact); m("complexity", f.complexity); m("riskSensitivity", f.riskSensitivity);
        if (f.submittedFrom) p.set("submittedFrom", f.submittedFrom); if (f.submittedTo) p.set("submittedTo", f.submittedTo);
        if (f.reviewDueFrom) p.set("reviewDueFrom", f.reviewDueFrom); if (f.reviewDueTo) p.set("reviewDueTo", f.reviewDueTo);
        if (f.transferred) p.set("transferred", f.transferred); if (f.linkedProjectId) p.set("linkedProjectId", f.linkedProjectId);
        if (f.linkedInitiativeId) p.set("linkedInitiativeId", f.linkedInitiativeId); if (f.hasAttachments) p.set("hasAttachments", f.hasAttachments);
        if (f.hasComments) p.set("hasComments", f.hasComments); m("tags", f.tags); m("demandSources", f.demandSources);
        m("transferTargetTypes", f.transferTargetTypes); if (f.transferDateFrom) p.set("transferDateFrom", f.transferDateFrom);
        if (f.transferDateTo) p.set("transferDateTo", f.transferDateTo); m("portfolioLinkStatus", f.portfolioLinkStatus);
        m("criticality", f.criticality); if (f.slaOverdue) p.set("slaOverdue", f.slaOverdue); if (f.dueFrom) p.set("dueFrom", f.dueFrom);
        if (f.dueTo) p.set("dueTo", f.dueTo); if (f.hasSupportingLinks) p.set("hasSupportingLinks", f.hasSupportingLinks);
        if (f.hasDuplicatesFlagged) p.set("hasDuplicatesFlagged", f.hasDuplicatesFlagged); if (f.hasRelatedIdeas) p.set("hasRelatedIdeas", f.hasRelatedIdeas);
        if (f.createdBy) p.set("createdBy", f.createdBy); if (f.createdFrom) p.set("createdFrom", f.createdFrom);
        if (f.createdTo) p.set("createdTo", f.createdTo); if (f.updatedFrom) p.set("updatedFrom", f.updatedFrom);
        if (f.updatedTo) p.set("updatedTo", f.updatedTo); if (f.lastActivityFrom) p.set("lastActivityFrom", f.lastActivityFrom);
        if (f.lastActivityTo) p.set("lastActivityTo", f.lastActivityTo); if (f.recordId) p.set("recordId", f.recordId);
        if (f.hasReviewerAssigned) p.set("hasReviewerAssigned", f.hasReviewerAssigned);
        return p;
    };
    const pushUrl = (f) => { const qs = toUrlParams(f).toString(); window.history.replaceState({}, "", `${window.location.pathname}${qs ? `?${qs}` : ""}`); };
    const dateInRange = (d, from, to) => {
        if (!d && (from || to)) return false; if (!from && !to) return true;
        const x = new Date(d);
        if (from && x < new Date(from)) return false;
        if (to && x > new Date(to + "T23:59:59")) return false;
        return true;
    };
    const matchesRow = (row, f) => {
        if (f.recordId && !String(row.id).toLowerCase().includes(f.recordId.toLowerCase())) return false;
        if (f.q) {
            const hay = [row.id, row.recordNumber, row.title, row.classification, row.ownerName, row.sponsorName, row.requestorName, row.businessUnit, row.requestType, row.category, ...(row.tags || []), (row.stakeholders || []).join(" ")].join(" ").toLowerCase();
            if (!hay.includes(f.q.toLowerCase())) return false;
        }
        if (f.statuses.length && !f.statuses.includes(row.status)) return false;
        if (f.priorities.length && !f.priorities.includes(row.priority)) return false;
        if (f.requestTypes.length && !f.requestTypes.includes(row.requestType)) return false;
        if (f.categories.length && !f.categories.includes(row.category)) return false;
        if (f.businessUnits.length && !f.businessUnits.includes(row.businessUnit)) return false;
        if (f.strategicThemes.length && !f.strategicThemes.includes(row.strategicTheme)) return false;
        if (f.owners.length && !f.owners.includes(row.ownerName)) return false;
        if (f.sponsors.length && !f.sponsors.includes(row.sponsorName)) return false;
        if (f.requestors.length && !f.requestors.includes(row.requestorName)) return false;
        if (f.stakeholders.length) { const sh = row.stakeholders || []; if (!f.stakeholders.some((s) => sh.includes(s))) return false; }
        if (f.reviewers.length && (!row.reviewerName || !f.reviewers.includes(row.reviewerName))) return false;
        if (f.complianceImpact.length && !f.complianceImpact.includes(row.complianceImpact)) return false;
        if (f.complexity.length && !f.complexity.includes(row.estimatedComplexity)) return false;
        if (f.riskSensitivity.length && !f.riskSensitivity.includes(row.riskSensitivity)) return false;
        if (!dateInRange(row.submittedAt, f.submittedFrom, f.submittedTo)) return false;
        if (!dateInRange(row.reviewDueDate, f.reviewDueFrom, f.reviewDueTo)) return false;
        if (!dateInRange(row.dueDate, f.dueFrom, f.dueTo)) return false;
        if (f.transferred === "yes" && !row.isTransferred && row.status !== "Transferred") return false;
        if (f.transferred === "no" && (row.isTransferred || row.status === "Transferred")) return false;
        if (f.linkedProjectId && String(row.linkedProjectId || "") !== f.linkedProjectId) return false;
        if (f.linkedInitiativeId && String(row.linkedInitiativeId || "") !== f.linkedInitiativeId) return false;
        if (f.hasAttachments === "yes" && !row.hasAttachments) return false;
        if (f.hasAttachments === "no" && row.hasAttachments) return false;
        if (f.hasSupportingLinks === "yes" && !row.hasSupportingLinks) return false;
        if (f.hasSupportingLinks === "no" && row.hasSupportingLinks) return false;
        if (f.hasComments === "yes" && !row.hasComments) return false;
        if (f.hasComments === "no" && row.hasComments) return false;
        if (f.tags.length) { const rt = row.tags || []; if (!f.tags.every((t) => rt.includes(t))) return false; }
        if (f.demandSources.length && !f.demandSources.includes(row.demandSource)) return false;
        if (f.transferTargetTypes.length && !f.transferTargetTypes.includes(row.transferTargetType)) return false;
        if (!dateInRange(row.transferDate, f.transferDateFrom, f.transferDateTo)) return false;
        if (f.portfolioLinkStatus.length && !f.portfolioLinkStatus.includes(row.portfolioLinkStatus)) return false;
        if (f.criticality.length && !f.criticality.includes(row.criticality)) return false;
        if (f.hasDuplicatesFlagged === "yes" && !row.hasDuplicatesFlagged) return false;
        if (f.hasRelatedIdeas === "yes" && !row.hasRelatedIdeas) return false;
        if (f.createdBy && String(row.createdBy || "") !== f.createdBy) return false;
        if (!dateInRange(row.createdAt, f.createdFrom, f.createdTo)) return false;
        if (!dateInRange(row.updatedAt, f.updatedFrom, f.updatedTo)) return false;
        if (!dateInRange(row.lastActivityAt, f.lastActivityFrom, f.lastActivityTo)) return false;
        if (f.hasReviewerAssigned === "yes" && !row.reviewerName) return false;
        if (f.hasReviewerAssigned === "no" && row.reviewerName) return false;
        if (f.slaOverdue === "yes" && row.dueDate && new Date(row.dueDate) >= new Date()) return false;
        return true;
    };
    const countActive = (f) => {
        let n = 0; if (f.q) n++;
        n += f.statuses.length + f.priorities.length + f.requestTypes.length + f.categories.length;
        n += f.businessUnits.length + f.strategicThemes.length + f.owners.length + f.sponsors.length;
        n += f.requestors.length + f.stakeholders.length + f.reviewers.length + f.complianceImpact.length + f.complexity.length + f.riskSensitivity.length;
        if (f.submittedFrom || f.submittedTo) n++; if (f.reviewDueFrom || f.reviewDueTo) n++; if (f.transferred) n++;
        if (f.linkedProjectId || f.linkedInitiativeId) n++; if (f.hasAttachments || f.hasComments) n++;
        n += f.tags.length + f.demandSources.length + f.transferTargetTypes.length;
        if (f.transferDateFrom || f.transferDateTo) n++; n += f.portfolioLinkStatus.length + f.criticality.length;
        if (f.slaOverdue || f.dueFrom || f.dueTo) n++; if (f.hasSupportingLinks || f.hasDuplicatesFlagged || f.hasRelatedIdeas) n++;
        if (f.createdBy || f.createdFrom || f.createdTo) n++; if (f.updatedFrom || f.updatedTo) n++;
        if (f.lastActivityFrom || f.lastActivityTo) n++; if (f.recordId) n++; if (f.hasReviewerAssigned) n++;
        return n;
    };
    window.DemandIdeaFilters = { defaultFilters, parseUrl, toUrlParams, pushUrl, matchesRow, countActive };
})();
