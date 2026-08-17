(function (window) {
  "use strict";

  function hasXlsx() {
    return Boolean(window.XLSX);
  }

  function toCsvValue(value) {
    return `"${String(value ?? "").replace(/"/g, '""')}"`;
  }

  function downloadBlob(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  function exportCsv(filename, headers, rows) {
    const lines = [headers.join(",")].concat(
      rows.map((row) => headers.map((h) => toCsvValue(row[h])).join(","))
    );
    downloadBlob(new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" }), filename);
  }

  function exportWorkbook(filename, sheets) {
    if (!hasXlsx()) throw new Error("XLSX exporter unavailable.");
    const wb = window.XLSX.utils.book_new();
    Object.entries(sheets || {}).forEach(([sheetName, rows]) => {
      const ws = window.XLSX.utils.json_to_sheet(rows || []);
      window.XLSX.utils.book_append_sheet(wb, ws, sheetName);
    });
    window.XLSX.writeFile(wb, filename);
  }

  function yearFromIso(v) {
    if (!v) return "";
    const y = String(v).slice(0, 4);
    return /^\d{4}$/.test(y) ? y : "";
  }

  async function buildAllSheets() {
    const [goals, objectives, initiatives, projects, connections] = await Promise.all([
      window.strategyGoalsApi?.list?.() || Promise.resolve({ items: [] }),
      window.strategyObjectivesApi?.list?.() || Promise.resolve({ items: [] }),
      window.initiativeStrategyApi?.list?.() || Promise.resolve({ items: [] }),
      window.projectStrategyApi?.list?.() || Promise.resolve({ items: [] }),
      window.strategyConnectionsApi?.list?.() || Promise.resolve({ items: [] })
    ]);

    const goalRows = (goals?.items || []).map((item) => {
      const metric = (item.metrics || [])[0] || {};
      return {
        "Goal ID": item.id || "",
        "Goal": item.name || "",
        "Goal Metric": metric.metricName || "",
        "Goal Metric Type": metric.metricType || "",
        "Goal Owner": item.owner || "",
        "Goal Status": item.status || "",
        "Goal Category": item.category || "",
        "Priority": item.priority || "",
        "Start Year": yearFromIso(item.planningHorizonStart),
        "End Year": yearFromIso(item.planningHorizonEnd),
        "Baseline Value": metric.baselineValue ?? "",
        "Target Value": metric.targetValue ?? "",
        "Unit of Measure": metric.unitOfMeasure || "",
        "Aggregation Method": metric.aggregationMethod || "",
        "Entity Scope": item.entityScope || "",
        "Decision Ref": item.decisionReference || "",
        "Evidence Ref": item.evidenceReference || "",
        "Version": item.version ?? 0
      };
    });
    const objectiveRows = (objectives?.items || []).map((x) => ({
      "Objective ID": x.id || "",
      "Objective": x.name || "",
      "Parent Goal ID": x.parentGoalId || "",
      "Owner": x.owner || "",
      "Status": x.status || "",
      "Type": x.type || "",
      "Priority": x.priority || "",
      "Contribution Type": x.contributionType || "",
      "Contribution Weight": x.contributionWeight ?? "",
      "Start Year": yearFromIso(x.timeHorizonStart),
      "End Year": yearFromIso(x.timeHorizonEnd),
      "Decision Ref": x.decisionReference || "",
      "Evidence Ref": x.evidenceReference || "",
      "Version": x.version ?? 0
    }));
    const initiativeRows = (initiatives?.items || []).map((x) => ({
      "Initiative ID": x.initiativeId || "",
      "Parent Objective ID": x.parentObjectiveId || "",
      "Parent Goal ID": x.parentGoalId || "",
      "Initiative": x.initiativeName || "",
      "Owner": x.owner || "",
      "Status": x.status || "",
      "Type": x.type || "",
      "Start Date": x.startDate || "",
      "End Date": x.endDate || "",
      "Planning Wave / Phase": x.waveOrPhase || "",
      "Primary KPI / Success Measure": x.primaryKpi || "",
      "Version": x.version ?? 0
    }));
    const projectRows = (projects?.items || []).map((x) => ({
      "Project ID": x.projectId || "",
      "Parent Initiative ID": x.parentInitiativeId || "",
      "Parent Objective ID": x.parentObjectiveId || "",
      "Parent Goal ID": x.parentGoalId || "",
      "Project": x.projectName || "",
      "Project Owner / PM": x.ownerPm || "",
      "Project Status": x.status || "",
      "Stage / Phase": x.phase || "",
      "Start Date": x.startDate || "",
      "End Date": x.endDate || "",
      "Project Success Metric": x.successMetric || "",
      "Risk Rating": x.riskRating || "",
      "Version": x.version ?? 0
    }));
    const connectionRows = (connections?.items || []).map((x) => {
      let meta = {};
      try { meta = JSON.parse(x.metricBindingsJson || "{}"); } catch { meta = {}; }
      const out = {
        "Goal ID": meta.goalId || "",
        "Goal": meta.goal || "",
        "Goal Metric": meta.goalMetric || "",
        "Objective": meta.objective || "",
        "Objective Metric": meta.objectiveMetric || "",
        "Initiative ID": meta.initiativeId || "",
        "Initiative": meta.initiative || "",
        "Initiative Metric": meta.initiativeMetric || "",
        "Project ID": meta.projectId || "",
        "Project": meta.project || "",
        "Project Metric": meta.projectMetric || "",
        "Metric Owner": meta.metricOwner || "",
        "Aggregation Method": meta.aggregationMethod || "",
        "Baseline Year": meta.baselineYear || "",
        "Baseline Value": meta.baselineValue ?? "",
        "Target Year": meta.targetYear || "",
        "Target Value": meta.targetValue ?? "",
        "Entry Notes": ""
      };
      for (let y = 2027; y <= 2046; y++) out[String(y)] = meta[String(y)] ?? "";
      return out;
    });
    return {
      Goals_List: goalRows,
      Objectives_List: objectiveRows,
      Initiatives_List: initiativeRows,
      Projects_List: projectRows,
      Connection_Map: connectionRows
    };
  }

  async function parseFile(file) {
    const ext = String(file?.name || "").toLowerCase().split(".").pop();
    if (ext === "csv") {
      const text = await file.text();
      const lines = text.split(/\r?\n/).filter(Boolean);
      if (!lines.length) return { ext, rows: [], sheets: {} };
      const headers = lines[0].split(",").map((h) => h.trim());
      const rows = lines.slice(1).map((line) => {
        const vals = line.split(",");
        const obj = {};
        headers.forEach((h, idx) => {
          obj[h] = String(vals[idx] || "").trim().replace(/^"|"$/g, "");
        });
        return obj;
      });
      return { ext, rows, sheets: { Sheet1: rows } };
    }
    if (ext === "xlsx") {
      if (!hasXlsx()) throw new Error("XLSX parser unavailable.");
      const buf = await file.arrayBuffer();
      const wb = window.XLSX.read(buf, { type: "array" });
      const sheets = {};
      wb.SheetNames.forEach((name) => {
        sheets[name] = window.XLSX.utils.sheet_to_json(wb.Sheets[name], { defval: "" });
      });
      return { ext, rows: sheets[wb.SheetNames[0]] || [], sheets };
    }
    throw new Error("Only .csv and .xlsx are supported.");
  }

  window.enterpriseWorkbookIo = {
    hasXlsx,
    exportCsv,
    exportWorkbook,
    parseFile,
    buildAllSheets,
  };
})(window);
