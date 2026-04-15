(function (window) {
  "use strict";

  const owners = [];
  const ownerReferences = [];

  const priorities = ["Critical", "High", "Medium", "Low"];
  const complexityRiskScale = ["Very High", "High", "Medium", "Low", "Critical", "Moderate"];
  const lifecycleStatus = ["Proposed", "Planned", "Approved", "In Progress", "Completed", "Cancelled"];
  const approvalStatus = ["Draft", "Pending Approval", "Approved", "Rejected", "Rework Required"];
  const goalObjectiveTypes = [
    "Growth", "Efficiency", "Risk Reduction", "Compliance",
    "Customer Experience", "Capability Building", "Innovation", "Sustainability"
  ];
  const initiativeTypes = [
    "Transformation Initiative", "Improvement Initiative", "Compliance Initiative",
    "Innovation Initiative", "Capability Initiative", "Cost Optimization Initiative"
  ];
  const strategicThemes = [
    "Digital Transformation", "Operational Excellence", "Customer Growth", "Compliance Excellence",
    "Cost Leadership", "Data-Driven Decision Making", "Talent & Capability", "Sustainability"
  ];
  const contributionTypes = ["Direct", "Supports", "Enabling", "Dependent"];
  const dependencyTypes = ["None", "Predecessor", "Successor", "Mutual", "External"];
  const directionOfPerformance = ["Increase", "Decrease", "Maintain", "Within Range"];
  const reportingFrequencies = ["Real Time", "Daily", "Weekly", "Monthly", "Quarterly", "Annually"];
  const thresholdModels = ["Green / Amber / Red", "Target Range", "Minimum Threshold", "Maximum Threshold", "Tolerance Band"];
  const reviewCadences = ["Monthly", "Quarterly", "Semiannual", "Annual"];
  const businessUnits = ["IT", "Operations", "Finance", "HR", "Legal", "Quality", "PMO"];
  const regions = ["Global", "EMEA", "APAC", "North America", "South America", "Germany", "UK", "US", "HQ"];
  const approvalGroups = ["esbp-gov-board", "esbp-investment-committee", "esbp-exec-council"];
  const approvalRouteTypes = ["IndividualApprover", "ApprovalGroup"];
  const planningCycles = ["cycle-fy2026", "cycle-fy2027", "cycle-fy2028", "cycle-fy2029", "cycle-fy2030"];
  const planningCycleTypes = [
    "Annual Plan",
    "Multi-Year Strategy",
    "Rolling Plan",
    "Quarterly Replan",
    "Transformation Horizon"
  ];
  const planningLifecycleStatuses = ["Draft", "Active", "Archived"];
  const strategyPeriodLifecycleStatuses = ["Draft", "Active", "Archived"];
  const strategyPeriodScenarioTypes = ["Base", "Optimistic", "Conservative", "Stress"];
  const riskIds = ["risk-001", "risk-002", "risk-003", "risk-004"];
  const fiscalPeriods = ["FY2026", "FY2027", "Q1", "Q2", "Q3", "Q4", "Monthly Cycle"];
  const dependencyObjectTypes = ["Objective", "Initiative", "Project", "External", "Milestone", "Other"];
  const dependencyCriticalities = ["Critical", "High", "Medium", "Low"];
  const entityScopes = [
    "Enterprise / BU / Market",
    "Enterprise / BU / Product",
    "Market / Segment / Account",
    "Plant / Function / Process",
    "Innovation Portfolio / Venture / Product",
    "Enterprise / Function",
    "Enterprise / Control Environment",
    "Enterprise / Program",
    "Enterprise / Supply Chain",
    "Enterprise / Function / Workforce",
    "Customer Journey / Channel / Region",
    "Enterprise / Portfolio / Entity"
  ];
  const unitOfMeasure = [
    "Percentage", "Currency", "Count", "Days", "Hours", "Minutes", "Ratio",
    "Index", "Score", "FTE", "Kg", "Liters", "Units", "Batches"
  ];

  const goalMetricType = ["%", "Sum", "Index", "Count", "Score", "Ratio", "%/Score"];
  const objectiveMetricType = ["%", "Sum", "Count", "Ratio", "Days", "Index", "Rank", "Score", "Hours/Days", "Count/Ratio", "Rate", "Hours", "%/Score", "%/Rate"];
  const initiativeMetricType = ["%", "Sum", "Ratio", "Count", "Days"];
  const projectMetricType = ["%", "Sum", "Score", "Count", "Days", "Ratio"];

  const aggregationTypes = ["Sum", "Average", "Weighted Average", "Minimum", "Maximum", "Latest Value"];
  const goalAggregation = aggregationTypes.slice();
  const connectionAggregation = aggregationTypes.slice();
  const objectiveTargetAggregation = aggregationTypes.slice();

  const waveValues = ["Wave 1"];
  const maturityValues = ["Emerging", "Defined", "Ready", "In Flight", "Scaled", "Stabilized"];
  const projectOwnerValues = [];
  const projectSponsorValues = [];
  const projectStageValues = ["Discovery", "Design", "Build", "Test", "Deploy", "Stabilize", "Close"];
  const projectDeliveryValues = ["Implementation"];
  const readinessValues = ["Not Started", "Ready", "In Progress", "Blocked", "At Risk", "Complete", "Planned"];
  const scopeModeValues = ["Enterprise", "SingleCompany", "MultiCompany"];
  const currencyCodes = ["USD", "EUR", "GBP", "AED", "SAR", "JPY", "INR", "CNY"];
  const budgetTypeValues = ["CapEx", "OpEx", "Mixed"];
  const budgetBasisValues = ["Top-down", "Bottom-up", "Hybrid"];
  const projectNumberingScheme = "PROJ-YYYY-NNNN";
  const companies = [];
  const organizationApiUrl = "https://ditenteknoloji.com:5003/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId";
  const userApiUrl = "https://ditenteknoloji.com:5055/api/PvUser/User/GetUsersByTenantId";
  const positionApiUrl = "http://my-possibility.eu:5000/api/OldSystem/GetAllPosition";
  const positions = [];
  const positionLoadMeta = { status: "idle", error: "" };
  let usersCache = [];
  let usersPromise = null;
  let organizationsPromise = null;
  let positionsPromise = null;
  let lookupsPromise = null;
  let hydrationPromise = null;

  function uniq(values) {
    return [...new Set((values || []).filter(Boolean).map((x) => String(x).trim()))];
  }

  function toOptionRow(entry) {
    if (entry && typeof entry === "object" && !Array.isArray(entry)) {
      const value = String(entry.value ?? entry.id ?? "").trim();
      const label = String(entry.label ?? entry.text ?? entry.fullName ?? entry.displayName ?? value).trim();
      if (!value && !label) return null;
      return { value, label, meta: entry };
    }
    const text = String(entry ?? "").trim();
    if (!text) return null;
    return { value: text, label: text, meta: null };
  }

  function uniqueOptions(values) {
    const seen = new Set();
    return (values || [])
      .map(toOptionRow)
      .filter(Boolean)
      .filter((item) => {
        const key = `${item.value}::${item.label}`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      });
  }

  function fillSelect(selectEl, values, options) {
    if (!selectEl) return;
    const opts = options || {};
    const placeholder = opts.placeholder ?? "";
    const keepCurrent = opts.keepCurrent !== false;
    const current = keepCurrent ? String(selectEl.value || "") : "";
    const list = uniqueOptions(values);
    const rows = [];
    if (placeholder !== null) rows.push(`<option value="">${placeholder}</option>`);
    list.forEach((item) => rows.push(`<option value="${escapeHtml(item.value)}">${escapeHtml(item.label)}</option>`));
    selectEl.innerHTML = rows.join("");
    if (current && list.some((item) => item.value === current)) selectEl.value = current;
    else if (opts.defaultValue && list.some((item) => item.value === opts.defaultValue)) selectEl.value = opts.defaultValue;
  }

  function fillDatalist(listEl, values) {
    if (!listEl) return;
    listEl.innerHTML = uniqueOptions(values).map((item) => {
      const value = escapeHtml(item.value || item.label);
      const label = escapeHtml(item.label || item.value);
      return `<option value="${value}" label="${label}"></option>`;
    }).join("");
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function normalizeUserRows(rows) {
    const seen = new Set();
    return (Array.isArray(rows) ? rows : [])
      .map((row) => {
        const id = String(row?.id || "").trim();
        const fullName = String(row?.fullName || "").trim();
        if (!id || !fullName) return null;
        return {
          id,
          value: id,
          label: fullName,
          fullName,
          userName: String(row?.userName || "").trim(),
          email: String(row?.email || "").trim(),
          phone: String(row?.phone || "").trim(),
          image: String(row?.image || row?.imageUrl || "").trim(),
          isActive: Boolean(row?.isActive),
          activeStr: String(row?.activeStr || "").trim(),
          userRoles: Array.isArray(row?.userRoles) ? row.userRoles : [],
          companyName: String(row?.companyName || "").trim()
        };
      })
      .filter(Boolean)
      .filter((user) => {
        if (seen.has(user.id)) return false;
        seen.add(user.id);
        return true;
      });
  }

  function mergeUsers(primaryRows, secondaryRows) {
    const merged = new Map();
    [...(Array.isArray(secondaryRows) ? secondaryRows : []), ...(Array.isArray(primaryRows) ? primaryRows : [])]
      .forEach((user) => {
        const id = String(user?.id || "").trim();
        if (!id) return;
        const current = merged.get(id) || {};
        merged.set(id, { ...current, ...user, id, value: id, label: String(user?.fullName || current.fullName || "").trim() });
      });
    return normalizeUserRows([...merged.values()]);
  }

  function normalizeOwnerReferenceRows(rows) {
    return normalizeUserRows((Array.isArray(rows) ? rows : []).map((row) => ({
      id: row?.ownerId,
      fullName: row?.displayName,
      userName: row?.userName,
      email: row?.email,
      phone: row?.phone,
      image: row?.image,
      isActive: row?.isActive,
      activeStr: row?.activeStr,
      userRoles: row?.userRoles,
      companyName: row?.companyName
    })));
  }

  function syncWorkbookUsers(users) {
    const normalizedUsers = Array.isArray(users) ? users.slice() : [];
    const ownerNames = normalizedUsers.map((user) => user.fullName).filter(Boolean);
    if (window.enterpriseWorkbookOptions) {
      window.enterpriseWorkbookOptions.users = normalizedUsers;
      window.enterpriseWorkbookOptions.owners = ownerNames;
      window.enterpriseWorkbookOptions.ownerReferences = normalizedUsers.map((user) => ({
        ownerId: user.id,
        displayName: user.fullName,
        userName: user.userName,
        email: user.email,
        phone: user.phone,
        image: user.image,
        isActive: user.isActive,
        activeStr: user.activeStr,
        userRoles: user.userRoles,
        companyName: user.companyName
      }));
      window.enterpriseWorkbookOptions.projectOwnerValues = ownerNames;
      window.enterpriseWorkbookOptions.projectSponsorValues = ownerNames;
    }
  }

  function normalizeLookupCompanyRows(rows) {
    return (Array.isArray(rows) ? rows : [])
      .map((row) => {
        const id = String(row?.companyId || row?.id || "").trim();
        const companyName = String(row?.companyName || "").trim();
        if (!id || !companyName) return null;
        return {
          id,
          companyId: id,
          companyName,
          companyCode: String(row?.companyCode || row?.abbrevation || "").trim(),
          status: String(row?.status || "").trim(),
          region: String(row?.region || row?.countryName || "").trim(),
          businessUnit: String(row?.businessUnit || "").trim(),
          parentCompanyName: String(row?.parentCompanyName || "").trim(),
          isGroup: Boolean(row?.isGroup)
        };
      })
      .filter(Boolean);
  }

  function syncWorkbookCompanies(rows) {
    const normalized = Array.isArray(rows) ? rows.slice() : [];
    if (window.enterpriseWorkbookOptions) {
      window.enterpriseWorkbookOptions.companies = normalized;
    }
  }

  function normalizeLookupPositionRows(rows) {
    return (Array.isArray(rows) ? rows : [])
      .map((row) => {
        if (!row) return null;
        if (typeof row === "string") {
          const val = row.trim();
          return val ? { positionId: val, positionName: val } : null;
        }
        const id = String(row?.positionId ?? row?.PositionId ?? row?.id ?? row?.value ?? "").trim();
        const name = String(row?.positionName ?? row?.PositionName ?? row?.name ?? row?.label ?? "").trim();
        if (!id || !name) return null;
        return {
          positionId: id,
          positionName: name
        };
      })
      .filter(Boolean);
  }

  /**
   * @typedef {{ positionId: string, positionName: string }} PositionRecord
   */

  function syncWorkbookPositions(rows) {
    const normalized = normalizeLookupPositionRows(rows);
    positions.splice(0, positions.length, ...normalized);
    if (window.enterpriseWorkbookOptions) {
      window.enterpriseWorkbookOptions.positions = positions.slice();
    }
  }

  async function fetchLookupCatalog(forceRefresh = false) {
    if (forceRefresh) lookupsPromise = null;
    if (!lookupsPromise) {
      lookupsPromise = (async () => {
        const apiBase = (window.APP_CONFIG?.API_BASE_URL || "").replace(/\/$/, "");
        const url = `${apiBase}/api/v1/enterprise-strategy/lookups`;
        try {
          const response = await fetch(url);
          const body = await response.json().catch(() => null);
          const data = body && Object.prototype.hasOwnProperty.call(body, "data") ? body.data : body;
          return data && typeof data === "object" ? data : {};
        } catch {
          return {};
        }
      })();
    }
    return await lookupsPromise;
  }

  async function fetchUsers(forceRefresh) {
    if (forceRefresh) usersPromise = null;
    if (!usersPromise) {
      usersPromise = (async () => {
        try {
          const response = await fetch(userApiUrl);
          const body = await response.json().catch(() => null);
          const data = body && Object.prototype.hasOwnProperty.call(body, "data") ? body.data : [];
          usersCache = normalizeUserRows(data);
        } catch {
          usersCache = [];
        }
        syncWorkbookUsers(usersCache);
        return usersCache.slice();
      })();
    }
    const users = await usersPromise;
    return Array.isArray(users) ? users.slice() : [];
  }

  function userOptions() {
    return usersCache.map((user) => ({
      value: user.id,
      label: user.fullName,
      userName: user.userName,
      email: user.email,
      phone: user.phone,
      image: user.image,
      isActive: user.isActive,
      activeStr: user.activeStr,
      userRoles: user.userRoles,
      companyName: user.companyName
    }));
  }

  function findUser(value) {
    const raw = String(value || "").trim();
    if (!raw) return null;
    const lowered = raw.toLowerCase();
    return usersCache.find((user) =>
      user.id.toLowerCase() === lowered ||
      user.fullName.toLowerCase() === lowered ||
      (user.userName && user.userName.toLowerCase() === lowered) ||
      (user.email && user.email.toLowerCase() === lowered)) || null;
  }

  function userId(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    return findUser(raw)?.id || raw;
  }

  function userDisplayName(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    return findUser(raw)?.fullName || raw;
  }

  function normalizeOwnershipMatchValue(value) {
    return String(value || "")
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, " ");
  }

  function isActiveUser(user) {
    if (!user) return false;
    if (typeof user.isActive === "boolean") return user.isActive;
    const status = normalizeOwnershipMatchValue(user.activeStr || "");
    if (!status) return true;
    return !["inactive", "passive", "disabled", "terminated", "false", "0"].includes(status);
  }

  function rolePairsForUser(user) {
    const roles = Array.isArray(user?.userRoles) ? user.userRoles : [];
    const seen = new Set();
    return roles
      .map((role) => {
        const apiPosition = findPositionRecord(
          role?.positionId ??
          role?.PositionId ??
          role?.id ??
          role?.value ??
          role?.positionName ??
          role?.PositionName ??
          role?.name ??
          role?.label ??
          role ??
          ""
        );
        if (!apiPosition) return null;
        const value = normalizePositionId(apiPosition.positionId);
        const label = String(apiPosition.positionName || "").trim();
        const key = `${value}::${label}`;
        if (seen.has(key)) return null;
        seen.add(key);
        return { value, label };
      })
      .filter(Boolean);
  }

  function companyMatchesUser(user, companyIdOrLabel) {
    const raw = normalizeCompanyId(companyIdOrLabel);
    if (!raw) return true;
    const company = findCompanyById(raw);
    const candidates = uniq([
      raw,
      company?.companyId,
      company?.companyCode,
      company?.companyName,
      companyDisplayName(raw)
    ].map((item) => String(item || "").trim()).filter(Boolean));
    const userCompany = normalizeOwnershipMatchValue(user?.companyName || "");
    if (!userCompany) return false;
    return candidates.some((candidate) => {
      const normalized = normalizeOwnershipMatchValue(candidate);
      return normalized && (userCompany === normalized || userCompany.includes(normalized) || normalized.includes(userCompany));
    });
  }

  function positionMatchesUser(user, positionIdOrLabel) {
    const raw = normalizePositionId(positionIdOrLabel);
    if (!raw) return true;
    const selectedPosition = findPositionById(raw);
    const candidates = uniq([
      raw,
      selectedPosition?.positionId,
      selectedPosition?.positionName,
      positionDisplayName(raw)
    ].map((item) => String(item || "").trim()).filter(Boolean));
    const roles = rolePairsForUser(user);
    return roles.some((role) =>
      candidates.some((candidate) => {
        const expected = normalizeOwnershipMatchValue(candidate);
        if (!expected) return false;
        return expected === normalizeOwnershipMatchValue(role.value) || expected === normalizeOwnershipMatchValue(role.label);
      }));
  }

  function usersForOwnershipContext(companyIdOrLabel, positionIdOrLabel, options) {
    const settings = options || {};
    const activeOnly = settings.activeOnly !== false;
    const users = userOptions()
      .map((user) => findUser(user.value) || user)
      .filter(Boolean)
      .filter((user) => !activeOnly || isActiveUser(user))
      .filter((user) => companyMatchesUser(user, companyIdOrLabel))
      .filter((user) => positionMatchesUser(user, positionIdOrLabel))
      .sort((left, right) => String(left.fullName || left.label || "").localeCompare(String(right.fullName || right.label || "")));
    return users;
  }

  function positionOptionsForCompany(companyIdOrLabel) {
    const seen = new Set();
    return usersForOwnershipContext(companyIdOrLabel, "", { activeOnly: false })
      .flatMap((user) => rolePairsForUser(user))
      .filter((role) => {
        const key = `${role.value}::${role.label}`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      })
      .sort((left, right) => left.label.localeCompare(right.label));
  }

  function resolveActiveIncumbent(companyIdOrLabel, positionIdOrLabel) {
    return usersForOwnershipContext(companyIdOrLabel, positionIdOrLabel, { activeOnly: true })[0] || null;
  }

  function positionIncumbent(positionIdOrLabel, companyIdOrLabel) {
    const incumbent = resolveActiveIncumbent(companyIdOrLabel || "", positionIdOrLabel);
    if (!incumbent) return null;
    return {
      ...incumbent,
      incumbentPersonId: String(incumbent.id || incumbent.value || "").trim()
    };
  }

  function userReferenceList() {
    return usersCache.map((user) => ({
      ownerId: user.id,
      displayName: user.fullName,
      userName: user.userName,
      email: user.email,
      phone: user.phone,
      image: user.image,
      isActive: user.isActive,
      activeStr: user.activeStr,
      userRoles: user.userRoles,
      companyName: user.companyName
    }));
  }

  function fillUserSelect(selectEl, options) {
    fillSelect(selectEl, userOptions(), options);
  }

  function buildEntitySelectors(goals, objectives, initiatives, projects) {
    const goalOptions = (goals || []).map((g) => ({
      id: g.id || "",
      name: g.name || "",
      label: `${g.id || ""} — ${g.name || ""}`
    }));
    const objectiveOptions = (objectives || []).map((o) => ({
      id: o.id || "",
      name: o.name || "",
      parentGoalId: o.parentGoalId || "",
      label: `${o.id || ""} — ${o.name || ""}`
    }));
    const initiativeOptions = (initiatives || []).map((i) => ({
      id: i.initiativeId || "",
      name: i.initiativeName || "",
      parentObjectiveId: i.parentObjectiveId || "",
      parentGoalId: i.parentGoalId || "",
      label: `${i.initiativeId || ""} — ${i.initiativeName || ""}`
    }));
    const projectOptions = (projects || []).map((p) => ({
      id: p.projectId || "",
      name: p.projectName || "",
      parentInitiativeId: p.parentInitiativeId || "",
      parentObjectiveId: p.parentObjectiveId || "",
      parentGoalId: p.parentGoalId || "",
      label: `${p.projectId || ""} — ${p.projectName || ""}`
    }));
    return { goalOptions, objectiveOptions, initiativeOptions, projectOptions };
  }

  function companyLabel(company) {
    if (!company) return "";
    const name = String(company.companyName || "").trim();
    return name;
  }

  function normalizeCompanyId(value) {
    return String(value || "").trim();
  }

  function findCompanyById(value) {
    const id = normalizeCompanyId(value);
    if (!id) return null;
    return (window.enterpriseWorkbookOptions?.companies || companies).find((company) =>
      normalizeCompanyId(company?.id || company?.companyId).toLowerCase() === id.toLowerCase()) || null;
  }

  function companyDisplayName(value) {
    const company = findCompanyById(value);
    if (company) return companyLabel(company) || normalizeCompanyId(value);
    return normalizeCompanyId(value);
  }

  function companyOptions() {
    return (window.enterpriseWorkbookOptions?.companies || companies)
      .map((company) => {
        const id = normalizeCompanyId(company?.id || company?.companyId);
        const label = companyLabel(company);
        if (!id || !label) return null;
        return { value: id, label };
      })
      .filter(Boolean);
  }

  function normalizePositionId(value) {
    return String(value ?? "").trim();
  }

  function findPositionRecord(value) {
    const raw = normalizePositionId(value);
    if (!raw) return null;
    const byId = positions.find((row) => normalizePositionId(row?.positionId).toLowerCase() === raw.toLowerCase());
    if (byId) return byId;
    return positions.find((row) => String(row?.positionName || "").trim().toLowerCase() === raw.toLowerCase()) || null;
  }

  function positionOptions() {
    return positions
      .map((row) => {
        const value = normalizePositionId(row?.positionId);
        const label = String(row?.positionName || "").trim();
        if (!value || !label) return null;
        return { value, label };
      })
      .filter(Boolean);
  }

  function findPositionById(value) {
    const id = normalizePositionId(value);
    if (!id) return null;
    return findPositionRecord(id);
  }

  function positionDisplayName(value) {
    if (!value) return "";
    if (typeof value === "object") {
      const objectId = normalizePositionId(value.positionId ?? value.PositionId ?? value.id ?? value.value ?? value.roleId ?? "");
      const named = String(value.positionName || value.PositionName || value.name || value.roleName || value.title || "").trim();
      const hitFromObject = findPositionRecord(objectId || named);
      return hitFromObject ? String(hitFromObject.positionName || "").trim() : "";
    }
    const raw = normalizePositionId(value);
    if (!raw) return "";
    const hit = findPositionRecord(raw);
    return hit ? String(hit.positionName || "").trim() : "";
  }

  function normalizeCompanyRows(rows) {
    return (Array.isArray(rows) ? rows : [])
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
      .filter(Boolean);
  }

  async function fetchOrganizations(forceRefresh = false) {
    if (forceRefresh) organizationsPromise = null;
    if (!organizationsPromise) {
      organizationsPromise = (async () => {
        try {
          const response = await fetch(organizationApiUrl);
          const body = await response.json().catch(() => null);
          const data = body && Object.prototype.hasOwnProperty.call(body, "data") ? body.data : [];
          const externalRows = normalizeCompanyRows(data);
          if (externalRows.length) {
            syncWorkbookCompanies(externalRows);
            return externalRows;
          }
        } catch {
        }
        const lookupData = await fetchLookupCatalog(false);
        const lookupRows = normalizeLookupCompanyRows(lookupData?.companies);
        syncWorkbookCompanies(lookupRows);
        return lookupRows;
      })();
    }
    const rows = await organizationsPromise;
    const normalized = Array.isArray(rows) ? rows.slice() : [];
    if (window.enterpriseWorkbookOptions) {
      window.enterpriseWorkbookOptions.companies = normalized;
    }
    return normalized;
  }

  async function fetchPositions(forceRefresh = false) {
    if (forceRefresh) positionsPromise = null;
    if (!positionsPromise) {
      positionsPromise = (async () => {
        positionLoadMeta.status = "loading";
        positionLoadMeta.error = "";
        try {
          if (typeof positionApiUrl !== "undefined" && positionApiUrl) {
            try {
              const response = await fetch(positionApiUrl);
              const body = await response.json().catch(() => null);
              const data = (body && Object.prototype.hasOwnProperty.call(body, "data")) ? body.data : body;
              const externalRows = normalizeLookupPositionRows(data);
              if (externalRows.length) {
                syncWorkbookPositions(externalRows);
                positionLoadMeta.status = "success";
                return externalRows;
              }
            } catch (externalError) {
              console.warn("External position API failed, falling back to local lookups:", externalError);
            }
          }

          const lookupData = await fetchLookupCatalog(false);
          const lookupRows = normalizeLookupPositionRows(lookupData?.positions);
          if (lookupRows.length) {
            syncWorkbookPositions(lookupRows);
            positionLoadMeta.status = "success";
            return lookupRows;
          }

          positionLoadMeta.status = "empty";
          syncWorkbookPositions([]);
          return [];
        } catch (error) {
          positionLoadMeta.status = "error";
          positionLoadMeta.error = (error instanceof Error) ? error.message : "Position data could not be loaded.";
          syncWorkbookPositions([]);
          return [];
        }
      })();
    }
    const rows = await positionsPromise;
    return Array.isArray(rows) ? rows : [];
  }

  function ownerNamesFromLookups(data) {
    return usersCache.map((user) => user.fullName).filter(Boolean);
  }

  async function hydrateUsersFromLookupData(data) {
    if (Array.isArray(data?.owners) && data.owners.length && window.enterpriseWorkbookOptions) {
      window.enterpriseWorkbookOptions.owners = ownerNamesFromLookups(data);
      window.enterpriseWorkbookOptions.projectOwnerValues = window.enterpriseWorkbookOptions.owners.slice();
      window.enterpriseWorkbookOptions.projectSponsorValues = window.enterpriseWorkbookOptions.owners.slice();
    }
  }

  async function hydrateCompaniesFromLookupData(data) {
    const lookupCompanies = normalizeLookupCompanyRows(data?.companies);
    if (lookupCompanies.length) {
      syncWorkbookCompanies(lookupCompanies);
      return;
    }
    try {
      const fallbackCompanies = await fetchOrganizations(false);
      syncWorkbookCompanies(fallbackCompanies);
    } catch {
      syncWorkbookCompanies([]);
    }
  }

  window.enterprisePositionsApi = {
    list: (forceRefresh = false) => fetchPositions(forceRefresh),
    getPositions: (forceRefresh = false) => fetchPositions(forceRefresh),
    options: () => positionOptions(),
    byId: (id) => findPositionById(id),
    getDisplayName: (value) => positionDisplayName(value),
    getState: () => ({ ...positionLoadMeta })
  };

  window.enterpriseUsersApi = {
    list: (forceRefresh = false) => fetchUsers(forceRefresh),
    options: () => userOptions(),
    byId: (id) => findUser(id),
    getId: (value) => userId(value),
    getDisplayName: (value) => userDisplayName(value)
  };

  window.enterpriseWorkbookOptions = {
    users: usersCache,
    owners,
    ownerReferences,
    priorities,
    complexityRiskScale,
    lifecycleStatus,
    approvalStatus,
    goalObjectiveTypes,
    initiativeTypes,
    strategicThemes,
    contributionTypes,
    dependencyTypes,
    directionOfPerformance,
    reportingFrequencies,
    thresholdModels,
    reviewCadences,
    businessUnits,
    regions,
    approvalGroups,
    approvalRouteTypes,
    planningCycles,
    planningCycleTypes,
    planningLifecycleStatuses,
    strategyPeriodLifecycleStatuses,
    strategyPeriodScenarioTypes,
    riskIds,
    fiscalPeriods,
    dependencyObjectTypes,
    dependencyCriticalities,
    entityScopes,
    unitOfMeasure,
    goalMetricType,
    objectiveMetricType,
    initiativeMetricType,
    projectMetricType,
    goalAggregation,
    connectionAggregation,
    objectiveTargetAggregation,
    waveValues,
    maturityValues,
    projectOwnerValues,
    projectSponsorValues,
    projectStageValues,
    projectDeliveryValues,
    readinessValues,
    scopeModeValues,
    currencyCodes,
    budgetTypeValues,
    budgetBasisValues,
    projectNumberingScheme,
    companies,
    positions,
    companyLabel,
    companyDisplayName,
    findCompanyById,
    companyOptions,
    findPositionById,
    positionOptions,
    positionDisplayName,
    positionLoadState: () => ({ ...positionLoadMeta }),
    getPositions: (forceRefresh = false) => fetchPositions(forceRefresh),
    fillSelect,
    fillDatalist,
    fillUserSelect,
    buildEntitySelectors,
    ensureLookupsLoaded: (forceRefresh = false) => {
      if (forceRefresh) hydrationPromise = null;
      if (!hydrationPromise) {
        hydrationPromise = window.enterpriseWorkbookOptions.hydrateLookupsFromServer();
      }
      return hydrationPromise;
    },
    ensureUsersLoaded: () => fetchUsers(false),
    refreshUsers: () => fetchUsers(true),
    ensureCompaniesLoaded: () => fetchOrganizations(false),
    refreshCompanies: () => fetchOrganizations(true),
    ensurePositionsLoaded: () => fetchPositions(false),
    refreshPositions: () => fetchPositions(true),
    userOptions,
    userId,
    userDisplayName,
    findUser,
    isActiveUser,
    companyMatchesUser,
    positionMatchesUser,
    usersForOwnershipContext,
    positionOptionsForCompany,
    positionIncumbent,
    resolveActiveIncumbent,
    async hydrateLookupsFromServer() {
      const data = await fetchLookupCatalog(true);
      try {
        if (!data || typeof data !== "object") {
          return false;
        }
        const pick = (k, fallback) => (Array.isArray(data[k]) ? data[k] : fallback);
        await hydrateUsersFromLookupData(data);
        Object.assign(window.enterpriseWorkbookOptions, {
          users: usersCache.slice(),
          owners: usersCache.map((user) => user.fullName).filter(Boolean),
          ownerReferences: userReferenceList(),
          priorities: pick("priorities", priorities),
          complexityRiskScale: pick("complexityRiskScale", complexityRiskScale),
          lifecycleStatus: pick("lifecycleStatus", lifecycleStatus),
          approvalStatus: pick("approvalStatus", approvalStatus),
          goalObjectiveTypes: pick("goalObjectiveTypes", goalObjectiveTypes),
          initiativeTypes: pick("initiativeTypes", initiativeTypes),
          strategicThemes: pick("strategicThemes", strategicThemes),
          contributionTypes: pick("contributionTypes", contributionTypes),
          dependencyTypes: pick("dependencyTypes", dependencyTypes),
          directionOfPerformance: pick("directionOfPerformance", directionOfPerformance),
          reportingFrequencies: pick("reportingFrequencies", reportingFrequencies),
          thresholdModels: pick("thresholdModels", thresholdModels),
          reviewCadences: pick("reviewCadences", reviewCadences),
          businessUnits: pick("businessUnits", businessUnits),
          regions: pick("regions", regions),
          approvalGroups: pick("approvalGroups", approvalGroups),
          approvalRouteTypes: pick("approvalRouteTypes", approvalRouteTypes),
          planningCycles: pick("planningCycles", planningCycles),
          planningCycleTypes: pick("planningCycleTypes", planningCycleTypes),
          planningLifecycleStatuses: pick("planningLifecycleStatuses", planningLifecycleStatuses),
          strategyPeriodLifecycleStatuses: pick("strategyPeriodLifecycleStatuses", strategyPeriodLifecycleStatuses),
          strategyPeriodScenarioTypes: pick("strategyPeriodScenarioTypes", strategyPeriodScenarioTypes),
          riskIds: pick("riskIds", riskIds),
          fiscalPeriods: pick("fiscalPeriods", fiscalPeriods),
          dependencyObjectTypes: pick("dependencyObjectTypes", dependencyObjectTypes),
          dependencyCriticalities: pick("dependencyCriticalities", dependencyCriticalities),
          entityScopes: pick("entityScopes", entityScopes),
          unitOfMeasure: pick("unitOfMeasure", unitOfMeasure),
          goalMetricType: pick("goalMetricType", goalMetricType),
          objectiveMetricType: pick("objectiveMetricType", objectiveMetricType),
          initiativeMetricType: pick("initiativeMetricType", initiativeMetricType),
          projectMetricType: pick("projectMetricType", projectMetricType),
          goalAggregation: pick("goalAggregation", goalAggregation),
          connectionAggregation: pick("connectionAggregation", connectionAggregation),
          objectiveTargetAggregation: pick("objectiveTargetAggregation", objectiveTargetAggregation),
          waveValues: pick("waveValues", waveValues),
          maturityValues: pick("maturityValues", maturityValues),
          projectOwnerValues: usersCache.map((user) => user.fullName).filter(Boolean),
          projectSponsorValues: usersCache.map((user) => user.fullName).filter(Boolean),
          projectStageValues: pick("projectStageValues", projectStageValues),
          projectDeliveryValues: pick("projectDeliveryValues", projectDeliveryValues),
          readinessValues: pick("readinessValues", readinessValues),
          scopeModeValues: pick("scopeModeValues", scopeModeValues),
          currencyCodes: pick("currencyCodes", currencyCodes),
          budgetTypeValues: pick("budgetTypeValues", budgetTypeValues),
          budgetBasisValues: pick("budgetBasisValues", budgetBasisValues),
          projectNumberingScheme: String(data.projectNumberingScheme || projectNumberingScheme || "").trim(),
          companies: [],
          positions: []
        });
        await hydrateCompaniesFromLookupData(data);
        await fetchPositions(false);
        return true;
      } catch {
        return false;
      }
    }
  };

  window.enterpriseWorkbookOptions.ensureLookupsLoaded?.().catch(() => { });
})(window);
