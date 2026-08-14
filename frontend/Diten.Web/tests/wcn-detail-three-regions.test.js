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

/**
 * detailHtml's own body — the composition, as opposed to the individual renderers.
 *
 * COMMENTS ARE STRIPPED. These guards assert what the page RENDERS, and the region names they look for
 * (`wcn-detail-head`, `role="tablist"`) are exactly the words the surrounding comments use to explain the
 * rules — so a prose explanation of "the rail must never go inside a tab" used to fail the test enforcing it.
 * The same `stripComments` discipline the localization suite already applies, for the same reason.
 */
const detailHtml = () => {
  const text = APP();
  const start = text.indexOf("const detailHtml = (item)");
  const end = text.indexOf("const subtaskPanel = ()", start);
  return text.slice(start, end)
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/(^|[^:])\/\/.*$/gm, "$1");
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

  test("TABS, but only over the content column — the decision, re-pinned", () => {
    /*
     * ⚠ THIS PIN WAS REVERSED (owner decision). It used to read "NO TABS", and that was the right rule for what
     * it was actually protecting: nothing the reader needs may hide behind a label.
     *
     * The detail page now has two — Genel / Etkinlik — over the CONTENT column alone. What the original pin
     * defended is defended more precisely here: the head (lifecycle) and the rail (available actions, status,
     * note) are composed OUTSIDE the panels, so no gate can ever sit behind a tab. A blanket "no tablist
     * anywhere" would now fail for a strip that breaks none of the rules it was written to enforce.
     */
    const body = detailHtml();

    // The strip exists, and it is inside the content column.
    expect(body).toMatch(/role="tablist"/);
    expect(body).toMatch(/wcn-detail-tabstrip/);
    /*
     * And it is NOT called `wcn-detail-tabs`. That name is taken: the split-detail side pane owns it, with a
     * sticky position, a border, a radius, a backdrop-filter and a 1rem top margin attached. Wearing the name
     * dressed this strip in all of it — two stray hairlines and a 16px drop out of line with the rail — from a
     * file nobody would think to open. A name is a claim on every rule already written for it.
     */
    expect(body).not.toMatch(/wcn-detail-tabs["'\s]/);

    // …and the two regions that must never be tabbed are composed outside any panel.
    expect(body).toMatch(/wcn-detail-head">\$\{commandCard\}/);
    expect(body).toMatch(/wcn-detail-rail">\$\{rail\}/);
    const panelArray = body.slice(body.indexOf("const content = ["), body.indexOf("const rail = ["));
    expect(panelArray).toContain("data-wcn-detail-panel");

    /*
     * The strip's SURFACE is borrowed, not authored: `.card` + `.card-body p-3`, the same two lines the list
     * page's own strip is built from. Background, radius, shadow and padding therefore follow the page — in
     * both themes, for free — and no colour, shadow or radius is declared anywhere for this component. A
     * hand-rolled surface is how two cards on one screen end up almost the same.
     */
    expect(body).toMatch(/class="card wcn-detail-tabcard"><div class="card-body p-3"/);

    /*
     * Vertical rhythm: ONE gap, everywhere.
     *
     * RE-PINNED. This used to assert an ORDER — strip closest to its own panel, head furthest from what follows
     * — on the reasoning that distance is what tells a reader which things belong together. It shipped as
     * .5rem / 1.5rem / 2rem and was rejected on sight: three different spacings register as three mistakes long
     * before they register as three meanings, and the graded version left the strip crowding its panel while
     * unrelated cards drifted apart.
     *
     * The value is the CREATE FORM's, measured, not a fresh choice: five cards there, 16px between every pair.
     * The head contributes nothing of its own because the row gutter beneath it is already that 16px.
     */
    const css = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot/assets/css/backbone-custom.css"), "utf8");
    // `0` is written unitless, so the unit is optional in the match — a bare 0 is still a measurement.
    const rem = (re) => parseFloat(css.match(re)[1]);
    const strip = rem(/\.wcn-detail-tabcard \{ margin-block-end: ([\d.]+)(?:rem)?; \}/);
    const cards = rem(/\.wcn-detail-panel > \.wcn-detail-card \{ margin-block-end: ([\d.]+)(?:rem)?; \}/);
    const head  = rem(/\.wcn-detail-head \{ margin-block-end: ([\d.]+)(?:rem)?; \}/) + 1; // + the row gutter
    expect([strip, cards, head]).toEqual([1, 1, 1]);
    expect(panelArray).not.toContain("${rail}");
    expect(panelArray).not.toContain("${commandCard}");
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
    /*
     * The cards moved from `content` into `generalPanel` when the column gained tabs — `content` now holds the
     * strip and the two panels. The CLAIM is unchanged: summary comes before the rest of the work, and the
     * activity record comes after all of it.
     */
    const body = detailHtml();
    const general = body.slice(body.indexOf("const generalPanel = ["), body.indexOf("const activityPanel ="));
    expect(general.indexOf("renderSummary(item)")).toBeGreaterThan(-1);
    expect(general.indexOf("renderSummary(item)")).toBeLessThan(general.indexOf("renderSubtasks(item)"));

    // The record follows the work: the activity panel is composed after the general one.
    expect(body.indexOf("const generalPanel = [")).toBeLessThan(body.indexOf("const activityPanel ="));
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
    // The cap and its control live in ONE helper now (they were rendered twice, and the second copy is where
    // the missing click handler hid), so the renderer is asserted to USE it.
    const source = fn("renderSubtasks");
    expect(source).toContain("SUBTASK_VISIBLE_LIMIT");
    expect(source).toContain("cappedList('subtasks'");
    expect(fn("cappedList")).toContain("wcn-scrollcap");
    expect(fn("cappedList")).toContain("ShowAllCount");
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
    /*
     * ⚠ THIS ASSERTION USED TO CLAIM MORE THAN IT MEASURED. Its name said "there is no way to release the cap"
     * and it checked that a BUTTON EXISTS. The button existed and released nothing — `data-wcn-showall` had no
     * click handler anywhere in app.js, so the control was drawn dead. The claim is split in two now: the button
     * is present HERE, and it actually works in "the cap can be released", below.
     */
    expect(app().querySelector("[data-wcn-showall]"), "the cap offers no control at all").not.toBeNull();
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

// ── 11. the parent nobody accepted, whose children are already working ──────

describe("a task waiting to be accepted says when its children are already running", () => {
  /*
   * <b>What the owner saw.</b> A task reading "Bu görev kabulünü bekliyor" — nobody has taken it — with three
   * subtasks reading "Devam ediyor". Both are true at once, and the page said nothing about the combination.
   *
   * <b>Why this is a SIGNAL and not a rule.</b> Nothing in TaskBlockingRules ties a child's START to the
   * parent's acceptance, and that direction is deliberate: if it did, one person forgetting to press "Accept"
   * would stop three people working. Jira and Azure DevOps behave the same way; top-down release (SAP WBS) is a
   * project-system model, not a task-centre one. What was missing is not a prohibition, it is a sentence.
   *
   * <b>Where the number comes from — MEASURED.</b> The projection already carries each subtask's status in the
   * contract vocabulary (WorkItemSubtaskDto.Status: done | in-progress | not-started | cancelled), which is
   * what lets the list print "Devam ediyor" today. Nothing was added to the wire for this round.
   */
  const running = (n) => ({ id: `r${n}`, title: `Çalışan ${n}`, status: "in-progress", canCancel: false });
  const cancelled = (n) => ({ id: `c${n}`, title: `İptal ${n}`, status: "cancelled", canCancel: false });

  const pendingAcceptance = (items) => projectionItem({
    admissionState: "pendingAcceptance",
    ownershipState: "assigned",
    normalizedStatus: "Pending",
    taskLifecycle: "Open",
    executionState: "notStarted",
    subtasks: { mode: "full", items }
  });

  const banner = () => app().querySelector(".wcn-guidance");

  it("says it, with the count, INSIDE the acceptance banner", async () => {
    await boot(pendingAcceptance([running(1), running(2)]));

    const banners = app().querySelectorAll(".wcn-guidance");
    expect(banners, "a second banner appeared instead of a second sentence").toHaveLength(1);
    expect(banner().textContent).toContain("GuidancePendingAcceptance");
    expect(banner().textContent).toContain("GuidanceChildrenRunning:2");
  });

  it("stays silent when there are no subtasks at all", async () => {
    await boot(pendingAcceptance([]));
    expect(banner().textContent).not.toContain("GuidanceChildrenRunning");
  });

  it("stays silent when the only subtasks are CANCELLED", async () => {
    // Called-off work is not work in progress — the same rule TaskBlockingRules states once and this derives.
    await boot(pendingAcceptance([cancelled(1), cancelled(2)]));
    expect(banner().textContent).not.toContain("GuidanceChildrenRunning");
  });

  it("stays silent for a NOT-STARTED child — 'already running' would not be true", async () => {
    await boot(pendingAcceptance([{ id: "n1", title: "Beklemede", status: "not-started", canCancel: false }]));
    expect(banner().textContent).not.toContain("GuidanceChildrenRunning");
  });

  it("stays silent once the task HAS been accepted, however busy its children are", async () => {
    await boot(projectionItem({ subtasks: { mode: "full", items: [running(1), running(2)] } }));
    const note = app().querySelector(".wcn-guidance");
    expect(note ? note.textContent : "").not.toContain("GuidanceChildrenRunning");
  });

  it("BLOCKS NOTHING — the actions on the banner's task are untouched", async () => {
    /*
     * The line between a signal and a gate, asserted. A page that grew a warning AND disabled something would
     * have quietly changed the product's direction.
     */
    const withChildren = await boot(pendingAcceptance([running(1), running(2)]));
    const enabled = [...app().querySelectorAll(".wcn-actrail button:not([disabled])")].length;

    await boot(pendingAcceptance([]));
    const baseline = [...app().querySelectorAll(".wcn-actrail button:not([disabled])")].length;

    expect(enabled, "running children disabled an action").toBe(baseline);
    expect(withChildren).toBeTruthy();
  });

  it("is one sentence in seven languages, with its counter", () => {
    const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    LANGS.forEach((lang) => {
      const xml = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
      const entry = /<data name="GuidanceChildrenRunning"[^>]*>\s*<value>([^<]*)<\/value>/.exec(xml);
      expect(entry, `GuidanceChildrenRunning missing in ${lang}`).toBeTruthy();
      expect(entry[1], `no {0} in ${lang}`).toContain("{0}");
    });
  });

  it("counts through ONE definition of 'cancelled does not count'", () => {
    // Written twice, the two would drift and the count would disagree with the blocking notice below it.
    const app_ = APP();
    expect(app_).toMatch(/const isCancelledSubtask/);
    expect(fn("runningSubtaskCount")).toContain("isCancelledSubtask");
    expect(fn("renderSubtasks")).toContain("isCancelledSubtask");
  });
});

// ── 12. the subtask list as a CHECKLIST (A2) ────────────────────────────────

describe("the subtask list reads and behaves like a checklist", () => {
  /*
   * <b>Layout A2.</b> Progress at the top (a count and a thin bar, so "how much is left" is answered without
   * reading rows), the ADD row directly under it (it was at the bottom, and its two buttons were detached in the
   * card's top-right corner), then two-layer rows: title on top, holder and date beneath, status and the row
   * menu on the right.
   *
   * <b>The checkbox is a promise.</b> A box that cannot be ticked lies about what it is, so it COMPLETES the
   * subtask — through the ordinary transition endpoint, with no new rule and no optimistic tick. If the server
   * refuses (the child has its own gates), the refusal is what the user sees.
   *
   * <b>MEASURED, and this is the part a fixed list would get wrong.</b> The projection states exactly two
   * permissions per subtask row: `canCancel` (server-evaluated, per row, because a child's requester is its own)
   * and `status`. It carries NO per-row action set. So the menu is built from those two facts plus navigation —
   * and "reassign" is deliberately absent, because nothing on the wire says this actor may reassign that child
   * and a button that 409s is the defect this project already shipped once.
   */
  const child = (overrides) => Object.assign({
    id: "11111111-1111-1111-1111-111111111111",
    title: "Bütçe kalemini doğrula",
    status: "not-started",
    assignee: { id: "u1", displayName: "Deniz Koç" },
    dueAt: "2026-08-20T00:00:00+00:00",
    canCancel: true
  }, overrides);

  const withSubtasks = (items) => projectionItem({ subtasks: { mode: "full", items } });
  const list = () => app().querySelector(".wcn-subtasks");
  const rows = () => [...app().querySelectorAll(".wcn-subtask")];

  // ── progress ──
  it("says how many are done, as a count AND a bar", async () => {
    await boot(withSubtasks([
      child({ id: "a", status: "done" }), child({ id: "b" }), child({ id: "c" })
    ]));

    const card = list().closest(".wcn-detail-section");
    // The visible reading is the DONE count; the full "1 / 3" is the bar's accessible name (item 8: the total
    // is the badge's job, and printing it twice is what confused the reader).
    expect(card.textContent).toContain("SubtaskDoneCount:1");

    const bar = card.querySelector("progress.wcn-progress");
    expect(bar.getAttribute("aria-label")).toContain("SubtaskProgressCount:1");
    expect(bar, "there is no progress bar").not.toBeNull();
    expect(bar.getAttribute("value")).toBe("1");
    expect(bar.getAttribute("max")).toBe("3");
  });

  it("counts CANCELLED work in neither half — it is not done and not outstanding", async () => {
    await boot(withSubtasks([child({ id: "a", status: "done" }), child({ id: "b", status: "cancelled" })]));
    const bar = app().querySelector("progress.wcn-progress");
    expect(bar.getAttribute("value")).toBe("1");
    expect(bar.getAttribute("max"), "a cancelled subtask still counts as outstanding work").toBe("1");
  });

  // ── the add row ──
  it("puts the add row ABOVE the list, not under it", async () => {
    await boot(withSubtasks([child()]));
    const add = app().querySelector(".wcn-subtask-add");
    expect(add, "there is no add row").not.toBeNull();
    expect(add.compareDocumentPosition(list()) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("reuses the page's OWN search-input pattern rather than inventing a second one", async () => {
    await boot(withSubtasks([child()]));
    const box = app().querySelector(".wcn-subtask-add .wcn-search.wcn-search-inline");
    expect(box, "the add box does not use wcn-search wcn-search-inline").not.toBeNull();
    expect(box.querySelector("[data-wcn-subtask-input]")).not.toBeNull();
    expect(box.querySelector("i.bx"), "the pattern's leading icon is missing").not.toBeNull();
  });

  it("labels 'add in detail' with WORDS — there is no universal glyph for it", async () => {
    await boot(withSubtasks([child()]));
    const detailed = app().querySelector("[data-wcn-subtask-add-detailed]");
    expect(detailed.textContent.trim().length, "the detailed-add button is icon-only").toBeGreaterThan(0);
  });

  it("adds on ENTER, through the same write path the button used", async () => {
    const { created } = await boot(withSubtasks([child()]));
    const input = app().querySelector("[data-wcn-subtask-input]");
    input.value = "Yeni alt görev";
    input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
    await new Promise((r) => setTimeout(r, 0));

    expect(created.map((c) => c.title)).toContain("Yeni alt görev");
  });

  it("SAYS what a quick-added subtask inherits — it was silent about it", async () => {
    /*
     * Measured: quick-add does not skip the required fields, it inherits them (dueAt from the parent, priority
     * and assignee from the parent record). The server requires a due date on every task, so this always
     * happened — and nothing on screen said so.
     */
    await boot(withSubtasks([child()]));
    expect(app().querySelector(".wcn-subtask-add-hint").textContent).toContain("SubtaskInheritsHint");
  });

  // ── the checkbox ──
  it("is a CHECKBOX now, and ticking it completes the subtask through the transition endpoint", async () => {
    const calls = [];
    await boot(withSubtasks([child({ id: "sub-1" })]));
    global.TasksApi.transition = (id, code) => { calls.push({ id, code }); return Promise.resolve({ ok: true, status: 204 }); };

    app().querySelector(".wcn-subtask-check").click();
    await new Promise((r) => setTimeout(r, 0));

    /*
     * The COUNT is not asserted, and that is a property of the harness rather than of the code: every boot in
     * this file leaves its document listeners behind (jsdom keeps one document per file), so a single click is
     * heard once per boot so far. What matters — and what is asserted — is that the click reaches the ordinary
     * completion transition for THIS subtask, and reaches nothing else.
     */
    expect(calls.length, "no transition was attempted").toBeGreaterThan(0);
    calls.forEach((call) => {
      expect(call.code).toBe("complete");
      expect(call.id).toBe("sub-1");
    });
  });

  it("does NOT tick optimistically — a refusal leaves the row exactly as it was, and says why", async () => {
    await boot(withSubtasks([child({ id: "sub-1" })]));
    const toasts = [];
    global.TasksApi.transition = () => Promise.resolve({ ok: false, status: 409 });
    global.TasksApi.isTransitionBlocked = () => true;
    global.TasksApi.failureMessage = () => "Alt görevin kendi kapısı açık";
    global.Swal = { fire: (opts) => { toasts.push(opts); return Promise.resolve({ isConfirmed: false }); } };

    app().querySelector(".wcn-subtask-check").click();
    await new Promise((r) => setTimeout(r, 0));

    expect(rows()[0].classList.contains("wcn-subtask-done"), "the row marked itself done anyway").toBe(false);
  });

  it("disables the box on a row that cannot be completed, and says why in its title", async () => {
    await boot(withSubtasks([child({ id: "d", status: "done" }), child({ id: "c", status: "cancelled" })]));

    rows().forEach((row) => {
      const box = row.querySelector(".wcn-subtask-check");
      expect(box.disabled, "a terminal row still offers its checkbox").toBe(true);
      expect((box.getAttribute("title") || "").length, "a disabled box gives no reason").toBeGreaterThan(0);
    });
  });

  // ── the row menu ──
  it("offers the row's actions from what the SERVER said about that row", async () => {
    await boot(withSubtasks([child({ id: "x", canCancel: true })]));
    const row = rows()[0];

    // Two actions (open · cancel) ⇒ a menu.
    expect(row.querySelector("[data-wcn-subtask-menu]"), "no row menu").not.toBeNull();
    expect(row.querySelector("[data-wcn-subtask-cancel]"), "cancel is missing though canCancel is true").not.toBeNull();
    expect(row.querySelector("[data-wcn-open-task]"), "there is no way to open the subtask").not.toBeNull();
  });

  it("drops cancel when the server says this actor may not cancel — and then shows NO menu", async () => {
    /*
     * One action is not a menu. A ⋯ that opens a single item is a click nobody needs, and it hides the one
     * thing it holds.
     */
    await boot(withSubtasks([child({ id: "x", canCancel: false })]));
    const row = rows()[0];

    expect(row.querySelector("[data-wcn-subtask-cancel]")).toBeNull();
    expect(row.querySelector("[data-wcn-subtask-menu]"), "a menu opened for a single action").toBeNull();
    expect(row.querySelector("[data-wcn-open-task]"), "the single action is not offered directly").not.toBeNull();
  });

  it("invents NO action the wire cannot justify — reassign is not offered", async () => {
    // The projection states `canCancel` and `status` per row and nothing else. A reassign button here would be
    // a guess, and the guess is a 409.
    await boot(withSubtasks([child()]));
    expect(app().querySelector(".wcn-subtask [data-wcn-subtask-reassign]")).toBeNull();
    expect(fn("renderSubtasks")).not.toContain("reassign");
  });

  // ── what must not have changed ──
  it("still prints the gate line that blocks the parent", async () => {
    await boot(withSubtasks([child({ id: "a" }), child({ id: "b" })]));
    expect(app().textContent).toContain("SubtasksBlockingNotice");
  });

  it("still sinks cancelled rows below the live ones", async () => {
    await boot(withSubtasks([
      child({ id: "cancelled-first", status: "cancelled" }),
      child({ id: "live", status: "not-started" })
    ]));
    expect(rows()[0].className).not.toContain("wcn-subtask-cancelled");
  });

  it("still caps the list at eight and offers 'show all'", async () => {
    // Presence only — that the control WORKS is asserted in its own tests below, because this one was written
    // as if presence proved it and it did not.
    await boot(withSubtasks(Array.from({ length: 12 }, (_, i) => child({ id: `s${i}` }))));
    expect(app().querySelector(".wcn-scrollcap")).not.toBeNull();
    expect(app().querySelector("[data-wcn-showall]")).not.toBeNull();
  });

  it("still says nothing-yet in ONE line, with the add row on it", async () => {
    await boot(withSubtasks([]));
    const empty = app().querySelector(".wcn-empty-line");
    expect(empty).not.toBeNull();
    expect(empty.querySelector("[data-wcn-subtask-input]")).not.toBeNull();
  });

  it("keeps the row one line high: the title is clipped, not wrapped", () => {
    const rule = /^\.wcn-subtask-title\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, ".wcn-subtask-title has no rule").toBeTruthy();
    expect(rule[1]).toMatch(/text-overflow:\s*ellipsis/);
    expect(rule[1]).toMatch(/white-space:\s*nowrap/);
    expect(rule[1]).toMatch(/overflow:\s*hidden/);
  });

  it("names the server's refusal instead of 'an error occurred' — measured live", () => {
    /*
     * FOUND BY THE CHECKBOX ITSELF, on its first live click: completing a not-started child returns
     * 409 TASK_INVALID_STATE, the code was unmapped, and the user read the generic sentence while the console
     * printed the app's own "add it to REASON_CODE_MESSAGE_KEYS" warning. A tick that fails must say what to do
     * next.
     */
    const api = read("wwwroot", "assets", "js", "Tasks", "api.js");
    expect(api).toMatch(/TASK_INVALID_STATE:\s*'errorTaskInvalidState'/);
    // …and it is a RULE, not a race: the surface refreshes and explains rather than blaming a concurrent edit.
    const set = api.slice(api.indexOf("const BLOCKING_REASON_CODES = new Set(["));
    expect(set.slice(0, set.indexOf("]);"))).toContain("'TASK_INVALID_STATE'");

    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      expect(read("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`), `${lang} has no sentence for it`)
        .toContain('name="ErrorTaskInvalidState"');
    });
    expect(read("Views", "Tasks", "_IndexL10n.cshtml")).toContain("ErrorTaskInvalidState");
  });

  it("reads the people lookup's OBJECT shape — the twin of a defect BL-057 left behind", () => {
    /*
     * Also found live, in the same click: `assignablePeople()` answers `{ people, excluded }` and this call site
     * still took `.data`, so `state.assignablePeople` became an object and the NEXT render died on
     * `people.map is not a function` — taking the whole page down, not just the picker.
     */
    const app_ = APP();
    expect(app_).toMatch(/Array\.isArray\(people\.data\?\.people\)/);
    expect(app_).not.toMatch(/state\.assignablePeople = \(people\.ok && people\.data\) \? people\.data :/);
  });

  it("puts the new strings in all seven languages, with their counters", () => {
    const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    const KEYS = ["SubtaskProgressCount", "SubtaskInheritsHint", "SubtaskAddDetailed", "SubtaskOpen",
      "SubtaskCheckDoneReason", "SubtaskCheckCancelledReason"];
    LANGS.forEach((lang) => {
      const xml = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
      KEYS.forEach((key) => expect(xml, `${lang} has no ${key}`).toContain(`name="${key}"`));
      const counted = /<data name="SubtaskProgressCount"[^>]*>\s*<value>([^<]*)<\/value>/.exec(xml);
      expect(counted[1], `${lang} SubtaskProgressCount has no counters`).toMatch(/\{0\}[\s\S]*\{1\}/);
    });
  });
});

// ── 13. the ten-item round: two real defects, eight of layout ───────────────

describe("cancelling a subtask actually cancels it", () => {
  /*
   * <b>MEASURED LIVE, and it is the worst kind of defect.</b> ⋯ → "Cancel subtask" → the confirm dialog opens →
   * "Yes" → NO network request, no toast, the row unchanged. The user believes they cancelled something.
   *
   * <b>Cause.</b> confirmDestructive resolved FALSE on the next tick — `setTimeout(() => resolve(false), 0)` —
   * because showConfirm "does not report dismissal". It does: the third argument takes an `onCancel` callback.
   * So the promise settled to false a millisecond after the dialog opened, and the user's click seconds later
   * resolved an already-settled promise into nothing.
   */
  const child = (overrides) => Object.assign({
    id: "sub-1", title: "Bütçe kalemini doğrula", status: "in-progress",
    assignee: null, dueAt: null, canCancel: true
  }, overrides);
  const withSubtasks = (items) => projectionItem({ subtasks: { mode: "full", items } });

  const withConfirm = (behaviour) => {
    // The real MOD-0013 shape: a callback for yes, options.onCancel for no — and NEITHER fires synchronously.
    global.showConfirm = (message, callback, options) => {
      setTimeout(() => {
        if (behaviour === "yes") { callback(); } else if (options && options.onCancel) { options.onCancel(); }
      }, 5);
    };
  };

  afterEach(() => { delete global.showConfirm; });

  it("calls the cancel transition once the user confirms", async () => {
    const calls = [];
    await boot(withSubtasks([child()]));
    withConfirm("yes");
    global.TasksApi.transition = (id, code) => { calls.push({ id, code }); return Promise.resolve({ ok: true, status: 204 }); };

    app().querySelector("[data-wcn-subtask-cancel]").click();
    await new Promise((r) => setTimeout(r, 30));

    expect(calls.length, "the confirmed cancel reached no endpoint at all").toBeGreaterThan(0);
    calls.forEach((call) => {
      expect(call.code).toBe("cancel");
      expect(call.id).toBe("sub-1");
    });
  });

  it("calls NOTHING when the user dismisses the dialog", async () => {
    const calls = [];
    await boot(withSubtasks([child()]));
    withConfirm("no");
    global.TasksApi.transition = (id, code) => { calls.push({ id, code }); return Promise.resolve({ ok: true, status: 204 }); };

    app().querySelector("[data-wcn-subtask-cancel]").click();
    await new Promise((r) => setTimeout(r, 30));

    expect(calls).toHaveLength(0);
  });

  it("shows the server's reason when the cancel is refused", async () => {
    await boot(withSubtasks([child()]));
    withConfirm("yes");
    global.TasksApi.transition = () => Promise.resolve({ ok: false, status: 409, reasonCode: "TASK_INVALID_STATE" });
    global.TasksApi.failureMessage = () => "Bu görev bu durumdayken iptal edilemez";

    app().querySelector("[data-wcn-subtask-cancel]").click();
    await new Promise((r) => setTimeout(r, 30));

    // The row must not pretend: nothing is marked cancelled on the client's say-so.
    expect(app().querySelector(".wcn-subtask").className).not.toContain("wcn-subtask-cancelled");
  });

  it("sends the subtask's OWN version with the cancel — measured live as a 409", () => {
    /*
     * The second half of the same defect, and it only became visible once the dialog stopped resolving early:
     * the request went out with `{}`, the server compared version 0 against the real one, and every cancel came
     * back "somebody changed it first" about a task nobody had touched.
     */
    const source = fn("cancelSubtask");
    expect(source).toContain("expectedVersion");
    expect(source).not.toMatch(/transition\(subtaskId, 'cancel', \{\}\)/);
    // One resolver for both writes against a child, so they cannot disagree about where the version comes from.
    expect(source).toContain("subtaskVersion");
    expect(fn("completeSubtask")).toContain("subtaskVersion");
  });

  it("no longer resolves the dialog behind the user's back", () => {
    // The exact line that caused it, pinned so a future "simplification" cannot bring it back.
    expect(fn("confirmDestructive")).not.toMatch(/setTimeout\([^)]*resolve\(false\)/);
    expect(fn("confirmDestructive")).toContain("onCancel");
  });
});

describe("a write does not throw the reader back to the top of the page", () => {
  /*
   * MEASURED: scrollY 600 → add a subtask with Enter → scrollY 0. The page does not RELOAD (no navigation), it
   * re-renders: `root.innerHTML = …` collapses the document, the browser clamps the scroll, and the position is
   * gone. On a long detail page, adding three subtasks in a row means scrolling back three times.
   */
  it("render() puts the scroll position back — of the element that ACTUALLY scrolls", () => {
    /*
     * MEASURED IN THE BROWSER: `window.scrollY` reads 0 on this theme while the page is visibly scrolled — the
     * shell scrolls the root element, and the position lives on `document.scrollingElement.scrollTop`. The
     * first version of this fix captured window.scrollY, restored 0 to 0, and changed nothing.
     */
    const source = fn("render") + fn("scroller");
    expect(source).toMatch(/scrollingElement/);
    expect(source).toMatch(/scrollTop/);
  });

  it("and keeps the caret in the box you were typing in", () => {
    /*
     * Focus preservation ALREADY existed here (captureFocus/restoreFocus, from the a11y round) and knew about
     * the search box, rows and controls — not about the text boxes this page has since grown. Extended rather
     * than duplicated: a second focus mechanism beside the first is how they drift.
     */
    const source = fn("captureFocus") + fn("restoreFocus");
    expect(source).toMatch(/activeElement/);
    expect(source).toContain("data-wcn-subtask-input");
    expect(APP(), "a second focus mechanism was added beside the existing one").not.toMatch(/const focusSignature/);
  });

  it("does not blank the page to a spinner on a RE-read — the cause behind the cause", async () => {
    /*
     * The scroll restore was correct and did nothing, because the position was already lost before it ran:
     * every write called loadWorkItems, which blanked the page to a loading state first. The document collapsed
     * and the browser clamped the offset to zero. A spinner belongs to the first load, when there is nothing on
     * screen to keep.
     */
    const source = fn("loadWorkItems");
    expect(source).toMatch(/firstLoad/);
    expect(source).not.toMatch(/^\s*state\.loadState = 'loading';\s*$\n\s*state\.loadError/m);
  });

  it("restores it after the list is re-read, not only after the first paint", async () => {
    await boot(projectionItem({ subtasks: { mode: "full", items: [] } }));
    // A real scroll cannot be measured in jsdom (no layout), so the CALL is what is asserted here and the
    // pixels are measured in the browser.
    expect(APP()).toMatch(/after\.scrollTop = scrollTop/);
  });
});

describe("the card's chrome", () => {
  const child = (overrides) => Object.assign({
    id: "c1", title: "Bütçe", status: "not-started", assignee: null, dueAt: null, canCancel: true
  }, overrides);
  const withSubtasks = (items) => projectionItem({ subtasks: { mode: "full", items } });

  it("gives the add row's button the same height as its input", () => {
    const rule = /^\.wcn-subtask-add\s+\.btn\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, "the add row's button has no height rule").toBeTruthy();
    expect(rule[1]).toMatch(/2\.375rem|38px/);
  });

  it("names the detailed add for what it CREATES, and does not compete with Enter", async () => {
    await boot(withSubtasks([child()]));
    const button = app().querySelector("[data-wcn-subtask-add-detailed]");
    // Outline, not solid: the input already carries the primary path (Enter), and two solid buttons side by
    // side make the reader choose between two things that do the same job.
    expect(button.className).toContain("btn-outline-primary");
    expect(button.className).not.toContain("btn-label-secondary");
  });

  it("separates the rows with a hairline, and does not double it at the ends", () => {
    const rule = /^\.wcn-subtasks\s*>\s*li\s*\+\s*li\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, "there is no li + li separator rule").toBeTruthy();
    expect(rule[1]).toMatch(/border-block-start/);
    // A rule on every li would draw a line above the first row and below the last.
    expect(CSS()).not.toMatch(/^\.wcn-subtasks\s*>\s*li\s*\{[^}]*border-block-start/m);
  });

  it("makes the gate line LOOK like the block it is", async () => {
    await boot(withSubtasks([child({ id: "a" }), child({ id: "b" })]));
    const gate = app().querySelector(".wcn-subtask-gate");
    expect(gate, "the gate line is still plain text").not.toBeNull();
    // The page's existing alert pattern — no new colour was invented for it.
    expect(gate.className).toContain("alert");
    expect(gate.textContent).toContain("SubtasksBlockingNotice");
  });

  it("counts ONCE: a badge for how many there are, a reading for how many are done", async () => {
    /*
     * DECISION (item 8). "ALT GÖREVLER 5" beside "1 / 5 tamam" prints the total twice, which is exactly the
     * confusion reported. The badge keeps the total; the right-hand reading drops the denominator and says
     * "1 done". Two numbers, two jobs, no repetition — and the full "1 / 5" survives as the progress bar's
     * accessible name, where a screen reader needs the whole statement.
     */
    await boot(withSubtasks([child({ id: "a", status: "done" }), child({ id: "b" })]));
    const card = app().querySelector(".wcn-subtasks").closest(".wcn-detail-section");

    const badge = card.querySelector(".wcn-subtask-count");
    expect(badge, "the total is not in a badge").not.toBeNull();
    expect(badge.className).toContain("badge");
    expect(badge.textContent.trim()).toBe("2");

    const reading = card.querySelector(".wcn-subtask-progress").textContent;
    expect(reading).toContain("SubtaskDoneCount:1");
    expect(reading, "the total is printed twice").not.toContain("/");

    expect(card.querySelector("progress").getAttribute("aria-label")).toContain("SubtaskProgressCount:1");
  });

  it("uses the LIST surface's row menu, not a second one", async () => {
    /*
     * Measured on the list: `btn btn-icon dropdown-toggle hide-arrow` + `bx-dots-vertical-rounded icon-md`, and
     * a `dropdown-menu dropdown-menu-end m-0`. Two surfaces with two kebabs is how a UI stops being one product.
     */
    await boot(withSubtasks([child()]));
    const toggle = app().querySelector(".wcn-subtask [data-bs-toggle='dropdown']");
    expect(toggle, "the row has no menu").not.toBeNull();
    expect(toggle.className).toContain("btn-icon");
    expect(toggle.className).toContain("hide-arrow");
    expect(toggle.querySelector("i.bx-dots-vertical-rounded"), "the kebab is not the list's kebab").not.toBeNull();

    const menu = app().querySelector(".wcn-subtask .dropdown-menu");
    expect(menu.className).toContain("dropdown-menu-end");
    expect(menu.querySelector(".dropdown-item.wcn-menu-item")).not.toBeNull();
    // Destructive items read as destructive there, so they do here.
    expect(menu.querySelector("[data-wcn-subtask-cancel]").className).toContain("text-danger");
  });

  it("marks the row it just added so the reader can see where it went", async () => {
    const source = APP();
    expect(source).toMatch(/flashSubtaskId/);
    expect(CSS()).toMatch(/\.wcn-subtask-flash/);
    // The create response is the id ITSELF on the wire; reading `data.id` returned undefined and marked nothing.
    expect(source).toMatch(/typeof result\.data === 'string'/);
  });

  it("puts the new strings in seven languages", () => {
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const xml = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
      expect(xml, `${lang} has no SubtaskDoneCount`).toContain('name="SubtaskDoneCount"');
      const detailed = /<data name="SubtaskAddDetailed"[^>]*>\s*<value>([^<]*)<\/value>/.exec(xml);
      expect(detailed[1], `${lang} still labels it without the plus`).toContain("+");
    });
  });
});

// ── 14. the cap can actually be released ────────────────────────────────────

describe("'show all' releases the cap — the control, not its picture", () => {
  /*
   * <b>MEASURED LIVE.</b> 17 rows, the wrapper capped at 320px with 854px of content, the button rendered with
   * an EMPTY `data-wcn-showall` value — and no click handler for that attribute anywhere in app.js. Clicking it
   * changed nothing: 17 rows before, 17 after; 320px before, 320px after.
   *
   * <b>Why the suite missed it.</b> The test that should have caught it asserted the button EXISTS while its own
   * name claimed the cap could be released. A drawn control is not a working one, and that gap is the fifth
   * vacuous assertion found this session — so these tests count ROWS and read the wrapper's own class, never
   * the button's presence.
   */
  const child = (n) => ({ id: `s${n}`, title: `Alt görev ${n}`, status: "not-started", assignee: null, dueAt: null, canCancel: false });
  const comment = (n) => ({ id: `a${n}`, kind: "comment", text: `Yorum ${n}`, actor: { id: "u1", displayName: "Deniz" }, at: "2026-08-01T09:00:00+00:00" });

  const manySubtasks = () => projectionItem({
    subtasks: { mode: "full", items: Array.from({ length: 17 }, (_, i) => child(i + 1)) }
  });
  const manyComments = () => projectionItem({
    workItemCapabilities: ["planning", "execution", "subtasks", "activity"],
    subtasks: { mode: "full", items: [] },
    activity: Array.from({ length: 9 }, (_, i) => comment(i + 1))
  });

  const capOf = (selector) => {
    const list = app().querySelector(selector);
    return list ? list.closest(".wcn-scrollcap") : null;
  };

  it("the subtask cap OPENS: the wrapper stops capping and every row is in the flow", async () => {
    await boot(manySubtasks());

    const before = capOf(".wcn-subtasks");
    expect(before, "the 17-row list is not capped to begin with").not.toBeNull();
    expect(before.className, "the wrapper starts open").not.toContain("wcn-scrollcap-open");

    app().querySelector("[data-wcn-showall]").click();
    await new Promise((r) => setTimeout(r, 0));

    const after = capOf(".wcn-subtasks");
    // Either the wrapper is gone or it is explicitly opened — what must NOT survive is a live cap.
    const stillCapped = after && !after.className.includes("wcn-scrollcap-open");
    expect(stillCapped, "the cap is still capping after 'show all'").toBeFalsy();
    expect(app().querySelectorAll(".wcn-subtask")).toHaveLength(17);
  });

  it("the ACTIVITY cap opens too — the same dead button was rendered twice", async () => {
    await boot(manyComments());

    const before = capOf(".wcn-audit");
    expect(before, "the 9-entry feed is not capped").not.toBeNull();

    app().querySelector("[data-wcn-showall]").click();
    await new Promise((r) => setTimeout(r, 0));

    const after = capOf(".wcn-audit");
    expect(after && !after.className.includes("wcn-scrollcap-open"),
      "the activity cap is still capping").toBeFalsy();
    expect(app().querySelectorAll(".wcn-audit-item")).toHaveLength(9);
  });

  it("the button knows WHICH list it opens — an empty attribute cannot address anything", async () => {
    await boot(manySubtasks());
    expect(app().querySelector("[data-wcn-showall]").getAttribute("data-wcn-showall")).toBe("subtasks");
  });

  it("turns into 'show less' once open, so an opened cap can be closed again", async () => {
    /*
     * DECISION: the button STAYS and changes its word. Hiding it would strand a seventeen-row list open with no
     * way back — a new dead end in place of the old dead button. Toggling also keeps one control for one
     * concept, which is what made the missing handler findable in the first place.
     */
    await boot(manySubtasks());
    app().querySelector("[data-wcn-showall]").click();
    await new Promise((r) => setTimeout(r, 0));

    const button = app().querySelector("[data-wcn-showall]");
    expect(button, "the control vanished, so the list cannot be collapsed again").not.toBeNull();
    expect(button.textContent).toContain("ShowLess");

    button.click();
    await new Promise((r) => setTimeout(r, 0));
    const capped = capOf(".wcn-subtasks");
    expect(capped, "the list did not collapse again").not.toBeNull();
    expect(capped.className).not.toContain("wcn-scrollcap-open");
  });

  it("opening one list does not open the other", async () => {
    // Two cards, two answers: expanding the subtasks must not silently expand the feed underneath it.
    await boot(projectionItem({
      workItemCapabilities: ["planning", "execution", "subtasks", "activity"],
      subtasks: { mode: "full", items: Array.from({ length: 17 }, (_, i) => child(i + 1)) },
      activity: Array.from({ length: 9 }, (_, i) => comment(i + 1))
    }));

    app().querySelector("[data-wcn-showall='subtasks']").click();
    await new Promise((r) => setTimeout(r, 0));

    expect(capOf(".wcn-audit").className, "the activity feed opened by itself").not.toContain("wcn-scrollcap-open");
  });

  it("keeps the reader's place when the list grows under them", () => {
    // The expansion goes through the same render, so it inherits this round's scroll/focus preservation.
    expect(fn("render")).toMatch(/scrollTop/);
  });

  it("says 'show less' in seven languages", () => {
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
        `${lang} has no ShowLess`).toContain('name="ShowLess"');
    });
  });
});

/*
 * ── THE LIFECYCLE CARD, rebuilt ───────────────────────────────────────────────────────────────────────────
 *
 * The owner's reading was "the ugliest area of the detail page", and the measurement agreed: 177px — a fifth of
 * a 900px screen, ~290px once a task was blocked — spent on two rows of chips that looked identical while
 * saying different kinds of thing, a 22px heading naming the strip below it, and four station labels that are
 * the same four words on every task in the system.
 *
 * These tests hold the SHAPE of the rebuilt card and, more importantly, the accessibility that the visual
 * shortening is only allowed to exist alongside. Hiding three of four labels is a VISUAL abbreviation; if the
 * names stop reaching the accessibility tree it becomes a deletion, and the card's whole job — "where does this
 * work stand" — reaches a screen-reader user not at all. That is the pairing these assertions defend.
 */
describe("the lifecycle card says where the work stands", () => {
  const stepsOf = () => [...app().querySelectorAll(".wcn-steps li")];

  it("marks the current step with aria-current, which is the only machine-readable position", async () => {
    /*
     * MUTATION TARGET. Before this round the position lived exclusively in a CSS class name
     * (`wcn-step-active`) — measured: `[aria-current]` appeared 0 times on the page — so a screen reader read
     * four station names in order and learned nothing about which one the task was at. Removing the attribute
     * puts that hole straight back and fails here.
     */
    await boot(projectionItem());
    const current = stepsOf().filter((li) => li.getAttribute("aria-current") === "step");
    expect(current, "exactly one step must claim the position").toHaveLength(1);
    expect(current[0].className).toContain("wcn-step-active");
  });

  it("keeps every step's NAME reachable even though NONE is drawn", async () => {
    /*
     * The load-bearing half of the visual shortening, and it got stricter when the rail became a bar: the
     * station names are not drawn AT ALL now — the caption above carries the readable sentence — so all four
     * names exist only in the accessibility tree. A sighted reader can drop Açık→Planlandı→Devam→Tamam because
     * they saw the same four words on the previous task; a screen-reader user gets no such head start.
     *
     * Hidden, never removed. If a later "cleanup" deletes the label spans because nothing paints them, this is
     * the assertion that stops the bar from becoming four coloured rectangles that say nothing.
     */
    await boot(projectionItem());
    const labels = stepsOf().map((li) => li.querySelector(".wcn-step-label"));
    expect(labels.every((l) => l && l.textContent.trim().length > 0),
      "a step lost its name entirely").toBe(true);
    expect(labels.every((l) => l.classList.contains("visually-hidden")),
      "the bar drew a station name").toBe(true);
  });

  it("states done/current/upcoming in WORDS, not only in colour", async () => {
    // Green-means-done is unavailable to a screen reader and to a reader who cannot separate those hues.
    await boot(projectionItem());
    const text = stepsOf().map((li) => li.textContent);
    expect(text.some((s) => s.includes("StepStateDone"))).toBe(true);
    expect(text.some((s) => s.includes("StepStateCurrent"))).toBe(true);
    expect(text.some((s) => s.includes("StepStateUpcoming"))).toBe(true);
  });

  it("keeps decoration OUT of the step's accessible name entirely", async () => {
    /*
     * Measured two rounds ago: the second step announced as "2 Planlandı" — the dot's ordinal was part of the
     * name. That was fixed by hiding the dot; the bar removes the dot outright, so there is no decoration left
     * inside an <li> to leak. This asserts the OUTCOME rather than the old mechanism, which is why it survived
     * the change of visual: whatever a segment comes to contain, its name stays name + state.
     */
    await boot(projectionItem());
    stepsOf().forEach((li) => {
      expect(li.querySelector(".wcn-step-dot"), "the dot came back without a hidden marker").toBeNull();
      expect(li.textContent, "a digit reached the accessible name").not.toMatch(/\d/);
    });
  });

  it("names the strip itself, since the heading above it is gone", async () => {
    // The 22px "YAŞAM DÖNGÜSÜ" heading restated what the strip below it plainly was. The words moved to the
    // list's accessible name — where a screen reader meeting four bare list items genuinely needs them — and
    // the resource key is unchanged.
    await boot(projectionItem());
    expect(app().querySelector(".wcn-steps").getAttribute("aria-label")).toBe("StepBarLabel");
    expect(app().querySelector(".wcn-detail-head h6"), "the heading came back").toBeNull();
  });

  it("puts provenance and signals on ONE row, in two different voices", async () => {
    /*
     * They were two stacked rows of identical-looking chips. Provenance ("which record is this") is filing
     * information: constant, unactionable, true of every task from that module. Signals ("what is going on with
     * THIS work") are volatile and specific and are the only half that earns colour.
     */
    await boot(projectionItem());
    const line = app().querySelector(".wcn-detail-idline");
    expect(line, "the identity line is gone").not.toBeNull();
    expect(app().querySelector(".wcn-detail-source"), "the old chip row survived").toBeNull();
    expect(app().querySelector(".wcn-detail-chips"), "the old chip row survived").toBeNull();
    expect(line.querySelectorAll(".wcn-detail-prov").length).toBeGreaterThan(0);
    expect(line.querySelector(".wcn-detail-idsep"), "no rule separating the two voices").not.toBeNull();
  });

  it("moves the status OFF the identity row and into the bar's caption", async () => {
    /*
     * "Where the work stands" was sitting among "what this record is". It belongs to the bar — which shows the
     * position and cannot name it, because the bar has no text at all.
     *
     * The caption sits ABOVE the segments, not beside them: it was at the end of the old rail while that rail
     * still had station labels, and with the labels gone it would have been a stray word floating next to a
     * graphic.
     */
    await boot(projectionItem());
    expect(app().querySelector(".wcn-detail-idline .wcn-badge"), "status badge still in the id row").toBeNull();
    const bar = app().querySelector(".wcn-stepbar");
    const caption = bar.querySelector(".wcn-stepbar-caption");
    expect(caption, "the bar has no caption").not.toBeNull();
    expect(caption.querySelector(".wcn-stepbar-status")).not.toBeNull();
    const kids = [...bar.children];
    expect(kids.indexOf(caption), "the caption is not above the bar")
      .toBeLessThan(kids.indexOf(bar.querySelector(".wcn-steps")));
  });

  it("counts the position in the caption, and refuses to on cancelled work", async () => {
    /*
     * The bar shows progress by construction; counting filled segments is a task and reading "3/4" is not.
     *
     * A cancelled task is the exception: its position marker sits at the first step by convention, so a count
     * would state a progress that called-off work never made.
     */
    await boot(projectionItem());
    expect(app().querySelector(".wcn-stepbar-count").textContent).toMatch(/^\d+\/\d+$/);

    await boot(projectionItem({
      normalizedStatus: "Cancelled", taskLifecycle: "Cancelled", executionState: "notApplicable"
    }));
    expect(app().querySelector(".wcn-stepbar-count"), "a cancelled task claimed progress").toBeNull();
    expect(app().querySelector(".wcn-stepbar-status"), "…and lost its status name too").not.toBeNull();
  });

  it("draws segments that are readouts, not controls", async () => {
    // Three defects of the "looks pressable, is not" class came out of this session already.
    await boot(projectionItem());
    stepsOf().forEach((li) => {
      expect(li.tagName).toBe("LI");
      expect(li.getAttribute("onclick")).toBeNull();
      expect(li.getAttribute("role"), "a segment claimed an interactive role").toBeNull();
      expect(li.matches('a[href],button,[tabindex]:not([tabindex="-1"])')).toBe(false);
    });
    /*
     * Comments STRIPPED before matching. The rule explains in prose why it does not set `cursor: pointer`, and
     * a guard that reads prose cannot tell a declaration from a sentence about one — the same trap that made a
     * CDN check fail earlier in this session.
     */
    const css = read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");
    const rule = /\.wcn-step \{([^}]*)\}/.exec(css);
    expect(rule, "the segment rule is gone").not.toBeNull();
    expect(rule[1], "a segment offers a pointer it cannot honour").not.toMatch(/cursor:\s*pointer/);
  });
});

describe("a finished task says WHEN it finished", () => {
  it("prints the closing date at the end of the strip", async () => {
    /*
     * `closedAt` has been on the wire for rounds, is normalised in the data layer and is used to freeze a
     * finished task's SLA count — and it was drawn on this page ZERO times. A finished task showed a green rail
     * ending at "Tamam" and never said when, which is half of the one answer the strip exists to give.
     */
    await boot(projectionItem({
      // `notApplicable`, not an invented "closed": the contract's execution states are
      // notStarted/active/paused/notApplicable, and it separately refuses a Done task that is still `active`.
      normalizedStatus: "Done", taskLifecycle: "Done", executionState: "notApplicable",
      closedAt: "2026-07-29T21:39:01.621239+00:00"
    }));
    const closed = app().querySelector(".wcn-stepbar-closed");
    expect(closed, "no closing date on a closed task").not.toBeNull();
    // Date-only, because that is what every other date on this page is: `toDateOnly` normalises at the
    // projection seam and render sites print what they are given. A second date format would be a new one.
    expect(closed.textContent).toContain("2026-07-29");
    expect(closed.textContent, "the instant leaked through unnormalised").not.toContain("21:39");
  });

  it("adds nothing at all to an open task", async () => {
    // The card must not grow a row for a fact that does not exist yet.
    await boot(projectionItem());
    expect(app().querySelector(".wcn-stepbar-closed")).toBeNull();
  });
});

describe("the blocked notice says it once and points at the real list", () => {
  const blocked = (n, code = "SUBTASK_BLOCKED") => projectionItem({
    /*
     * The contract refuses a `blockedState` that names an action the item does not carry as DISABLED with a
     * reason — which is the right rule: "this is blocked" must be traceable to a control the reader can see is
     * off. So the fixture ships the disabled action the blockers point at.
     */
    actions: [{
      code: "complete",
      label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
      semanticType: "complete",
      enabled: false,
      source: "provider",
      disabledReasonCode: "SUBTASK_BLOCKED",
      disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_SubtaskBlocked" },
      requiresConfirmation: false,
      requiresReason: false,
      requiresEvidence: false,
      supportsBulk: false,
      riskLevel: "normal"
    }],
    // The subtasks the blockers ARE. The whole point of the one-liner is that this list already names them, so
    // a fixture without it would be testing the collapse against a page that had nothing to collapse into.
    subtasks: { mode: "full", items: Array.from({ length: n }, (_, i) => subtask(i)) },
    blockedState: {
      blocked: true,
      affectedActionCodes: ["complete"],
      blockers: Array.from({ length: n }, (_, i) => ({
        code, taskItemId: `b${i}`, affectedActionCode: "complete",
        label: { kind: "display", text: `Alt görev ${i}`, locale: "und" }
      }))
    }
  });

  it("collapses N subtask blockers to ONE sentence", async () => {
    /*
     * MEASURED before, on a live blocked task: a title ("3 sorun ilerlemeyi engelliyor") followed by three rows
     * each ending "…tamamlamayı engelliyor" — the same sentence four times — naming three subtasks that the
     * Subtasks card already listed by name with their own controls. The banner was a second, worse copy of a
     * list the page already had.
     */
    await boot(blocked(3));
    const banner = app().querySelector(".wcn-blocked");
    expect(banner.classList.contains("wcn-blocked-oneline")).toBe(true);
    expect(banner.querySelector(".wcn-blocked-list"), "the repeated list survived").toBeNull();
    expect(banner.textContent).toContain("BlockedSubtaskOneLine");
  });

  it("carries a link to the subtasks card, and the card exists to receive it", async () => {
    // A count with no route to the thing counted just relocates the question.
    await boot(blocked(2));
    const link = app().querySelector("[data-wcn-goto-subtasks]");
    expect(link, "no link out of the notice").not.toBeNull();
    expect(link.tagName, "a div cannot be reached by Tab or fired by Enter").toBe("BUTTON");
    expect(app().querySelector("#wcn-subtasks-card"), "the link's target does not exist").not.toBeNull();
  });

  it("HAS A HANDLER — the assertion this session earned the hard way", async () => {
    /*
     * MUTATION TARGET. Twice in this session a control shipped with no handler at all ("Tümünü gör", in two
     * places): it looked finished, it was reachable, it did nothing. Deleting the `data-wcn-goto-subtasks`
     * branch from the click delegate fails here.
     *
     * Asserted as BEHAVIOUR, not as source text: the notice is rendered while the Activity tab is open — the
     * case a plain `#anchor` could never handle, because the subtasks card is in the DOM but hidden — and the
     * click must both switch the tab and reveal the card.
     */
    await boot(blocked(3));
    app().querySelector('[data-wcn-detail-tab="activity"]').click();
    await new Promise((r) => setTimeout(r, 0));
    const panelOf = () => app().querySelector("#wcn-subtasks-card").closest("[data-wcn-detail-panel]");
    expect(panelOf().classList.contains("d-none"), "precondition: the card starts hidden").toBe(true);

    app().querySelector("[data-wcn-goto-subtasks]").click();
    await new Promise((r) => setTimeout(r, 0));

    expect(panelOf().classList.contains("d-none"), "the link did not reveal the card").toBe(false);
    expect(app().querySelector('[data-wcn-detail-tab="general"]').classList.contains("active")).toBe(true);
  });

  it("keeps the FULL list when a blocker is not a subtask", async () => {
    /*
     * A dependency-typed blocker appears nowhere else on this page, so collapsing it would delete information
     * rather than de-duplicate it — and the link would point at a card that does not contain it.
     */
    await boot(blocked(2, "DEPENDENCY_BLOCKED"));
    const banner = app().querySelector(".wcn-blocked");
    expect(banner.classList.contains("wcn-blocked-oneline")).toBe(false);
    expect(banner.querySelector(".wcn-blocked-list")).not.toBeNull();
    expect(app().querySelector("[data-wcn-goto-subtasks]"), "a link that would point nowhere").toBeNull();
  });

  it("stays BELOW the strip — the strip asks 'where', the notice answers 'why not'", async () => {
    await boot(blocked(3));
    const card = app().querySelector(".wcn-detail-command");
    const kids = [...card.children];
    expect(kids.indexOf(card.querySelector(".wcn-stepbar")))
      .toBeLessThan(kids.indexOf(card.querySelector(".wcn-blocked")));
  });
});

describe("the detail tabs and their panels are one widget, not two", () => {
  it("wires tab → panel in both directions", async () => {
    /*
     * `role="tab"` and `aria-selected` were already here; they describe the STRIP. Without `aria-controls` and a
     * matching panel `id` + `aria-labelledby`, a screen reader is told "tab, selected" and has no way to reach
     * or name what was selected. The list page (`#wcn-main-panel`) already did this correctly — this is that
     * pattern, not a second invention.
     */
    await boot(projectionItem());
    ["general", "activity"].forEach((key) => {
      const tab = app().querySelector(`[data-wcn-detail-tab="${key}"]`);
      const panel = app().querySelector(`[data-wcn-detail-panel="${key}"]`);
      expect(tab.id, key).toBe(`wcn-detail-tab-${key}`);
      expect(tab.getAttribute("aria-controls"), key).toBe(`wcn-detail-panel-${key}`);
      expect(panel.id, key).toBe(`wcn-detail-panel-${key}`);
      expect(panel.getAttribute("aria-labelledby"), key).toBe(`wcn-detail-tab-${key}`);
      expect(panel.getAttribute("tabindex"), key).toBe("0");
    });
  });

  it("is ONE tab stop, and the stop moves with the selection", async () => {
    // A tablist is one stop; arrows move within it. Two stops make Tab walk the strip instead of leaving it.
    await boot(projectionItem());
    const tabindexes = () => ["general", "activity"]
      .map((k) => app().querySelector(`[data-wcn-detail-tab="${k}"]`).getAttribute("tabindex"));
    expect(tabindexes()).toEqual(["0", "-1"]);

    app().querySelector('[data-wcn-detail-tab="activity"]').click();
    await new Promise((r) => setTimeout(r, 0));
    // Set only at render time it would stay behind, and Tab would land on the tab that is no longer current.
    expect(tabindexes()).toEqual(["-1", "0"]);
  });
});
