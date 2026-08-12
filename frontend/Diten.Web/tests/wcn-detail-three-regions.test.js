const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THE TASK DETAIL PAGE — three regions, no tabs.
 *
 * <b>Why no tabs, written down so it is not re-litigated.</b> The axis law says a tab means OWNERSHIP, and it
 * means that on the list. Teaching the same control a second grammar on the detail page is how a UI stops being
 * learnable. The stronger reason is the page's job: a detail page is read in order to DECIDE, and a gate behind
 * a tab is a gate nobody sees — "3 open subtasks must be closed before this can be completed" is exactly the
 * sentence a tab would hide, and it would make a blocked task look unblocked. SAP Fiori's object page, Jira,
 * Linear and Azure DevOps all use one flowing page with a side rail for the same reason.
 *
 * <b>The three regions.</b>
 *   head    — what this IS: title, chips, lifecycle. Independent of the content below it.
 *   content — what the work SAYS: summary, subtasks, activity.
 *   rail    — what you can DO and what stands in the way: actions, status, note, technical details.
 *
 * <b>HOW THIS IS TESTED.</b> The real app.js is booted against jsdom through the shared wcn-boot harness (the
 * one BL-033 added), so the claims below are made against the DOM the user actually gets — not against the
 * source text. Where a claim is about the SHAPE of the code rather than its output (the order of the rail, the
 * threshold constants, no inline styles) it is read off the source, and that is said at the assertion.
 *
 * jsdom performs no layout, so the pixel claims — two columns at ≥1400px, dark, RTL — are measured in the
 * browser and reported with the round.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const APP = () => read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
const CSS = () => read("wwwroot", "assets", "css", "backbone-custom.css");

/** The body of a named arrow function, from its declaration to the next top-level `const … =` at the same indent. */
const fn = (name, source) => {
  const text = source || APP();
  const start = text.indexOf(`const ${name} = `);
  if (start < 0) { return ""; }
  const next = text.indexOf("\n    const ", start + 10);
  return text.slice(start, next < 0 ? text.length : next);
};

/** detailHtml's own body — the composition, as opposed to the individual renderers. */
const detailHtml = () => {
  const text = APP();
  const start = text.indexOf("const detailHtml = (item)");
  const end = text.indexOf("const subtaskPanel = ()", start);
  return text.slice(start, end);
};

// ── 1. three regions, in order, and no tabs ─────────────────────────────────

describe("the page is three regions", () => {
  test("head, content and rail exist and appear in that order", () => {
    const body = detailHtml();
    const head = body.indexOf("wcn-detail-head");
    const content = body.indexOf("wcn-detail-content");
    const rail = body.indexOf("wcn-detail-rail");

    expect(head, "there is no head region").toBeGreaterThan(-1);
    expect(content).toBeGreaterThan(head);
    expect(rail).toBeGreaterThan(content);
  });

  test("content is the 8 and the rail is the 4, both direct children of the grid row", () => {
    /*
     * The Create form lost a closing tag this week and the two columns silently became one — invisible in a
     * narrow pane, which is where it was being looked at. Same claim, asserted here before it can happen.
     */
    const body = detailHtml();
    expect(body).toMatch(/<div class="col-12 col-lg-8 wcn-detail-content">/);
    expect(body).toMatch(/<div class="col-12 col-lg-4 wcn-detail-rail">/);
  });

  test("NO TABS — the decision, pinned", () => {
    const body = detailHtml();
    expect(body).not.toMatch(/nav-tabs|data-bs-toggle="tab"|role="tablist"/);
  });

  test("the lifecycle strip lives in the HEAD, not among the content cards", () => {
    /*
     * Asserted through the block it sits in rather than by source position: the head's markup is composed above
     * the return statement, so "which line comes first" in the file says nothing about which region renders it.
     */
    const body = detailHtml();
    const commandCard = body.slice(body.indexOf("const commandCard = "), body.indexOf("const cell = "));
    expect(commandCard).toContain("renderLifecycleStepper(item)");
    expect(body).toMatch(/wcn-detail-head">\$\{commandCard\}/);

    const content = body.slice(body.indexOf("const content = ["), body.indexOf("const rail = ["));
    expect(content).not.toContain("renderLifecycleStepper");
  });

  test("the rail carries actions, status, note and the technical block — in that order", () => {
    const body = detailHtml();
    const rail = body.slice(body.indexOf("const rail = ["), body.indexOf("].filter(Boolean).join('')", body.indexOf("const rail = [")));
    const order = ["renderActionRail", "renderStatusCard", "renderNote", "renderTechnicalDetails"];
    let cursor = -1;
    order.forEach((name) => {
      const at = rail.indexOf(name);
      expect(at, `${name} is not in the rail`).toBeGreaterThan(-1);
      expect(at, `${name} is out of order`).toBeGreaterThan(cursor);
      cursor = at;
    });
  });
});

// ── 2. the summary card ─────────────────────────────────────────────────────

describe("the summary card answers 'what is this?' before 'what can you do?'", () => {
  test("it is the FIRST card in the content column", () => {
    const body = detailHtml();
    const content = body.slice(body.indexOf("const content = ["));
    expect(content.indexOf("renderSummary(item)")).toBeGreaterThan(-1);
    expect(content.indexOf("renderSummary(item)")).toBeLessThan(content.indexOf("renderSubtasks(item)"));
    expect(content.indexOf("renderSummary(item)")).toBeLessThan(content.indexOf("activitySection"));
  });

  test("it carries the seven facts the reader came for", () => {
    const source = fn("renderSummary");
    expect(source, "renderSummary does not exist").not.toBe("");
    // Priority goes through hasPriority/priorityLabel — the existing rule that an unranked task must not be
    // shown as "Medium". Naming the raw field here would push the card into deciding that a second time.
    ["item.assignee", "item.requester", "item.dueAt", "item.startAt", "hasPriority(item)", "item.estimateHours", "item.tags"]
      .forEach((field) => expect(source, `${field} is not on the summary card`).toContain(field));
  });

  test("an EMPTY field prints nothing — no 'Son tarih: —' rows", () => {
    /*
     * The page's standing rule: a dash claims the value was checked and found empty. The fact helper drops the
     * row instead, so a task with no start date has no start-date line at all.
     */
    const source = fn("renderSummary") + fn("summaryFact");
    expect(source).not.toContain("'—'");
    expect(fn("summaryFact"), "there is no fact helper that can drop a row").toMatch(/return\s+''/);
  });

  test("the description is printed only when there is one", () => {
    expect(fn("renderSummary")).toMatch(/item\.summary\s*\?/);
  });

  test("tags render as chips, and an empty tag list is not an empty chip strip", () => {
    const source = fn("renderSummary");
    expect(source).toMatch(/item\.tags.*length/s);
  });
});

// ── 3. gates + dates = one STATUS card ──────────────────────────────────────

describe("'where does this stand?' is ONE card", () => {
  test("renderStatusCard exists and is what the rail uses", () => {
    expect(fn("renderStatusCard"), "there is no status card").not.toBe("");
    const body = detailHtml();
    expect(body).not.toMatch(/card\(renderGates\(item\)\)/);
    expect(body).not.toMatch(/card\(renderPlanDates\(item\)\)/);
  });

  test("it is built FROM the two existing renderers rather than a third copy of their rules", () => {
    const source = fn("renderStatusCard");
    expect(source).toContain("gateRow");
    expect(source).toContain("item.dueAt");
  });

  test("a gate that is NOT REQUIRED prints nothing — the card was 'Gerekmiyor / Gerekmiyor'", () => {
    /*
     * ⚠ THIS REVERSES AN EARLIER DECISION, deliberately and on the owner's call. The old comment argued that
     * "no approval needed" is itself an answer. Measured on a real task, it produced a full-height card whose
     * entire content was the word "Gerekmiyor" twice — noise that pushed the real state below the fold.
     */
    const source = fn("gateRow");
    expect(source).toMatch(/notRequired/);
    expect(source).toMatch(/return\s+''/);
  });

  test("with no gates AND no dates the card does not render at all", () => {
    expect(fn("renderStatusCard")).toMatch(/if\s*\(!rows/);
  });
});

// ── 4. technical details, closed ────────────────────────────────────────────

describe("developer data is kept, and folded away", () => {
  test("it is a <details> element that does NOT start open", () => {
    const source = fn("renderTechnicalDetails");
    expect(source, "there is no technical block").not.toBe("");
    expect(source).toContain("<details");
    expect(source).not.toMatch(/<details[^>]*\sopen/);
  });

  test("the version reads as a sentence, not as a wire value", () => {
    // Measured on screen: "Kaynak sürümü: version: 8".
    // The sentence is built by technicalVersion(); the block calls it. Both halves are asserted.
    expect(fn("technicalVersion")).toContain("TechVersionValue");
    expect(fn("renderTechnicalDetails")).toContain("technicalVersion(item)");
    expect(APP()).not.toMatch(/\$\{item\.concurrency\.kind\}: \$\{/);
  });

  test("the id stays copyable and says what it is", () => {
    expect(fn("referenceField")).toContain("data-wcn-copy");
  });

  test("nothing was DELETED — support still needs every field", () => {
    const source = fn("renderTechnicalDetails");
    ["DetailModuleName", "DetailSourceType", "DetailActionDepth", "DetailSourceVersion", "referenceField"]
      .forEach((field) => expect(source, `${field} was dropped from the technical block`).toContain(field));
  });
});

// ── 5. empty states are a LINE, not a card ──────────────────────────────────

describe("a new task's page is not a wall of 'there is nothing here'", () => {
  test("no subtasks ⇒ one line that also offers the action", () => {
    const source = fn("renderSubtasks");
    expect(source).toContain("wcn-empty-line");
    expect(source).toMatch(/SubtasksEmpty/);
  });

  test("no activity ⇒ one line", () => {
    expect(detailHtml()).toContain("wcn-empty-line");
  });

  test("the empty line is a row, not a block — the stylesheet says so", () => {
    const rule = /^\.wcn-empty-line\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, ".wcn-empty-line has no rule").toBeTruthy();
    expect(rule[1]).toMatch(/display:\s*flex/);
  });
});

// ── 6. long lists scroll INSIDE their card ──────────────────────────────────

describe("many subtasks do not push the rail off the screen", () => {
  test("the thresholds are stated as constants, not sprinkled", () => {
    const app = APP();
    expect(app).toMatch(/const SUBTASK_VISIBLE_LIMIT = 8;/);
    expect(app).toMatch(/const ACTIVITY_VISIBLE_LIMIT = 5;/);
  });

  test("past the threshold the list is capped and offers 'show all' — not a tab", () => {
    const source = fn("renderSubtasks");
    expect(source).toContain("SUBTASK_VISIBLE_LIMIT");
    expect(source).toContain("wcn-scrollcap");
    expect(source).toContain("ShowAllCount");
  });

  test("the cap is a scroll, so nothing becomes unreachable", () => {
    const rule = /^\.wcn-scrollcap\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, ".wcn-scrollcap has no rule").toBeTruthy();
    expect(rule[1]).toMatch(/overflow-y:\s*auto/);
    expect(rule[1]).toMatch(/max-block-size/);
  });
});

// ── 7. the chip that said nothing ───────────────────────────────────────────

describe("every chip carries text", () => {
  test("the role chip is not emitted when the role is unknown", () => {
    /*
     * MEASURED LIVE: `<span class="wcn-chip wcn-chip-role"><i …></i><span></span></span>` — an icon and an empty
     * label. `item.viewerRole` is not a projection field, so `t(undefined)` produced "".
     */
    const body = detailHtml();
    expect(body).not.toContain("t(ROLE_KEY[item.viewerRole] || item.viewerRole)");
    expect(fn("roleChip")).toMatch(/return\s+''/);
  });

  test("and where the projection CAN say it, it does — derived from isCurrentUser", () => {
    const mapper = read("wwwroot", "assets", "js", "WorkCenterNext", "mock-data.js");
    expect(mapper).toContain("isCurrentUser");
    expect(mapper).toMatch(/viewerRole/);
  });
});

// ── 8. l10n ─────────────────────────────────────────────────────────────────

describe("l10n — every new string in seven languages", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const resx = (lang) => read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
  /*
   * CopyId/CopiedToast are deliberately NOT here: the copy control already exists with `CopyReference`, in all
   * seven languages. A second key for the same sentence is how a translation set starts drifting.
   */
  const NEW_KEYS = [
    "SummaryCardLabel", "StatusCardLabel", "TechnicalDetailsLabel", "TechVersionValue",
    "DetailStartAt", "DetailEstimate", "DetailTags", "DetailPriority", "EstimateHoursValue",
    "ShowAllCount"
  ];

  test("each key exists in all seven files", () => {
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      NEW_KEYS.forEach((key) => expect(xml, `${lang} has no ${key}`).toContain(`name="${key}"`));
    });
  });

  test("the counted strings carry their {0}", () => {
    const tr = resx("tr");
    ["TechVersionValue", "EstimateHoursValue", "ShowAllCount"].forEach((key) => {
      const entry = new RegExp(`name="${key}"[\\s\\S]{0,200}?<value>([^<]*)</value>`).exec(tr);
      expect(entry, `${key} missing from tr`).toBeTruthy();
      expect(entry[1], `${key} has no {0}`).toContain("{0}");
    });
  });

  test("the key sets stay identical across the seven", () => {
    const keysOf = (lang) => new Set([...resx(lang).matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]));
    const reference = keysOf("tr");
    LANGS.filter((l) => l !== "tr").forEach((lang) => {
      const mine = keysOf(lang);
      const missing = [...reference].filter((k) => !mine.has(k));
      const extra = [...mine].filter((k) => !reference.has(k));
      expect(missing, `${lang} is missing: ${missing.join(", ")}`).toHaveLength(0);
      expect(extra, `${lang} has extra: ${extra.join(", ")}`).toHaveLength(0);
    });
  });
});

// ── 9. what must not have changed ───────────────────────────────────────────

describe("the things this round must not break", () => {
  test("every action still reaches the rail through the same renderer", () => {
    expect(detailHtml()).toContain("renderActionRail(item)");
  });

  test("the gate SENTENCES stay on the page — a blocked task still says why", () => {
    const app = APP();
    expect(app).toContain("SubtasksBlockingNotice");
    expect(app).toContain("WorkAggregation_ActionDisabled_ChecklistIncomplete");
  });

  test("MOD-0024 still only REPORTS gates (Binding A) — no approve/reject control appears", () => {
    const source = fn("renderStatusCard") + fn("gateRow");
    expect(source).not.toMatch(/data-wcn-(approve|reject)/);
  });

  test("FG-003 — no inline styles are written by the new blocks", () => {
    ["renderSummary", "renderStatusCard", "renderTechnicalDetails"].forEach((name) => {
      expect(fn(name), `${name} writes a style attribute`).not.toMatch(/style="/);
    });
  });
});

// ── 10. the DOM the reader actually gets ────────────────────────────────────

const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

const projectionItem = (overrides) => Object.assign({
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
  title: { kind: "display", text: "Q3 nakit akışı", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "subtasks"],
  subtasks: { mode: "full", items: [] },
  actions: [],
  concurrency: { kind: "version", token: "8" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-07-30T00:00:00+00:00"
}, overrides);

/*
 * The harness's default translator answers with the KEY and drops the arguments, which is right for asserting
 * "which string was chosen" and useless for asserting "the number reached the screen". This one interpolates,
 * so a value that never reaches its sentence is visible here rather than only in the browser.
 */
const boot = (item) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item],
  wcn: {
    t: (key) => key,
    tf: (key, ...args) => args.reduce((text, value, i) => text.split(`{${i}}`).join(String(value)), `${key}:{0}`),
    tn: (key) => key
  }
});

const subtask = (n) => ({
  id: `s${n}`, title: `Alt görev ${n}`, status: "not-started", canCancel: false
});

describe("the rendered page", () => {
  it("renders — the non-vacuity guard for everything below", async () => {
    await boot(projectionItem());
    expect(app().querySelector(".wcn-detail-command")).not.toBeNull();
  });

  it("puts the three regions on the page, and the columns are 8 and 4", async () => {
    await boot(projectionItem());
    const grid = app().querySelector(".wcn-detail-grid");
    const head = grid.querySelector(":scope > .wcn-detail-head");
    const content = grid.querySelector(":scope > .wcn-detail-content");
    const rail = grid.querySelector(":scope > .wcn-detail-rail");

    expect(head, "no head region").not.toBeNull();
    expect(content.classList.contains("col-lg-8")).toBe(true);
    expect(rail.classList.contains("col-lg-4")).toBe(true);
    // Order matters: head, then content, then rail — the rail must never precede what it acts on.
    expect(head.compareDocumentPosition(content) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(content.compareDocumentPosition(rail) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("shows the summary FIRST, with the facts on it", async () => {
    await boot(projectionItem({
      summary: { kind: "display", text: "Nakit akışını kontrol et" },
      startAt: "2026-08-03T00:00:00+00:00",
      estimateHours: 6.5,
      tags: ["finans", "q3"],
      priority: "High"
    }));

    const content = app().querySelector(".wcn-detail-content");
    const first = content.querySelector(".wcn-detail-card");
    expect(first.textContent).toContain("Nakit akışını kontrol et");

    const text = first.textContent;
    expect(text, "the due date is not shown as a field").toContain("2026-07-30");
    expect(text, "the start date is not shown").toContain("2026-08-03");
    // …as a DATE. It arrives as a full ISO instant and the card printed
    // "2026-07-26T07:51:18.432407+03:00" the first time it rendered live.
    expect(text, "the start date is printed as a raw instant").not.toMatch(/2026-08-03T/);
    expect(text, "the estimate is not shown").toContain("6.5");
    expect([...first.querySelectorAll(".wcn-tag")].map((n) => n.textContent)).toEqual(["finans", "q3"]);
  });

  it("prints NO row for a field the task does not have", async () => {
    await boot(projectionItem());   // no description, no start, no estimate, no tags
    const summary = app().querySelector(".wcn-detail-content .wcn-detail-card");
    expect(summary.querySelector(".wcn-fact-tags")).toBeNull();
    expect(summary.textContent).not.toContain("—");
  });

  it("merges gates and dates into ONE card in the rail", async () => {
    await boot(projectionItem({
      gates: {
        approval: { required: true, status: "pending", decider: { id: "x", displayName: "Deniz Koç" } },
        review: { required: false, status: "notRequired" }
      }
    }));

    const rail = app().querySelector(".wcn-detail-rail");
    const withGates = [...rail.querySelectorAll(".wcn-detail-card")].filter((c) => c.querySelector(".wcn-gates"));
    expect(withGates, "the gates are not in exactly one card").toHaveLength(1);
    // …and the dates are in that SAME card, not a second one.
    expect(withGates[0].querySelector(".wcn-dates"), "the dates are not on the status card").not.toBeNull();
    // The gate that does not apply is not printed at all.
    expect(withGates[0].querySelectorAll(".wcn-gate")).toHaveLength(1);
  });

  it("folds the technical block away — present, closed, and complete", async () => {
    await boot(projectionItem());
    const tech = app().querySelector(".wcn-detail-rail details.wcn-tech");
    expect(tech, "there is no technical block").not.toBeNull();
    expect(tech.hasAttribute("open"), "the technical block starts open").toBe(false);
    expect(tech.querySelector("[data-wcn-copy]"), "the id is no longer copyable").not.toBeNull();
    // "version: 8" was the wire's spelling; the reader gets a sentence.
    expect(tech.textContent).not.toContain("version: 8");
  });

  it("says 'no subtasks yet' in ONE line that still adds one", async () => {
    await boot(projectionItem());
    const empty = app().querySelector(".wcn-empty-line");
    expect(empty, "the empty state is not a line").not.toBeNull();
    expect(empty.querySelector("[data-wcn-subtask-input]"), "the line cannot add anything").not.toBeNull();
  });

  it("caps a long subtask list inside its own card instead of running past the rail", async () => {
    await boot(projectionItem({
      subtasks: { mode: "full", items: Array.from({ length: 12 }, (_, i) => subtask(i + 1)) }
    }));

    const cap = app().querySelector(".wcn-scrollcap");
    expect(cap, "a 12-row list is not capped").not.toBeNull();
    // Nothing is dropped — the cap is a scroll, and every row is still in the DOM.
    expect(cap.querySelectorAll(".wcn-subtask")).toHaveLength(12);
    expect(app().querySelector("[data-wcn-showall]"), "there is no way to release the cap").not.toBeNull();
  });

  it("leaves a SHORT list uncapped", async () => {
    // Non-vacuity for the threshold: at or below the limit nothing is wrapped.
    await boot(projectionItem({
      subtasks: { mode: "full", items: Array.from({ length: 3 }, (_, i) => subtask(i + 1)) }
    }));
    expect(app().querySelector(".wcn-scrollcap")).toBeNull();
  });

  it("emits no chip without text", async () => {
    await boot(projectionItem());
    const empty = [...app().querySelectorAll(".wcn-chip")].filter((c) => !c.textContent.trim());
    expect(empty.map((c) => c.className), "a chip renders with no text").toHaveLength(0);
  });

  it("and DOES say the role when the projection marks the caller", async () => {
    await boot(projectionItem());   // assignee.isCurrentUser = true
    const role = app().querySelector(".wcn-chip-role");
    expect(role, "the role chip vanished entirely").not.toBeNull();
    expect(role.textContent.trim().length).toBeGreaterThan(0);
  });

  it("still shows the gate SENTENCE that blocks completion", async () => {
    await boot(projectionItem({
      subtasks: { mode: "full", items: [subtask(1), subtask(2), subtask(3)] }
    }));
    // The one thing a tab would have hidden.
    expect(app().textContent).toContain("SubtasksBlockingNotice");
  });
});
