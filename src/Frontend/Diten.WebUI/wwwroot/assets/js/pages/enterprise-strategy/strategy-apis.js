(function (window) {
  "use strict";

  const apiBase = (window.APP_CONFIG?.API_BASE_URL || "").replace(/\/$/, "");
  const base = `${apiBase}/api/v1/enterprise-strategy`;
  const deliveryBase = `${apiBase}/api/v1/delivery-execution`;
  const esbpBase = `${apiBase}/api/esbp`;
  const responseCache = new Map();

  function clearCache(prefix) {
    for (const key of responseCache.keys()) {
      if (!prefix || key.startsWith(prefix)) responseCache.delete(key);
    }
  }

  async function fetchJson(url, options) {
    const response = await fetch(url, options);
    const rawText = await response.text();
    let body = null;
    try {
      body = rawText ? JSON.parse(rawText) : null;
    } catch {
      body = null;
    }
    if (!response.ok || (body && body.success === false)) {
      const textFallback = () => {
        if (!rawText) return "";
        const stripped = String(rawText).replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
        if (!stripped) return "";
        return stripped.length > 240 ? `${stripped.slice(0, 240)}…` : stripped;
      };
      const extractMessage = (payload) => {
        if (!payload || typeof payload !== "object") return "";
        if (typeof payload.message === "string" && payload.message.trim()) return payload.message.trim();
        if (typeof payload.detail === "string" && payload.detail.trim()) return payload.detail.trim();
        if (typeof payload.title === "string" && payload.title.trim()) return payload.title.trim();
        const details =
          payload?.error?.details ||
          payload?.error?.Details ||
          payload?.Error?.details ||
          payload?.Error?.Details ||
          payload?.errors ||
          payload?.Errors;
        if (details && typeof details === "object") {
          for (const value of Object.values(details)) {
            if (Array.isArray(value) && value.length && String(value[0] || "").trim()) return String(value[0]).trim();
            if (!Array.isArray(value) && String(value || "").trim()) return String(value).trim();
          }
        }
        return "";
      };
      const err = new Error(extractMessage(body) || textFallback() || "Request failed");
      err.payload = body;
      err.rawText = rawText;
      err.status = response.status;
      throw err;
    }
    return body && Object.prototype.hasOwnProperty.call(body, "data") ? body.data : body;
  }

  async function fetchGet(url, ttlMs = 10000, options) {
    const skipCache = Boolean(options?.skipCache);
    const now = Date.now();
    if (!skipCache) {
      const cached = responseCache.get(url);
      if (cached && cached.expiresAt > now) return cached.value;
    }
    const value = await fetchJson(url);
    if (!skipCache && ttlMs > 0) {
      responseCache.set(url, { value, expiresAt: now + ttlMs });
    }
    return value;
  }

  /** Runtime strategy grids need full datasets; API defaults to pageSize=20 without this. */
  const defaultStrategyListPageSize = 5000;
  function ensurePagedListQuery(query) {
    const p = new URLSearchParams(query || "");
    if (!p.has("page")) p.set("page", "1");
    if (!p.has("pageSize")) p.set("pageSize", String(defaultStrategyListPageSize));
    return p.toString();
  }

  window.strategyGoalsApi = {
    list: (query = "") => fetchGet(`${base}/goals?${ensurePagedListQuery(query)}`),
    create: async (payload) => {
      const result = await fetchJson(`${base}/goals`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      clearCache(`${base}/goals`);
      if (payload?.saveAsTemplate) clearCache(`${base}/strategy-library`);
      return result;
    },
    get: (goalId) => fetchGet(`${base}/goals/${encodeURIComponent(goalId)}`),
    update: async (goalId, payload, expectedVersion) => {
      const result = await fetchJson(`${base}/goals/${encodeURIComponent(goalId)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${base}/goals`);
      if (payload?.saveAsTemplate) clearCache(`${base}/strategy-library`);
      return result;
    },
    status: async (goalId, status, expectedVersion) => {
      const result = await fetchJson(`${base}/goals/${encodeURIComponent(goalId)}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status, expectedVersion }),
      });
      clearCache(`${base}/goals`);
      return result;
    },
    archive: async (goalId, expectedVersion) => {
      const result = await fetchJson(`${base}/goals/${encodeURIComponent(goalId)}/archive`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion }),
      });
      clearCache(`${base}/goals`);
      return result;
    },
    restore: async (goalId, expectedVersion) => {
      const result = await fetchJson(`${base}/goals/${encodeURIComponent(goalId)}/restore`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion }),
      });
      clearCache(`${base}/goals`);
      return result;
    },
    objectives: (goalId) => fetchGet(`${base}/goals/${encodeURIComponent(goalId)}/objectives`),
    summary: (goalId) => fetchGet(`${base}/goals/${encodeURIComponent(goalId)}/summary`),
    getPlanningContext: async (goalId) => {
      const goal = await window.strategyGoalsApi.get(goalId);
      const strategyPeriodId = String(goal?.strategyPeriodId || "").trim();
      if (!strategyPeriodId) return null;
      let period = null;
      try {
        period = await window.strategyPlanningApi.getStrategyPeriod(strategyPeriodId);
      } catch {
        period = null;
      }
      return {
        goalId: String(goal?.id || goalId || "").trim(),
        strategyPeriodId,
        planningCycleId: String(period?.planningCycleId || "").trim(),
        planningCycleCode: String(period?.planningCycleCode || "").trim(),
        planningCycleName: String(period?.planningCycleName || "").trim(),
        startDate: period?.startDate || null,
        endDate: period?.endDate || null,
        companyId: String(period?.companyId || "").trim(),
        businessUnitId: String(period?.businessUnitId || "").trim() || null,
        regionId: String(period?.regionId || "").trim() || null,
        reviewCadence: String(period?.reviewCadence || "").trim() || null,
        strategyPeriodName: String(period?.name || "").trim(),
        strategyPeriodCode: String(period?.code || "").trim(),
        strategyPeriodStatus: String(period?.status || "").trim()
      };
    }
  };

  window.strategyObjectivesApi = {
    list: (query = "") => fetchGet(`${base}/objectives?${ensurePagedListQuery(query)}`),
    create: async (payload) => {
      const result = await fetchJson(`${base}/objectives`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      clearCache(`${base}/objectives`);
      return result;
    },
    get: (id) => fetchGet(`${base}/objectives/${encodeURIComponent(id)}`),
    update: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${base}/objectives/${encodeURIComponent(id)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${base}/objectives`);
      return result;
    },
    status: async (id, status, expectedVersion) => {
      const result = await fetchJson(`${base}/objectives/${encodeURIComponent(id)}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status, expectedVersion }),
      });
      clearCache(`${base}/objectives`);
      return result;
    },
    archive: async (id, expectedVersion) => {
      const result = await fetchJson(`${base}/objectives/${encodeURIComponent(id)}/archive`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion }),
      });
      clearCache(`${base}/objectives`);
      return result;
    },
    restore: async (id, expectedVersion) => {
      const result = await fetchJson(`${base}/objectives/${encodeURIComponent(id)}/restore`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion }),
      });
      clearCache(`${base}/objectives`);
      return result;
    },
    initiatives: (id) => fetchGet(`${base}/objectives/${encodeURIComponent(id)}/initiatives`),
    projects: (id) => fetchGet(`${base}/objectives/${encodeURIComponent(id)}/projects`),
    alignmentSummary: (id) => fetchGet(`${base}/objectives/${encodeURIComponent(id)}/alignment-summary`),
  };

  window.strategyConnectionsApi = {
    list: (query = "") => fetchGet(`${base}/connections${query ? `?${query}` : ""}`),
    create: async (payload) => {
      const result = await fetchJson(`${base}/connections`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      clearCache(`${base}/connections`);
      return result;
    },
    get: (id) => fetchGet(`${base}/connections/${encodeURIComponent(id)}`),
    update: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${base}/connections/${encodeURIComponent(id)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${base}/connections`);
      return result;
    },
    status: async (id, status, expectedVersion) => {
      const result = await fetchJson(`${base}/connections/${encodeURIComponent(id)}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status, expectedVersion }),
      });
      clearCache(`${base}/connections`);
      return result;
    },
    remove: async (id) => {
      const result = await fetchJson(`${base}/connections/${encodeURIComponent(id)}`, {
        method: "DELETE",
      });
      clearCache(`${base}/connections`);
      return result;
    },
    tree: () => fetchGet(`${base}/connections/tree`),
    graph: () => fetchGet(`${base}/connections/graph`),
    matrix: (mode) => fetchGet(`${base}/connections/matrix?mode=${encodeURIComponent(mode || "")}`),
    coverageGaps: () => fetchGet(`${base}/connections/coverage-gaps`),
    validateGraph: () =>
      fetchJson(`${base}/connections/validate-graph`, {
        method: "POST",
      }),
  };

  window.initiativeStrategyApi = {
    list: (query = "") => fetchGet(`${deliveryBase}/initiatives?${ensurePagedListQuery(typeof query === "object" && query !== null ? toQueryString(query) : query)}`),
    create: async (payload) => {
      const result = await fetchJson(`${deliveryBase}/initiatives`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/initiatives`);
      return result;
    },
    get: (id) => fetchGet(`${deliveryBase}/initiatives/${encodeURIComponent(id)}`),
    update: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${deliveryBase}/initiatives/${encodeURIComponent(id)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/initiatives`);
      return result;
    },
    upsertLink: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${deliveryBase}/initiatives/${encodeURIComponent(id)}/strategy-link`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/initiatives`);
      return result;
    },
    status: async (id, status, expectedVersion) => {
      const result = await fetchJson(`${deliveryBase}/initiatives/${encodeURIComponent(id)}/strategy-link/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status, expectedVersion }),
      });
      clearCache(`${deliveryBase}/initiatives`);
      return result;
    },
    unlink: async (id) => {
      const result = await fetchJson(`${deliveryBase}/initiatives/${encodeURIComponent(id)}/strategy-link`, {
        method: "DELETE",
      });
      clearCache(`${deliveryBase}/initiatives`);
      return result;
    },
    sync: async () => {
      const result = await fetchJson(`${deliveryBase}/initiatives/sync`, {
        method: "POST",
      });
      clearCache(`${deliveryBase}/initiatives`);
      return result;
    },
    projects: (id) => fetchGet(`${deliveryBase}/initiatives/${encodeURIComponent(id)}/projects`),
    traceability: (id) => fetchGet(`${deliveryBase}/initiatives/${encodeURIComponent(id)}/traceability`),
  };
  window.projectStrategyApi = {
    list: (query = "") => fetchGet(`${deliveryBase}/projects?${ensurePagedListQuery(query)}`),
    get: (id) => fetchGet(`${deliveryBase}/projects/${encodeURIComponent(id)}`),
    create: async (payload) => {
      const result = await fetchJson(`${deliveryBase}/projects`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
    update: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${deliveryBase}/projects/${encodeURIComponent(id)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
    upsertLink: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${deliveryBase}/projects/${encodeURIComponent(id)}/strategy-link`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
    status: async (id, status, expectedVersion) => {
      const result = await fetchJson(`${deliveryBase}/projects/${encodeURIComponent(id)}/strategy-link/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status, expectedVersion }),
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
    unlink: async (id) => {
      const result = await fetchJson(`${deliveryBase}/projects/${encodeURIComponent(id)}/strategy-link`, {
        method: "DELETE",
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
    sync: async () => {
      const result = await fetchJson(`${deliveryBase}/projects/sync`, {
        method: "POST",
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
    compatibleTemplates: (parentType, entityScope) =>
      fetchGet(`${deliveryBase}/projects/templates/compatible?parentType=${encodeURIComponent(parentType || "")}&entityScope=${encodeURIComponent(entityScope || "")}`),
    auditTrail: (id) => fetchGet(`${deliveryBase}/projects/${encodeURIComponent(id)}/audit-trail`),
    traceability: (id) => fetchGet(`${deliveryBase}/projects/${encodeURIComponent(id)}/traceability`),
    upstreamLineage: (id) => fetchGet(`${deliveryBase}/projects/${encodeURIComponent(id)}/upstream-lineage`),
    createStrategyLinked: async (payload) => {
      const result = await fetchJson(`${deliveryBase}/projects/strategy-linked`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      clearCache(`${deliveryBase}/projects`);
      return result;
    },
  };
  window.strategyPlanningApi = {
    listCycles: (search, status) => {
      const query = toQueryString({ search, status });
      return fetchGet(`${base}/planning/cycles${query ? `?${query}` : ""}`);
    },
    createCycle: async (payload) => {
      const result = await fetchJson(`${base}/planning/cycles`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload || {})
      });
      clearCache(`${base}/planning/cycles`);
      clearCache(`${base}/planning/strategy-periods`);
      return result;
    },
    getCycle: (cycleId) => fetchGet(`${base}/planning/cycles/${encodeURIComponent(cycleId)}`),
    updateCycle: async (cycleId, payload) => {
      const result = await fetchJson(`${base}/planning/cycles/${encodeURIComponent(cycleId)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload || {})
      });
      clearCache(`${base}/planning/cycles`);
      clearCache(`${base}/planning/strategy-periods`);
      return result;
    },
    changeCycleStatus: async (cycleId, status) => {
      const result = await fetchJson(`${base}/planning/cycles/${encodeURIComponent(cycleId)}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status })
      });
      clearCache(`${base}/planning/cycles`);
      clearCache(`${base}/planning/strategy-periods`);
      return result;
    },
    archiveCycle: (cycleId) => window.strategyPlanningApi.changeCycleStatus(cycleId, "Archived"),
    activateCycle: (cycleId) => window.strategyPlanningApi.changeCycleStatus(cycleId, "Active"),
    listStrategyPeriods: (planningCycleId, search, status) => {
      const query = toQueryString({ planningCycleId, search, status });
      return fetchGet(`${base}/planning/strategy-periods${query ? `?${query}` : ""}`);
    },
    listActiveByScope: (companyId, businessUnitId, regionId, search) => {
      const query = toQueryString({ companyId, businessUnitId, regionId, search });
      return fetchGet(`${esbpBase}/strategy-periods/active-by-scope${query ? `?${query}` : ""}`);
    },
    getStrategyPeriod: async (id) => {
        const resp = await fetch(`${planningBase}/strategy-periods/${id}`);
        if (!resp.ok) throw resp;
        return (await resp.json()).data;
    },
    getStrategyPeriodUsageSummary: async (id) => {
        const resp = await fetch(`${planningBase}/strategy-periods/${id}/usage-summary`);
        if (!resp.ok) throw resp;
        return (await resp.json()).data;
    },
    createStrategyPeriod: async (payload) => {
      const result = await fetchJson(`${base}/planning/strategy-periods`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload || {})
      });
      clearCache(`${base}/planning/strategy-periods`);
      return result;
    },
    getStrategyPeriod: (periodId) => fetchGet(`${base}/planning/strategy-periods/${encodeURIComponent(periodId)}`),
    updateStrategyPeriod: async (periodId, payload) => {
      const result = await fetchJson(`${base}/planning/strategy-periods/${encodeURIComponent(periodId)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload || {})
      });
      clearCache(`${base}/planning/strategy-periods`);
      return result;
    },
    changePeriodStatus: async (periodId, status) => {
      const result = await fetchJson(`${base}/planning/strategy-periods/${encodeURIComponent(periodId)}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status })
      });
      clearCache(`${base}/planning/strategy-periods`);
      return result;
    },
    activatePeriod: (periodId) => window.strategyPlanningApi.changePeriodStatus(periodId, "Active"),
    archivePeriod: (periodId) => window.strategyPlanningApi.changePeriodStatus(periodId, "Archived"),
    resolveDefault: (companyId, businessUnitId, regionId) => {
      const p = new URLSearchParams();
      p.set("companyId", companyId || "");
      if (businessUnitId) p.set("businessUnitId", businessUnitId);
      if (regionId) p.set("regionId", regionId);
      return fetchGet(`${base}/planning/strategy-periods/default?${p.toString()}`);
    },
    getAllPositions: () => fetchGet(`http://my-possibility.eu:5000/api/OldSystem/GetAllPosition`)
  };
  window.strategyCompaniesApi = {
    list: async () => {
      const organizationApiUrl = "https://ditenteknoloji.com:5003/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId";
      try {
        const raw = await fetchJson(organizationApiUrl);
        const rows = Array.isArray(raw) ? raw : [];
        return {
          items: rows
            .map((row) => {
              const id = String(row?.id || "").trim();
              const companyName = String(row?.companyName || "").trim();
              if (!id || !companyName) return null;
              return {
                id,
                companyId: id,
                companyName,
                companyCode: String(row?.abbrevation || "").trim(),
                countryName: String(row?.countryName || "").trim(),
                parentCompanyName: String(row?.parentCompanyName || "").trim(),
                isGroup: Boolean(row?.isGroup)
              };
            })
            .filter(Boolean)
        };
      } catch {
        const opts = window.enterpriseWorkbookOptions;
        return { items: (opts?.companies || []).map((c) => ({ ...c })) };
      }
    }
  };
  function toQueryString(input) {
    if (!input) return "";
    if (typeof input === "string") return input;
    const params = new URLSearchParams();
    Object.entries(input).forEach(([key, value]) => {
      if (value === null || value === undefined) return;
      const text = String(value).trim();
      if (!text) return;
      params.set(key, text);
    });
    return params.toString();
  }

  window.strategyKpisApi = {
    list: (query = "") => fetchGet(`${base}/kpis${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    get: (id) => fetchGet(`${base}/kpis/${encodeURIComponent(id)}`),
    usage: (id) => fetchGet(`${base}/kpis/${encodeURIComponent(id)}/usage`),
    ownership: () => fetchGet(`${base}/kpis/ownership`),
    scorecard: (query = "") => fetchGet(`${base}/kpis/scorecard${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    create: async (payload) => {
      const result = await fetchJson(`${base}/kpis`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      clearCache(`${base}/kpis`);
      return result;
    },
    update: async (id, payload, expectedVersion) => {
      const result = await fetchJson(`${base}/kpis/${encodeURIComponent(id)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": String(expectedVersion ?? 0) },
        body: JSON.stringify(payload)
      });
      clearCache(`${base}/kpis`);
      return result;
    },
    archive: async (id, expectedVersion) => {
      const result = await fetchJson(`${base}/kpis/${encodeURIComponent(id)}/archive`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expectedVersion: expectedVersion ?? 0 })
      });
      clearCache(`${base}/kpis`);
      return result;
    },
    instantiateFromLibrary: async (templateId, allowDuplicates = false) => {
      const result = await fetchJson(`${base}/kpis/instantiate-from-library`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ templateId, allowDuplicates })
      });
      clearCache(`${base}/kpis`);
      clearCache(`${base}/kpi-library`);
      return result;
    }
  };

  window.kpiLibraryApi = {
    templates: (query = "") => fetchGet(`${base}/kpi-library/templates${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    template: (id) => fetchGet(`${base}/kpi-library/templates/${encodeURIComponent(id)}`),
    cloneTemplate: (id) => fetchJson(`${base}/kpi-library/templates/${encodeURIComponent(id)}/clone`, { method: "POST" }),
    lifecycle: (id, action) => fetchJson(`${base}/kpi-library/templates/${encodeURIComponent(id)}/lifecycle`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ action })
    }),
    thresholdModels: () => fetchGet(`${base}/kpi-library/threshold-models`),
    thresholdModel: (idOrCode) => fetchGet(`${base}/kpi-library/threshold-models/${encodeURIComponent(idOrCode)}`),
    packs: (query = "") => fetchGet(`${base}/kpi-library/packs${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    pack: (id) => fetchGet(`${base}/kpi-library/packs/${encodeURIComponent(id)}`),
    packItems: (id) => fetchGet(`${base}/kpi-library/packs/${encodeURIComponent(id)}/items`),
    governanceSummary: () => fetchGet(`${base}/kpi-library/governance/summary`),
    governanceExceptions: () => fetchGet(`${base}/kpi-library/governance/exceptions`),
    governanceActions: () => fetchGet(`${base}/kpi-library/governance/actions`)
  };

  window.strategyCascadeApi = {
    builder: (query = "") => fetchGet(`${base}/cascade/builder${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    targetAllocation: (query = "") => fetchGet(`${base}/cascade/target-allocation${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    consolidation: (query = "") => fetchGet(`${base}/cascade/consolidation${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    variance: (query = "") => fetchGet(`${base}/cascade/variance${toQueryString(query) ? `?${toQueryString(query)}` : ""}`)
  };

  window.strategyReviewsApi = {
    calendar: () => fetchGet(`${base}/reviews/calendar`),
    pack: (query = "") => fetchGet(`${base}/reviews/pack${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    decisions: (query = "") => fetchGet(`${base}/reviews/decisions${toQueryString(query) ? `?${toQueryString(query)}` : ""}`),
    createDecision: (payload) => fetchJson(`${base}/reviews/decisions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload || {})
    }),
    updateDecisionStatus: (decisionId, status) => fetchJson(`${base}/reviews/decisions/${encodeURIComponent(decisionId)}/status`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status, expectedVersion: 0 })
    }),
    history: () => fetchGet(`${base}/reviews/history`)
  };

  window.strategyLibraryApi = {
    importWorkbook: async (payload) => {
      const result = await fetchJson(`${base}/strategy-library/import`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload || {})
      });
      clearCache(`${base}/strategy-library`);
      return result;
    },
    getImportBatch: (batchId) => fetchGet(`${base}/strategy-library/import/${encodeURIComponent(batchId)}`),
    approveImport: async (batchId) => {
      const result = await fetchJson(`${base}/strategy-library/import/${encodeURIComponent(batchId)}/approve`, {
        method: "POST"
      });
      clearCache(`${base}/strategy-library`);
      return result;
    },
    catalog: (query = "", options) => fetchGet(`${base}/strategy-library/catalog${toQueryString(query) ? `?${toQueryString(query)}` : ""}`, 10000, options),
    projectsLibrary: (query = "") => fetchGet(`${base}/strategy-library/projects?${ensurePagedListQuery(typeof query === "object" && query !== null ? toQueryString(query) : query)}`),
    projectLibraryMetrics: (id) => fetchGet(`${base}/strategy-library/projects/${encodeURIComponent(id)}/metrics`),
    template: (id) => fetchGet(`${base}/strategy-library/templates/${encodeURIComponent(id)}`),
    blueprint: (id) => fetchGet(`${base}/strategy-library/blueprints/${encodeURIComponent(id)}`),
    templateVersions: (id) => fetchGet(`${base}/strategy-library/templates/${encodeURIComponent(id)}/versions`),
    submitReviewTemplate: (id) => fetchJson(`${base}/strategy-library/templates/${encodeURIComponent(id)}/submit-review`, { method: "POST" }),
    approveTemplate: (id) => fetchJson(`${base}/strategy-library/templates/${encodeURIComponent(id)}/approve`, { method: "POST" }),
    publishTemplate: (id) => fetchJson(`${base}/strategy-library/templates/${encodeURIComponent(id)}/publish`, { method: "POST" }),
    retireTemplate: (id) => fetchJson(`${base}/strategy-library/templates/${encodeURIComponent(id)}/retire`, { method: "POST" }),
    publishBlueprint: (id) => fetchJson(`${base}/strategy-library/blueprints/${encodeURIComponent(id)}/publish`, { method: "POST" }),
    retireBlueprint: (id) => fetchJson(`${base}/strategy-library/blueprints/${encodeURIComponent(id)}/retire`, { method: "POST" }),
    instantiateTemplate: (id, payload) => fetchJson(`${base}/strategy-library/templates/${encodeURIComponent(id)}/instantiate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload || {})
    }),
    instantiateBlueprint: (id, payload) => fetchJson(`${base}/strategy-library/blueprints/${encodeURIComponent(id)}/instantiate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload || {})
    }),
    usageSummary: () => fetchGet(`${base}/strategy-library/usage/summary`),
    usageTemplates: () => fetchGet(`${base}/strategy-library/usage/templates`),
    usageBlueprints: () => fetchGet(`${base}/strategy-library/usage/blueprints`)
  };

  window.metricCatalogApi = { list: () => window.strategyKpisApi.list() };
  window.auditEvidenceApi = { list: () => Promise.resolve({ items: [] }) };

  /** Lookups + runtime ID preview (ES&amp;BP governed selectors). */
  window.strategyEnterpriseMetaApi = {
    lookups: () => fetchGet(`${base}/lookups`),
    /** Skip cache so preview reflects latest allocations. */
    runtimeIdPreview: () => fetchJson(`${base}/runtime-ids/preview`),
    getUsersByTenantId: () => fetchGet(`https://ditenteknoloji.com:5055/api/PvUser/User/GetUsersByTenantId`)
  };
})(window);
