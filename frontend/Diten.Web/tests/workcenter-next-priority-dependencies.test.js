const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-032 (priority) and BL-028 (dependencies) on the browser side.
 *
 * Both are the same failure in different clothes: a value existed on one side of a seam under one spelling and on
 * the other under a different one, and NOTHING checked. Priority was 'high' in the fixtures and High in the
 * engine, so the column was hidden rather than fixed. Dependencies were 'FS' in the fixtures and FinishToStart in
 * the engine, and the blocked banner read `reasonKey`/`blockedBy` — fields no contract has ever declared.
 */
const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";

const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

const contract = () => {
  loadScript(scriptRoot + "fixture-contract.js");
  return global.WorkCenterNextContract;
};

const workItem = (overrides) => Object.assign({
  fixtureKind: "workItem",
  id: TASK_ID,
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "InProgress",
  taskLifecycle: "InProgress",
  executionState: "active",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: "Yeni maliyet merkezi açılış talebi", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [action()],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

function action(overrides) {
  return Object.assign({
    code: "complete",
    label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }, overrides);
}

const disabledAction = (code, reasonCode) => action({
  code,
  semanticType: code,
  enabled: false,
  disabledReasonCode: reasonCode,
  disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_DependencyBlocked" }
});

const codesOf = (result) => result.errors.map((error) => error.code);

describe("the contract declares how urgent work is (BL-032)", () => {
  it("accepts the engine's own three levels", () => {
    const c = contract();
    expect(c.enums.PRIORITIES).toEqual(["Low", "Medium", "High"]);

    c.enums.PRIORITIES.forEach((priority) => {
      expect(codesOf(c.validateWorkItem(workItem({ priority })))).not.toContain("PRIORITY_INVALID");
    });
  });

  it("refuses the old lowercase spelling", () => {
    // The exact value the fixtures carried for months. It has to FAIL now, or the migration proves nothing.
    const result = contract().validateWorkItem(workItem({ priority: "high" }));

    expect(codesOf(result)).toContain("PRIORITY_INVALID");
  });

  it("refuses a level nobody declared", () => {
    // "P1" was considered and rejected: it promises a response time no SLA engine exists to honour.
    expect(codesOf(contract().validateWorkItem(workItem({ priority: "P1" })))).toContain("PRIORITY_INVALID");
  });

  it("lets a provider that does not rank its work say nothing", () => {
    // Absent is a legitimate answer; defaulting it to Medium would tell the reader something nobody said.
    const c = contract();
    expect(codesOf(c.validateWorkItem(workItem()))).not.toContain("PRIORITY_INVALID");
    expect(codesOf(c.validateWorkItem(workItem({ priority: null })))).not.toContain("PRIORITY_INVALID");
  });

  it("is spelled the same way everywhere the shell and the fixtures use it", () => {
    // The whole point of the migration: one spelling in the fixtures, the shell's maps, and the contract.
    const fixtureDir = path.join(__dirname, "..", scriptRoot, "fixtures");
    const files = [];
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) { walk(full); } else if (entry.name.endsWith(".js")) { files.push(full); }
    });
    walk(fixtureDir);
    files.push(path.join(__dirname, "..", scriptRoot, "app.js"));
    files.push(path.join(__dirname, "..", scriptRoot, "mock-data.js"));
    files.push(path.join(__dirname, "..", scriptRoot, "migration-fixture-adapter.js"));

    expect(files.length).toBeGreaterThan(3);
    files.forEach((file) => {
      const source = fs.readFileSync(file, "utf8");
      expect(source, `${path.basename(file)} still writes a lowercase priority`)
        .not.toMatch(/priority: *'(high|medium|low)'/);
    });
  });
});

describe("the contract declares typed dependencies (BL-028)", () => {
  const edge = (overrides) => Object.assign({
    id: "DEP-1",
    title: { kind: "display", text: "Sözleşme imzası", locale: "und" },
    type: "FinishToStart",
    state: "in-progress",
    direction: "pred"
  }, overrides);

  const withEdges = (edges) => workItem({
    workItemCapabilities: ["planning", "execution", "dependencies"],
    dependencies: edges
  });

  it("accepts the engine's four edge types", () => {
    const c = contract();
    expect(c.enums.DEPENDENCY_TYPES)
      .toEqual(["FinishToStart", "FinishToFinish", "StartToStart", "StartToFinish"]);

    c.enums.DEPENDENCY_TYPES.forEach((type) => {
      expect(codesOf(c.validateWorkItem(withEdges([edge({ type })])))).not.toContain("DEPENDENCY_TYPE_INVALID");
    });
  });

  it("refuses the old two-letter spelling", () => {
    expect(codesOf(contract().validateWorkItem(withEdges([edge({ type: "FS" })]))))
      .toContain("DEPENDENCY_TYPE_INVALID");
  });

  it("takes a dependency's state from the shared task-state vocabulary", () => {
    const c = contract();
    // Same list as subtasks, deliberately: it is the same question about another task. `cancelled` is the value
    // the blocking rule needs — called-off work blocks nothing.
    expect(c.enums.SUBTASK_STATUSES).toContain("cancelled");
    expect(codesOf(c.validateWorkItem(withEdges([edge({ state: "cancelled" })]))))
      .not.toContain("DEPENDENCY_STATE_INVALID");
    expect(codesOf(c.validateWorkItem(withEdges([edge({ state: "inProgress" })]))))
      .toContain("DEPENDENCY_STATE_INVALID");
  });

  it("requires each edge to say which way it points", () => {
    expect(codesOf(contract().validateWorkItem(withEdges([edge({ direction: undefined })]))))
      .toContain("DEPENDENCY_DIRECTION_INVALID");
  });
});

describe("the contract declares what being blocked looks like", () => {
  const blocked = (overrides) => workItem(Object.assign({
    actions: [disabledAction("start", "DEPENDENCY_BLOCKED")],
    blockedState: {
      blocked: true,
      affectedActionCodes: ["start"],
      blockers: [{
        code: "DEPENDENCY_BLOCKED",
        label: { kind: "display", text: "Sözleşme imzası", locale: "und" },
        taskItemId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        dependencyType: "FinishToStart",
        affectedActionCode: "start"
      }]
    }
  }, overrides));

  it("accepts the shape the provider emits", () => {
    expect(codesOf(contract().validateWorkItem(blocked()))).toEqual([]);
  });

  it("refuses a blocked item with nothing blocking it", () => {
    // "Blocked, but no reason" is the invented-data failure in banner form.
    const result = contract().validateWorkItem(blocked({
      blockedState: { blocked: true, affectedActionCodes: [], blockers: [] },
      actions: [action()]
    }));

    expect(codesOf(result)).toContain("BLOCKED_STATE_BLOCKER_REQUIRED");
  });

  it("refuses an affected action that is still enabled", () => {
    // The rule that makes "disabled, not hidden" enforceable: the reader must be able to SEE the blocked button.
    const result = contract().validateWorkItem(blocked({ actions: [action({ code: "start" })] }));

    expect(codesOf(result)).toContain("BLOCKER_ACTION_REFERENCE_INVALID");
  });

  it("refuses an affected action that is not in the action list at all", () => {
    const result = contract().validateWorkItem(blocked({ actions: [disabledAction("complete", "X")] }));

    expect(codesOf(result)).toContain("BLOCKER_ACTION_REFERENCE_INVALID");
  });

  it("refuses a blocker pointing at an action the blocked state never named", () => {
    const item = blocked();
    item.blockedState.blockers[0].affectedActionCode = "complete";

    expect(codesOf(contract().validateWorkItem(item))).toContain("BLOCKER_ACTION_REFERENCE_INVALID");
  });

  it("refuses an edge type nobody declared inside a blocker", () => {
    const item = blocked();
    item.blockedState.blockers[0].dependencyType = "FS";

    expect(codesOf(contract().validateWorkItem(item))).toContain("BLOCKER_DEPENDENCY_TYPE_INVALID");
  });

  it("still accepts a blocker that is not a dependency", () => {
    // A blocking checklist item — and, later, an open subtask (BL-035) — wears the same shape with the three
    // dependency fields left off. Designing that in now is what keeps BL-035 from needing a new container.
    const item = blocked();
    item.blockedState.blockers = [{
      code: "CHECKLIST_INCOMPLETE",
      label: { kind: "resource", key: "WorkAggregation_ActionDisabled_ChecklistIncomplete" }
    }];

    expect(codesOf(contract().validateWorkItem(item))).toEqual([]);
  });
});
