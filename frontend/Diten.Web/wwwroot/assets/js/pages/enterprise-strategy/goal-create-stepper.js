(function () {
  const wizard = document.querySelector("#wizard-goal-create-stepper");
  if (!wizard || typeof window.Stepper === "undefined") {
    return;
  }

  const form = wizard.querySelector("#wizard-goal-create-stepper-form");
  if (!form) {
    return;
  }
  const goalKpiModalEl = document.getElementById("goalKpiModal");
  const goalKpiModal = goalKpiModalEl && window.bootstrap?.Modal
    ? new window.bootstrap.Modal(goalKpiModalEl)
    : null;
  const goalSourcePickerModalEl = document.getElementById("goalSourcePickerModal");
  const goalSourcePickerModal = goalSourcePickerModalEl && window.bootstrap?.Modal
    ? new window.bootstrap.Modal(goalSourcePickerModalEl)
    : null;
  const goalKpiYearlyValueModalEl = document.getElementById("goalKpiYearlyValueModal");
  const goalKpiYearlyValueModal = goalKpiYearlyValueModalEl && window.bootstrap?.Modal
    ? new window.bootstrap.Modal(goalKpiYearlyValueModalEl)
    : null;
  let goalKpiDataTable = null;
  let goalSourcePickerDataTable = null;
  let goalKpiRowSequence = 0;
  let goalKpiRows = [];
  let goalKpiYearlyValueModalState = null;
  let activeGoalKpiEditRowId = "";
  const goalBudgetTbody = document.getElementById("goal-budget-year-rows");
  let goalSourcePickerRows = [];
  let selectedSourceTemplateId = "";
  let selectedSourceTemplateVersion = null;
  let selectedSourceMeta = null;
  let strategyPeriodsById = new Map();

  goalKpiYearlyValueModalEl?.addEventListener("hidden.bs.modal", function () {
    goalKpiYearlyValueModalState = null;
  });

  function syncKpiTableYears() {
    const years = buildTargetYears();
    const firstYear = years.length ? String(years[0]) : "-";
    const spanText = years.length ? String(years.length) + " Years" : "-";
    const tableEl = form.querySelector("#goal-kpi-table");

    if (goalKpiRows.length) {
      goalKpiRows = goalKpiRows.map(function (row) {
        return Object.assign({}, row, {
          year: firstYear,
          yearSpanLabel: spanText
        });
      });
    }

    if (goalKpiDataTable) {
      goalKpiDataTable.clear();
      goalKpiDataTable.rows.add(goalKpiRows);
      goalKpiDataTable.draw(false);
      if (goalKpiModalEl?.classList.contains("show")) {
        buildGoalKpiYearlyPlanRows();
        syncGoalKpiRuntimeFields();
      }
      syncBudgetYearRowsWithHorizonChange();
      return;
    }

    const rows = tableEl?.querySelectorAll("tbody tr") || [];

    rows.forEach(function (row) {
      const yearCell = row.children[3];
      const spanCell = row.children[4];
      if (yearCell) {
        yearCell.textContent = firstYear;
      }
      if (spanCell) {
        spanCell.textContent = spanText;
      }
    });

    if (goalKpiModalEl?.classList.contains("show")) {
      buildGoalKpiYearlyPlanRows();
      syncGoalKpiRuntimeFields();
    }
    syncBudgetYearRowsWithHorizonChange();
  }

  function fillSelect(selectEl, values, placeholder, defaultValue) {
    if (!selectEl) {
      return;
    }

    const options = Array.isArray(values) ? values : [];
    const currentValue = String(selectEl.value || "").trim();
    const rows = [];

    if (typeof placeholder === "string") {
      rows.push('<option value="">' + placeholder + "</option>");
    }

    options.forEach(function (item) {
      const value = typeof item === "object"
        ? String(item.value ?? item.id ?? "").trim()
        : String(item || "").trim();
      const label = typeof item === "object"
        ? String(item.label ?? item.text ?? value).trim()
        : value;
      const disabled = typeof item === "object" ? Boolean(item.disabled) : false;

      if (!value && !label) {
        return;
      }

      rows.push('<option value="' + value.replace(/"/g, "&quot;") + '"' + (disabled ? ' disabled="disabled"' : "") + ">" + label + "</option>");
    });

    selectEl.innerHTML = rows.join("");

    if (currentValue && options.some(function (item) {
      const value = typeof item === "object" ? String(item.value ?? item.id ?? "").trim() : String(item || "").trim();
      return value === currentValue;
    })) {
      selectEl.value = currentValue;
      return;
    }

    if (defaultValue) {
      selectEl.value = defaultValue;
    }
  }

  function hydrateIdentityOptions() {
    const workbook = window.enterpriseWorkbookOptions || {};

    fillSelect(
      form.querySelector("#goal-category"),
      workbook.goalObjectiveTypes || [],
      "Select goal type"
    );

    fillSelect(
      form.querySelector("#goal-strategic-theme"),
      workbook.strategicThemes || [],
      "Select strategic theme"
    );

    fillSelect(
      form.querySelector("#goal-priority"),
      workbook.priorities || ["Critical", "High", "Medium", "Low"],
      "Select priority",
      "Medium"
    );

    fillSelect(
      form.querySelector("#goalLifecycle"),
      ["Draft"],
      null,
      "Draft"
    );
  }

  function syncCreationModeUi() {
    const mode = String(document.getElementById("goalCreationMode")?.value || "").trim().toLowerCase();
    const browseButton = document.getElementById("goalBrowseCatalog");
    if (!browseButton) {
      return;
    }

    browseButton.disabled = mode !== "template";
    if (mode !== "template" && selectedSourceTemplateId) {
      clearSelectedTemplate();
    }
  }

  function updateTemplateSummaryCard() {
    const wrapEl = document.getElementById("goal-template-summary-card-wrap");
    const versionEl = document.getElementById("goal-template-summary-version");
    const categoryEl = document.getElementById("goal-template-summary-category");
    const titleEl = document.getElementById("goal-template-summary-title");
    const nameEl = document.getElementById("goal-template-summary-name");
    const noteEl = document.getElementById("goal-template-summary-note");
    const idEl = document.getElementById("goal-template-summary-id");

    if (!wrapEl) {
      return;
    }

    if (!selectedSourceTemplateId) {
      wrapEl.classList.add("d-none");
      return;
    }

    wrapEl.classList.remove("d-none");
    if (versionEl) {
      versionEl.textContent = "Version " + (selectedSourceMeta?.version ?? selectedSourceTemplateVersion ?? "-");
    }
    if (categoryEl) {
      categoryEl.textContent = selectedSourceMeta?.category || "-";
    }
    if (titleEl) {
      titleEl.textContent = selectedSourceMeta?.name || selectedSourceTemplateId;
    }
    if (nameEl) {
      nameEl.textContent = document.getElementById("goal-name")?.value?.trim() || "Template-selected goal draft";
    }
    if (noteEl) {
      noteEl.textContent = selectedSourceMeta?.note || "Values prefilled from the selected Goal Template; adjust before save.";
    }
    if (idEl) {
      idEl.textContent = "Template ID: " + selectedSourceTemplateId;
    }
  }

  function clearSelectedTemplate() {
    selectedSourceTemplateId = "";
    selectedSourceTemplateVersion = null;
    selectedSourceMeta = null;
    updateTemplateSummaryCard();
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function normalizeCatalogItems(data) {
    if (Array.isArray(data)) {
      return data;
    }
    if (Array.isArray(data?.items)) {
      return data.items;
    }
    if (Array.isArray(data?.data?.items)) {
      return data.data.items;
    }
    if (Array.isArray(data?.data)) {
      return data.data;
    }
    return [];
  }

  function normalizeSourcePickerRow(row) {
    const rawType = String(row?.type ?? row?.Type ?? row?.templateType ?? row?.TemplateType ?? row?.itemType ?? row?.ItemType ?? "").trim();
    return {
      id: String(row?.id ?? row?.Id ?? row?.templateCode ?? row?.TemplateCode ?? "").trim(),
      name: String(row?.name ?? row?.Name ?? row?.templateCode ?? row?.TemplateCode ?? "").trim(),
      goalType: String(row?.goalType ?? row?.GoalType ?? row?.category ?? row?.Category ?? "").trim(),
      category: String(row?.category ?? row?.Category ?? row?.goalType ?? row?.GoalType ?? "").trim(),
      statement: String(row?.statement ?? row?.Statement ?? row?.description ?? row?.Description ?? "").trim(),
      templateType: String(row?.templateType ?? row?.TemplateType ?? rawType).trim(),
      itemType: String(row?.itemType ?? row?.ItemType ?? "").trim(),
      owner: String(row?.owner ?? row?.Owner ?? "").trim(),
      entityScope: String(row?.entityScope ?? row?.EntityScope ?? "").trim(),
      status: String(row?.status ?? row?.Status ?? row?.lifecycleStatus ?? row?.LifecycleStatus ?? "").trim(),
      lifecycleStatus: String(row?.lifecycleStatus ?? row?.LifecycleStatus ?? row?.status ?? row?.Status ?? "").trim(),
      version: row?.version ?? row?.Version ?? row?.versionLabel ?? row?.VersionLabel ?? null
    };
  }

  function isGoalTemplateType(row) {
    const type = String(row?.templateType || row?.itemType || "").trim().toLowerCase();
    return type === "goal" || type === "goaltemplate" || type === "goal template";
  }

  function normalizeSourceVersion(value) {
    const n = Number(value);
    return Number.isInteger(n) ? n : null;
  }

  function updateGoalSourcePickerTypeFilter(rows) {
    const el = document.getElementById("goal-source-picker-type");
    if (!el) {
      return;
    }

    const previous = String(el.value || "");
    const values = [...new Set((rows || []).map(function (row) {
      return String(row?.goalType || row?.category || "").trim();
    }).filter(Boolean))];
    el.innerHTML = '<option value="">All types</option>' + values.map(function (value) {
      return '<option value="' + escapeHtml(value) + '">' + escapeHtml(value) + "</option>";
    }).join("");
    if (previous && values.includes(previous)) {
      el.value = previous;
    }
  }

  function updateGoalSourcePickerEntityScopeFilter(rows) {
    const el = document.getElementById("goal-source-picker-entity-scope");
    if (!el) {
      return;
    }

    const previous = String(el.value || "");
    const values = [...new Set((rows || []).map(function (row) {
      return String(row?.entityScope || "").trim();
    }).filter(Boolean))];
    el.innerHTML = '<option value="">All entity scopes</option>' + values.map(function (value) {
      return '<option value="' + escapeHtml(value) + '">' + escapeHtml(value) + "</option>";
    }).join("");
    if (previous && values.includes(previous)) {
      el.value = previous;
    }
  }

  function fixGoalSourcePickerTableLayout() {
    window.setTimeout(function () {
      const wrapper = document.querySelector("#goalSourcePickerModal .dt-container");
      if (!wrapper) {
        return;
      }

      const elementsToModify = [
        { selector: ".dt-buttons .btn", classToRemove: "btn-secondary" },
        { selector: ".dt-search .form-control", classToRemove: "form-control-sm" },
        { selector: ".dt-length .form-select", classToRemove: "form-select-sm", classToAdd: "ms-0" },
        { selector: ".dt-length", classToAdd: "mb-md-6 mb-0" },
        { selector: ".dt-search", classToAdd: "mb-md-6 mb-2" },
        {
          selector: ".dt-layout-end",
          classToRemove: "justify-content-between",
          classToAdd: "d-flex gap-md-2 justify-content-md-end justify-content-center gap-2 flex-wrap mt-0"
        },
        { selector: ".dt-layout-start", classToAdd: "mt-0" },
        { selector: ".dt-buttons", classToAdd: "d-flex gap-2 mb-md-0 mb-6" },
        { selector: ".dt-layout-table", classToRemove: "row mt-2" },
        { selector: ".dt-layout-full", classToRemove: "col-md col-12", classToAdd: "table-responsive" }
      ];

      elementsToModify.forEach(function (config) {
        wrapper.querySelectorAll(config.selector).forEach(function (element) {
          if (config.classToRemove) {
            config.classToRemove.split(" ").forEach(function (className) {
              element.classList.remove(className);
            });
          }
          if (config.classToAdd) {
            config.classToAdd.split(" ").forEach(function (className) {
              element.classList.add(className);
            });
          }
        });
      });

      const mountFilterPanel = function () {
        const host = document.getElementById("goal-source-filterCollapse");
        const filterBtn = wrapper.querySelector(".dt-filter-btn");
        if (!host || !filterBtn) {
          return;
        }

        const toolbarRow =
          filterBtn.closest(".dt-layout-row") ||
          filterBtn.closest(".row") ||
          filterBtn.closest(".dt-layout-end")?.parentElement;

        if (toolbarRow && host.previousElementSibling !== toolbarRow) {
          toolbarRow.insertAdjacentElement("afterend", host);
          host.classList.add("px-3");
        }
      };

      mountFilterPanel();

      const dtButtons = wrapper.querySelector(".dt-buttons");
      if (dtButtons) {
        const eyeBtn = dtButtons.querySelector(".dt-eye-btn");
        const filterBtn = dtButtons.querySelector(".dt-filter-btn");
        if (eyeBtn && filterBtn && !eyeBtn.parentElement.classList.contains("btn-group")) {
          const group = document.createElement("div");
          group.className = "btn-group";
          eyeBtn.parentNode.insertBefore(group, eyeBtn);
          group.appendChild(eyeBtn);
          group.appendChild(filterBtn);

          [eyeBtn, filterBtn].forEach(function (btn) {
            btn.classList.remove("ms-2", "mx-1", "mx-2", "mx-3", "mx-4", "ms-3");
            btn.style.margin = "0";
          });
        }
      }
    }, 100);
  }

  function updateGoalSourceFilterBadge() {
    const filterGroups = [
      { selector: ".goal_source_type select", label: "Type" },
      { selector: ".goal_source_entity_scope select", label: "Entity Scope" }
    ];

    let count = 0;
    const tooltipRows = [];

    filterGroups.forEach(function (group) {
      const select = document.querySelector(group.selector);
      if (select && select.value) {
        count += 1;
        const selectedText = select.options[select.selectedIndex]?.text || select.value;
        tooltipRows.push(group.label + ": " + selectedText);
      }
    });

    const btn = document.querySelector("#goalSourcePickerModal .dt-filter-btn");
    if (!btn) {
      return;
    }

    let badge = btn.querySelector(".badge");
    if (count > 0) {
      if (!badge) {
        badge = document.createElement("span");
        badge.className = "badge rounded-pill bg-primary badge-notifications";
        badge.style.position = "absolute";
        badge.style.top = "-5px";
        badge.style.right = "-5px";
        badge.style.padding = "0.2rem 0.4rem";
        badge.style.fontSize = "0.65rem";
        badge.style.border = "2px solid white";
        btn.appendChild(badge);
      }

      btn.style.position = "relative";
      badge.textContent = count;
      badge.setAttribute("data-bs-toggle", "tooltip");
      badge.setAttribute("data-bs-placement", "top");
      badge.setAttribute("data-bs-html", "true");
      badge.setAttribute("title", tooltipRows.join("<br>"));

      if (window.bootstrap?.Tooltip) {
        const existing = window.bootstrap.Tooltip.getInstance(badge);
        if (existing) {
          existing.dispose();
        }
        new window.bootstrap.Tooltip(badge);
      }
    } else if (badge) {
      if (window.bootstrap?.Tooltip) {
        window.bootstrap.Tooltip.getInstance(badge)?.dispose();
      }
      badge.remove();
    }
  }

  async function applyGoalTemplateDetailToStepper(templateId) {
    const detail = await window.strategyLibraryApi?.template?.(templateId);
    const attrs = detail?.attributes || {};
    const prefill = detail?.goalPrefill || detail?.GoalPrefill || null;
    const workbook = window.enterpriseWorkbookOptions || {};

    selectedSourceTemplateId = String(templateId || "").trim();
    selectedSourceTemplateVersion = detail?.version ?? null;
    selectedSourceMeta = {
      id: selectedSourceTemplateId,
      name: detail?.name || selectedSourceMeta?.name || selectedSourceTemplateId,
      category: String(detail?.category || detail?.Category || "").trim(),
      version: detail?.version ?? selectedSourceTemplateVersion,
      note: "Values prefilled from the selected Goal Template; adjust before save."
    };

    const goalNameEl = document.getElementById("goal-name");
    const goalStatementEl = document.getElementById("goal-statement");
    const goalCategoryEl = document.getElementById("goal-category");
    const goalThemeEl = document.getElementById("goal-strategic-theme");
    const goalEntityScopeEl = document.getElementById("goal-entity-scope");
    const goalPriorityEl = document.getElementById("goal-priority");
    const goalChangeLogEl = document.getElementById("goal-change-log-ref");
    const goalDecisionEl = document.getElementById("goal-decision-reference");
    const goalEvidenceEl = document.getElementById("goal-evidence-reference");
    const goalChangeModeEl = document.getElementById("goalCreationMode");
    const goalLifecycleEl = document.getElementById("goalLifecycle");
    const ownerCompanyEl = document.getElementById("goal-owner-company");
    const ownerRoleEl = document.getElementById("goal-owner-role");
    const ownerPersonEl = document.getElementById("goal-owner-person");
    const strategyPeriodEl = document.getElementById("goal-strategy-period");
    const scopeModeEl = document.getElementById("goal-scope-mode");
    const applicableCompaniesEl = document.getElementById("goal-applicable-companies");
    const businessUnitEl = document.getElementById("goal-business-unit");
    const regionEl = document.getElementById("goal-region");
    const versionEl = document.getElementById("goal-version");
    const planningStartEl = document.getElementById("goal-planning-start-year");
    const planningEndEl = document.getElementById("goal-planning-end-year");

    if (goalChangeModeEl) {
      goalChangeModeEl.value = "template";
      syncCreationModeUi();
    }
    if (goalNameEl) {
      goalNameEl.value = prefill?.name || detail?.name || "";
    }
    if (goalStatementEl) {
      goalStatementEl.value = prefill?.statement || attrs.Statement || attrs.statement || "";
    }
    if (goalCategoryEl && (prefill?.category || attrs.Category || attrs.category)) {
      goalCategoryEl.value = prefill?.category || attrs.Category || attrs.category;
    }
    if (goalThemeEl && (prefill?.strategicThemeId || attrs.StrategicThemeId || attrs.strategicThemeId)) {
      goalThemeEl.value = prefill?.strategicThemeId || attrs.StrategicThemeId || attrs.strategicThemeId;
    }
    if (goalEntityScopeEl) {
      goalEntityScopeEl.value = prefill?.entityScope || detail?.entityScope || "";
    }
    if (goalPriorityEl) {
      goalPriorityEl.value = prefill?.priority || detail?.priority || "";
    }
    if (goalLifecycleEl && (prefill?.status || detail?.status || detail?.lifecycleStatus)) {
      goalLifecycleEl.value = String(prefill?.status || detail?.status || detail?.lifecycleStatus || "Draft").trim();
    }
    if (goalChangeLogEl) {
      goalChangeLogEl.value = prefill?.changeLogRef || "";
    }
    if (goalDecisionEl) {
      goalDecisionEl.value = prefill?.decisionReference || "";
    }
    if (goalEvidenceEl) {
      goalEvidenceEl.value = prefill?.evidenceReference || "";
    }
    if (versionEl) {
      versionEl.value = detail?.version ?? 0;
    }

    const ownerCompanyId = String(prefill?.ownerCompanyId || detail?.ownerCompanyId || "").trim();
    const ownerRoleValue = String(prefill?.owner || prefill?.ownerRole || detail?.owner || detail?.ownerRole || "").trim();
    const ownerPersonValue = String(prefill?.ownerPersonId || detail?.ownerPersonId || "").trim();

    if (ownerCompanyEl && ownerCompanyId) {
      if (!Array.from(ownerCompanyEl.options || []).some(function (option) { return String(option.value || "").trim() === ownerCompanyId; })) {
        const option = document.createElement("option");
        option.value = ownerCompanyId;
        option.textContent = typeof workbook.companyDisplayName === "function"
          ? workbook.companyDisplayName(ownerCompanyId) || ownerCompanyId
          : ownerCompanyId;
        ownerCompanyEl.appendChild(option);
      }
      ownerCompanyEl.value = ownerCompanyId;
    }

    if (ownerRoleEl && ownerRoleValue) {
      const roleOptions = Array.from(ownerRoleEl.options || []);
      const byValue = roleOptions.find(function (option) {
        return String(option.value || "").trim().toLowerCase() === ownerRoleValue.toLowerCase();
      });
      const byText = roleOptions.find(function (option) {
        return String(option.textContent || "").trim().toLowerCase() === ownerRoleValue.toLowerCase();
      });
      if (byValue) {
        ownerRoleEl.value = byValue.value;
      } else if (byText) {
        ownerRoleEl.value = byText.value;
      }
    }

    if (ownerPersonEl && ownerPersonValue) {
      if (!Array.from(ownerPersonEl.options || []).some(function (option) { return String(option.value || "").trim() === ownerPersonValue; })) {
        const option = document.createElement("option");
        option.value = ownerPersonValue;
        option.textContent = typeof workbook.userDisplayName === "function"
          ? workbook.userDisplayName(ownerPersonValue) || ownerPersonValue
          : ownerPersonValue;
        ownerPersonEl.appendChild(option);
      }
      ownerPersonEl.value = ownerPersonValue;
    }

    const strategyPeriodId = String(prefill?.strategyPeriodId || detail?.strategyPeriodId || "").trim();
    if (strategyPeriodEl && strategyPeriodId) {
      if (!Array.from(strategyPeriodEl.options || []).some(function (option) { return String(option.value || "").trim() === strategyPeriodId; })) {
        const option = document.createElement("option");
        option.value = strategyPeriodId;
        option.textContent = strategyPeriodId;
        strategyPeriodEl.appendChild(option);
      }
      strategyPeriodEl.value = strategyPeriodId;
    }

    const scopeMode = String(prefill?.scopeMode || detail?.scopeMode || "Enterprise").trim();
    if (scopeModeEl) {
      scopeModeEl.value = scopeMode === "MultiCompany" ? "AppliesToSelectedCompanies" : scopeMode;
    }

    if (planningStartEl) {
      planningStartEl.value = String(prefill?.planningStartYear || detail?.planningHorizonStart || "").trim();
    }
    if (planningEndEl) {
      planningEndEl.value = String(prefill?.planningEndYear || detail?.planningHorizonEnd || "").trim();
    }

    const applicableCompanyIds = []
      .concat(prefill?.applicableCompanyIds || [])
      .concat(detail?.applicableCompanyIds || [])
      .filter(Boolean)
      .map(function (value) { return String(value).trim(); });
    if (applicableCompaniesEl) {
      Array.from(applicableCompaniesEl.options || []).forEach(function (option) {
        option.selected = applicableCompanyIds.includes(String(option.value || "").trim());
      });
      if (window.jQuery && window.jQuery(applicableCompaniesEl).hasClass("select2-hidden-accessible")) {
        window.jQuery(applicableCompaniesEl).trigger("change.select2");
      }
    }

    if (businessUnitEl) {
      businessUnitEl.value = String(prefill?.businessUnit || detail?.businessUnit || "").trim();
    }
    if (regionEl) {
      regionEl.value = String(prefill?.region || detail?.region || "").trim();
    }

    const metrics = (detail?.goalMetrics || detail?.GoalMetrics || []).map(function (metric) {
      return {
        goalMetricType: String(metric?.metricType || metric?.MetricType || "Primary KPI").trim() || "Primary KPI",
        metric: String(metric?.metricName || metric?.MetricName || "Primary KPI / Metric").trim() || "Primary KPI / Metric",
        unitOfMeasure: String(metric?.unitOfMeasure || metric?.UnitOfMeasure || "-").trim() || "-"
      };
    });
    if (metrics.length) {
      goalKpiRows = metrics.map(function (metric) {
        return createSourceMetricRow(metric);
      });
      if (goalKpiDataTable) {
        goalKpiDataTable.clear();
        goalKpiDataTable.rows.add(goalKpiRows);
        goalKpiDataTable.draw(false);
      }
    }

    const yearlyBudgets = (detail?.goalYearlyBudgets || detail?.GoalYearlyBudgets || []).map(function (row) {
      return {
        year: row?.year ?? row?.Year,
        revenueTarget: row?.revenueTarget ?? row?.RevenueTarget,
        ebitdaTarget: row?.ebitdaTarget ?? row?.EbitdaTarget,
        capexEnvelope: row?.capexEnvelope ?? row?.CapexEnvelope,
        opexEnvelope: row?.opexEnvelope ?? row?.OpexEnvelope,
        savingsTarget: row?.savingsTarget ?? row?.SavingsTarget,
        fundingPoolEnvelope: row?.fundingPoolEnvelope ?? row?.FundingPoolEnvelope,
        commentary: row?.commentary ?? row?.Commentary ?? ""
      };
    });
    if (document.getElementById("goal-budget-enabled")) {
      document.getElementById("goal-budget-enabled").checked = yearlyBudgets.length > 0 || Boolean(detail?.budgetEnvelopeEnabled);
    }

    renderBudgetYearRows(yearlyBudgets);
    syncBudgetEnvelopeUi();
    syncOwnershipSummary();
    syncCompanyApplicabilitySummary();
    syncCompanyApplicabilityMode();
    syncKpiTableYears();
    updateTemplateSummaryCard();
  }

  async function loadGoalSourcePickerCatalog() {
    if (!window.strategyLibraryApi?.catalog) {
      goalSourcePickerRows = [];
      return;
    }

    const data = await window.strategyLibraryApi.catalog({ page: 1, pageSize: 200, templateType: "Goal" }, { skipCache: true });
    goalSourcePickerRows = normalizeCatalogItems(data)
      .map(normalizeSourcePickerRow)
      .filter(Boolean)
      .filter(function (row) {
        return isGoalTemplateType(row);
      });

    updateGoalSourcePickerTypeFilter(goalSourcePickerRows);
    updateGoalSourcePickerEntityScopeFilter(goalSourcePickerRows);
  }

  function initGoalSourcePickerDataTable() {
    const tableEl = document.getElementById("goal-source-picker-table");
    if (!tableEl || !window.DataTable || goalSourcePickerDataTable) {
      return;
    }

    goalSourcePickerDataTable = new window.DataTable(tableEl, {
      data: goalSourcePickerRows,
      processing: true,
      serverSide: false,
      deferRender: true,
      autoWidth: false,
      columns: [
        { data: "id" },
        { data: "name" },
        { data: "statement" },
        { data: "goalType" },
        { data: "owner" },
        { data: "entityScope" },
        { data: "lifecycleStatus" },
        { data: "version" },
        { data: null, className: "text-end" }
      ],
      columnDefs: [
        {
          targets: -1,
          orderable: false,
          searchable: false,
          render: function (data, type, full) {
            return '<button type="button" class="btn btn-sm btn-outline-primary goal-pick-source" data-template-id="' + escapeHtml(full?.id || "") + '">Use</button>';
          }
        },
        {
          targets: 7,
          render: function (data) {
            return escapeHtml(data ?? "-");
          }
        }
      ],
      order: [[1, "asc"]],
      layout: {
        topStart: {
          rowClass: "row m-3 justify-content-between",
          features: [
            {
              pageLength: {
                menu: [10, 25, 50, 100],
                text: "_MENU_"
              }
            }
          ]
        },
        topEnd: {
          rowClass: "row mx-3 justify-content-between",
          features: [
            {
              search: {
                placeholder: "Search template",
                text: "_INPUT_"
              }
            },
            {
              buttons: [
                {
                  extend: "collection",
                  className: "btn btn-label-secondary dropdown-toggle",
                  text: '<i class="icon-base bx bx-export icon-sm me-2"></i>Export',
                  buttons: ["print", "csv", "excel", "pdf", "copy"]
                },
                {
                  text: '<i class="icon-base bx bx-show icon-sm"></i>',
                  className: "btn btn-icon btn-label-secondary dt-eye-btn",
                  action: function () {
                    fixGoalSourcePickerTableLayout();
                  }
                },
                {
                  text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                  className: "btn btn-icon btn-label-secondary dt-filter-btn",
                  action: function () {
                    const filterEl = document.getElementById("goal-source-filterCollapse");
                    if (filterEl && window.bootstrap?.Collapse) {
                      const bsCollapse = window.bootstrap.Collapse.getOrCreateInstance(filterEl);
                      bsCollapse.toggle();
                      this.node().classList.toggle("active");
                    }
                  }
                }
              ]
            }
          ]
        },
        bottomStart: {
          rowClass: "row mx-3 justify-content-between",
          features: ["info"]
        },
        bottomEnd: {
          paging: { firstLast: false }
        }
      },
      language: {
        paginate: {
          next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
          previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
        }
      },
      initComplete: function () {
        const api = this.api();
        const createFilter = function (colIdx, container, placeholder, sourceOptions) {
          const wrapper = document.querySelector(container);
          if (!wrapper) {
            return;
          }

          wrapper.innerHTML = "";
          const select = document.createElement("select");
          select.className = "form-select form-select-sm text-capitalize";
          select.innerHTML = '<option value="">' + placeholder + "</option>";
          wrapper.appendChild(select);

          (sourceOptions || []).forEach(function (value) {
            const opt = document.createElement("option");
            opt.value = value;
            opt.textContent = value;
            select.appendChild(opt);
          });
        };

        createFilter(3, ".goal_source_type", "Select Type", [...new Set(goalSourcePickerRows.map(function (row) {
          return String(row.goalType || row.category || "").trim();
        }).filter(Boolean))]);
        createFilter(5, ".goal_source_entity_scope", "Select Entity Scope", [...new Set(goalSourcePickerRows.map(function (row) {
          return String(row.entityScope || "").trim();
        }).filter(Boolean))]);

        document.querySelector(".goal-source-btn-apply-filter")?.addEventListener("click", function () {
          const typeSelect = document.querySelector(".goal_source_type select");
          const scopeSelect = document.querySelector(".goal_source_entity_scope select");
          const typeValue = String(typeSelect?.value || "").trim();
          const scopeValue = String(scopeSelect?.value || "").trim();
          api.column(3).search(typeValue ? "^" + typeValue.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$" : "", true, false);
          api.column(5).search(scopeValue ? "^" + scopeValue.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$" : "", true, false);
          api.draw();
          const filterEl = document.getElementById("goal-source-filterCollapse");
          if (filterEl) {
            window.bootstrap?.Collapse.getInstance(filterEl)?.hide();
            document.querySelector("#goalSourcePickerModal .dt-filter-btn")?.classList.remove("active");
          }
          updateGoalSourceFilterBadge();
        });

        document.querySelector(".goal-source-btn-reset-filter")?.addEventListener("click", function () {
          const selects = document.querySelectorAll("#goal-source-filterCollapse select");
          selects.forEach(function (select) {
            select.value = "";
          });
          api.columns().search("");
          api.draw();
          updateGoalSourceFilterBadge();
        });

        fixGoalSourcePickerTableLayout();
        updateGoalSourceFilterBadge();
      },
      drawCallback: function () {
        fixGoalSourcePickerTableLayout();
        updateGoalSourceFilterBadge();
        tableEl.querySelectorAll(".goal-pick-source").forEach(function (button) {
          if (button.dataset.bound === "true") {
            return;
          }
          button.dataset.bound = "true";
          button.addEventListener("click", async function () {
            const templateId = String(button.dataset.templateId || "").trim();
            if (!templateId) {
              return;
            }

            try {
              await applyGoalTemplateDetailToStepper(templateId);
              goalSourcePickerModal?.hide();
            } catch (_) {
            }
          });
        });
      }
    });
  }

  function refreshGoalSourcePickerDataTable() {
    initGoalSourcePickerDataTable();
    if (!goalSourcePickerDataTable) {
      return;
    }

    goalSourcePickerDataTable.clear();
    goalSourcePickerDataTable.rows.add(goalSourcePickerRows);
    goalSourcePickerDataTable.draw(false);
  }

  function openGoalSourcePicker() {
    const mode = String(document.getElementById("goalCreationMode")?.value || "").trim().toLowerCase();
    if (mode !== "template") {
      return;
    }

    Promise.resolve(loadGoalSourcePickerCatalog()).then(function () {
      refreshGoalSourcePickerDataTable();
      goalSourcePickerModal?.show();
    }).catch(function () {
      refreshGoalSourcePickerDataTable();
      goalSourcePickerModal?.show();
    });
  }

  function hydrateKpiModalOptions() {
    const workbook = window.enterpriseWorkbookOptions || {};

    fillSelect(
      document.getElementById("goal-kpi-modal-type"),
      workbook.goalMetricType || [],
      "Select"
    );

    fillSelect(
      document.getElementById("goal-kpi-modal-unit"),
      workbook.unitOfMeasure || [],
      "Select"
    );

    fillSelect(
      document.getElementById("goal-kpi-modal-aggregation"),
      workbook.goalAggregation || [],
      "Select"
    );

    fillSelect(
      document.getElementById("goal-kpi-modal-polarity"),
      workbook.directionOfPerformance || [],
      "Select"
    );

    fillSelect(
      document.getElementById("goal-kpi-modal-threshold-model"),
      workbook.thresholdModels || [],
      "Select"
    );

    fillSelect(
      document.getElementById("goal-kpi-modal-reporting-frequency"),
      workbook.reportingFrequencies || [],
      "Select"
    );
  }

  function activateGoalKpiModalTab(tabKey) {
    const trigger = document.querySelector('[data-bs-target="#goal-kpi-tab-' + tabKey + '"]');
    if (!trigger) {
      return;
    }

    if (window.bootstrap?.Tab) {
      window.bootstrap.Tab.getOrCreateInstance(trigger).show();
      return;
    }

    trigger.click();
  }

  function clearGoalKpiModalValidation() {
    [
      "goal-kpi-modal-metric",
      "goal-kpi-modal-type",
      "goal-kpi-modal-unit",
      "goal-kpi-modal-aggregation",
      "goal-kpi-modal-polarity",
      "goal-kpi-modal-threshold-model",
      "goal-kpi-modal-reporting-frequency",
      "goal-kpi-governance-cascade",
      "goal-kpi-governance-origin",
      "goal-kpi-governance-role",
      "goal-kpi-governance-restriction",
      "goal-kpi-governance-rollup"
    ].forEach(function (id) {
      document.getElementById(id)?.classList.remove("is-invalid");
    });

    document.querySelectorAll('#goalKpiModal .nav-link').forEach(function (el) {
      el.classList.remove("text-danger");
    });

    const yearlyTable = document.getElementById("goal-kpi-yearly-plan-table");
    const yearlyFeedback = document.getElementById("goal-kpi-yearly-plan-feedback");
    yearlyTable?.classList.remove("border-danger");
    yearlyFeedback?.classList.add("d-none");
  }

  function bindGoalKpiModalValidationReset() {
    [
      "goal-kpi-modal-metric",
      "goal-kpi-modal-type",
      "goal-kpi-modal-unit",
      "goal-kpi-modal-aggregation",
      "goal-kpi-modal-polarity",
      "goal-kpi-modal-threshold-model",
      "goal-kpi-modal-reporting-frequency",
      "goal-kpi-governance-cascade",
      "goal-kpi-governance-origin",
      "goal-kpi-governance-role",
      "goal-kpi-governance-restriction",
      "goal-kpi-governance-rollup"
    ].forEach(function (id) {
      const field = document.getElementById(id);
      if (!field || field.dataset.validationBound === "true") {
        return;
      }

      field.dataset.validationBound = "true";
      field.addEventListener("input", function () {
        field.classList.remove("is-invalid");
      });
      field.addEventListener("change", function () {
        field.classList.remove("is-invalid");
        document.querySelectorAll('#goalKpiModal .nav-link.text-danger').forEach(function (el) {
          el.classList.remove("text-danger");
        });
      });
    });

    const yearlyHost = document.getElementById("goal-kpi-yearly-plan-rows");
    if (yearlyHost && yearlyHost.dataset.validationBound !== "true") {
      yearlyHost.dataset.validationBound = "true";
      yearlyHost.addEventListener("input", function (event) {
        if (event.target?.classList?.contains("metric-year-target")) {
          document.getElementById("goal-kpi-yearly-plan-table")?.classList.remove("border-danger");
          document.getElementById("goal-kpi-yearly-plan-feedback")?.classList.add("d-none");
          document.querySelector('[data-bs-target="#goal-kpi-tab-yearly-plan"]')?.classList.remove("text-danger");
        }
      });
    }
  }

  function markGoalKpiModalFieldInvalid(fieldId, tabKey) {
    const field = document.getElementById(fieldId);
    field?.classList.add("is-invalid");
    document.querySelector('[data-bs-target="#goal-kpi-tab-' + tabKey + '"]')?.classList.add("text-danger");
  }

  function validateGoalKpiModal() {
    clearGoalKpiModalValidation();

    const requiredFields = [
      { id: "goal-kpi-modal-metric", tab: "definition" },
      { id: "goal-kpi-modal-type", tab: "definition" },
      { id: "goal-kpi-modal-unit", tab: "definition" },
      { id: "goal-kpi-modal-aggregation", tab: "definition" },
      { id: "goal-kpi-modal-polarity", tab: "definition" },
      { id: "goal-kpi-modal-threshold-model", tab: "definition" },
      { id: "goal-kpi-modal-reporting-frequency", tab: "definition" },
      { id: "goal-kpi-governance-cascade", tab: "governance" },
      { id: "goal-kpi-governance-origin", tab: "governance" },
      { id: "goal-kpi-governance-role", tab: "governance" },
      { id: "goal-kpi-governance-restriction", tab: "governance" },
      { id: "goal-kpi-governance-rollup", tab: "governance" }
    ];

    let firstInvalidTab = "";

    requiredFields.forEach(function (entry) {
      const field = document.getElementById(entry.id);
      const value = String(field?.value || "").trim();
      if (!value) {
        markGoalKpiModalFieldInvalid(entry.id, entry.tab);
        if (!firstInvalidTab) {
          firstInvalidTab = entry.tab;
        }
      }
    });

    const yearlyRows = Array.from(document.querySelectorAll("#goal-kpi-yearly-plan-rows tr"));
    const missingYearlyTargets = yearlyRows.some(function (row) {
      return !String(row.querySelector(".metric-year-target")?.value || "").trim();
    });

    if (missingYearlyTargets || yearlyRows.length === 0) {
      document.querySelector('[data-bs-target="#goal-kpi-tab-yearly-plan"]')?.classList.add("text-danger");
      document.getElementById("goal-kpi-yearly-plan-table")?.classList.add("border-danger");
      document.getElementById("goal-kpi-yearly-plan-feedback")?.classList.remove("d-none");
      if (!firstInvalidTab) {
        firstInvalidTab = "yearly-plan";
      }
    }

    if (firstInvalidTab) {
      activateGoalKpiModalTab(firstInvalidTab);
      return false;
    }

    return true;
  }

  function initTooltips() {
    if (!window.bootstrap || typeof window.bootstrap.Tooltip !== "function") {
      return;
    }

    form.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
      try {
        new window.bootstrap.Tooltip(el);
      } catch (_) {
      }
    });
  }

  function initSelect2For(el, config) {
    if (!el || !window.jQuery || !window.jQuery.fn?.select2) {
      return;
    }

    const $el = window.jQuery(el);
    if ($el.hasClass("select2-hidden-accessible")) {
      try {
        $el.select2("destroy");
      } catch (_) {
      }
    }

    $el.select2(Object.assign({
      width: "100%"
    }, config || {}));

    $el.off("select2:select select2:unselect select2:clear");
    $el.on("select2:select select2:unselect select2:clear", function () {
      this.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }

  function formatDate(value) {
    const raw = String(value || "").trim();
    if (!raw) {
      return "---";
    }

    const date = new Date(raw);
    if (Number.isNaN(date.getTime())) {
      return raw;
    }

    return date.toLocaleDateString("en-GB");
  }

  function toDateInputIso(value) {
    const raw = String(value || "").trim();
    if (!raw) {
      return "";
    }

    if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
      return raw;
    }

    const date = new Date(raw);
    if (Number.isNaN(date.getTime())) {
      return "";
    }

    const year = String(date.getFullYear());
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return year + "-" + month + "-" + day;
  }

  function syncPlanningHorizonSummary(period) {
    const summaryEl = form.querySelector("#goal-allowed-horizon-summary");
    const startEl = form.querySelector("#goal-planning-start-year");
    const endEl = form.querySelector("#goal-planning-end-year");

    const minIso = toDateInputIso(period?.startDate);
    const maxIso = toDateInputIso(period?.endDate);

    if (!minIso || !maxIso) {
      if (summaryEl) {
        summaryEl.textContent = "Allowed horizon: ---. You may narrow, but not extend.";
      }
      if (startEl) {
        startEl.min = "";
        startEl.max = "";
      }
      if (endEl) {
        endEl.min = "";
        endEl.max = "";
      }
      return;
    }

    if (summaryEl) {
      summaryEl.textContent = "Allowed horizon: " + formatDate(minIso) + " - " + formatDate(maxIso) + ". You may narrow, but not extend.";
    }

    if (startEl) {
      startEl.min = minIso;
      startEl.max = maxIso;
      if (!startEl.value) {
        startEl.value = minIso;
      }
    }

    if (endEl) {
      endEl.min = minIso;
      endEl.max = maxIso;
      if (!endEl.value) {
        endEl.value = maxIso;
      }
    }

    syncPlanningDatePairConstraints();
  }

  function clampIsoDate(value, min, max) {
    const raw = String(value || "").trim();
    if (!raw) {
      return raw;
    }

    let next = raw;
    if (min && next < min) {
      next = min;
    }
    if (max && next > max) {
      next = max;
    }
    return next;
  }

  function syncPlanningDatePairConstraints() {
    const startEl = form.querySelector("#goal-planning-start-year");
    const endEl = form.querySelector("#goal-planning-end-year");
    if (!startEl || !endEl) {
      return;
    }

    const baseStartMin = String(startEl.min || "").trim();
    const baseStartMax = String(startEl.max || "").trim();
    const baseEndMin = String(endEl.min || "").trim();
    const baseEndMax = String(endEl.max || "").trim();
    let startValue = clampIsoDate(startEl.value, baseStartMin, baseStartMax);
    let endValue = clampIsoDate(endEl.value, baseEndMin, baseEndMax);

    if (startValue !== String(startEl.value || "").trim()) {
      startEl.value = startValue;
    }
    if (endValue !== String(endEl.value || "").trim()) {
      endEl.value = endValue;
    }

    if (startValue) {
      endEl.min = startValue > baseEndMin ? startValue : baseEndMin;
      if (endValue && endValue < startValue) {
        endValue = startValue;
        endEl.value = endValue;
      }
    } else {
      endEl.min = baseEndMin;
    }

    if (endValue) {
      startEl.max = endValue && (!baseStartMax || endValue < baseStartMax) ? endValue : baseStartMax;
      if (startValue && startValue > endValue) {
        startValue = endValue;
        startEl.value = startValue;
      }
    } else {
      startEl.max = baseStartMax;
    }

    startValue = clampIsoDate(startEl.value, baseStartMin, String(startEl.max || "").trim());
    endValue = clampIsoDate(endEl.value, String(endEl.min || "").trim(), baseEndMax);
    if (startValue !== String(startEl.value || "").trim()) {
      startEl.value = startValue;
    }
    if (endValue !== String(endEl.value || "").trim()) {
      endEl.value = endValue;
    }
  }

  async function initPlanningOptions() {
    const periodEl = form.querySelector("#goal-strategy-period");
    if (!periodEl || !window.strategyPlanningApi) {
      return;
    }

    try {
      const response = await window.strategyPlanningApi.listStrategyPeriods("", "", "");
      const rows = Array.isArray(response?.items) ? response.items : (Array.isArray(response) ? response : []);
      strategyPeriodsById = new Map();
      const options = rows.map(function (row) {
        const id = String(row?.id || row?.strategyPeriodId || "").trim();
        const code = String(row?.code || "").trim();
        const name = String(row?.name || "").trim();
        const status = String(row?.status || "").trim();
        const startDate = String(row?.startDate || "").trim();
        const endDate = String(row?.endDate || "").trim();
        const labelBase = [code, name].filter(Boolean).join(" - ");
        const dateRange = (startDate || endDate)
          ? [formatDate(startDate), formatDate(endDate)].join(" -> ")
          : "";
        const label = [labelBase || id, dateRange, status].filter(Boolean).join(" / ");
        const isSelectable = status.toLowerCase() === "active";

        return {
          value: id,
          label: label,
          meta: row,
          disabled: !isSelectable
        };
      }).filter(function (row) {
        return row.value && row.label;
      });

      options.forEach(function (option) {
        strategyPeriodsById.set(option.value, option.meta);
      });

      fillSelect(periodEl, options, "Select strategy period (Active only)...");

      periodEl.addEventListener("change", async function () {
        const periodId = String(periodEl.value || "").trim();
        if (!periodId) {
          syncPlanningHorizonSummary(null);
          syncKpiTableYears();
          return;
        }

        const selected = options.find(function (item) {
          return item.value === periodId;
        });

        if (selected?.meta) {
          syncPlanningHorizonSummary(selected.meta);
          syncKpiTableYears();
          return;
        }

        try {
          const detail = await window.strategyPlanningApi.getStrategyPeriod(periodId);
          if (String(detail?.status || "").trim().toLowerCase() !== "active") {
            periodEl.value = "";
            syncPlanningHorizonSummary(null);
            syncKpiTableYears();
            return;
          }
          syncPlanningHorizonSummary(detail);
          syncKpiTableYears();
        } catch (_) {
          syncPlanningHorizonSummary(null);
          syncKpiTableYears();
        }
      });
    } catch (_) {
      syncPlanningHorizonSummary(null);
    }
  }

  function buildTargetYears() {
    const startRaw = String(form.querySelector("#goal-planning-start-year")?.value || "").trim();
    const endRaw = String(form.querySelector("#goal-planning-end-year")?.value || "").trim();
    const startYear = startRaw ? Number(startRaw.slice(0, 4)) : NaN;
    const endYear = endRaw ? Number(endRaw.slice(0, 4)) : NaN;

    if (Number.isFinite(startYear) && Number.isFinite(endYear) && endYear >= startYear) {
      const years = [];
      for (let year = startYear; year <= endYear; year += 1) {
        years.push(year);
      }
      return years;
    }

    return [];
  }

  function createSourceMetricRow(sourceRow) {
    const years = buildTargetYears();
    goalKpiRowSequence += 1;
    return {
      uid: "goal-kpi-row-" + goalKpiRowSequence,
      goalMetricType: String(sourceRow?.goalMetricType || "Primary KPI").trim() || "Primary KPI",
      metric: String(sourceRow?.metric || "Primary KPI / Metric").trim() || "Primary KPI / Metric",
      unitOfMeasure: String(sourceRow?.unitOfMeasure || "-").trim() || "-",
      metricDefinitionId: String(sourceRow?.metricDefinitionId || "").trim(),
      aggregationMethod: String(sourceRow?.aggregationMethod || "").trim(),
      polarity: String(sourceRow?.polarity || "").trim(),
      thresholdModel: String(sourceRow?.thresholdModel || "").trim(),
      reportingFrequency: String(sourceRow?.reportingFrequency || "").trim(),
      cascadeMetric: String(sourceRow?.cascadeMetric ?? "true"),
      metricOrigin: String(sourceRow?.metricOrigin || "Local").trim() || "Local",
      metricRole: String(sourceRow?.metricRole || "Strategic").trim() || "Strategic",
      restrictionMode: String(sourceRow?.restrictionMode || "").trim(),
      rollupEligible: String(sourceRow?.rollupEligible ?? "true"),
      yearlyPlanRows: Array.isArray(sourceRow?.yearlyPlanRows) ? sourceRow.yearlyPlanRows.map(function (row) { return Object.assign({}, row); }) : [],
      year: years.length ? String(years[0]) : "-",
      yearCount: years.length,
      yearSpanLabel: years.length ? String(years.length) + " Years" : "-"
    };
  }

  function formatDecimalForInput(value) {
    if (value === null || value === undefined || value === "") {
      return "";
    }

    const num = Number(value);
    return Number.isFinite(num) ? String(num) : "";
  }

  function parseDecimalInputValue(value) {
    const normalized = String(value || "").trim().replace(",", ".");
    if (!normalized) {
      return null;
    }

    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
  }

  function openGoalKpiYearlyValueModal(config) {
    if (!goalKpiYearlyValueModalEl || !goalKpiYearlyValueModal) {
      return;
    }

    const titleEl = document.getElementById("goal-kpi-yearly-value-modal-title");
    const messageEl = document.getElementById("goal-kpi-yearly-value-modal-message");
    const label1El = document.getElementById("goal-kpi-yearly-value-modal-label-1");
    const label2El = document.getElementById("goal-kpi-yearly-value-modal-label-2");
    const field1El = document.getElementById("goal-kpi-yearly-value-modal-field-1");
    const field2El = document.getElementById("goal-kpi-yearly-value-modal-field-2");
    const field2WrapEl = document.getElementById("goal-kpi-yearly-value-modal-field-2-wrap");

    goalKpiYearlyValueModalState = config || null;

    if (titleEl) {
      titleEl.textContent = String(config?.title || "Yearly Value Input");
    }
    if (messageEl) {
      messageEl.textContent = String(config?.message || "Enter value.");
    }
    if (label1El) {
      label1El.textContent = String(config?.label1 || "Value");
    }
    if (field1El) {
      field1El.value = "";
      field1El.placeholder = String(config?.placeholder1 || "Enter value");
    }
    if (config?.label2) {
      if (label2El) {
        label2El.textContent = String(config.label2);
      }
      if (field2El) {
        field2El.value = "";
        field2El.placeholder = String(config?.placeholder2 || "Enter value");
      }
      field2WrapEl?.classList.remove("d-none");
    } else {
      field2WrapEl?.classList.add("d-none");
      if (field2El) {
        field2El.value = "";
      }
    }

    goalKpiYearlyValueModal.show();
    window.setTimeout(function () {
      field1El?.focus();
    }, 150);
  }

  function buildGoalKpiYearlyPlanRows() {
    const tbody = document.getElementById("goal-kpi-yearly-plan-rows");
    if (!tbody) {
      return;
    }

    const years = buildTargetYears();
    const rows = years.map(function (year) {
      return '<tr>' +
        '<td class="fw-medium">' + year + "</td>" +
        '<td><input class="form-control form-control-sm metric-year-target text-end" type="number" step="any" inputmode="decimal" placeholder="Target"></td>' +
        '<td class="metric-runtime-col d-none"><input class="form-control form-control-sm metric-year-actual text-end" type="number" step="any" inputmode="decimal" placeholder="Actual"></td>' +
        '<td class="metric-runtime-col d-none"><input class="form-control form-control-sm metric-year-forecast text-end" type="number" step="any" inputmode="decimal" placeholder="Forecast"></td>' +
        '<td class="metric-threshold-col"><input class="form-control form-control-sm metric-year-threshold-min text-end" type="number" step="any" inputmode="decimal" placeholder="Min"></td>' +
        '<td class="metric-threshold-col"><input class="form-control form-control-sm metric-year-threshold-max text-end" type="number" step="any" inputmode="decimal" placeholder="Max"></td>' +
        '<td><input class="form-control form-control-sm metric-year-commentary" type="text" maxlength="300" placeholder="Commentary"></td>' +
      "</tr>";
    });

    tbody.innerHTML = rows.join("");
    document.getElementById("goal-kpi-yearly-plan-table")?.classList.remove("border-danger");
    document.getElementById("goal-kpi-yearly-plan-feedback")?.classList.add("d-none");

    if (!years.length) {
      tbody.innerHTML = '<tr><td colspan="7" class="text-muted text-center py-4">Planning Horizon Start Date and End Date are required to generate yearly plan rows.</td></tr>';
    }
  }

  function collectGoalKpiYearlyPlanRows() {
    return Array.from(document.querySelectorAll("#goal-kpi-yearly-plan-rows tr")).map(function (tr) {
      const yearText = String(tr.querySelector("td")?.textContent || "").trim();
      const year = Number(yearText);
      if (!Number.isFinite(year)) {
        return null;
      }
      return {
        year: year,
        targetValue: parseDecimalInputValue(tr.querySelector(".metric-year-target")?.value),
        actualValue: parseDecimalInputValue(tr.querySelector(".metric-year-actual")?.value),
        forecastValue: parseDecimalInputValue(tr.querySelector(".metric-year-forecast")?.value),
        thresholdMin: parseDecimalInputValue(tr.querySelector(".metric-year-threshold-min")?.value),
        thresholdMax: parseDecimalInputValue(tr.querySelector(".metric-year-threshold-max")?.value),
        commentary: String(tr.querySelector(".metric-year-commentary")?.value || "").trim()
      };
    }).filter(Boolean);
  }

  function applyGoalKpiYearlyPlanRows(rows) {
    buildGoalKpiYearlyPlanRows();
    (rows || []).forEach(function (row) {
      const tr = Array.from(document.querySelectorAll("#goal-kpi-yearly-plan-rows tr")).find(function (candidate) {
        return Number(String(candidate.querySelector("td")?.textContent || "").trim()) === Number(row.year);
      });
      if (!tr) {
        return;
      }
      const setValue = function (selector, value) {
        const input = tr.querySelector(selector);
        if (input) {
          input.value = value == null ? "" : String(value);
        }
      };
      setValue(".metric-year-target", row.targetValue);
      setValue(".metric-year-actual", row.actualValue);
      setValue(".metric-year-forecast", row.forecastValue);
      setValue(".metric-year-threshold-min", row.thresholdMin);
      setValue(".metric-year-threshold-max", row.thresholdMax);
      setValue(".metric-year-commentary", row.commentary);
    });
  }

  function syncGoalKpiRuntimeFields() {
    const modalEl = document.getElementById("goalKpiModal");
    if (!modalEl) {
      return;
    }

    const showRuntime = modalEl.dataset.showRuntime === "true";
    modalEl.querySelectorAll(".metric-runtime-col").forEach(function (el) {
      el.classList.toggle("d-none", !showRuntime);
    });

    const toggle = document.getElementById("goal-kpi-yearly-toggle-runtime");
    if (toggle) {
      toggle.textContent = showRuntime ? "Hide advanced yearly fields" : "Advanced yearly fields";
    }
  }

  function applyGoalKpiFlatFill() {
    openGoalKpiYearlyValueModal({
      title: "Fill Flat",
      message: "Apply the same target value to all years in the yearly plan.",
      label1: "Target Value",
      placeholder1: "Enter target value",
      onSave: function (values) {
        const value = values[0];
        if (value === null || value === undefined) {
          return;
        }

        const formatted = formatDecimalForInput(value);
        document.querySelectorAll("#goal-kpi-yearly-plan-rows .metric-year-target").forEach(function (input) {
          input.value = formatted;
        });
      }
    });
  }

  function interpolateGoalKpiTargets() {
    const rows = Array.from(document.querySelectorAll("#goal-kpi-yearly-plan-rows tr"));
    if (rows.length < 2) {
      return;
    }

    openGoalKpiYearlyValueModal({
      title: "Interpolate Targets",
      message: "Generate a linear target progression between the first and last year.",
      label1: "Start Target Value",
      placeholder1: "Enter start value",
      label2: "End Target Value",
      placeholder2: "Enter end value",
      onSave: function (values) {
        const startValue = values[0];
        const endValue = values[1];
        if (startValue === null || startValue === undefined || endValue === null || endValue === undefined) {
          return;
        }

        const steps = rows.length - 1;
        rows.forEach(function (tr, idx) {
          const value = steps === 0 ? startValue : startValue + ((endValue - startValue) * idx / steps);
          const input = tr.querySelector(".metric-year-target");
          if (input) {
            input.value = Number.isFinite(value) ? formatDecimalForInput(value) : "";
          }
        });
      }
    });
  }

  function copyGoalKpiPreviousRows() {
    const rows = Array.from(document.querySelectorAll("#goal-kpi-yearly-plan-rows tr"));
    rows.slice(1).forEach(function (tr, idx) {
      const prev = rows[idx];
      const map = [
        [".metric-year-target", ".metric-year-target"],
        [".metric-year-threshold-min", ".metric-year-threshold-min"],
        [".metric-year-threshold-max", ".metric-year-threshold-max"],
        [".metric-year-commentary", ".metric-year-commentary"],
        [".metric-year-actual", ".metric-year-actual"],
        [".metric-year-forecast", ".metric-year-forecast"]
      ];

      map.forEach(function (pair) {
        const currentEl = tr.querySelector(pair[0]);
        const prevEl = prev.querySelector(pair[1]);
        if (currentEl && prevEl) {
          currentEl.value = prevEl.value;
        }
      });
    });
  }

  function clearGoalKpiYearRows() {
    document.querySelectorAll("#goal-kpi-yearly-plan-rows .metric-year-target, #goal-kpi-yearly-plan-rows .metric-year-actual, #goal-kpi-yearly-plan-rows .metric-year-forecast, #goal-kpi-yearly-plan-rows .metric-year-threshold-min, #goal-kpi-yearly-plan-rows .metric-year-threshold-max, #goal-kpi-yearly-plan-rows .metric-year-commentary").forEach(function (input) {
      if (!input.disabled) {
        input.value = "";
      }
    });
  }

  function isBudgetEnvelopeEnabled() {
    return Boolean(document.getElementById("goal-budget-enabled")?.checked);
  }

  function syncBudgetEnvelopeUi() {
    const enabled = isBudgetEnvelopeEnabled();
    const content = document.getElementById("goal-budget-content");
    const note = document.getElementById("goal-budget-disabled-note");

    if (content) {
      content.classList.toggle("is-disabled", !enabled);
      content.setAttribute("aria-hidden", enabled ? "false" : "true");
    }

    if (note) {
      note.classList.toggle("d-none", enabled);
    }
  }

  function renderBudgetYearRows(existing) {
    if (!goalBudgetTbody) {
      return;
    }

    const years = buildTargetYears();
    const rowMap = new Map((existing || []).map(function (row) {
      return [Number(row.year), row];
    }));

    goalBudgetTbody.innerHTML = "";

    years.forEach(function (year) {
      const row = rowMap.get(year) || {};
      const tr = document.createElement("tr");
      tr.innerHTML =
        '<td><input class="form-control form-control-sm budget-year" value="' + year + '" readonly></td>' +
        '<td><input type="number" class="form-control form-control-sm budget-rev text-end" step="any" inputmode="decimal" value="' + formatDecimalForInput(row.revenueTarget) + '"></td>' +
        '<td><input type="number" class="form-control form-control-sm budget-ebitda text-end" step="any" inputmode="decimal" value="' + formatDecimalForInput(row.ebitdaTarget) + '"></td>' +
        '<td><input type="number" class="form-control form-control-sm budget-capex text-end" step="any" inputmode="decimal" value="' + formatDecimalForInput(row.capexEnvelope) + '"></td>' +
        '<td><input type="number" class="form-control form-control-sm budget-opex text-end" step="any" inputmode="decimal" value="' + formatDecimalForInput(row.opexEnvelope) + '"></td>' +
        '<td><input type="number" class="form-control form-control-sm budget-savings text-end" step="any" inputmode="decimal" value="' + formatDecimalForInput(row.savingsTarget) + '"></td>' +
        '<td><input type="number" class="form-control form-control-sm budget-funding text-end" step="any" inputmode="decimal" value="' + formatDecimalForInput(row.fundingPoolEnvelope ?? row.fundingPool) + '"></td>' +
        '<td><input class="form-control form-control-sm budget-commentary" maxlength="300" value="' + String(row.commentary || "").replace(/"/g, "&quot;") + '"></td>';
      goalBudgetTbody.appendChild(tr);
    });

    if (!years.length) {
      goalBudgetTbody.innerHTML = '<tr><td colspan="8" class="text-muted text-center py-4">Planning Horizon Start Date and End Date are required to generate yearly budget rows.</td></tr>';
    }

    syncBudgetEnvelopeUi();
  }

  function collectYearlyBudgetsFromDom() {
    return Array.from(goalBudgetTbody?.querySelectorAll("tr") || []).map(function (tr) {
      return {
        year: Number(tr.querySelector(".budget-year")?.value || 0),
        revenueTarget: parseDecimalInputValue(tr.querySelector(".budget-rev")?.value),
        ebitdaTarget: parseDecimalInputValue(tr.querySelector(".budget-ebitda")?.value),
        capexEnvelope: parseDecimalInputValue(tr.querySelector(".budget-capex")?.value),
        opexEnvelope: parseDecimalInputValue(tr.querySelector(".budget-opex")?.value),
        savingsTarget: parseDecimalInputValue(tr.querySelector(".budget-savings")?.value),
        fundingPoolEnvelope: parseDecimalInputValue(tr.querySelector(".budget-funding")?.value),
        fundingPool: parseDecimalInputValue(tr.querySelector(".budget-funding")?.value),
        commentary: String(tr.querySelector(".budget-commentary")?.value || "").trim() || null
      };
    }).filter(function (row) {
      return Number.isInteger(row.year) && row.year > 0;
    });
  }

  function syncBudgetYearRowsWithHorizonChange() {
    renderBudgetYearRows(collectYearlyBudgetsFromDom());
  }

  function budgetSelectorForKey(key) {
    const selectorByKey = {
      revenue: ".budget-rev",
      revenueTarget: ".budget-rev",
      ebitda: ".budget-ebitda",
      ebitdaTarget: ".budget-ebitda",
      capex: ".budget-capex",
      capexEnvelope: ".budget-capex",
      opex: ".budget-opex",
      opexEnvelope: ".budget-opex",
      savings: ".budget-savings",
      savingsTarget: ".budget-savings",
      funding: ".budget-funding",
      fundingPoolEnvelope: ".budget-funding",
      fundingPool: ".budget-funding"
    };

    return selectorByKey[String(key || "").trim()] || "";
  }

  function fillBudgetColumn(key, value) {
    if (!isBudgetEnvelopeEnabled()) {
      return;
    }

    const selector = budgetSelectorForKey(key);
    if (!selector) {
      return;
    }

    goalBudgetTbody?.querySelectorAll(selector).forEach(function (input) {
      input.value = formatDecimalForInput(value);
    });
  }

  function copyBudgetColumnDown(key) {
    if (!isBudgetEnvelopeEnabled()) {
      return;
    }

    const selector = budgetSelectorForKey(key);
    if (!selector) {
      return;
    }

    const rows = Array.from(goalBudgetTbody?.querySelectorAll("tr") || []);
    if (rows.length < 2) {
      return;
    }

    rows.slice(1).forEach(function (tr) {
      const prev = tr.previousElementSibling;
      if (!prev) {
        return;
      }

      const currentEl = tr.querySelector(selector);
      const previousEl = prev.querySelector(selector);
      if (currentEl) {
        currentEl.value = previousEl?.value || "";
      }
    });
  }

  function interpolateBudgetColumn(key) {
    if (!isBudgetEnvelopeEnabled()) {
      return;
    }

    const selector = budgetSelectorForKey(key);
    if (!selector) {
      return;
    }

    const rows = Array.from(goalBudgetTbody?.querySelectorAll("tr") || []);
    if (rows.length < 2) {
      return;
    }

    openGoalKpiYearlyValueModal({
      title: "Interpolate Budget",
      message: "Generate a linear progression for the selected budget column.",
      label1: "Start Value",
      placeholder1: "Enter start value",
      label2: "End Value",
      placeholder2: "Enter end value",
      onSave: function (values) {
        const startValue = values[0];
        const endValue = values[1];
        if (startValue === null || startValue === undefined || endValue === null || endValue === undefined) {
          return;
        }

        const steps = rows.length - 1;
        rows.forEach(function (tr, idx) {
          const value = steps === 0 ? startValue : startValue + ((endValue - startValue) * idx / steps);
          const input = tr.querySelector(selector);
          if (input) {
            input.value = Number.isFinite(value) ? formatDecimalForInput(value) : "";
          }
        });
      }
    });
  }

  function clearBudgetColumn(key) {
    if (!isBudgetEnvelopeEnabled()) {
      return;
    }

    const selector = budgetSelectorForKey(key);
    if (!selector) {
      return;
    }

    goalBudgetTbody?.querySelectorAll(selector).forEach(function (input) {
      input.value = "";
    });
  }

  function initKpiOptions() {
    function escapeHtml(value) {
      return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
    }

    function planningYearMeta() {
      const years = buildTargetYears();
      return {
        firstYear: years.length ? String(years[0]) : "-",
        yearCount: years.length,
        yearSpanLabel: years.length ? String(years.length) + " Years" : "-"
      };
    }

    function createMetricRow(sourceRow) {
      const seed = sourceRow || {};
      const created = createSourceMetricRow({
        goalMetricType: seed.goalMetricType,
        metric: seed.metric,
        unitOfMeasure: seed.unitOfMeasure,
        metricDefinitionId: seed.metricDefinitionId || seed.metricDefId,
        aggregationMethod: seed.aggregationMethod,
        polarity: seed.polarity,
        thresholdModel: seed.thresholdModel,
        reportingFrequency: seed.reportingFrequency,
        cascadeMetric: seed.cascadeMetric,
        metricOrigin: seed.metricOrigin,
        metricRole: seed.metricRole,
        restrictionMode: seed.restrictionMode,
        rollupEligible: seed.rollupEligible,
        yearlyPlanRows: Array.isArray(seed.yearlyPlanRows) ? seed.yearlyPlanRows : []
      });

      if (seed.uid) {
        created.uid = String(seed.uid).trim();
      }

      return created;
    }

    function resetGoalKpiModal() {
      const typeEl = document.getElementById("goal-kpi-modal-type");
      const metricEl = document.getElementById("goal-kpi-modal-metric");
      const unitEl = document.getElementById("goal-kpi-modal-unit");
      const definitionIdEl = document.getElementById("goal-kpi-modal-definition-id");
      const aggregationEl = document.getElementById("goal-kpi-modal-aggregation");
      const polarityEl = document.getElementById("goal-kpi-modal-polarity");
      const thresholdModelEl = document.getElementById("goal-kpi-modal-threshold-model");
      const reportingFrequencyEl = document.getElementById("goal-kpi-modal-reporting-frequency");

      if (typeEl) {
        typeEl.value = "";
      }
      if (metricEl) {
        metricEl.value = "";
      }
      if (definitionIdEl) {
        definitionIdEl.value = "";
      }
      if (unitEl) {
        unitEl.value = "";
      }
      if (aggregationEl) {
        aggregationEl.value = "";
      }
      if (polarityEl) {
        polarityEl.value = "";
      }
      if (thresholdModelEl) {
        thresholdModelEl.value = "";
      }
      if (reportingFrequencyEl) {
        reportingFrequencyEl.value = "";
      }
      document.getElementById("goal-kpi-governance-cascade") && (document.getElementById("goal-kpi-governance-cascade").value = "true");
      document.getElementById("goal-kpi-governance-origin") && (document.getElementById("goal-kpi-governance-origin").value = "Local");
      document.getElementById("goal-kpi-governance-role") && (document.getElementById("goal-kpi-governance-role").value = "Strategic");
      document.getElementById("goal-kpi-governance-restriction") && (document.getElementById("goal-kpi-governance-restriction").value = "");
      document.getElementById("goal-kpi-governance-rollup") && (document.getElementById("goal-kpi-governance-rollup").value = "true");
      activeGoalKpiEditRowId = "";
      const saveBtn = document.getElementById("goal-kpi-modal-save");
      if (saveBtn) {
        saveBtn.textContent = "Save KPI";
      }

      clearGoalKpiModalValidation();
    }

    function openGoalKpiModal(rowData) {
      resetGoalKpiModal();
      if (rowData) {
        activeGoalKpiEditRowId = String(rowData.uid || "").trim();
        document.getElementById("goal-kpi-modal-metric") && (document.getElementById("goal-kpi-modal-metric").value = rowData.metric || "");
        document.getElementById("goal-kpi-modal-definition-id") && (document.getElementById("goal-kpi-modal-definition-id").value = rowData.metricDefinitionId || "");
        document.getElementById("goal-kpi-modal-type") && (document.getElementById("goal-kpi-modal-type").value = rowData.goalMetricType || "");
        document.getElementById("goal-kpi-modal-unit") && (document.getElementById("goal-kpi-modal-unit").value = rowData.unitOfMeasure || "");
        document.getElementById("goal-kpi-modal-aggregation") && (document.getElementById("goal-kpi-modal-aggregation").value = rowData.aggregationMethod || "");
        document.getElementById("goal-kpi-modal-polarity") && (document.getElementById("goal-kpi-modal-polarity").value = rowData.polarity || "");
        document.getElementById("goal-kpi-modal-threshold-model") && (document.getElementById("goal-kpi-modal-threshold-model").value = rowData.thresholdModel || "");
        document.getElementById("goal-kpi-modal-reporting-frequency") && (document.getElementById("goal-kpi-modal-reporting-frequency").value = rowData.reportingFrequency || "");
        document.getElementById("goal-kpi-governance-cascade") && (document.getElementById("goal-kpi-governance-cascade").value = rowData.cascadeMetric || "true");
        document.getElementById("goal-kpi-governance-origin") && (document.getElementById("goal-kpi-governance-origin").value = rowData.metricOrigin || "Local");
        document.getElementById("goal-kpi-governance-role") && (document.getElementById("goal-kpi-governance-role").value = rowData.metricRole || "Strategic");
        document.getElementById("goal-kpi-governance-restriction") && (document.getElementById("goal-kpi-governance-restriction").value = rowData.restrictionMode || "");
        document.getElementById("goal-kpi-governance-rollup") && (document.getElementById("goal-kpi-governance-rollup").value = rowData.rollupEligible || "true");
        applyGoalKpiYearlyPlanRows(rowData.yearlyPlanRows || []);
        const saveBtn = document.getElementById("goal-kpi-modal-save");
        if (saveBtn) {
          saveBtn.textContent = "Update KPI";
        }
      } else {
        buildGoalKpiYearlyPlanRows();
      }
      if (goalKpiModalEl) {
        goalKpiModalEl.dataset.showRuntime = "false";
      }
      syncGoalKpiRuntimeFields();
      if (goalKpiModal) {
        goalKpiModal.show();
      }
    }

    function syncGoalKpiRowsToTable() {
      if (!goalKpiDataTable) {
        return;
      }

      goalKpiDataTable.clear();
      goalKpiDataTable.rows.add(goalKpiRows);
      goalKpiDataTable.draw(false);
    }

    function addMetricRow(sourceRow) {
      goalKpiRows.push(createMetricRow(sourceRow));
      if (goalKpiDataTable) {
        syncGoalKpiRowsToTable();
      }
    }

    function updateMetricRow(rowId, sourceRow) {
      goalKpiRows = goalKpiRows.map(function (row) {
        if (row.uid !== rowId) {
          return row;
        }
        const next = createMetricRow(sourceRow);
        next.uid = row.uid;
        return next;
      });
      syncGoalKpiRowsToTable();
    }

    function duplicateMetricRow(rowId) {
      const source = goalKpiRows.find(function (row) {
        return row.uid === rowId;
      });

      addMetricRow(source || null);
    }

    function editMetricRow(rowId) {
      const source = goalKpiRows.find(function (row) {
        return row.uid === rowId;
      });
      if (!source) {
        return;
      }
      openGoalKpiModal(source);
    }

    function removeMetricRow(rowId) {
      if (goalKpiRows.length <= 1) {
        return;
      }

      goalKpiRows = goalKpiRows.filter(function (row) {
        return row.uid !== rowId;
      });

      syncGoalKpiRowsToTable();
    }

    function fixGoalKpiTableLayout() {
      window.setTimeout(function () {
        const wrapper = tableEl?.closest(".dt-container");
        if (!wrapper) {
          return;
        }

        const elementsToModify = [
          { selector: ".dt-buttons .btn", classToRemove: "btn-secondary" },
          { selector: '.dt-search .form-control', classToRemove: "form-control-sm" },
          { selector: ".dt-length .form-select", classToRemove: "form-select-sm", classToAdd: "ms-0" },
          { selector: ".dt-length", classToAdd: "mb-md-6 mb-0" },
          { selector: ".dt-search", classToAdd: "mb-md-6 mb-2" },
          {
            selector: ".dt-layout-end",
            classToRemove: "justify-content-between",
            classToAdd: "d-flex gap-md-2 justify-content-md-end justify-content-center gap-2 flex-wrap mt-0"
          },
          { selector: ".dt-layout-start", classToAdd: "mt-0" },
          { selector: ".dt-buttons", classToAdd: "d-flex gap-2 mb-md-0 mb-6" },
          { selector: ".dt-layout-table", classToRemove: "row mt-2" },
          { selector: ".dt-layout-full", classToRemove: "col-md col-12", classToAdd: "table-responsive" }
        ];

        elementsToModify.forEach(function (config) {
          wrapper.querySelectorAll(config.selector).forEach(function (element) {
            if (config.classToRemove) {
              config.classToRemove.split(" ").forEach(function (className) {
                element.classList.remove(className);
              });
            }
            if (config.classToAdd) {
              config.classToAdd.split(" ").forEach(function (className) {
                element.classList.add(className);
              });
            }
          });
        });

        const searchInput = wrapper.querySelector('.dt-search .form-control');
        if (searchInput) {
          searchInput.placeholder = "Search KPI";
        }

        const mountFilterPanel = function () {
          const host = document.getElementById("filterCollapse");
          const filterBtn = wrapper.querySelector(".dt-filter-btn");
          if (!host || !filterBtn) {
            return;
          }

          const toolbarRow =
            filterBtn.closest(".dt-layout-row") ||
            filterBtn.closest(".row") ||
            filterBtn.closest(".dt-layout-end")?.parentElement;

          if (toolbarRow && host.previousElementSibling !== toolbarRow) {
            toolbarRow.insertAdjacentElement("afterend", host);
            host.classList.add("px-3");
          }
        };

        mountFilterPanel();

        const dtButtons = wrapper.querySelector(".dt-buttons");
        if (dtButtons) {
          const eyeBtn = dtButtons.querySelector(".dt-eye-btn");
          const filterBtn = dtButtons.querySelector(".dt-filter-btn");
          if (eyeBtn && filterBtn && !eyeBtn.parentElement.classList.contains("btn-group")) {
            const group = document.createElement("div");
            group.className = "btn-group";
            eyeBtn.parentNode.insertBefore(group, eyeBtn);
            group.appendChild(eyeBtn);
            group.appendChild(filterBtn);

            [eyeBtn, filterBtn].forEach(function (btn) {
              btn.classList.remove("ms-2", "mx-1", "mx-2", "mx-3", "mx-4", "ms-3");
              btn.style.margin = "0";
            });
          }
        }
      }, 100);
    }

    function bindMetricActions() {
      const wrapper = tableEl?.closest(".dt-container") || tableEl?.parentElement || tableEl;
      if (!wrapper) {
        return;
      }

      wrapper.querySelectorAll(".metric-duplicate").forEach(function (button) {
        if (button.dataset.bound === "true") {
          return;
        }

        button.dataset.bound = "true";
        button.addEventListener("click", function () {
          duplicateMetricRow(String(button.dataset.rowId || "").trim());
        });
      });

      wrapper.querySelectorAll(".metric-edit").forEach(function (button) {
        if (button.dataset.bound === "true") {
          return;
        }

        button.dataset.bound = "true";
        button.addEventListener("click", function () {
          editMetricRow(String(button.dataset.rowId || "").trim());
        });
      });

      wrapper.querySelectorAll(".metric-remove").forEach(function (button) {
        if (button.dataset.bound === "true") {
          return;
        }

        button.dataset.bound = "true";
        button.addEventListener("click", function () {
          if (button.classList.contains("disabled")) {
            return;
          }
          removeMetricRow(String(button.dataset.rowId || "").trim());
        });
      });
    }

    const tableEl = form.querySelector("#goal-kpi-table");
    if (tableEl && window.DataTable && !goalKpiDataTable) {
      goalKpiDataTable = new window.DataTable(tableEl, {
        data: goalKpiRows,
        responsive: {
          details: {
            display: window.DataTable.Responsive.display.modal({
              header: function (row) {
                const data = row.data() || {};
                return '<h5 class="modal-title">Strategic KPI Detail - ' + escapeHtml(data.metric || "") + "</h5>";
              }
            }),
            type: "column",
            renderer: function (api, rowIdx, columns) {
              const data = window.jQuery.map(columns, function (col) {
                return col.hidden && col.columnIndex !== 6
                  ? '<tr data-dt-row="' + col.rowIndex + '" data-dt-column="' + col.columnIndex + '">' +
                      "<td>" + escapeHtml(col.title) + ":</td>" +
                      "<td>" + col.data + "</td>" +
                    "</tr>"
                  : "";
              }).join("");

              return data ? window.jQuery('<table class="table"/>').append(data) : false;
            }
          }
        },
        processing: true,
        serverSide: false,
        deferRender: true,
        autoWidth: false,
        columns: [
          { data: null, defaultContent: "" },
          { data: "goalMetricType" },
          { data: "metric" },
          { data: "unitOfMeasure" },
          { data: "year" },
          { data: "yearSpanLabel" },
          { data: null, className: "text-end" }
        ],
        columnDefs: [
          {
            className: "control",
            orderable: false,
            searchable: false,
            responsivePriority: 1000,
            targets: 0
          },
          {
            targets: 1,
            responsivePriority: 1,
            render: function (data) {
              return '<span class="badge bg-label-primary">' + escapeHtml(data || "-") + "</span>";
            }
          },
          {
            targets: 2,
            responsivePriority: 1,
            render: function (data) {
              return '<span class="text-heading fw-medium">' + escapeHtml(data || "-") + "</span>";
            }
          },
          {
            targets: 3,
            render: function (data) {
              return '<span class="text-body">' + escapeHtml(data || "-") + "</span>";
            }
          },
          {
            targets: 4,
            render: function (data) {
              return '<span class="fw-medium">' + escapeHtml(data || "-") + "</span>";
            }
          },
          {
            targets: 5,
            render: function (data, type, full) {
              return '<span class="badge bg-label-secondary">' + escapeHtml(data || (full?.yearCount ? String(full.yearCount) + " Years" : "-")) + "</span>";
            }
          },
          {
            targets: -1,
            responsivePriority: 1,
            className: "all text-end",
            orderable: false,
            searchable: false,
            render: function (data, type, full) {
              const rowId = escapeHtml(full?.uid || "");
              return '<div class="d-flex align-items-center justify-content-end">' +
                '<a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">' +
                  '<i class="icon-base bx bx-dots-vertical-rounded icon-md"></i>' +
                "</a>" +
                '<div class="dropdown-menu dropdown-menu-end m-0">' +
                  '<a href="javascript:;" class="dropdown-item metric-edit" data-row-id="' + rowId + '">Edit</a>' +
                  '<a href="javascript:;" class="dropdown-item metric-duplicate" data-row-id="' + rowId + '">Duplicate</a>' +
                  '<a href="javascript:;" class="dropdown-item metric-remove' + (goalKpiRows.length <= 1 ? " disabled" : "") + '" data-row-id="' + rowId + '">Remove</a>' +
                "</div>" +
              "</div>";
            }
          }
        ],
        order: [[2, "asc"]],
        layout: {
          topStart: {
            rowClass: "row m-3 justify-content-between",
            features: [
              {
                pageLength: {
                  menu: [5, 10, 25, 50],
                  text: "_MENU_"
                }
              }
            ]
          },
          topEnd: {
            rowClass: "row mx-3 justify-content-between",
            features: [
              {
                search: {
                  placeholder: "Search KPI",
                  text: "_INPUT_"
                }
              },
              {
                buttons: [
                  {
                    extend: "collection",
                    className: "btn btn-label-secondary dropdown-toggle",
                    text: '<i class="icon-base bx bx-export icon-sm me-2"></i>Export',
                    buttons: ["print", "csv", "excel", "pdf", "copy"]
                  },
                  {
                    text: '<i class="icon-base bx bx-show icon-sm"></i>',
                    className: "btn btn-icon btn-label-secondary dt-eye-btn",
                    action: function () {
                      fixGoalKpiTableLayout();
                    }
                  },
                  {
                    text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                    className: "btn btn-icon btn-label-secondary dt-filter-btn",
                    action: function () {
                      const searchEl = tableEl.closest(".dt-container")?.querySelector('.dt-search input[type="search"]');
                      if (searchEl) {
                        searchEl.focus();
                        searchEl.select();
                      }
                    }
                  },
                  {
                    text: '<i class="icon-base bx bx-plus icon-sm me-sm-2"></i>Add Primary KPI',
                    className: "btn btn-primary",
                    action: function () {
                      openGoalKpiModal();
                    }
                  }
                ]
              }
            ]
          },
          bottomStart: {
            rowClass: "row mx-3 justify-content-between",
            features: ["info"]
          },
          bottomEnd: {
            paging: { firstLast: false }
          }
        },
        language: {
          paginate: {
            next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
            previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
          }
        },
        initComplete: function () {
          fixGoalKpiTableLayout();
          bindMetricActions();
        },
        drawCallback: function () {
          fixGoalKpiTableLayout();
          bindMetricActions();
        }
      });
    }

    form.querySelector("#goal-planning-start-year")?.addEventListener("change", function () {
      syncPlanningDatePairConstraints();
      syncKpiTableYears();
    });
    form.querySelector("#goal-planning-end-year")?.addEventListener("change", function () {
      syncPlanningDatePairConstraints();
      syncKpiTableYears();
    });
    document.getElementById("goal-kpi-yearly-toggle-runtime")?.addEventListener("click", function () {
      if (!goalKpiModalEl) {
        return;
      }

      goalKpiModalEl.dataset.showRuntime = goalKpiModalEl.dataset.showRuntime === "true" ? "false" : "true";
      syncGoalKpiRuntimeFields();
    });
    document.getElementById("goal-kpi-yearly-fill-flat")?.addEventListener("click", applyGoalKpiFlatFill);
    document.getElementById("goal-kpi-yearly-copy-prev")?.addEventListener("click", copyGoalKpiPreviousRows);
    document.getElementById("goal-kpi-yearly-fill-linear")?.addEventListener("click", interpolateGoalKpiTargets);
    document.getElementById("goal-kpi-yearly-clear")?.addEventListener("click", clearGoalKpiYearRows);
    document.getElementById("goal-budget-enabled")?.addEventListener("change", syncBudgetEnvelopeUi);
    document.getElementById("goal-budget-fill-column")?.addEventListener("click", function () {
      openGoalKpiYearlyValueModal({
        title: "Fill Budget Column",
        message: "Apply the same value to the selected budget column for all years.",
        label1: "Column Value",
        placeholder1: "Enter value",
        onSave: function (values) {
          const value = values[0];
          if (value === null || value === undefined) {
            return;
          }

          fillBudgetColumn(document.getElementById("goal-budget-helper-column")?.value || "revenueTarget", value);
        }
      });
    });
    document.getElementById("goal-budget-interpolate")?.addEventListener("click", function () {
      interpolateBudgetColumn(document.getElementById("goal-budget-helper-column")?.value || "revenueTarget");
    });
    document.getElementById("goal-budget-copy-down")?.addEventListener("click", function () {
      copyBudgetColumnDown(document.getElementById("goal-budget-helper-column")?.value || "revenueTarget");
    });
    document.getElementById("goal-budget-clear-column")?.addEventListener("click", function () {
      clearBudgetColumn(document.getElementById("goal-budget-helper-column")?.value || "revenueTarget");
    });
    document.getElementById("goal-kpi-yearly-value-modal-save")?.addEventListener("click", function () {
      if (!goalKpiYearlyValueModalState?.onSave) {
        goalKpiYearlyValueModal?.hide();
        return;
      }

      const field1Value = parseDecimalInputValue(document.getElementById("goal-kpi-yearly-value-modal-field-1")?.value);
      const field2WrapEl = document.getElementById("goal-kpi-yearly-value-modal-field-2-wrap");
      const needsSecond = field2WrapEl && !field2WrapEl.classList.contains("d-none");
      const field2Value = needsSecond
        ? parseDecimalInputValue(document.getElementById("goal-kpi-yearly-value-modal-field-2")?.value)
        : null;

      goalKpiYearlyValueModalState.onSave([field1Value, field2Value]);
      goalKpiYearlyValueModal.hide();
    });
    document.getElementById("goal-kpi-modal-save")?.addEventListener("click", function () {
      if (!validateGoalKpiModal()) {
        return;
      }

      const metricType = String(document.getElementById("goal-kpi-modal-type")?.value || "").trim() || "Primary KPI";
      const metric = String(document.getElementById("goal-kpi-modal-metric")?.value || "").trim();
      const unitOfMeasure = String(document.getElementById("goal-kpi-modal-unit")?.value || "").trim();
      const payload = {
        goalMetricType: metricType,
        metric: metric || "Untitled KPI",
        unitOfMeasure: unitOfMeasure || "-",
        metricDefinitionId: String(document.getElementById("goal-kpi-modal-definition-id")?.value || "").trim(),
        aggregationMethod: String(document.getElementById("goal-kpi-modal-aggregation")?.value || "").trim(),
        polarity: String(document.getElementById("goal-kpi-modal-polarity")?.value || "").trim(),
        thresholdModel: String(document.getElementById("goal-kpi-modal-threshold-model")?.value || "").trim(),
        reportingFrequency: String(document.getElementById("goal-kpi-modal-reporting-frequency")?.value || "").trim(),
        cascadeMetric: String(document.getElementById("goal-kpi-governance-cascade")?.value || "true").trim(),
        metricOrigin: String(document.getElementById("goal-kpi-governance-origin")?.value || "Local").trim(),
        metricRole: String(document.getElementById("goal-kpi-governance-role")?.value || "Strategic").trim(),
        restrictionMode: String(document.getElementById("goal-kpi-governance-restriction")?.value || "").trim(),
        rollupEligible: String(document.getElementById("goal-kpi-governance-rollup")?.value || "true").trim(),
        yearlyPlanRows: collectGoalKpiYearlyPlanRows()
      };

      if (activeGoalKpiEditRowId) {
        updateMetricRow(activeGoalKpiEditRowId, payload);
      } else {
        addMetricRow(payload);
      }

      if (goalKpiModal) {
        goalKpiModal.hide();
      }
    });

    syncKpiTableYears();
    renderBudgetYearRows();
    syncBudgetEnvelopeUi();
  }

  function syncOwnershipSummary() {
    const workbook = window.enterpriseWorkbookOptions || {};
    const companyId = String(form.querySelector("#goal-owner-company")?.value || "").trim();
    const positionId = String(form.querySelector("#goal-owner-role")?.value || "").trim();
    const personId = String(form.querySelector("#goal-owner-person")?.value || "").trim();

    const companyLabel = typeof workbook.companyDisplayName === "function"
      ? workbook.companyDisplayName(companyId)
      : companyId;
    const positionLabel = typeof workbook.positionDisplayName === "function"
      ? workbook.positionDisplayName(positionId)
      : positionId;
    const personLabel = typeof workbook.userDisplayName === "function"
      ? workbook.userDisplayName(personId)
      : personId;

    const summaryEl = form.querySelector("#goal-owner-accountable-display");
    if (summaryEl) {
      summaryEl.value = [
        companyLabel || "-",
        positionLabel || "-",
        personLabel || "-"
      ].join(" -> ");
    }

    const hiddenOwnerEl = form.querySelector("#goal-owner");
    if (hiddenOwnerEl) {
      hiddenOwnerEl.value = positionId || personId;
    }
  }

  function getSelectedValues(selectEl) {
    if (!selectEl) {
      return [];
    }

    return Array.from(selectEl.selectedOptions || []).map(function (option) {
      return String(option.value || "").trim();
    }).filter(Boolean);
  }

  function syncCompanyApplicabilitySummary() {
    const workbook = window.enterpriseWorkbookOptions || {};
    const mode = String(form.querySelector("#goal-scope-mode")?.value || "").trim();
    const appliesAll = Boolean(form.querySelector("#goal-applies-to-all-companies")?.checked);
    const companyIds = getSelectedValues(form.querySelector("#goal-applicable-companies"));
    const businessUnit = String(form.querySelector("#goal-business-unit")?.value || "").trim();
    const region = String(form.querySelector("#goal-region")?.value || "").trim();

    const companyNames = companyIds.map(function (id) {
      return typeof workbook.companyDisplayName === "function" ? workbook.companyDisplayName(id) : id;
    }).filter(Boolean);

    const modeLabel = mode === "Enterprise"
      ? "Enterprise"
      : mode === "AppliesToSelectedCompanies"
        ? "Selected Companies"
        : (mode || "-");

    const parts = [modeLabel];
    if (appliesAll) {
      parts.push("All Companies");
    } else if (companyNames.length) {
      parts.push(companyNames.join(", "));
    }
    if (businessUnit) {
      parts.push("BU/Function: " + businessUnit);
    }
    if (region) {
      parts.push("Region: " + region);
    }

    const summary = parts.filter(Boolean).join(" | ") || "-";
    const summaryEl = form.querySelector("#goal-related-entity-scope-summary");
    const summaryDisplayEl = form.querySelector("#goal-related-entity-scope-summary-display");
    const planningPreviewEl = form.querySelector("#goal-planning-scope-preview");
    const hiddenScopeEl = form.querySelector("#goal-entity-scope");

    if (summaryEl) {
      summaryEl.value = summary;
    }
    if (summaryDisplayEl) {
      summaryDisplayEl.textContent = summary;
    }
    if (planningPreviewEl) {
      planningPreviewEl.value = summary;
    }
    if (hiddenScopeEl) {
      hiddenScopeEl.value = summary;
    }
  }

  function syncCompanyApplicabilityMode() {
    const mode = String(form.querySelector("#goal-scope-mode")?.value || "").trim();
    const appliesAllEl = form.querySelector("#goal-applies-to-all-companies");
    const applicableCompaniesEl = form.querySelector("#goal-applicable-companies");
    const applicableHintEl = form.querySelector("#goal-applicable-companies-hint");

    if (appliesAllEl) {
      appliesAllEl.checked = mode === "Enterprise";
    }

    if (applicableCompaniesEl) {
      const allOptions = Array.from(applicableCompaniesEl.options || []);
      if (mode === "Enterprise") {
        allOptions.forEach(function (option) {
          option.selected = true;
        });
        applicableCompaniesEl.disabled = true;
      } else {
        applicableCompaniesEl.disabled = false;
      }
    }

    if (applicableHintEl) {
      applicableHintEl.textContent = mode === "Enterprise"
        ? "All companies are selected automatically for Enterprise applicability."
        : "Select one or more companies for scope.";
    }

    if (window.jQuery && applicableCompaniesEl) {
      const $applicable = window.jQuery(applicableCompaniesEl);
      if ($applicable.hasClass("select2-hidden-accessible")) {
        $applicable.trigger("change.select2");
      }
    }

    syncCompanyApplicabilitySummary();
  }

  async function initCompanyApplicabilityOptions() {
    const workbook = window.enterpriseWorkbookOptions || {};

    try {
      await workbook.ensureLookupsLoaded?.();
      await workbook.ensureCompaniesLoaded?.();
    } catch (_) {
    }

    fillSelect(
      form.querySelector("#goal-scope-mode"),
      [
        { value: "Enterprise", label: "Enterprise" },
        { value: "AppliesToSelectedCompanies", label: "Selected Companies" }
      ],
      "Select...",
      "Enterprise"
    );

    fillSelect(
      form.querySelector("#goal-primary-company"),
      typeof workbook.companyOptions === "function" ? workbook.companyOptions() : [],
      "Select company..."
    );

    fillSelect(
      form.querySelector("#goal-applicable-companies"),
      typeof workbook.companyOptions === "function" ? workbook.companyOptions() : [],
      null
    );

    initSelect2For(form.querySelector("#goal-applicable-companies"), {
      placeholder: "Search and select applicable companies...",
      closeOnSelect: false,
      allowClear: false
    });

    form.querySelector("#goal-scope-mode")?.addEventListener("change", syncCompanyApplicabilityMode);
    form.querySelector("#goal-applicable-companies")?.addEventListener("change", syncCompanyApplicabilitySummary);
    form.querySelector("#goal-business-unit")?.addEventListener("input", syncCompanyApplicabilitySummary);
    form.querySelector("#goal-region")?.addEventListener("input", syncCompanyApplicabilitySummary);
    form.querySelector("#goal-primary-company")?.addEventListener("change", syncCompanyApplicabilitySummary);

    syncCompanyApplicabilityMode();
  }

  function fillOwnershipCompanies() {
    const workbook = window.enterpriseWorkbookOptions || {};
    fillSelect(
      form.querySelector("#goal-owner-company"),
      typeof workbook.companyOptions === "function" ? workbook.companyOptions() : [],
      "Select owner company / org..."
    );
  }

  function fillOwnershipPositions() {
    const workbook = window.enterpriseWorkbookOptions || {};
    fillSelect(
      form.querySelector("#goal-owner-role"),
      typeof workbook.positionOptions === "function" ? workbook.positionOptions() : [],
      "Select position..."
    );
  }

  function fillOwnershipPeople() {
    const workbook = window.enterpriseWorkbookOptions || {};
    const hintEl = form.querySelector("#goal-owner-person-hint");
    const personEl = form.querySelector("#goal-owner-person");

    fillSelect(
      personEl,
      (typeof workbook.userOptions === "function" ? workbook.userOptions() : []).map(function (user) {
        return {
          value: String(user?.id || user?.value || "").trim(),
          label: String(user?.fullName || user?.label || user?.name || user?.value || "").trim()
        };
      }).filter(function (row) {
        return row.value && row.label;
      }),
      "Select current owner person..."
    );

    if (hintEl) {
      hintEl.textContent = "";
    }

    syncOwnershipSummary();
  }

  async function initOwnershipOptions() {
    const workbook = window.enterpriseWorkbookOptions || {};

    try {
      await workbook.ensureLookupsLoaded?.();
      await workbook.ensureCompaniesLoaded?.();
      await workbook.ensurePositionsLoaded?.();
      await workbook.ensureUsersLoaded?.();
    } catch (_) {
    }

    fillOwnershipCompanies();
    fillOwnershipPositions();
    fillOwnershipPeople();
    syncOwnershipSummary();

    const companyEl = form.querySelector("#goal-owner-company");
    const positionEl = form.querySelector("#goal-owner-role");
    const personEl = form.querySelector("#goal-owner-person");

    companyEl?.addEventListener("change", function () {
      syncOwnershipSummary();
    });

    positionEl?.addEventListener("change", function () {
      syncOwnershipSummary();
    });

    personEl?.addEventListener("change", function () {
      syncOwnershipSummary();
    });
  }

  async function refreshGoalIdPreview() {
    const idEl = form.querySelector("#goal-id");
    if (!idEl) {
      return;
    }

    idEl.readOnly = true;
    idEl.disabled = true;
    idEl.placeholder = "Loading preview...";

    try {
      const preview = await window.strategyEnterpriseMetaApi?.runtimeIdPreview?.();
      idEl.value = preview?.goalId || "";
      idEl.placeholder = "";
    } catch (_) {
      idEl.value = "";
      idEl.placeholder = "Assigned on save";
    }
  }

  function getFieldContainer(field) {
    return field?.closest(".col-12, .col-md-3, .col-md-4, .col-md-6, .col-md-8, .col-sm-6, .col-lg-12, .col-lg-6") || field?.parentElement || null;
  }

  function clearFieldValidation() {
    form.querySelectorAll(".is-invalid").forEach(function (field) {
      field.classList.remove("is-invalid");
    });
    form.querySelectorAll(".stepper-inline-error").forEach(function (node) {
      node.remove();
    });
    const alertHost = document.getElementById("goal-stepper-validation-alert-host");
    if (alertHost) {
      alertHost.innerHTML = "";
      alertHost.classList.add("d-none");
    }
  }

  function setFieldInvalid(fieldId, message) {
    const field = document.getElementById(fieldId);
    if (!field) {
      return;
    }

    field.classList.add("is-invalid");
    if (fieldId === "goal-kpi-table" || fieldId === "goal-budget-year-table") {
      return;
    }
    const container = getFieldContainer(field);
    if (!container) {
      return;
    }

    let feedback = container.querySelector(".stepper-inline-error");
    if (!feedback) {
      feedback = document.createElement("div");
      feedback.className = "invalid-feedback stepper-inline-error d-block";
      container.appendChild(feedback);
    }
    feedback.textContent = message;
  }

  function isValidAbsoluteUrl(value) {
    const text = String(value || "").trim();
    if (!text) {
      return true;
    }
    try {
      const url = new URL(text);
      return url.protocol === "http:" || url.protocol === "https:";
    } catch (_) {
      return false;
    }
  }

  function validateIdentityStep() {
    const errors = [];
    if (!String(document.getElementById("goal-name")?.value || "").trim()) errors.push(["goal-name", "Goal is required."]);
    if (!String(document.getElementById("goal-category")?.value || "").trim()) errors.push(["goal-category", "Goal Type is required."]);
    if (!String(document.getElementById("goal-strategic-theme")?.value || "").trim()) errors.push(["goal-strategic-theme", "Strategic Theme / Pillar is required."]);
    if (!String(document.getElementById("goal-priority")?.value || "").trim()) errors.push(["goal-priority", "Priority is required."]);
    if (!String(document.getElementById("goal-statement")?.value || "").trim()) errors.push(["goal-statement", "Goal Statement is required."]);
    return errors;
  }

  function validateOwnershipStep() {
    const errors = [];
    if (!String(document.getElementById("goal-owner-company")?.value || "").trim()) errors.push(["goal-owner-company", "Owner Company / Org is required."]);
    if (!String(document.getElementById("goal-owner-role")?.value || "").trim()) errors.push(["goal-owner-role", "Owner Position is required."]);
    if (!String(document.getElementById("goal-owner-person")?.value || "").trim()) errors.push(["goal-owner-person", "Current Owner Person is required."]);
    return errors;
  }

  function validatePlanningStep() {
    const errors = [];
    const period = String(document.getElementById("goal-strategy-period")?.value || "").trim();
    const start = String(document.getElementById("goal-planning-start-year")?.value || "").trim();
    const end = String(document.getElementById("goal-planning-end-year")?.value || "").trim();
    if (!period) errors.push(["goal-strategy-period", "Strategy Period is required."]);
    if (!start) errors.push(["goal-planning-start-year", "Start Date is required."]);
    if (!end) errors.push(["goal-planning-end-year", "End Date is required."]);
    if (start && end && new Date(end) < new Date(start)) errors.push(["goal-planning-end-year", "End Date must be after Start Date."]);
    return errors;
  }

  function validateCompanyStep() {
    const errors = [];
    const scopeMode = String(document.getElementById("goal-scope-mode")?.value || "").trim();
    const applicable = Array.from(document.getElementById("goal-applicable-companies")?.selectedOptions || []).map(function (option) {
      return String(option.value || "").trim();
    }).filter(Boolean);
    if (!scopeMode) errors.push(["goal-scope-mode", "Applicability Mode is required."]);
    if (scopeMode === "AppliesToSelectedCompanies" && !applicable.length) errors.push(["goal-applicable-companies", "At least one Applicable Company is required for selected-company applicability."]);
    return errors;
  }

  function validateKpiStep() {
    const errors = [];
    if (!goalKpiRows.length) errors.push(["goal-kpi-table", "Primary KPI / Metric is required."]);
    return errors;
  }

  function validateBudgetStep() {
    const errors = [];
    if (!isBudgetEnvelopeEnabled()) {
      return errors;
    }
    const years = buildTargetYears();
    const budgetRows = collectYearlyBudgetsFromDom();
    const invalidYear = budgetRows.some(function (row) {
      return years.length && !years.includes(Number(row.year));
    });
    if (invalidYear) errors.push(["goal-budget-year-table", "Yearly budget contains out-of-range years."]);
    return errors;
  }

  function validateGovernanceStep() {
    const errors = [];
    if (!isValidAbsoluteUrl(document.getElementById("goal-evidence-reference")?.value)) {
      errors.push(["goal-evidence-reference", "Must be a valid URL."]);
    }
    return errors;
  }

  function toStoredScopeMode(value) {
    const raw = String(value || "").trim();
    if (raw === "AppliesToSelectedCompanies") {
      return "MultiCompany";
    }
    return raw || "Enterprise";
  }

  function resolveSelectedStrategyPeriodCompanyId() {
    const periodId = String(document.getElementById("goal-strategy-period")?.value || "").trim();
    if (!periodId) {
      return "";
    }

    const selectedPeriod = strategyPeriodsById.get(periodId) || null;
    return String(selectedPeriod?.companyId || selectedPeriod?.CompanyId || "").trim();
  }

  async function ensureSelectedStrategyPeriodMeta() {
    const periodId = String(document.getElementById("goal-strategy-period")?.value || "").trim();
    if (!periodId) {
      return null;
    }

    const cached = strategyPeriodsById.get(periodId) || null;
    const cachedCompanyId = String(cached?.companyId || cached?.CompanyId || "").trim();
    if (cached && cachedCompanyId) {
      return cached;
    }

    if (typeof window.strategyPlanningApi?.getStrategyPeriod !== "function") {
      return cached;
    }

    try {
      const detail = await window.strategyPlanningApi.getStrategyPeriod(periodId);
      if (detail) {
        strategyPeriodsById.set(periodId, detail);
      }
      return detail || cached;
    } catch (_) {
      return cached;
    }
  }

  function collectCreateRequest() {
    const scopeModeUi = String(document.getElementById("goal-scope-mode")?.value || "Enterprise").trim();
    const scopeModeCode = toStoredScopeMode(scopeModeUi);
    const selectedApplicableCompanyIds = Array.from(document.getElementById("goal-applicable-companies")?.selectedOptions || [])
      .map(function (option) { return String(option.value || "").trim(); })
      .filter(Boolean);
    const ownerCompanyId = String(document.getElementById("goal-owner-company")?.value || "").trim() || null;
    const selectedStrategyPeriodCompanyId = resolveSelectedStrategyPeriodCompanyId() || null;
    const primaryCompanyId = selectedStrategyPeriodCompanyId || ownerCompanyId || null;
    const applicableCompanyIds = scopeModeCode === "Enterprise"
      ? []
      : Array.from(new Set(
        selectedApplicableCompanyIds
          .concat(selectedStrategyPeriodCompanyId ? [selectedStrategyPeriodCompanyId] : [])
          .filter(Boolean)
      ));
    const ownerRoleId = String(document.getElementById("goal-owner-role")?.value || "").trim() || null;
    const ownerPersonId = String(document.getElementById("goal-owner-person")?.value || "").trim() || null;
    const metrics = goalKpiRows.map(function (row, index) {
      const yearlyValues = Array.isArray(row.yearlyPlanRows) ? row.yearlyPlanRows : [];
      const sortedYears = yearlyValues.slice().sort(function (left, right) {
        return Number(left.year || 0) - Number(right.year || 0);
      });
      const baselineValue = sortedYears.length ? (sortedYears[0].targetValue ?? null) : null;
      const targetValue = sortedYears.length ? (sortedYears[sortedYears.length - 1].targetValue ?? null) : null;
      return {
        metricAssignmentId: row.uid || null,
        metricDefId: row.metricDefinitionId || row.metricDefId || null,
        metricDefinitionId: row.metricDefinitionId || row.metricDefId || null,
        metricName: row.metric || "",
        metricTypeCode: row.goalMetricType || "",
        metricType: row.goalMetricType || "",
        baselineValue,
        targetValue,
        unitOfMeasureCode: row.unitOfMeasure || "",
        unitOfMeasure: row.unitOfMeasure || "",
        aggregationMethodCode: row.aggregationMethod || row.aggregationMethodCode || "",
        aggregationMethod: row.aggregationMethod || row.aggregationMethodCode || "",
        polarityCode: row.polarity || row.directionPolarity || "",
        directionPolarity: row.polarity || row.directionPolarity || "",
        thresholdModelCode: row.thresholdModel || row.thresholdModelCode || "",
        thresholdModel: row.thresholdModel || row.thresholdModelCode || "",
        reportingFrequencyCode: row.reportingFrequency || row.reportingFrequencyCode || "",
        reportingFrequency: row.reportingFrequency || row.reportingFrequencyCode || "",
        cascadeMetric: String(row.cascadeMetric || "true") === "true",
        metricOrigin: row.metricOrigin || "Local",
        metricRole: row.metricRole || "Strategic",
        restrictionMode: row.restrictionMode || "GoalGovernedStructure",
        rollupEligible: String(row.rollupEligible || "true") === "true",
        yearlyValues: yearlyValues.map(function (item) {
          return {
            year: Number(item.year),
            baselineValue: item.baselineValue ?? null,
            targetValue: item.targetValue ?? null,
            actualValue: item.actualValue ?? null,
            forecastValue: item.forecastValue ?? null,
            thresholdMin: item.thresholdMin ?? null,
            thresholdMax: item.thresholdMax ?? null,
            commentary: String(item.commentary || "").trim() || null,
            thresholdCommentary: String(item.commentary || "").trim() || null
          };
        }),
        yearlyTargets: yearlyValues.map(function (item) {
          return {
            goalMetricId: row.uid || null,
            year: Number(item.year),
            targetValue: item.targetValue ?? null,
            thresholdMin: item.thresholdMin ?? null,
            thresholdMax: item.thresholdMax ?? null,
            commentary: String(item.commentary || "").trim() || null
          };
        }),
        strategicGoalMetricYearlyTargets: yearlyValues.map(function (item) {
          return {
            goalMetricId: row.uid || null,
            year: Number(item.year),
            targetValue: item.targetValue ?? null,
            thresholdMin: item.thresholdMin ?? null,
            thresholdMax: item.thresholdMax ?? null,
            commentary: String(item.commentary || "").trim() || null
          };
        }),
        sortOrder: index + 1
      };
    });

    const yearlyBudgets = collectYearlyBudgetsFromDom();

    return {
      goal: String(document.getElementById("goal-name")?.value || "").trim(),
      goalTitle: String(document.getElementById("goal-name")?.value || "").trim(),
      goalTypeId: String(document.getElementById("goal-category")?.value || "").trim(),
      categoryCode: String(document.getElementById("goal-category")?.value || "").trim(),
      strategicThemeId: String(document.getElementById("goal-strategic-theme")?.value || "").trim(),
      ownerId: ownerPersonId || ownerRoleId,
      ownerRole: ownerRoleId,
      ownerPositionId: ownerRoleId,
      ownerCompanyId: ownerCompanyId,
      ownerOrgId: ownerCompanyId,
      ownerPersonId,
      currentOwnerPersonId: ownerPersonId,
      accountableOwnerDisplay: String(document.getElementById("goal-owner-accountable-display")?.value || "").trim(),
      statusCode: "Draft",
      priorityCode: String(document.getElementById("goal-priority")?.value || "").trim(),
      goalStatement: String(document.getElementById("goal-statement")?.value || "").trim(),
      planning: {
        startDate: String(document.getElementById("goal-planning-start-year")?.value || "").trim() || null,
        endDate: String(document.getElementById("goal-planning-end-year")?.value || "").trim() || null,
        startYear: null,
        endYear: null,
        strategyPeriodId: String(document.getElementById("goal-strategy-period")?.value || "").trim() || null,
        relatedEntityScope: String(document.getElementById("goal-entity-scope")?.value || "").trim(),
        changeLogRef: String(document.getElementById("goal-change-log-ref")?.value || "").trim()
      },
      businessUnit: String(document.getElementById("goal-business-unit")?.value || "").trim(),
      region: String(document.getElementById("goal-region")?.value || "").trim(),
      companyScope: {
        scopeModeCode,
        appliesToSelectedCompaniesFlag: applicableCompanyIds.length > 0,
        appliesToAllCompaniesFlag: scopeModeCode === "Enterprise",
        primaryCompanyId: primaryCompanyId,
        applicableCompanyIds,
        relatedEntityScopeSummary: String(document.getElementById("goal-related-entity-scope-summary")?.value || "").trim()
      },
      yearlyBudgets: yearlyBudgets,
      budgetEnvelopes: yearlyBudgets.map(function (row) {
        return {
          year: row.year,
          revenueTarget: row.revenueTarget,
          ebitdaTarget: row.ebitdaTarget,
          capexEnvelope: row.capexEnvelope,
          opexEnvelope: row.opexEnvelope,
          savingsTarget: row.savingsTarget,
          fundingPool: row.fundingPoolEnvelope ?? row.fundingPool ?? null,
          commentary: row.commentary || null
        };
      }),
      budgetEnvelopeEnabled: isBudgetEnvelopeEnabled(),
      applicabilityMode: scopeModeCode,
      appliesToAllCompanies: scopeModeCode === "Enterprise",
      applicableCompanyIds,
      metrics,
      governance: {
        decisionReference: String(document.getElementById("goal-decision-reference")?.value || "").trim() || null,
        evidenceLink: String(document.getElementById("goal-evidence-reference")?.value || "").trim() || null
      },
      _startYearRaw: String(document.getElementById("goal-planning-start-year")?.value || "").trim(),
      _endYearRaw: String(document.getElementById("goal-planning-end-year")?.value || "").trim()
    };
  }

  async function collectCreateRequestForSubmit() {
    const selectedPeriod = await ensureSelectedStrategyPeriodMeta();
    const payload = collectCreateRequest();
    const periodCompanyId = String(selectedPeriod?.companyId || selectedPeriod?.CompanyId || "").trim();

    if (periodCompanyId) {
      payload.companyScope = payload.companyScope || {};
      payload.companyScope.primaryCompanyId = periodCompanyId;

      if (String(payload.companyScope.scopeModeCode || "").trim() !== "Enterprise") {
        const applicableIds = Array.isArray(payload.companyScope.applicableCompanyIds)
          ? payload.companyScope.applicableCompanyIds.slice()
          : [];
        if (!applicableIds.includes(periodCompanyId)) {
          applicableIds.push(periodCompanyId);
        }
        payload.companyScope.applicableCompanyIds = applicableIds;
        payload.applicableCompanyIds = applicableIds.slice();
      } else {
        payload.companyScope.applicableCompanyIds = [];
        payload.applicableCompanyIds = [];
      }
    }

    return payload;
  }

  function resolveSavedGoalIdentity(result, fallbackGoalId) {
    const data = result?.goal || result?.Goal || result?.data || result || {};
    const id = [
      data?.id, data?.goalId, data?.goalID,
      result?.id, result?.goalId, result?.goalID,
      fallbackGoalId
    ].map(function (value) { return String(value || "").trim(); }).find(Boolean) || "";
    const version = [data?.version, result?.version]
      .map(function (value) { return Number(value); })
      .find(function (value) { return Number.isFinite(value) && value > 0; }) || null;
    return { id, version };
  }

  function getBackendErrors(err, fallbackMessage) {
    const defaultMessage = String(fallbackMessage || "Save failed.");
    const utils = window.enterpriseModalFormUtils;
    if (typeof utils?.backendErrors === "function") {
      const list = utils.backendErrors(err, defaultMessage);
      if (Array.isArray(list) && list.length) {
        return list;
      }
    }

    const responseErrors = err?.responseJSON?.errors || err?.errors || err?.data?.errors;
    if (responseErrors && typeof responseErrors === "object") {
      const flattened = [];
      Object.keys(responseErrors).forEach(function (key) {
        const value = responseErrors[key];
        if (Array.isArray(value)) {
          value.forEach(function (message) {
            if (message) {
              flattened.push(String(message));
            }
          });
        } else if (value) {
          flattened.push(String(value));
        }
      });
      if (flattened.length) {
        return flattened;
      }
    }

    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, defaultMessage)
      || err?.message
      || defaultMessage;
    return [String(message)];
  }

  function inferFieldIdFromErrorMessage(message) {
    const text = String(message || "").toLowerCase();
    if (!text) {
      return "";
    }

    const lookup = [
      ["goal-name", ["goal title", "goal name", "goal is required", "name is required"]],
      ["goal-category", ["goal type", "category"]],
      ["goal-strategic-theme", ["strategic theme", "pillar"]],
      ["goal-priority", ["priority"]],
      ["goal-statement", ["goal statement", "statement"]],
      ["goal-owner-company", ["owner company", "owner org", "owner company / org", "ownercompanyid", "ownerorgid"]],
      ["goal-owner-role", ["owner position", "owner role", "ownerrole", "ownerpositionid"]],
      ["goal-owner-person", ["owner person", "current owner person", "ownerpersonid", "currentownerpersonid"]],
      ["goal-strategy-period", ["strategy period", "strategyperiodid"]],
      ["goal-planning-start-year", ["start date", "planning.startdate", "startyear"]],
      ["goal-planning-end-year", ["end date", "planning.enddate", "endyear"]],
      ["goal-scope-mode", ["applicability mode", "scopemodecode"]],
      ["goal-applicable-companies", ["applicable compan", "applicablecompanyids"]],
      ["goal-kpi-table", ["metric", "kpi", "yearly target", "metricassignments"]],
      ["goal-budget-year-table", ["budget", "budgetyearlyvalues", "budget envelope"]],
      ["goal-evidence-reference", ["evidence", "evidencelink", "url"]],
      ["goal-version", ["version"]]
    ];

    for (let index = 0; index < lookup.length; index += 1) {
      const entry = lookup[index];
      if (entry[1].some(function (token) { return text.includes(token); })) {
        return entry[0];
      }
    }

    return "";
  }

  function validateStep(stepId) {
    switch (stepId) {
      case "goal-step-identity": return validateIdentityStep();
      case "goal-step-ownership": return validateOwnershipStep();
      case "goal-step-strategy": return validatePlanningStep();
      case "goal-step-kpi": return validateCompanyStep();
      case "goal-step-scope": return validateKpiStep();
      case "goal-step-budget": return validateBudgetStep();
      case "goal-step-governance": return validateGovernanceStep();
      default: return [];
    }
  }

  function showStepErrors(errors, options) {
    const settings = Object.assign({ showAlert: true }, options || {});
    clearFieldValidation();
    errors.forEach(function (entry) {
      setFieldInvalid(entry[0], entry[1]);
    });

    const alertHost = document.getElementById("goal-stepper-validation-alert-host");
    if (settings.showAlert && alertHost && errors.length) {
      const uniqueMessages = [...new Set(errors.map(function (entry) { return entry[1]; }).filter(Boolean))];
      alertHost.classList.remove("d-none");
      alertHost.innerHTML =
        '<div class="alert alert-danger alert-dismissible" role="alert">' +
          '<h4 class="alert-heading d-flex align-items-center flex-wrap gap-1">' +
            '<span class="alert-icon rounded-circle"><i class="icon-base bx bx-error"></i></span>Error!!' +
          "</h4>" +
          '<p>Please review the required fields before continuing.</p>' +
          "<hr>" +
          '<p class="mb-0">' + uniqueMessages.map(function (message) {
            return escapeHtml(message);
          }).join("<br>") + "</p>" +
          '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>' +
        "</div>";
    }

    const first = errors[0];
    if (first) {
      document.getElementById(first[0])?.focus?.();
    }
  }

  const stepper = wizard.bsStepper || new window.Stepper(wizard, {
    linear: true,
    animation: false
  });

  const nextButtons = form.querySelectorAll(".btn-next");
  const prevButtons = form.querySelectorAll(".btn-prev");
  const submitButton = form.querySelector(".btn-submit");

  nextButtons.forEach(function (button) {
    button.addEventListener("click", function (event) {
      event.preventDefault();
      const stepId = button.closest(".content")?.id || "";
      const errors = validateStep(stepId);
      if (errors.length) {
        showStepErrors(errors, {
          showAlert: !["goal-step-identity", "goal-step-ownership", "goal-step-strategy"].includes(stepId)
        });
        return;
      }
      clearFieldValidation();
      stepper.next();
    });
  });

  prevButtons.forEach(function (button) {
    button.addEventListener("click", function (event) {
      event.preventDefault();
      stepper.previous();
    });
  });

  if (submitButton) {
    submitButton.addEventListener("click", async function (event) {
      event.preventDefault();
      const allErrors = []
        .concat(validateIdentityStep())
        .concat(validateOwnershipStep())
        .concat(validatePlanningStep())
        .concat(validateCompanyStep())
        .concat(validateKpiStep())
        .concat(validateBudgetStep())
        .concat(validateGovernanceStep());

      if (allErrors.length) {
        showStepErrors(allErrors);
        if (window.showToast) {
          window.showToast("warning", allErrors[0][1]);
        }
        return;
      }

      const idleLabel = String(submitButton.textContent || "").trim() || "Create Goal Draft";
      const payload = await collectCreateRequestForSubmit();
      delete payload._startYearRaw;
      delete payload._endYearRaw;

      try {
        submitButton.disabled = true;
        submitButton.textContent = "Saving...";

        const result = await window.strategyGoalsApi.create(payload);
        const savedIdentity = resolveSavedGoalIdentity(
          result,
          String(document.getElementById("goal-id")?.value || "").trim()
        );

        if (savedIdentity.id) {
          const goalIdEl = document.getElementById("goal-id");
          if (goalIdEl) {
            goalIdEl.value = savedIdentity.id;
          }
        }

        clearFieldValidation();

        if (window.showToast) {
          window.showToast("success", "Goal draft saved successfully.");
        }

        if (savedIdentity.id) {
          window.location.assign("/management-governance/enterprise-strategy-business-performance/goals/" + encodeURIComponent(savedIdentity.id));
          return;
        }

        window.location.assign("/management-governance/enterprise-strategy-business-performance/goals");
      } catch (err) {
        const backendMessages = getBackendErrors(err, "Goal draft could not be saved.");
        const backendFieldErrors = backendMessages.map(function (message) {
          return [inferFieldIdFromErrorMessage(message), String(message)];
        });
        showStepErrors(backendFieldErrors, { showAlert: true });

        document.getElementById("goal-stepper-validation-alert-host")?.scrollIntoView({
          behavior: "smooth",
          block: "start"
        });

        if (window.showToast) {
          window.showToast("error", backendMessages[0] || "Goal draft could not be saved.");
        }
      } finally {
        submitButton.disabled = false;
        submitButton.textContent = idleLabel;
      }
    });
  }

  hydrateIdentityOptions();
  hydrateKpiModalOptions();
  bindGoalKpiModalValidationReset();
  initTooltips();
  syncCreationModeUi();
  updateTemplateSummaryCard();
  document.getElementById("goalBrowseCatalog")?.addEventListener("click", openGoalSourcePicker);
  document.getElementById("goalCreationMode")?.addEventListener("change", syncCreationModeUi);
  document.getElementById("goal-template-summary-clear")?.addEventListener("click", clearSelectedTemplate);
  void initOwnershipOptions();
  void initPlanningOptions();
  void initCompanyApplicabilityOptions();
  initKpiOptions();
  void refreshGoalIdPreview();
})();
