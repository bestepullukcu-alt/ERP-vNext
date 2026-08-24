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
    const order = ["renderActionRail", "renderStatusCard", "renderNote", "renderSourceCard"];
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
    /*
     * The helper this used to inspect (`summaryFact`) is gone with the fact grid; the rule outlived it and now
     * lives in `renderSummary`'s own `row()`. Asserted on BEHAVIOUR as well as source, so the next refactor
     * cannot quietly reintroduce a dash while keeping the shape.
     */
    const source = fn("renderSummary");
    expect(source).not.toContain("'—'");
    expect(source, "no row builder that can decline to print").toMatch(/\?\s*''/);
  });

  test("the description is printed only when there is one", () => {
    /*
     * The sentence is a LABELLED FIELD now, not a bare paragraph — measured on two purpose-built tasks: a task
     * with a description projects `summary = {kind:"display", text:…}`, one without projects `summary = null`.
     * There is no generated fallback, so the field simply does not render when there is nothing to say.
     */
    expect(fn("renderSummary")).toMatch(/'DetailDescription', item\.summary/);
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
    /*
     * ⚠ THE DATES LEFT THIS CARD (BL-114). It was named "Durum" and, on a real task, held nothing but two
     * dates — a card named for status containing none. Worse, its due date rendered RED while the Summary's
     * rendered grey: one screen, two answers about one fact.
     *
     * The due date moved to the Summary and took the red with it; the personal plan moved to the Personal card.
     * The GATE rows stayed, which is why the card still exists at all — the brief's premise that it held only
     * dates was true of that task, not of the renderer.
     */
    const source = fn("renderStatusCard");
    expect(source).toContain("gateRow");
    expect(source, "the dates came back to the status card").not.toContain("item.dueAt");
    expect(fn("renderSummary"), "the due date is not in the summary").toContain("SourceDueLabel");
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

describe("the source card replaced the folded technical block", () => {
  /*
   * ⚠ THIS WHOLE BLOCK REVERSED, and the reasoning is worth keeping.
   *
   * It used to REQUIRE the card to be a closed `<details>` holding every field "support might need". Measured
   * against what it cost: a three-row card behind a click, titled "Teknik bilgi" — a sign saying THIS IS NOT
   * FOR YOU to the one reader who actually needs the source record — printing "Görevler", "task" and a GUID
   * that are identical on every task, plus a concurrency token that changes on every save and means nothing on
   * its own.
   *
   * "Nothing was DELETED" was the old rule. It is still true of the RESOURCE KEYS (none were removed) and no
   * longer true of the rows: a field kept because support "might" want it is how a card fills with noise. The
   * fields that distinguish something come back on their own for a foreign provider.
   */
  it("is a plain card, not a disclosure", async () => {
    await boot(projectionItem());
    expect(app().querySelector("details.wcn-tech"), "the fold survived").toBeNull();
    expect(app().querySelector(".wcn-source"), "there is no source card").not.toBeNull();
  });

  it("keeps every retired resource key, marked unused rather than deleted", () => {
    // A removed key is a broken fallback for anything still asking for it, and these may return on a
    // diagnostics surface.
    const tr = read("Resources", "Views", "WorkCenterNext", "WorkCenterNextIndex.tr.resx");
    ["TechnicalDetailsLabel", "TechVersionValue", "DetailSourceVersion", "DetailActionDepth",
      "ActionDepthInline", "ActionDepthDeeplink"].forEach((key) =>
      expect(tr, `${key} was deleted rather than retired`).toContain(`name="${key}"`));
  });

  it("keeps the id copyable where the id is worth copying", async () => {
    // ⚠ FIXTURE-ONLY branch: no live record is foreign today.
    await boot(projectionItem({
      source: {
        providerCode: "workflow", providerContractVersion: "1.0",
        objectType: "approval", objectId: "REG-2026-0184"
      }
    }));
    expect(app().querySelector(".wcn-source [data-wcn-copy]"), "the foreign key cannot be copied").not.toBeNull();
  });
});

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
    // The call now carries the resolver's lock as a second argument — one renderer still, and the flag comes
    // from the resolver rather than being recomputed inside it.
    expect(detailHtml()).toContain("renderActionRail(item, surface.interactionLocked, surface)");
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
    ["renderSummary", "renderStatusCard", "renderSourceCard"].forEach((name) => {
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
    /*
     * ⚠ THE SEED CARRIES A SLOT PER ARGUMENT, not just `{0}`.
     *
     * It used to be `` `${key}:{0}` ``, so a two-argument message silently lost its SECOND value — and the very
     * defect this harness exists to catch (a sentence that drops one of its two facts) would have passed. Found
     * by `WaitingOnWithReason`, whose whole point is that the person and the reason both survive.
     */
    tf: (key, ...args) => args.reduce(
      (text, value, i) => text.split(`{${i}}`).join(String(value)),
      `${key}:` + args.map((_, i) => `{${i}}`).join(" ")),
    tn: (key) => key
  }
});

const withPlanAction = (plannedDate) => projectionItem({
  plannedDate,
  primaryActionCode: "plan",
  actions: [{
    code: "plan", label: { kind: "resource", key: "WorkAggregation_Action_plan" },
    semanticType: "plan", enabled: true, source: "provider",
    disabledReasonCode: null, disabledReason: null, requiresConfirmation: false,
    requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
  }]
});

/*
 * Wait for a CONDITION, with a ceiling — never for a fixed number of milliseconds (BL-159).
 *
 * A sleep encodes a guess about how fast the machine is. Under full-suite load that guess is wrong often enough
 * to produce a red run that means nothing, and the usual repair — a bigger number — is a deferral, not a fix.
 * This returns the instant the thing being waited for is true, and fails loudly if it never becomes true.
 */
const until = async (predicate, { timeout = 2000, step = 5 } = {}) => {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (predicate()) { return; }
    await new Promise((r) => setTimeout(r, step));
  }
  // One last look, so a predicate that flipped inside the final gap is not reported as a timeout.
  if (!predicate()) { throw new Error(`until(): condition never became true within ${timeout}ms`); }
};

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

  it("keeps the GATES in one card — the dates no longer live there", async () => {
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
    expect(withGates[0].querySelector(".wcn-dates"), "the dates came back to the status card").toBeNull();
    // …and the due date is in the Summary, in red, which is where the two-answers contradiction was settled.
    // The summary uses the product's golden field pattern now (`backbone-preview-field`), not a list of its own.
    expect(app().querySelector(".backbone-preview-field"), "the summary lost the golden field pattern").not.toBeNull();
    // The gate that does not apply is not printed at all.
    expect(withGates[0].querySelectorAll(".wcn-gate")).toHaveLength(1);
  });

  it("renders the source card open, in the rail, with the other cards", async () => {
    /*
     * ⚠ REVERSED. It required a CLOSED `<details>` "present and complete". The fold cost a click and the
     * completeness cost the card its readability: six fields of which four were identical on every task.
     */
    await boot(projectionItem());
    const rail = app().querySelector(".wcn-detail-rail");
    const card = rail.querySelector(".wcn-source");
    expect(card, "there is no source card in the rail").not.toBeNull();
    expect(card.closest("details"), "the card is still folded").toBeNull();
    expect(card.querySelector("h6"), "the card has no standard heading").not.toBeNull();
  });

  it("keeps the whole card when there are no subtasks yet", async () => {
    /*
     * ⚠ THIS REVERSES A WRITTEN DECISION (owner, 2026-08-24), and it is reversed openly rather than quietly.
     *
     * The old shape — and the old assertion — was ONE LINE: the card's head disappeared with its last child and
     * a `wcn-empty-line` took the whole card's place. Reported as a defect: the card a reader had been looking
     * at vanished the moment the list emptied, so "never had any" and "just emptied" looked like two different
     * screens, and the counter, the head and the add row all went with it.
     *
     * The new shape is the SIBLING CARD'S: head kept (count 0), add row where it always is, and the sentence
     * beneath it as a `.wcn-block-hint` — no alert, because an empty list is not a warning.
     */
    await boot(projectionItem());
    const card = app().querySelector("#wcn-subtasks-card");
    expect(card, "the card vanished with its last child").not.toBeNull();
    expect(card.querySelector(".wcn-subtask-count").textContent).toBe("0");
    expect(card.querySelector("[data-wcn-subtask-input]"), "the card cannot add anything").not.toBeNull();
    expect(app().querySelector(".wcn-empty-line"), "the one-line empty state is back").toBeNull();
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

  it("says nothing-yet UNDER the add row, in the card that stayed", async () => {
    // Same reversal as above (owner, 2026-08-24): the empty card is still a card. The sentence lands where a
    // removed row would have been, so an emptied list and a never-filled one look the same.
    await boot(withSubtasks([]));
    const card = app().querySelector("#wcn-subtasks-card");
    expect(card.querySelector("[data-wcn-subtask-input]")).not.toBeNull();
    const hints = [...card.querySelectorAll(".wcn-block-hint")].map((h) => h.textContent.trim());
    expect(hints).toContain("SubtasksEmpty");
    expect(card.innerHTML.indexOf("wcn-subtask-add")).toBeLessThan(card.innerHTML.indexOf("SubtasksEmpty"));
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
    /*
     * ⚠ INVERTED (BL-113). The unwrapping moved OUT of every caller and into `TasksApi.assignablePeople`, which
     * is the only change that could stop a fifth caller writing a fifth wrong expression. `data` is the array.
     */
    const app_ = APP();
    expect(app_, "app.js is unwrapping the envelope again").not.toMatch(/people\.data\?\.people|res\.data\?\.people/);
    const api = read("wwwroot", "assets", "js", "Tasks", "api.js");
    expect(api, "TasksApi stopped unwrapping the envelope").toMatch(/Array\.isArray\(res\.data\?\.people\)/);
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
    /*
     * ⚠ BL-159 — WAIT FOR THE CONDITION, NOT THE CLOCK.
     *
     * This was `setTimeout(30)`, and it failed in roughly one full-suite run in three: the stubbed confirm
     * resolves on a 5ms timer and the write is two awaits behind it, so 25ms of slack is enough on an idle
     * machine and not enough under load. Raising the number would only move the threshold — the next slower run
     * fails again, and by then nobody remembers why the number is what it is.
     *
     * `until` polls for the thing the test is actually about (the call arrived), so it returns as soon as it is
     * true and gives up only after a ceiling no realistic scheduler reaches.
     */
    await until(() => calls.length > 0);

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
    /*
     * A FIXED WAIT IS CORRECT HERE, unlike its neighbour above, and the difference is the direction of the
     * assertion: this one proves nothing happened. A condition-wait has nothing to wait FOR — it would return
     * immediately and prove only that the call had not arrived YET. Its failure mode is also the safe one: too
     * short makes a false pass, never a false red, so it cannot produce the noisy failure BL-159 was about.
     */
    await new Promise((r) => setTimeout(r, 30));

    expect(calls).toHaveLength(0);
  });

  it("shows the server's reason when the cancel is refused", async () => {
    await boot(withSubtasks([child()]));
    withConfirm("yes");
    global.TasksApi.transition = () => Promise.resolve({ ok: false, status: 409, reasonCode: "TASK_INVALID_STATE" });
    global.TasksApi.failureMessage = () => "Bu görev bu durumdayken iptal edilemez";

    app().querySelector("[data-wcn-subtask-cancel]").click();
    // Wait for the REFUSAL to have been processed — the write settled and the surface re-read — rather than for
    // a number of milliseconds (BL-159).
    await until(() => !!app().querySelector(".wcn-subtask"));

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

  it("gives each row its own box rather than a hairline between rows", () => {
    /*
     * ⚠ THIS ASSERTION REPLACED, deliberately. It used to REQUIRE the `li + li` hairline — a separator that drew
     * a line between rows and none at the ends, which was correct for a list of bare lines.
     *
     * The subtask row is a BOX now, in the same language as the checklist row beside it (both are interactive
     * objects carrying their own controls, and once the checklist's grey fill went white the box became the only
     * thing distinguishing a row). A box needs no separator: it has four sides of its own, and the `li + li`
     * hairline would have drawn a second line immediately under the box above it.
     */
    const rule = /^\.wcn-subtasks\s*>\s*li\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, "the subtask row lost its box").toBeTruthy();
    expect(rule[1]).toMatch(/border:\s*1px solid/);
    expect(CSS(), "the between-rows hairline survived alongside the box")
      .not.toMatch(/^\.wcn-subtasks\s*>\s*li\s*\+\s*li\s*\{[^}]*border-block-start/m);
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
    /*
     * ⚠ PROVENANCE IS NOW CONDITIONAL (BL-118). Measured: every record on this surface carries
     * `providerCode: "tasks"` and `objectType: "task"`, so "Görevler · Görev" printed on every task and
     * distinguished nothing — two constants dressed as facts, taking the eye's first pass ahead of the signals
     * that actually vary. It is not deleted: the day a second provider lands here it appears on its own.
     *
     * The separator goes with it. A hairline before a row that starts with its first chip divides nothing.
     */
    expect(line.querySelectorAll(".wcn-detail-prov"), "a default provider printed its name").toHaveLength(0);
    expect(line.querySelector(".wcn-detail-idsep"), "a rule was drawn with nothing to separate").toBeNull();
    // …and the signals, which DO vary, are still there.
    expect(line.querySelectorAll(".wcn-chip").length, "the signal chips are gone").toBeGreaterThan(0);
  });

  it("SHOWS provenance the moment it distinguishes something", async () => {
    // MUTATION TARGET (provenance). The field must reappear without being rebuilt when a second provider
    // arrives — that is the whole reason it was conditioned rather than removed.
    await boot(projectionItem({
      source: {
        providerCode: "workflow", providerContractVersion: "1.0",
        objectType: "approval", objectId: "A-1", sourceSystem: "MOD-0023"
      }
    }));
    const line = app().querySelector(".wcn-detail-idline");
    expect(line.querySelectorAll(".wcn-detail-prov").length, "a foreign provider stayed hidden").toBeGreaterThan(0);
    expect(line.querySelector(".wcn-detail-idsep"), "the rule did not come back with it").not.toBeNull();
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

describe("the page header spaces itself like every other detail page", () => {
  it("sits OUTSIDE the grid row, so it collects one margin and not two", async () => {
    /*
     * MEASURED, live, 1440px: breadcrumb → first card was 28px on this page and 12px on `/Tasks/Create` and the
     * Positions/OrgUnits Details pages — the Golden Reference Compact shape whose markup this header explicitly
     * copies. The markup was copied; the PLACEMENT was not.
     *
     * The header was `<div class="col-12">` inside `.row.g-4`, so the card below it received the header's own
     * `mb-3` (12px) AND the row's vertical gutter (16px). Two spacing systems stacking into a number neither of
     * them chose — invisible in code review, obvious on screen.
     *
     * Asserted STRUCTURALLY because jsdom performs no layout: the pixel claim is measured in the browser and
     * reported with the round, and this is the shape that produces it. Putting the header back into the row
     * fails here.
     */
    await boot(projectionItem());
    const page = app().querySelector(".wcn-details-page");
    const grid = page.querySelector(".wcn-detail-grid");
    const breadcrumb = app().querySelector(".breadcrumb");

    expect(breadcrumb, "no breadcrumb on the page").not.toBeNull();
    expect(grid.contains(breadcrumb), "the header is back inside the grid row").toBe(false);
    // …and it is a direct child of the page, before the grid — not floated somewhere else on the page.
    const kids = [...page.children];
    const header = kids.find((el) => el.contains(breadcrumb));
    expect(header, "the header is not a direct child of the page").toBeTruthy();
    expect(kids.indexOf(header)).toBeLessThan(kids.indexOf(grid));
  });

  it("leaves the grid carrying only the three regions", async () => {
    // The row's columns are head/content/rail. A fourth full-width column would be the header sneaking back in.
    await boot(projectionItem());
    const cols = [...app().querySelector(".wcn-detail-grid").children];
    expect(cols).toHaveLength(3);
    expect(cols[0].className).toContain("wcn-detail-head");
    expect(cols[1].className).toContain("wcn-detail-content");
    expect(cols[2].className).toContain("wcn-detail-rail");
  });
});

/*
 * ── THE ACTIONS CARD ──────────────────────────────────────────────────────────────────────────────────────
 *
 * It was 412px — half the rail — of four near-identical rows: a button and a sentence, four times, told apart
 * only by which one was `btn-primary`. The page exists so somebody can ACT, and the act was a matter of noticing
 * a hue among four explanations none of which had been chosen yet.
 */
describe("the actions card puts its weight where the decision is", () => {
  const withActions = (list) => projectionItem({
    primaryActionCode: list[0].code,
    actions: list.map((a) => Object.assign({
      label: { kind: "resource", key: `WorkAggregation_Action_${a.code}` },
      semanticType: a.code, enabled: true, source: "provider",
      disabledReasonCode: null, disabledReason: null, requiresConfirmation: false,
      requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
    }, a))
  });

  it("separates the three tiers by STRUCTURE, not by colour", async () => {
    /*
     * MUTATION TARGET (tier). The primary is named by the SERVER (`primaryActionCode`); this asserts the card
     * obeys it structurally — a full-width row of its own — rather than tinting one of four equal rows.
     */
    await boot(withActions([
      { code: "accept" }, { code: "plan" }, { code: "cancel", riskLevel: "destructive" }
    ]));
    const card = app().querySelector(".wcn-acts");
    expect(card, "the actions card is gone").not.toBeNull();
    expect(card.querySelectorAll(".wcn-act-primary")).toHaveLength(1);
    expect(card.querySelector(".wcn-act-primary span").textContent).toContain("accept");
    expect(card.querySelectorAll(".wcn-act-secondary").length).toBeGreaterThan(0);
  });

  it("keeps the destructive action VISIBLE, never folded into a kebab", async () => {
    /*
     * MUTATION TARGET (destructive). "Görevi iptal et" used to live inside a "Diğer aksiyonlar" menu. Hiding a
     * destructive act is not safety — the reader who wants it hunts, and the reader who does not is protected by
     * the confirm dialog, not by the menu. What the menu bought was a page that could cancel a task without ever
     * showing the word.
     */
    await boot(withActions([{ code: "accept" }, { code: "cancel", riskLevel: "destructive" }]));
    const card = app().querySelector(".wcn-acts");
    expect(card.querySelector(".wcn-actrail-menu"), "the kebab came back").toBeNull();
    const destructive = card.querySelector(".wcn-act-destructive");
    expect(destructive, "the destructive action is not drawn in the open").not.toBeNull();
    expect(destructive.querySelector("button").className).toContain("wcn-act-bare-danger");
    /*
     * Last, under a rule that reaches the card's edges. The destructive tier is its own BLOCK now (not a list
     * row): the card's padding moved down to the blocks so the divider between them spans edge to edge without
     * a negative margin fighting it.
     */
    const block = destructive.closest(".wcn-acts-destructive");
    expect(block, "the destructive tier lost its own block").not.toBeNull();
    expect(block.parentElement.lastElementChild, "the destructive tier is not last").toBe(block);
  });

  it("carries prose on the PRIMARY only — the rest moved to their dialogs", async () => {
    // Four sentences read before choosing anything is four sentences nobody reads. They now lead the dialog
    // each action opens, under the same resource keys.
    await boot(withActions([{ code: "accept" }, { code: "plan" }, { code: "inquire" }]));
    const card = app().querySelector(".wcn-acts");
    expect(card.querySelector(".wcn-act-primary .wcn-act-outcome"), "the primary lost its sentence").not.toBeNull();
    expect(card.querySelectorAll(".wcn-act-secondary .wcn-act-outcome"), "a secondary kept prose").toHaveLength(0);
    // …and the helper that carries them into the dialogs still exists and still uses the same key table.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src).toMatch(/const outcomeLead = \(action\) =>[\s\S]{0,200}ACTION_OUTCOME_KEY\[action\.code\]/);
    /*
     * ⚠ UPDATED (2026-08-24, A3): the plan dialog is no longer a raw `Swal.fire` with its own `<input>` — it
     * goes through the shared confirm, so its lead sentence rides `subtext` instead of a hand-built `html`
     * string. The claim under test is unchanged: the PRIMARY's prose reaches its dialog.
     */
    expect(src, "the plan dialog lost its lead").toMatch(/subtext: outcomeLead\(action\)/);
  });

  it("never draws a disabled action it cannot explain", async () => {
    /*
     * MUTATION TARGET (reason). A greyed button with no reason tells the reader a rule exists and refuses to
     * name it — worse than the button being absent, because they go looking for a permission screen that will
     * not help.
     *
     * ⚠ MEASURED WHILE WRITING THIS: the executable contract ALREADY refuses such an action
     * (`DISABLED_REASON_REQUIRED`, fixture-contract.js), so one cannot be built through the validated path at
     * all. The renderer's filter is therefore a SECOND line, not the only one — and this test says so rather
     * than pretending to construct an impossible fixture. Both halves are asserted: the gate, and the guard.
     */
    const contract = read("wwwroot", "assets", "js", "WorkCenterNext", "fixture-contract.js");
    expect(contract, "the contract stopped requiring a reason").toContain("DISABLED_REASON_REQUIRED");
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src, "the rail no longer filters unexplained disabled actions")
      .toMatch(/if \(!a\.disabled \|\| a\.disabledReason\) \{ return true; \}/);

    // And the explained one KEEPS its place, with its reason inside its own row — the reason is about THIS
    // button, so it belongs where the button is rather than in whichever card owns the underlying cause.
    await boot(withActions([
      { code: "complete", enabled: false, disabledReasonCode: "CHECKLIST_INCOMPLETE",
        disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_ChecklistIncomplete" } }
    ]));
    const blocked = app().querySelector(".wcn-acts .wcn-act-disabled");
    expect(blocked, "the blocked action was dropped along with its reason").not.toBeNull();
    expect(blocked.querySelector(".wcn-act-reason"), "the blocked action lost its reason").not.toBeNull();
  });

  it("locks the whole card while a write is in flight, from the resolver's flag", async () => {
    /*
     * MUTATION TARGET (lock). `interactionLocked` is the resolver's, consumed — not recomputed in the rail. The
     * list rows already gate on the same submit state; a second local model here is the "two parallel models,
     * one of them dead" shape already on record this session.
     */
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src, "the rail no longer receives the resolver's lock")
      .toMatch(/renderActionRail\(item, surface\.interactionLocked, surface\)/);
    expect(src, "the lock is recomputed locally inside the rail")
      .not.toMatch(/renderActionRail = \(item[^)]*\) => \{[\s\S]{0,400}state\.submittingItemId/);
    // …and it is narrowed to THIS item before the resolver sees it, so another item's submit cannot lock it.
    expect(src).toMatch(/submittingActionCode: state\.submittingItemId === item\.id/);
  });

  it("says why when there is nothing to do", async () => {
    // A heading over blank space reads as a page that failed to load, not as a task that is finished.
    await boot(projectionItem({
      normalizedStatus: "Done", taskLifecycle: "Done", executionState: "notApplicable", actions: []
    }));
    const none = app().querySelector(".wcn-act-none");
    expect(none, "the empty card says nothing").not.toBeNull();
    expect(none.textContent).toContain("ActionsNoneClosed");
  });

  it("ships the new strings in all seven languages", () => {
    ["ActionSubmitting", "ActionsNoneClosed", "ActionsNoneNotYours"].forEach((key) => {
      ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
        expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
          `${lang} has no ${key}`).toContain(`name="${key}"`);
      });
    });
  });
});

describe("the rail's three cards after the Status card was dissolved", () => {
  it("carries EXACTLY ONE filled button — the rest have no fill at all", async () => {
    /*
     * MUTATION TARGET (fill). Every button was a Sneat `btn-label-*` tint, and in this theme a tint reads as
     * DISABLED — measured on screen as a pale green primary with white text beside two pale grey pills. A card
     * whose most important control looks switched off is worse than one with no emphasis: the reader concludes
     * they are not allowed to act.
     *
     * Asserted on CLASS rather than computed colour, because jsdom performs no cascade; the pixel measurement
     * is in the round's report. Giving any secondary a fill class fails here.
     */
    await boot(projectionItem({
      primaryActionCode: "accept",
      actions: [
        { code: "accept", semanticType: "accept", label: { kind: "resource", key: "WorkAggregation_Action_accept" },
          enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
          requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
          riskLevel: "normal" },
        { code: "cancel", semanticType: "cancel", label: { kind: "resource", key: "WorkAggregation_Action_cancel" },
          enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
          requiresConfirmation: true, requiresReason: false, requiresEvidence: false, supportsBulk: false,
          riskLevel: "destructive" }
      ]
    }));
    const btns = [...app().querySelectorAll(".wcn-acts .wcn-act-btn")];
    expect(btns.length, "the actions card drew nothing").toBeGreaterThan(1);
    const filled = btns.filter((b) => b.className.includes("wcn-act-fill"));
    expect(filled, "there is not exactly one filled button").toHaveLength(1);
    expect(filled[0].closest(".wcn-act").className).toContain("wcn-act-primary");
    btns.filter((b) => b !== filled[0]).forEach((b) =>
      expect(b.className, "a secondary carries a fill").toContain("wcn-act-bare"));
  });

  it("puts no icon on an action button — every one of them repeated its own label", async () => {
    /*
     * A tick on "Accept", a question mark on "Ask", a calendar on "Plan" — and anything unmapped fell back to
     * `bx-right-arrow-alt`, which is what put an arrow in front of "Tamamla" and made a button read as a link.
     * The one icon that survives is the LOCK on a blocked reason: it states the prohibition rather than
     * repeating a word beside it.
     */
    await boot(projectionItem({
      primaryActionCode: "accept",
      actions: [{ code: "accept", semanticType: "accept",
        label: { kind: "resource", key: "WorkAggregation_Action_accept" }, enabled: true, source: "provider",
        disabledReasonCode: null, disabledReason: null, requiresConfirmation: false, requiresReason: false,
        requiresEvidence: false, supportsBulk: false, riskLevel: "normal" }]
    }));
    app().querySelectorAll(".wcn-acts .wcn-act-btn").forEach((b) =>
      expect(b.querySelector("i.bx-right-arrow-alt, i.bx-check, i.bx-question-mark, i.bx-calendar-plus"),
        "an action button carries a label-repeating icon").toBeNull());
  });

  it("always states WHO holds the task, even when nobody does", async () => {
    /*
     * MUTATION TARGET (assignee). Every other empty row is dropped — a dash claims the field was checked and
     * found empty, which the reader cannot tell from a value that failed to load.
     *
     * This row is the exception because an unassigned task is not a missing field, it is the FACT whose
     * consequence is that nothing happens until somebody notices. Dropping it would hide exactly the state that
     * needs seeing. Verified live on a genuinely unassigned record.
     */
    await boot(projectionItem({ assignee: null }));
    const field = [...app().querySelectorAll(".backbone-preview-field")]
      .find((f) => f.querySelector(".backbone-preview-label").textContent.includes("DetailAssignee"));
    expect(field, "the assignee field was dropped when empty").not.toBeNull();
    const value = field.querySelector(".backbone-preview-value");
    expect(value.textContent.trim(), "the unassigned state was printed as a dash").not.toMatch(/^[—–-]$/);
    expect(value.textContent).toContain("SummaryUnassigned");
    expect(field.className, "the empty state is not marked as one").toContain("backbone-preview-field-muted");
  });

  it("colours the due date RED when it is late — the contradiction the Status card left", async () => {
    /*
     * MUTATION TARGET (overdue). The same date rendered grey in the Summary and RED in the Status card: one
     * screen, two answers about one fact. The red was the correct one, so the red is what survived the merge —
     * and from the SAME source the Status card used, `slaState === 'overdue'`. No new lateness rule is derived
     * here; deriving one would be a third answer.
     */
    await boot(projectionItem({ slaState: "overdue", dueAt: "2026-07-28T17:00:00+03:00" }));
    const due = [...app().querySelectorAll(".backbone-preview-field")]
      .find((f) => f.querySelector(".backbone-preview-label").textContent.includes("SourceDueLabel"));
    expect(due, "the due date field is gone").not.toBeNull();
    /*
     * THE WHOLE FIELD goes red — icon, label and value — not only the number. Colouring one of three parts
     * reads as a typo; colouring the field reads as a state.
     */
    expect(due.className, "a late due date is not marked late").toContain("backbone-preview-field-overdue");

    await boot(projectionItem({ slaState: "on-track", dueAt: "2026-07-28T17:00:00+03:00" }));
    const ok = [...app().querySelectorAll(".backbone-preview-field")]
      .find((f) => f.querySelector(".backbone-preview-label").textContent.includes("SourceDueLabel"));
    expect(ok.className, "an on-track date was marked late").not.toContain("backbone-preview-field-overdue");
  });

  it("uses the product's golden field pattern rather than a shape of its own", async () => {
    /*
     * Two earlier shapes were this card's own inventions and both failed on measurement: a three-column tile
     * grid that orphaned a fourth fact on a second row, and a definition list that used a 690px card as a 350px
     * column with the right half empty.
     *
     * `backbone-preview-field/-label/-value` is what every Compact details page in this product is built from
     * (`Views/DevEnablement/GoldenReferenceCompact/Details.cshtml`), and this card already sits inside
     * `backbone-preview-section` — it had simply never used the field pattern that section was designed around.
     */
    await boot(projectionItem());
    const fields = app().querySelectorAll(".backbone-preview-field");
    expect(fields.length, "no golden fields").toBeGreaterThan(0);
    fields.forEach((f) => {
      expect(f.querySelector("i"), "a field lost its icon").not.toBeNull();
      expect(f.querySelector(".backbone-preview-label"), "a field lost its label").not.toBeNull();
    });
    // Two columns, from Bootstrap's own grid — no bespoke column rule.
    expect(app().querySelector(".backbone-preview-field").closest(".col-md-6, .col-12")).not.toBeNull();
    expect(app().querySelector(".wcn-sumlist"), "the definition list came back").toBeNull();
    expect(app().querySelector(".wcn-facts"), "the fact grid came back").toBeNull();
  });

  /*
   * MUTATION TARGET (where the plan lives). THIS TEST USED TO ASSERT THE OPPOSITE, and the assertion was the
   * defect: it pinned the plan date INSIDE the Personal card, under a heading that says "Kişisel".
   *
   * The date is not personal. Measured on `TaskItem` (TaskItem.cs:132) — the shared task row — projected as a
   * top-level field (TaskWorkItemProvider.cs:551), read back by the requester, and a plan write moves the SHARED
   * lifecycle to `Planned`. A shared field under a private heading is a false statement about who can see it, so
   * it moved to the Summary with the task's other shared dates (BL-141, owner decision).
   */
  it("keeps the shared plan date OUT of the Personal card", async () => {
    await boot(projectionItem({ plannedDate: "2026-08-20" }));

    const personal = [...app().querySelectorAll(".wcn-detail-rail .card")]
      .find((c) => c.querySelector("h6")?.textContent.includes("PersonalCardLabel"));
    expect(personal, "there is no Personal card at all").not.toBeNull();
    expect(personal.querySelector(".wcn-personal-plan"), "the plan is still in the private card").toBeNull();
    expect(personal.textContent, "the plan's value is still in the private card").not.toContain("2026-08-20");
    expect(app().querySelector(".wcn-dates"), "the Status card kept its dates").toBeNull();
  });

  it("shows the plan date in the Summary, beside the task's other shared dates", async () => {
    await boot(projectionItem({ plannedDate: "2026-08-20" }));

    const field = app().querySelector(".wcn-sumfield-plan");
    expect(field, "the plan never arrived in the Summary").not.toBeNull();
    expect(field.closest(".wcn-sum"), "the plan field is not inside the Summary card").not.toBeNull();
    expect(field.querySelector(".backbone-preview-label").textContent).toContain("DetailPlannedDate");
    expect(field.querySelector(".backbone-preview-value").textContent).toContain("2026-08-20");
    // The golden field pattern, like its five siblings — icon plus labelled value, not a bespoke row.
    expect(field.querySelector("i"), "the plan field lost its icon").not.toBeNull();
  });

  /*
   * ⚠ TWO ENTRANCES TO ONE JOB IS FINE; TWO MECHANISMS IS NOT.
   *
   * The Summary row opens the plan editor by carrying the SAME `data-wcn-action="plan"` the action button
   * carries, so the page's single action handler routes both. This asserts there is no second picker, no second
   * submit path — the attribute IS the proof, because a bespoke editor would not need it.
   */
  it("opens the plan editor through the SAME action the button uses, not a second editor", async () => {
    await boot(withPlanAction("2026-08-20"));

    const field = app().querySelector(".wcn-sumfield-plan");
    expect(field.getAttribute("data-wcn-action"), "the plan row invented its own editor").toBe("plan");
    expect(field.getAttribute("data-wcn-id")).toBe(TASK_ID);
    // Announced and reachable as a control, not as decorative text.
    expect(field.getAttribute("role")).toBe("button");
    expect(field.getAttribute("tabindex")).toBe("0");

    // …and the keyboard reaches the same handler. A role="button" that ignores Enter is worse than plain text.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const branch = src.slice(src.indexOf('[data-wcn-action][role="button"]'));
    expect(branch.slice(0, 400), "the plan row is a button the keyboard cannot press").toContain("performAction");
  });

  it("states the date without offering a control when the projection offers no plan action", async () => {
    // Somebody else's task, or a closed one: the fact is still worth reading, the button would only be refused.
    await boot(projectionItem({ plannedDate: "2026-08-20", actions: [] }));

    const field = app().querySelector(".wcn-sumfield-plan");
    expect(field, "the date vanished along with the action").not.toBeNull();
    expect(field.getAttribute("role"), "an unavailable action is still offered as a button").toBeNull();
  });

  /*
   * NO ROW WHEN THERE IS NO PLAN — the Summary's own rule (a row is printed for a fact that exists), and the
   * honest one here: `plan` is already offered as an action. Measured live on a task with no plan, "Planla"
   * appears in the actions card AND in the narrow-screen bar; a third invitation would be a third copy.
   */
  it("prints no plan row at all when nobody has planned yet", async () => {
    await boot(projectionItem({ plannedDate: null }));

    expect(app().querySelector(".wcn-sumfield-plan"), "an empty plan row was printed").toBeNull();
  });

  it("ships the new strings in all seven languages", () => {
    ["SummaryUnassigned", "PersonalCardLabel"].forEach((key) =>
      ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) =>
        expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
          `${lang} has no ${key}`).toContain(`name="${key}"`)));
  });
});

describe("the actions card's divider reaches the card's edges", () => {
  it("takes its padding from the BLOCKS, not the card — no negative margin", async () => {
    /*
     * MUTATION TARGET (divider). MEASURED: the rule ran 748→1053 inside a card spanning 732→1069 — 16px short at
     * each end, because the card's `p-4` pushed it inward. A rule that stops before the edge reads as a mistake
     * rather than as a division.
     *
     * The fix is structural, not a nudge: a negative margin would fight the padding and break the next time the
     * padding changed. The card clips and the blocks carry their own inset, so the rule between them spans edge
     * to edge for free.
     *
     * Asserted on the CARD CLASS and the stylesheet because jsdom performs no layout; the pixel proof
     * (980→1399 against a card of 980→1399) is in the round's report.
     */
    await boot(projectionItem({
      primaryActionCode: "accept",
      actions: [
        { code: "accept", semanticType: "accept", label: { kind: "resource", key: "WorkAggregation_Action_accept" },
          enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
          requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
          riskLevel: "normal" },
        { code: "cancel", semanticType: "cancel", label: { kind: "resource", key: "WorkAggregation_Action_cancel" },
          enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
          requiresConfirmation: true, requiresReason: false, requiresEvidence: false, supportsBulk: false,
          riskLevel: "destructive" }
      ]
    }));
    const card = app().querySelector(".wcn-acts").closest(".card");
    expect(card.className, "the actions card still carries its own padding").toContain("wcn-acts-card");
    expect(card.className, "the card kept p-4, which insets the rule").not.toMatch(/\bp-4\b/);

    const css = read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");
    const rule = /\.wcn-acts-card \{([^}]*)\}/.exec(css);
    expect(rule, "the clipping rule is gone").not.toBeNull();
    expect(rule[1]).toMatch(/overflow:\s*hidden/);
    expect(rule[1]).toMatch(/padding:\s*0/);
  });

  it("outlines the quiet actions without filling them — the one-fill rule is untouched", async () => {
    /*
     * They were bare text beside a solid primary and read as three links under a button; an action that changes
     * state should look like a control. A BORDER IS NOT A FILL, so the "exactly one filled button" rule from the
     * previous round still holds — that test runs unchanged alongside this one.
     *
     * Height is pinned at 2rem: the theme's own `btn-sm` measures 30px, under the 32px floor this round set
     * (WCAG 2.5.8's base is 24px; 32 clears it comfortably).
     */
    await boot(projectionItem({
      primaryActionCode: "accept",
      actions: [
        { code: "accept", semanticType: "accept", label: { kind: "resource", key: "WorkAggregation_Action_accept" },
          enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
          requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
          riskLevel: "normal" },
        { code: "plan", semanticType: "plan", label: { kind: "resource", key: "WorkAggregation_Action_plan" },
          enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
          requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
          riskLevel: "normal" }
      ]
    }));
    const bare = [...app().querySelectorAll(".wcn-act-bare")];
    expect(bare.length, "no outlined actions").toBeGreaterThan(0);
    bare.forEach((b) => expect(b.className, "an outlined action gained a fill").not.toContain("wcn-act-fill"));

    const css = read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");
    const rule = /\.wcn-act-bare \{([^}]*)\}/.exec(css);
    expect(rule, "the outlined rule is gone").not.toBeNull();
    expect(rule[1], "the quiet actions lost their border").toMatch(/border:\s*1px solid/);
    expect(rule[1], "the 32px floor was dropped").toMatch(/min-block-size:\s*2rem/);
  });
});

describe("card section dividers reach the card's edges, with equal space each side", () => {
  /*
   * THE SAME DEFECT WAS FOUND TWICE, IN TWO CARDS, IN TWO ROUNDS. This is the rule that stops the third.
   *
   * A divider that stops short of the edge reads as a mistake rather than as a division, and unequal space
   * either side reads as a misalignment. Both were measured: the actions card's rule ran 16px short at each end,
   * and the summary card's tag rule ran 8px short with 4px above against 16px below.
   */
  const dividerCss = () => read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");

  it("gives the padding to the BLOCKS and never uses a negative margin", () => {
    /*
     * MUTATION TARGET (divider). ⚠ THE FIRST FIX FOR THIS USED `margin: 1rem -1.5rem 0` while its own comment
     * claimed it did not — a negative margin cancelling the padding it sits inside, which breaks the moment that
     * padding changes. This asserts the honest version: the parent holds no inline padding, so a block inside is
     * already card-width and the border reaches both edges on its own.
     */
    const css = dividerCss();
    ["wcn-acts-card", "wcn-sum-card"].forEach((cardClass) => {
      const rule = new RegExp(`\\.${cardClass} \\{([^}]*)\\}`).exec(css);
      expect(rule, `${cardClass} does not clip its own padding`).not.toBeNull();
      expect(rule[1]).toMatch(/padding:\s*0/);
      expect(rule[1]).toMatch(/overflow:\s*hidden/);
    });
    // No negative inline margin anywhere in the two divider blocks.
    ["wcn-acts-destructive", "wcn-sumtags"].forEach((blockClass) => {
      const rule = new RegExp(`\\.${blockClass} \\{([^}]*)\\}`).exec(css);
      expect(rule, `${blockClass} lost its rule`).not.toBeNull();
      expect(rule[1], `${blockClass} uses a negative margin`).not.toMatch(/margin[^;]*-\d/);
      expect(rule[1]).toMatch(/border-block-start:\s*1px solid/);
    });
  });

  it("spaces the rule equally above and below, from the card's own group value", () => {
    // Measured 24px above against 16px below on the first pass: the block above ended on the card's 1.5rem
    // inset while the block below opened with the group's 1rem. Both are the group value now.
    const css = dividerCss();
    [["wcn-acts-main", "wcn-acts-destructive"], ["wcn-sum-main", "wcn-sumtags"]].forEach(([main, div]) => {
      const above = new RegExp(`\\.${main} \\{([^}]*)\\}`).exec(css);
      const below = new RegExp(`\\.${div} \\{([^}]*)\\}`).exec(css);
      expect(above[1], `${main} does not end on the group value`).toMatch(/padding:\s*1\.5rem 1\.5rem 1rem/);
      expect(below[1], `${div} does not open on the group value`).toMatch(/padding:\s*1rem 1\.5rem 1\.5rem/);
    });
  });
});

describe("the two lists speak one row language", () => {
  it("draws both rows as the same box, on the card's own surface", async () => {
    /*
     * MUTATION TARGET (row language). Measured, two lists in one card stack speaking two dialects: the checklist
     * row was a grey box (bg rgb(245,245,249), border, radius 6px) and the subtask row a bare separated line
     * (transparent, border-top only, radius 0).
     *
     * Both are interactive OBJECTS carrying their own controls, so both are boxes; and once the checklist's grey
     * went white the box became the ONLY thing distinguishing a row, which is why leaving subtasks as lines
     * would have widened the gap rather than closed it.
     */
    const css = read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");
    const subtask = /\.wcn-subtasks > li \{([^}]*)\}/.exec(css);
    expect(subtask, "the subtask row lost its box").not.toBeNull();
    expect(subtask[1]).toMatch(/border:\s*1px solid/);
    expect(subtask[1]).toMatch(/border-radius/);
    expect(subtask[1], "a row is a panel inside a panel").toMatch(/background:\s*var\(--bs-card-bg\)/);
    // The checklist row sits on the same surface — not the page background, which measured grey on a white card.
    const check = /\.diten-checkitem \{([^}]*)\}/.exec(css);
    expect(check[1]).toMatch(/background:\s*var\(--bs-card-bg\)/);
  });

  it("answers the pointer AND the keyboard on both lists", () => {
    /*
     * MUTATION TARGET (hover). Neither list had any hover rule, while the project's own idiom (`.wcn-row:hover`)
     * sits one file over. `:focus-within` carries the same background — without it a mouse user sees where they
     * are and a keyboard user does not, which is the half of every hover rule that gets forgotten.
     */
    const css = read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");
    const start = css.indexOf(".diten-checkitem:hover,");
    expect(start, "the shared hover rule is gone").toBeGreaterThan(-1);
    const block = css.slice(start, css.indexOf("}", start) + 1);
    [".diten-checkitem:hover", ".diten-checkitem:focus-within",
      ".wcn-subtasks > li:hover", ".wcn-subtasks > li:focus-within"].forEach((sel) =>
      expect(block, `${sel} is not covered`).toContain(sel));
    expect(block).toMatch(/rgba\(var\(--bs-primary-rgb\), \.03\)/);
  });
});

describe("the two subtask panels commit on Enter, like every other box on the page", () => {
  it("binds Enter on BOTH panel title fields, through the same save the button uses", () => {
    /*
     * MUTATION TARGET (Enter). MEASURED: `#wcnSubtaskTitle` and `#wcnNewSubtaskTitle` listened for `input` only
     * and neither panel is a `<form>`, so Enter did nothing — no save, no error, no focus move. Meanwhile the
     * subtask add row, the comment box and the checklist add row all commit on Enter. Three inputs teaching a
     * habit and two silently refusing it is worse than none of them having it.
     *
     * Routed through the SAME save the button calls, so validation, the busy flag and the failure path stay one
     * implementation rather than a keyboard copy.
     */
    const app = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    [["#wcnSubtaskTitle", "saveSubtaskPanel", "data-wcn-subtask-save"],
      ["#wcnNewSubtaskTitle", "saveNewSubtask", "data-wcn-newsubtask-save"]].forEach(([id, save, attr]) => {
      const branch = new RegExp(
        `event\\.key === 'Enter'[^}]*matches\\('${id}'\\)[\\s\\S]{0,420}?${save}\\(`);
      expect(app, `${id} does not commit on Enter`).toMatch(branch);
      expect(app, `${id} does not route through the button's own save`).toContain(attr);
    });
  });

  it("names and reaches the capped scroll region", () => {
    // 320px of scrollable list with no tabindex, no role and no name: the keyboard could not scroll it and a
    // screen reader stepped in without being told it had entered anything.
    const app = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(app).toMatch(/class="wcn-scrollcap"[^`]*tabindex="0"[^`]*role="region"/);
    expect(app, "the scroll region has no name").toMatch(/aria-label="\$\{esc\(t\(key === 'subtasks'/);
    // …and the toggle says which state it is in.
    expect(app, "the show-all toggle does not report its state").toMatch(/data-wcn-showall="\$\{esc\(key\)\}" aria-expanded=/);
  });
});

/*
 * ── THE SOURCE CARD ───────────────────────────────────────────────────────────────────────────────────────
 *
 * ⚠ ONE OF THESE TWO MODES CANNOT BE PRODUCED LIVE. Every record on this surface today carries
 * `providerCode: "tasks"` / `objectType: "task"`, so the FOREIGN branch never executes until a second provider
 * (MOD-0023 workflow) lands. Own-module mode is measured in the browser and reported with the round; the foreign
 * mode is covered here, by fixture, and is explicitly NOT claimed as live-verified.
 *
 * That distinction matters: the fixture here COVERS an unreachable branch. It does not stand in for production
 * code or hide a defect in it, which is the kind of fake this session has banned.
 */
describe("the source card shows a field only when it distinguishes something", () => {
  const withSource = (source) => projectionItem({
    source: Object.assign({ providerContractVersion: "1.0", objectId: "X-1" }, source)
  });

  it("hides module, id and type for our OWN module — they are the same on every task", async () => {
    /*
     * MUTATION TARGET (conditional fields). Measured on the live page: "Görevler", "task" and a GUID printed on
     * every single task. The GUID is the sharpest case — the page's own URL already carries it and is clickable,
     * and nobody pastes a GUID into a support thread when a link is at hand.
     */
    await boot(withSource({ providerCode: "tasks", objectType: "task" }));
    const card = app().querySelector(".wcn-source");
    expect(card, "the source card is gone").not.toBeNull();
    const labels = [...card.querySelectorAll(".wcn-source-key")].map((k) => k.textContent);
    expect(labels.join(" "), "the module name printed on our own work").not.toContain("DetailModuleName");
    expect(labels.join(" "), "the object type printed on our own work").not.toContain("DetailSourceType");
    expect(card.querySelector(".wcn-reference-id"), "the GUID printed on our own work").toBeNull();
    // The source's own status word stays — it is the one field that differs per task.
    expect(labels.join(" ")).toContain("DetailNativeStatusInSource");
  });

  it("SHOWS them, with a copy button, for a foreign provider", async () => {
    /*
     * ⚠ FIXTURE-ONLY — this branch cannot be reached live today (see the block comment above).
     *
     * On a foreign provider the id is that system's searchable key ("REG-2026-0184"), not our GUID, and it is
     * the single most useful thing on the card — so it comes back and keeps the copy control.
     */
    await boot(withSource({ providerCode: "workflow", objectType: "approval", objectId: "REG-2026-0184" }));
    const card = app().querySelector(".wcn-source");
    const labels = [...card.querySelectorAll(".wcn-source-key")].map((k) => k.textContent).join(" ");
    expect(labels).toContain("DetailModuleName");
    expect(labels).toContain("DetailSourceType");
    const id = card.querySelector(".wcn-reference-id");
    expect(id, "a foreign record hid its own key").not.toBeNull();
    expect(id.textContent).toContain("REG-2026-0184");
    expect(card.querySelector("[data-wcn-copy]"), "the key cannot be copied").not.toBeNull();
  });

  it("is not folded away, and is not called 'technical'", async () => {
    // A three-row card behind a disclosure costs a click and saves nothing; and "Teknik bilgi" is a sign saying
    // THIS IS NOT FOR YOU to the one reader who needs the source record.
    await boot(projectionItem());
    expect(app().querySelector("details.wcn-tech"), "the card is still folded").toBeNull();
    const head = app().querySelector(".wcn-source h6");
    expect(head, "the card lost its standard heading").not.toBeNull();
    expect(head.textContent).toContain("SourceCardLabel");
    expect(head.querySelector("i.bx"), "the heading lost its glyph").not.toBeNull();
  });

  it("drops the version token and the action-depth row", () => {
    /*
     * The concurrency token is a write-safety mechanism: it changes on every save, means nothing on its own, and
     * was the clearest case of a field present because it EXISTED. "İşlem derinliği" read "Burada tamamlanır" on
     * essentially every task — the information is in the other case, and that case is an ACTION, not a fact.
     */
    const app_ = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = app_.slice(app_.indexOf("const renderSourceCard"), app_.indexOf("const sourceRow"));
    expect(fn, "the version row came back").not.toContain("DetailSourceVersion");
    expect(fn, "the action-depth row came back").not.toContain("DetailActionDepth");
  });
});

describe("work that cannot be finished here leads with a way to finish it", () => {
  const deeplinkItem = () => projectionItem({
    actionDepth: "deeplink",
    primaryActionCode: "inquire",
    actions: [
      { code: "inquire", semanticType: "inquire", label: { kind: "resource", key: "WorkAggregation_Action_inquire" },
        enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
        requiresConfirmation: false, requiresReason: true, requiresEvidence: false, supportsBulk: false,
        riskLevel: "normal" },
      { code: "reassign", semanticType: "reassign", label: { kind: "resource", key: "WorkAggregation_Action_reassign" },
        enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
        requiresConfirmation: false, requiresReason: true, requiresEvidence: false, supportsBulk: false,
        riskLevel: "normal" }
    ],
    source: {
      providerCode: "workflow", providerContractVersion: "1.0", objectType: "approval",
      objectId: "REG-1", sourceSystem: "MOD-0023", deepLink: "/Workflow/Approvals/REG-1"
    }
  });

  it("makes the primary a link into the owning module, keeping exactly one filled control", async () => {
    /*
     * MUTATION TARGET (deeplink primary). ⚠ FIXTURE-ONLY: `actionDepth` has exactly two values — measured,
     * `ACTION_DEPTHS = ['inline', 'deeplink']` — and no live record is `deeplink` today.
     *
     * The old "İşlem derinliği" row stated this as a fact in a technical card nobody opened. It is not a fact,
     * it is the answer to "why is there no Complete button?", and it belongs where the buttons are.
     */
    await boot(deeplinkItem());
    const card = app().querySelector(".wcn-acts");
    expect(card, "the actions card is gone").not.toBeNull();
    const primary = card.querySelector(".wcn-act-primary .wcn-act-fill");
    expect(primary, "no leading control").not.toBeNull();
    expect(primary.tagName, "the lead is not a link").toBe("A");
    expect(primary.getAttribute("href")).toBe("/Workflow/Approvals/REG-1");
    expect(primary.textContent).toContain("ActionCompleteInSource");
    expect(primary.querySelector("i.bx-link-external"), "no external-link glyph").not.toBeNull();
    // The sentence that says why, under the control it explains.
    expect(card.querySelector(".wcn-act-primary .wcn-act-outcome").textContent)
      .toContain("ActionCompleteInSourceHint");
    // …and the one-fill rule still holds.
    const filled = [...card.querySelectorAll(".wcn-act-fill")];
    expect(filled, "more than one filled control").toHaveLength(1);
  });

  it("keeps the engine actions that still apply, as secondaries", async () => {
    // Asking and reassigning are OUR engine's, not the owning module's — they still work here.
    await boot(deeplinkItem());
    const secondaries = app().querySelectorAll(".wcn-acts .wcn-act-secondary");
    expect(secondaries.length, "the engine actions were dropped with the completion").toBeGreaterThan(0);
  });

  it("stands the source card's open button down, so one destination has one control", async () => {
    await boot(deeplinkItem());
    expect(app().querySelector(".wcn-source .wcn-opensource"),
      "two controls for one destination").toBeNull();
    // …and it is there in the ordinary case.
    await boot(projectionItem());
    expect(app().querySelector(".wcn-source .wcn-opensource")).not.toBeNull();
  });

  it("ships the new strings in all seven languages", () => {
    ["SourceCardLabel", "DetailNativeStatusInSource",
      "ActionCompleteInSource", "ActionCompleteInSourceHint"].forEach((key) =>
      ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) =>
        expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
          `${lang} has no ${key}`).toContain(`name="${key}"`)));
    // The retired label is KEPT, not deleted — a removed key is a broken fallback for anything still asking.
    expect(read("Resources", "Views", "WorkCenterNext", "WorkCenterNextIndex.tr.resx"))
      .toContain('name="TechnicalDetailsLabel"');
  });
});

describe("a panel that is open is never re-rendered underneath itself", () => {
  /*
   * ── THE DEFECT, AND WHY IT HID ────────────────────────────────────────────────────────────────────────────
   *
   * MEASURED with a MutationObserver over two real clicks:
   *     t=83014  node #2 created — showPanel bound a Bootstrap Offcanvas to it and called .show()
   *     t=83077  node #3 created — 63ms later, exactly the round-trip of the people lookup
   *     final    node #3, no instance, no `show` class
   *
   * `openSubtaskCreatePanel` ran `render() → showPanel() → await lookup → render()`. That second render replaced
   * the very node the instance was bound to, mid-animation. The instance survived on a detached node; the node
   * on screen had none and could never be shown again. It LOOKED like it worked once because the opening
   * animation was visible for those 63 ms.
   *
   * Its sibling `openSubtaskPanel` never re-rendered after its await — which is why one panel worked and the
   * other did not, and is the comparison that found this.
   *
   * ── AND THE WORSE HALF, found by measuring the SAVE paths ─────────────────────────────────────────────────
   *
   * Both panels also re-rendered to show a busy button and on failure. With the node replaced,
   * `hidden.bs.offcanvas` never fires, so Bootstrap never releases `body { overflow: hidden }`. Measured live
   * after a failed create: panel invisible, no instance, backdrop gone — and THE PAGE WOULD NOT SCROLL.
   *
   * These tests assert the SHAPE, because jsdom runs no Bootstrap and no animation; the open/close/open proof is
   * live and reported with the round.
   */
  const APP = () => read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
  const fnBody = (name, end) => {
    const src = APP();
    const start = src.indexOf(`const ${name} =`);
    return src.slice(start, src.indexOf(`const ${end} =`, start));
  };

  it("never renders again once the create panel is open — the lookup patches the select in place", () => {
    /*
     * MUTATION TARGET. Restoring the trailing `render()` puts the second render back and the panel stops
     * reopening.
     *
     * ⚠ AND NOT BY AWAITING FIRST, which was the obvious fix and the wrong one: it made the panel wait on the
     * people service, so a slow or failing lookup left the reader with a button that did nothing. Found by three
     * existing tests going red — they open the panel with no lookup stubbed, which is exactly the hanging-service
     * case. The panel opens immediately; the options are written into the live `<select>` afterwards.
     */
    /* Comments STRIPPED: this function EXPLAINS at length that it no longer calls render(), and a guard that
       reads prose cannot tell a call from a sentence about one. Third time this trap has fired in this session. */
    const body = fnBody("openSubtaskCreatePanel", "fillAssigneeSelect").replace(/\/\*[\s\S]*?\*\//g, "");
    const showAt = body.indexOf("showPanel(");
    const awaitAt = body.indexOf("await global.TasksApi.assignablePeople()");
    expect(showAt, "the panel is never shown").toBeGreaterThan(-1);
    expect(showAt, "the panel now waits on the lookup before opening").toBeLessThan(awaitAt);
    expect(body.slice(showAt), "a render still runs while the panel is open").not.toContain("render()");
    expect(body, "the lookup does not land in the select").toContain("fillAssigneeSelect()");
  });

  it("shows a panel's busy state on the BUTTON, never through render()", () => {
    // A render while an offcanvas is open detaches its node, and Bootstrap then never releases the body lock.
    ["saveNewSubtask", "saveSubtaskPanel"].forEach((name) => {
      const body = fnBody(name, name === "saveNewSubtask" ? "cancelSubtask" : "openSubtaskCreatePanel");
      expect(body, `${name} still renders while its panel is open`).not.toMatch(/^\s*render\(\);/m);
      expect(body, `${name} does not set its busy state in place`).toContain("setPanelBusy(");
    });
  });

  it("closes through Bootstrap, so the body lock is released by whoever applied it", () => {
    /*
     * On success both panels used to clear state and re-render, removing the node while it was still shown —
     * `hidden.bs.offcanvas` never fired and `body { overflow: hidden }` stayed. `hidePanel` calls `hide()` and
     * lets showPanel's own `hidden` listener do the clearing and the single render.
     */
    ["saveNewSubtask", "saveSubtaskPanel"].forEach((name) => {
      const body = fnBody(name, name === "saveNewSubtask" ? "cancelSubtask" : "openSubtaskCreatePanel");
      expect(body, `${name} does not close through Bootstrap`).toContain("hidePanel(");
    });
    const helper = fnBody("hidePanel", "showPanel");
    expect(helper, "hidePanel does not call hide()").toContain(".hide()");
  });

  it("keeps the failed save's typed values, instead of wiping them with a render", () => {
    // The reader has something to fix; re-rendering would clear the form they are being asked to correct.
    const body = fnBody("saveNewSubtask", "cancelSubtask");
    const failAt = body.indexOf("if (!result.ok)");
    const tail = body.slice(failAt, body.indexOf("return;", failAt));
    expect(tail, "the failure path re-renders the open panel").not.toContain("render()");
    expect(tail, "the failure path does not restore the button").toContain("setPanelBusy(");
  });
});

describe("the rail follows the reader instead of running out", () => {
  const CSS = () => read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");

  it("sticks the rail — but only where it is a COLUMN", () => {
    /*
     * MUTATION TARGET. MEASURED at 1440×900: the content column runs to 1860px and the rail stops at 925px, so
     * 936px of the page is read beside an empty column — with "Mevcut aksiyonlar", the card the page exists for,
     * off screen for all of it.
     *
     * Two alternatives were measured and rejected, and the numbers are why:
     *   checklist → rail    rail 1273 vs content 983 — the imbalance simply inverts
     *   two content columns each column falls to 427px, against a subtask title already using 626
     *
     * Below 992 the rail STACKS under the content; a sticky element inside a stacked block pins to the wrong
     * thing, so the media query is not decoration.
     */
    const css = CSS();
    const mq = /@media \(min-width: 992px\) \{\s*\.wcn-detail-rail \{([^}]*)\}/.exec(css);
    expect(mq, "the rail is no longer sticky inside a min-width guard").not.toBeNull();
    expect(mq[1]).toMatch(/position:\s*sticky/);
    expect(mq[1]).toMatch(/inset-block-start/);
    /*
     * `align-self: start` is load-bearing, not tidiness: a grid item stretches to its row by default, and a
     * full-height box has nowhere to stick to.
     */
    expect(mq[1], "a stretched grid item cannot stick").toMatch(/align-self:\s*start/);
    // And it degrades instead of clipping: measured, the rail needs 676px of viewport, so a shorter window must
    // scroll it internally rather than hide its lower cards with no way to reach them.
    expect(mq[1], "a short window would clip the rail's own cards").toMatch(/overflow-y:\s*auto/);
    expect(mq[1]).toMatch(/max-block-size/);
  });

  it("keeps the sticky rule out of the stacked layout", () => {
    // A bare `.wcn-detail-rail { position: sticky }` outside the query would follow the reader at 900px too,
    // where the rail sits UNDER the content and has nothing meaningful to pin against.
    const css = CSS();
    const bare = /\n\.wcn-detail-rail \{([^}]*)\}/.exec(css);
    expect(bare && /position:\s*sticky/.test(bare[1]),
      "the rail is sticky unconditionally").not.toBe(true);
  });
});

describe("render() refuses to run silently under an open panel", () => {
  it("is wired into render(), and the warning names the panel and what it breaks", () => {
    /*
     * MUTATION TARGET. The rule "never render while an offcanvas is open" was applied by hand in two places, and
     * a third panel would have had to rediscover it — which is exactly what happened last round, at the cost of
     * a panel that opened once per page load and a body lock that left the page unscrollable.
     *
     * ⚠ A console warning ALONE is not a guard: this session has already had a swallowed warning hide a defect.
     * The pair is the guard — the wiring asserted here, and the warning OBSERVED FIRING in the live browser
     * (reported with the round: it named `wcnSubtaskCreatePanel` and the body-scroll consequence). It is not
     * asserted at runtime here because jsdom's click never reaches the app's delegated handler in this harness,
     * and a test that silently proves nothing is worse than one that says what it proves.
     */
    const app_ = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(app_, "render() no longer checks for an open panel")
      .toMatch(/const render = \(\) => \{\s*warnIfPanelOpen\(\);/);

    // The selector lives in `openPanelIds`, which the warning consults — take both.
    const guard = app_.slice(app_.indexOf("const openPanelIds"), app_.indexOf("const showPanel"));
    expect(guard, "the guard does not look for open offcanvases").toContain(".offcanvas.show");
    expect(guard, "the guard warns without naming the panel").toMatch(/open\.join/);
    expect(guard, "the guard does not say what breaks").toMatch(/body scroll stays locked/);
    expect(guard, "the guard does not name the way out").toMatch(/setPanelBusy|hidePanel/);
  });
});

describe("the narrow-screen action bar", () => {
  const withActions = () => projectionItem({
    primaryActionCode: "accept",
    actions: [
      { code: "accept", semanticType: "accept", label: { kind: "resource", key: "WorkAggregation_Action_accept" },
        enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
        requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
        riskLevel: "normal" },
      { code: "plan", semanticType: "plan", label: { kind: "resource", key: "WorkAggregation_Action_plan" },
        enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
        requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
        riskLevel: "normal" }
    ]
  });

  it("is drawn ONLY below 992 — the wide layout is untouched", async () => {
    /*
     * MUTATION TARGET (breakpoint). MEASURED at 900px: "Mevcut aksiyonlar" began at the page's 1876th pixel of
     * 2597 — 2.08 screens of scrolling to learn what you may do. At >=992 the rail is sticky and the actions
     * never leave the screen, so a bar there would be a second copy of a control already in view.
     *
     * `d-lg-none` is the whole mechanism: `display: none` takes it out of layout, the accessibility tree and the
     * tab order together. One render output, no width branch in JS, no resize listener.
     */
    await boot(withActions());
    const bar = app().querySelector(".wcn-actionbar");
    expect(bar, "there is no action bar").not.toBeNull();
    expect(bar.className, "the bar is not hidden on wide screens").toContain("d-lg-none");
    // Bootstrap's own sticky utility, not a positioning scheme invented here.
    expect(bar.className).toContain("sticky-bottom");
  });

  it("reads its actions from the SAME derivation as the card", async () => {
    /*
     * MUTATION TARGET (single source). Two views of one answer; deriving the set twice is how they drift, and
     * this session has produced that shape repeatedly (two chip vocabularies, two lock models, three unwrappings
     * of one envelope). Live measurement on a real task: card and bar both offered accept/cancel/inquire/plan/
     * reassign.
     */
    await boot(withActions());
    const codes = (root) => [...root.querySelectorAll("[data-wcn-action]")]
      .map((b) => b.getAttribute("data-wcn-action")).sort();
    const card = codes(app().querySelector(".wcn-acts"));
    const bar = codes(app().querySelector(".wcn-actionbar"));
    expect(card.length, "the card offers nothing").toBeGreaterThan(0);
    expect([...new Set(bar)].sort(), "the bar and the card disagree about what can be done").toEqual(card);

    // …and the derivation itself is one function, not two copies of a filter.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src).toMatch(/const actionTiers = \(item\) => \{/);
    const bar_ = src.slice(src.indexOf("const renderActionBar"), src.indexOf("const renderActionRail"));
    expect(bar_, "the bar derives its own set").toContain("actionTiers(item)");
    const rail = src.slice(src.indexOf("const renderActionRail"), src.indexOf("const renderLifecycleStepper"));
    expect(rail, "the card derives its own set").toContain("actionTiers(item)");
  });

  it("draws no bar when there is nothing to do", async () => {
    await boot(projectionItem({
      normalizedStatus: "Done", taskLifecycle: "Done", executionState: "notApplicable", actions: []
    }));
    expect(app().querySelector(".wcn-actionbar"), "a closed task got an action bar").toBeNull();
  });

  it("announces itself, and keeps a blocked primary's reason", async () => {
    await boot(projectionItem({
      primaryActionCode: "complete",
      actions: [{ code: "complete", semanticType: "complete",
        label: { kind: "resource", key: "WorkAggregation_Action_complete" },
        enabled: false, source: "provider", disabledReasonCode: "CHECKLIST_INCOMPLETE",
        disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_ChecklistIncomplete" },
        requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
        riskLevel: "normal" }]
    }));
    const bar = app().querySelector(".wcn-actionbar");
    expect(bar.getAttribute("role")).toBe("region");
    expect(bar.getAttribute("aria-label")).toBeTruthy();
    expect(bar.querySelector(".wcn-actionbar-lead").disabled, "a blocked primary is pressable in the bar").toBe(true);
    // On a narrow screen the bar may be the only part of the page the reader has seen; "you cannot press this"
    // without "because…" is worse here than in the card.
    expect(bar.querySelector(".wcn-actionbar-reason"), "the bar hides the reason").not.toBeNull();
  });
});

describe("the checklist is capped like its siblings", () => {
  it("uses the shared helper past its threshold, not a cap of its own", () => {
    /*
     * MEASURED: 6 items render 294px UN-capped, so a 20-item checklist would be ~1000px of one card. The subtask
     * list and the activity feed have used `cappedList` all along; the checklist simply never did.
     *
     * Same helper, same 320px box: a checklist row and a subtask row are both 38px, so the cap shows the same
     * number of rows on both — which is why reusing it beats choosing a third number. `aria-expanded` and the
     * region label arrive with the helper.
     */
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const renderChecklist"), src.indexOf("const renderDependencies"));
    expect(fn, "the checklist still renders unbounded").toContain("cappedList('checklist'");
    expect(fn, "the threshold is not named").toContain("CHECKLIST_CAP");
    const helper = src.slice(src.indexOf("const cappedList"), src.indexOf("const cappedList") + 1400);
    expect(helper, "the shared cap lost its expanded state").toContain("aria-expanded");
  });
});

describe("the narrow-screen menu opens where it can be seen", () => {
  /*
   * MUTATION TARGET (the dropup). MEASURED live at 900×900 before this fix: the button sat 845→888 and the menu
   * rendered 889→1063 — 174px tall, 163px of it BELOW a 900px viewport, with nothing to scroll to because the
   * bar is glued to that edge. `aria-expanded` flipped, `.show` arrived, and the screen did not change.
   *
   * WHY THE AUTOMATIC FLIP DID NOT SAVE IT: the toggle carries `data-bs-display="static"`, which switches
   * Bootstrap off Popper — and Popper is the part that flips. The open menu measured `transform: none`, the
   * fingerprint of exactly that.
   *
   * So the cure is stated, not negotiated: a bar pinned to the bottom edge has no case where downward is right.
   */
  it("carries dropup on the wrapper, so the menu grows upward always and not sometimes", async () => {
    // A primary plus two siblings, so the bar has something to fold behind "Diğer aksiyonlar".
    const action = (code, extra) => Object.assign({
      code, label: { kind: "resource", key: `WorkAggregation_Action_${code}` },
      semanticType: code, enabled: true, source: "provider",
      disabledReasonCode: null, disabledReason: null, requiresConfirmation: false,
      requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
    }, extra || {});
    await boot(projectionItem({
      primaryActionCode: "accept",
      actions: [action("accept"), action("plan"), action("cancel", { riskLevel: "destructive" })]
    }));

    const wrapper = app().querySelector(".wcn-actionbar .wcn-actionbar-more");
    expect(wrapper, "the narrow-screen bar has no overflow menu at all").not.toBeNull();
    expect(wrapper.classList.contains("dropup"), "the menu still opens downward off a bottom-pinned bar").toBe(true);
  });

  /*
   * The other half, and it is a separate failure: a menu that opens upward INTO the bar it grew from is just as
   * unusable as one below the fold. Measured menu z-index 1000 against the bar's 1020 — the bar wins on the
   * numbers, and the only reason it did not cover the menu is that the menu happens to be a descendant of the
   * bar's stacking context. That is markup luck, so the rule is written down.
   */
  it("puts the menu above the bar it grows out of, and lets the bar not clip it", () => {
    const menu = /^\.wcn-actionbar-more\s+\.dropdown-menu\s*\{([^}]*)\}/m.exec(CSS());
    expect(menu, "the menu has no z-index of its own").not.toBeNull();
    expect(Number(/z-index:\s*(\d+)/.exec(menu[1])[1]), "the menu does not clear the bar").toBeGreaterThan(1020);

    // Comments stripped first: this stylesheet's own prose says the word "overflow" three times, and a guard
    // that matches its own explanation is a guard that cannot fail (three of them did this session).
    const bar = /^\.wcn-actionbar\s*\{([^}]*)\}/m.exec(CSS());
    const declarations = bar[1].replace(/\/\*[\s\S]*?\*\//g, "");
    expect(declarations, "the bar clips the menu that grows out of its top edge").toMatch(/overflow:\s*visible/);
  });
});

describe("a date picker opened from a modal is above that modal", () => {
  /*
   * MEASURED: the snooze dialog's calendar rendered at z-index 1074 (flatpickr's OWN vendor value) while the
   * SweetAlert container it belongs to sits at 1090. `document.elementFromPoint` at the calendar's centre
   * returned `.swal2-container` — so every click on a day landed on the dialog instead. Open, and unusable.
   *
   * The vendor file is not edited. This is the flatpickr counterpart of the select2 `dropdownParent` rule in
   * frontend-js-standard.md: a popup stacks above the layer it was opened from.
   */
  it("raises the calendar over the dialog layer, in this stylesheet and not the vendor one", () => {
    const rule = /\.swal2-shown\s+\.flatpickr-calendar\.open[^{]*\{([^}]*)\}/m.exec(CSS());
    expect(rule, "nothing lifts the calendar over a modal").not.toBeNull();
    expect(Number(/z-index:\s*(\d+)/.exec(rule[1])[1]), "the calendar still loses to the dialog").toBeGreaterThan(1090);
  });
});

describe("the note row reads as one row", () => {
  /*
   * MUTATION TARGET (the alignment). MEASURED at 900px with `align-items: baseline`: the note text sat at centre
   * 1421.8, the date at 1422.5 and the 24px delete at 1419.4 — three leaves of one row, three middles.
   *
   * Baseline aligns TEXT BASELINES, and this row holds boxes of three heights (17.9 / 15.1 / 24) at two font
   * sizes; the delete button has no text at all, so its "baseline" is its bottom edge and it rode high. The two
   * rows this one was built to match — the checklist item and the subtask — have used `center` all along. The
   * note row was the only one that did not.
   */
  it("centres its leaves, the way the checklist and subtask rows already did", () => {
    const rule = /^\.wcn-note-row\s*\{([^}]*)\}/m.exec(CSS());
    expect(rule, "the note row lost its own rule").not.toBeNull();
    expect(rule[1], "the note row no longer centres its leaves").toMatch(/align-items:\s*center/);
    expect(rule[1], "baseline alignment came back").not.toMatch(/align-items:\s*baseline/);
  });

  it("uses the SAME alignment as the two rows it was built to match", () => {
    const alignOf = (selector) => {
      const rule = new RegExp("^" + selector + "\\s*\\{([^}]*)\\}", "m").exec(CSS());
      return rule && /align-items:\s*([a-z-]+)/.exec(rule[1])?.[1];
    };
    // One row language across three lists — the thing this round standardised.
    expect(alignOf("\\.wcn-note-row")).toBe("center");
    expect(alignOf("\\.wcn-check, \\.wcn-subtask")).toBe("center");
  });
});

describe("the page reaches the product's one confirm implementation", () => {
  /*
   * MEASURED 2026-08-14: app.js held FIFTEEN direct `Swal.fire(` calls while the product has exactly one confirm
   * — `window.showConfirm` — which owns the icon circle, the button classes, the reversed order and the
   * `scrollbarPadding/heightAuto` pair that keeps the navbar from jumping.
   *
   * ⚠ ALSO MEASURED: `window.DitenModal` is undefined on this page (premium-modal.js is not loaded here), and
   * DitenModal.confirm delegates to showConfirm anyway. Calling showConfirm directly reaches the same
   * implementation; routing through the absent global would have been a silent no-op.
   */
  it("routes confirms and reason prompts through the shared wrapper, not through Swal", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const seam = src.slice(src.indexOf("const sharedConfirm"), src.indexOf("const sharedConfirm") + 1600);
    expect(seam, "the seam does not reach the product's confirm").toContain("global.showConfirm");
    // A confirm that cannot be shown must not read as "cancelled".
    expect(seam, "an unavailable wrapper fails silently").toContain("console.error");
  });

  it("keeps only the dialog shapes the shared wrapper cannot express", () => {
    /*
     * The wrapper offers a TEXTAREA and nothing else. Date pickers, a number, a select, multi-field forms, the
     * "+ Yeni" menu, the bulk progress bar and the bulk result notice have no shape there — they are REPORTED
     * (BL-146), not bent through one that does not fit, and not solved by growing a shared component to suit one
     * module. This pins the count so a new raw dialog cannot be added quietly.
     */
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js")
      .replace(/\/\*[\s\S]*?\*\//g, "")
      .replace(/(^|[^:])\/\/.*$/gm, "$1");
    /*
     * ⚠ FOUR, NOT EIGHT (2026-08-24, A3). The four that ASK FOR ONE VALUE went through the shared component —
     * the plan date, the meeting time, the logged minutes, and the module choice, the last of which needed
     * `inputOptions` forwarded (the wrapper's seventh and final parameter).
     *
     * The four that remain are not confirmations and never will be: a MENU with no confirm button, a
     * four-field meeting form, a two-field reason+assignee form, and a progress readout that asks nothing.
     * They are raw by necessity — but they are no longer UNSTYLED: each one now spreads
     * `window.DitenDialogAppearance()` over its config, so the look still has exactly one definition.
     */
    /*
     * ⚠ THREE (2026-08-24, A2): the four-field MEETING FORM was DELETED rather than restyled — measured, it
     * wrote to `state.meetings` and made no API call, so everything it collected vanished on reload.
     */
    expect((src.match(/Swal\.fire\(/g) || []).length).toBe(3);
    // …and every one of the three takes the published appearance rather than inventing its own.
    expect((src.match(/dialogLook\(\)/g) || []).length,
      "a raw dialog is drawing itself again").toBe(3);
  });
});

describe("the feed says who changed what", () => {
  /*
   * "Who changed the due date?" had no answer anywhere before 2026-08-23. It has one now, and these pin the
   * SHAPE of the answer — one row per save, the fields named, and a hidden field's history hidden too.
   */
  const edited = (fieldChanges) => projectionItem({
    workItemCapabilities: ["planning", "execution", "subtasks", "activity"],
    subtasks: { mode: "full", items: [] },
    activity: [{
      id: "e1", kind: "event", actor: "Diten Admin", at: "2026-08-20T09:00:00+00:00",
      event: { code: "edited", from: "InProgress", to: "InProgress", fieldChanges }
    }]
  });

  it("belongs to the EVENT family — no avatar, one line", async () => {
    await boot(edited([{ field: "dueAt", from: "2026-08-15", to: "2026-08-20" }]));

    const row = app().querySelector(".wcn-audit-item");
    // The third kind joins the two that exist; it does not invent a third marker.
    expect(row.className).toContain("wcn-audit-event");
    expect(row.className, "an edit was drawn as a comment").not.toContain("wcn-audit-comment");
    expect(row.querySelector(".wcn-audit-avatar"), "an event grew an avatar").toBeNull();
  });

  it("names the field AND both ends when exactly one changed", async () => {
    await boot(edited([{ field: "dueAt", from: "2026-08-15", to: "2026-08-20" }]));

    const line = app().querySelector(".wcn-audit-line").textContent;
    expect(line).toContain("AuditFieldChangeValued");
    expect(line, "the field is not named").toContain("AuditFieldDueAt");
    expect(line, "the old value is missing").toContain("2026-08-15");
    expect(line, "the new value is missing").toContain("2026-08-20");
    // Who and when still travel on the same line, as they do for every other event.
    expect(line).toContain("Diten Admin");
  });

  /*
   * SEVERAL fields → the NAMES, not four before/after pairs. The reader's first question is which fields; four
   * pairs on one row is a paragraph. Measured decision, recorded in the composer's own comment.
   */
  it("names the fields and drops the values when several changed at once", async () => {
    await boot(edited([
      { field: "dueAt", from: "2026-08-15", to: "2026-08-20" },
      { field: "priority", from: "Medium", to: "High" },
      { field: "title", from: "Eski", to: "Yeni" }
    ]));

    const line = app().querySelector(".wcn-audit-line").textContent;
    expect(line).toContain("AuditFieldChangeNamed");
    ["AuditFieldDueAt", "AuditFieldPriority", "AuditFieldTitle"].forEach((key) =>
      expect(line, `${key} is missing from the list`).toContain(key));
    expect(line, "a before/after pair survived into the multi-field sentence").not.toContain("2026-08-15");
  });

  it("builds the list with the platform's localized formatter, never a hard-coded separator", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const composer = src.slice(src.indexOf("const formatList"), src.indexOf("const formatList") + 700);
    // Turkish ends a list with "ve", English with "and", Arabic with "و" — a comma is not a translation.
    expect(composer).toContain("Intl.ListFormat");
  });

  /*
   * MUTATION TARGET (the back door). BL-024 hides a field's VALUE; a history that reported it would hand the
   * same value back through a different door. The server sends `redacted` with nothing else — and the screen
   * must not fill the gap with the field's name either.
   */
  it("says only that A field changed when the reader may not see it", async () => {
    await boot(edited([{ redacted: true }]));

    const line = app().querySelector(".wcn-audit-line").textContent;
    expect(line).toContain("AuditFieldHidden");
    expect(line, "a redacted change leaked a field name").not.toContain("AuditFieldDueAt");
  });

  it("keeps a redacted field IN the list, so two readers count the same changes", async () => {
    await boot(edited([
      { field: "dueAt", from: "2026-08-15", to: "2026-08-20" },
      { redacted: true }
    ]));

    const line = app().querySelector(".wcn-audit-line").textContent;
    expect(line).toContain("AuditFieldDueAt");
    // Dropping it would make one reader see "one field changed" and another "two", from one record.
    expect(line).toContain("AuditFieldHidden");
  });

  it("says only that the field changed when the values were too long to keep", async () => {
    await boot(edited([{ field: "description", valuesOmitted: true }]));

    const line = app().querySelector(".wcn-audit-line").textContent;
    expect(line).toContain("AuditFieldChangeNamed");
    expect(line).toContain("AuditFieldDescription");
  });

  it("counts an edit in the tab badge, and keeps it out of the comments filter", async () => {
    await boot(edited([{ field: "dueAt", from: "2026-08-15", to: "2026-08-20" }]));

    // The badge counts the whole feed, so a third kind needs no arithmetic of its own.
    expect(app().querySelector(".wcn-audit-count").textContent).toBe("1");
    // …and "comments only" filters on kind, so an event stays out by construction.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src).toContain(`item.activity.filter((entry) => entry.kind === 'comment')`);
  });

  it("ships every field name and both sentence patterns in all seven languages", () => {
    const keys = [
      "AuditEventEdited", "AuditFieldChangeValued", "AuditFieldChangeNamed", "AuditFieldHidden",
      "AuditFieldDueAt", "AuditFieldStartAt", "AuditFieldPlannedDate", "AuditFieldPriority",
      "AuditFieldAssignee", "AuditFieldTitle", "AuditFieldDescription", "AuditFieldEstimateHours",
      "AuditFieldTags"
    ];
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const resx = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
      keys.forEach((key) => expect(resx, `${lang} has no ${key}`).toContain(`name="${key}"`));
      // Whole patterns: the valued sentence owns all three slots, so a language may reorder them.
      const valued = new RegExp(
        '<data name="AuditFieldChangeValued"[^>]*>\\s*<value>([\\s\\S]*?)</value>').exec(resx)[1];
      ["{0}", "{1}", "{2}"].forEach((slot) =>
        expect(valued, `${lang} dropped ${slot}`).toContain(slot));
    });
  });

  it("declares the code in the executable contract", () => {
    // An undeclared code renders as the generic "the task changed" — which is exactly what this replaces.
    const contract = read("wwwroot", "assets", "js", "WorkCenterNext", "fixture-contract.js");
    expect(contract).toMatch(/'edited',?/);
  });
});

describe("a parked task says WHO it waits on, not only why", () => {
  /*
   * The projection has carried a `waitingOn` slot since WC-1 with a hard null in it, and the comment beside it
   * said why: "stays null until something can resolve a real identity to put there." The holder names the person
   * when they park the task now, so the slot has something to carry.
   */
  const parked = (waitingContext) => projectionItem({
    normalizedStatus: "Waiting", taskLifecycle: "Waiting", waitingContext
  });
  const person = { id: "ffffffff-ffff-ffff-ffff-ffffffffffff", displayName: "Ayşe Yılmaz" };
  const reason = { kind: "display", text: "Muhasebeden ekstre bekleniyor.", locale: "und" };

  /*
   * MUTATION TARGET (both facts). ⚠ THE OLD CODE PRINTED THE PERSON *INSTEAD OF* THE REASON. Naming somebody
   * therefore COST the reader the sentence saying what was actually being waited for — on two surfaces.
   */
  it("shows the person AND the reason when both are known", async () => {
    await boot(parked({ type: "externalInformation", waitingOn: person, reason,
      since: "2026-08-10T09:00:00+00:00" }));

    const note = app().querySelector(".wcn-parked-waiting");
    expect(note, "the waiting note disappeared").not.toBeNull();
    expect(note.textContent).toContain("WaitingOnWithReason");
    expect(note.textContent, "the person is missing").toContain("Ayşe Yılmaz");
    expect(note.textContent, "the reason was dropped for the person").toContain("Muhasebeden ekstre bekleniyor.");
  });

  it("shows the reason alone when nobody was named — the case that must not change", async () => {
    await boot(parked({ type: "externalInformation", waitingOn: null, reason,
      since: "2026-08-10T09:00:00+00:00" }));

    const note = app().querySelector(".wcn-parked-waiting");
    expect(note.textContent).toContain("Muhasebeden ekstre bekleniyor.");
    expect(note.textContent, "a person appeared out of nowhere").not.toContain("WaitingOnWithReason");
  });

  it("composes the sentence in ONE place, for every surface", () => {
    /*
     * Three surfaces say this — the detail note, the list chip and the lifecycle strip — and each used to
     * compose it itself, which is how two of them ended up with the same drop-the-reason defect.
     */
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const composer = src.slice(src.indexOf("const waitingSentence"), src.indexOf("const guidanceFor"));
    expect(composer).toContain("WaitingOnWithReason");
    // …and nobody else builds it. `waitingOn` may still be READ (to decide whether to show a chip at all).
    const others = src.replace(composer, "");
    expect(others, "a second surface composes the waiting sentence itself").not.toContain("tf('WaitingOn',");
  });

  it("builds a WHOLE sentence per language, never fragments joined in JS", () => {
    /*
     * "{person} bekleniyor — {reason}" puts the person first in Turkish and after a verb in Russian. A language
     * that wants a different order can only have it if it owns the whole pattern.
     */
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const composer = src.slice(src.indexOf("const waitingSentence"), src.indexOf("const guidanceFor"));
    expect(composer, "the sentence is being assembled from pieces").not.toMatch(/\+\s*['"] — ['"]/);

    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const resx = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
      const value = new RegExp(
        '<data name="WaitingOnWithReason"[^>]*>\\s*<value>([\\s\\S]*?)</value>').exec(resx);
      expect(value, `${lang} has no WaitingOnWithReason`).not.toBeNull();
      // Both slots present, so no language is silently dropping one of the two facts.
      expect(value[1], `${lang} lost a slot`).toContain("{0}");
      expect(value[1], `${lang} lost a slot`).toContain("{1}");
    });
  });

  it("ships the picker's own words in all seven languages", () => {
    ["WaitingOnLabel", "WaitingOnNobody"].forEach((key) =>
      ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) =>
        expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
          `${lang} has no ${key}`).toContain(`name="${key}"`)));
  });
});

describe("the person picker on the waiting dialog", () => {
  /*
   * MUTATION TARGET (optional). A wait is often on somebody this system has never heard of — a supplier, a
   * customer, an authority — and forcing a selection would make the honest answer unreachable.
   */
  it("offers the picker for inquire, and never demands it", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    // Declared as a LIST beside its required sibling, not as an `if (code === 'inquire')`.
    expect(src).toMatch(/const WAITING_ON_ACTIONS = \['inquire'\]/);
    expect(src, "inquire drifted into the REQUIRED list").not.toMatch(
      /ASSIGNEE_REQUIRED_ACTIONS = \[[^\]]*inquire/);

    // The optional branch returns whatever was chosen — including nothing — without validating it.
    const dialog = src.slice(src.indexOf("const offersWaitingOn"), src.indexOf("const offersWaitingOn") + 4000);
    expect(dialog).toContain("wcnWaitingOn");
    const optionalBranch = dialog.slice(dialog.indexOf("if (!needsAssignee)"));
    expect(optionalBranch.slice(0, 400), "an empty choice is being refused")
      .not.toContain("showValidationMessage");
  });

  it("takes its people from the ONE seam, unwrapped once", () => {
    // `TasksApi.assignablePeople()` returns the array directly (BL-113). This envelope was opened three
    // different ways in one session, and the fourth broke silently.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const dialog = src.slice(src.indexOf("const offersWaitingOn"), src.indexOf("const offersWaitingOn") + 1200);
    expect(dialog).toContain("TasksApi.assignablePeople()");
    expect(dialog, "the envelope is being unwrapped by hand again").not.toMatch(/data\?\.people|data\.people/);
  });

  it("sends the person only when one was chosen", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const body = src.slice(src.indexOf("inquire: ({ expectedVersion"), src.indexOf("return: ({"));
    expect(body).toContain("waitingOnUserId");
    /*
     * `undefined`, not null: JSON.stringify OMITS an undefined key, so a request with nobody named is
     * byte-identical to the one this client sent before the field existed.
     */
    expect(body).toContain("waitingOnUserId || undefined");
  });
});

describe("a comment can be rewritten and withdrawn, and says so", () => {
  /*
   * ⚠ THIS REVERSES A WRITTEN DECISION. Comments were immutable, deliberately: changing a sentence somebody has
   * already replied to can make their reply nonsense. What opened them is the TRAIL — an edit that says it was
   * edited and a withdrawal that leaves a marker keep the property immutability protected, which was "nothing
   * changes or disappears silently".
   */
  const comment = (extra) => Object.assign({
    id: "c1", kind: "comment", text: "muhasebeye sordum", actor: "Diten Admin",
    at: "2026-08-10T09:00:00+00:00"
  }, extra || {});
  const withFeed = (entries) => projectionItem({
    workItemCapabilities: ["planning", "execution", "subtasks", "activity"],
    subtasks: { mode: "full", items: [] },
    activity: entries
  });

  it("offers edit and withdraw on a comment the SERVER says is mine", async () => {
    await boot(withFeed([comment({ editable: true })]));

    const row = app().querySelector(".wcn-audit-comment");
    expect(row.querySelector("[data-wcn-comment-edit]"), "no way to edit my own comment").not.toBeNull();
    expect(row.querySelector("[data-wcn-comment-withdraw]"), "no way to withdraw my own comment").not.toBeNull();
    expect(row.querySelector("[data-wcn-comment-edit]").getAttribute("data-wcn-comment-task")).toBe(TASK_ID);
  });

  /*
   * MUTATION TARGET (authority). The flag is the SERVER's answer. Comparing the author's NAME here would hand
   * two people who share a name each other's buttons — and the handler would then refuse a control the screen
   * had offered.
   */
  it("offers nothing on somebody else's comment", async () => {
    await boot(withFeed([comment({ editable: false })]));

    const row = app().querySelector(".wcn-audit-comment");
    expect(row.querySelector("[data-wcn-comment-edit]"), "somebody else's comment is editable").toBeNull();
    expect(row.querySelector("[data-wcn-comment-withdraw]")).toBeNull();
  });

  it("marks an edited comment, and carries the instant on the mark", async () => {
    await boot(withFeed([comment({ editable: true, editedAt: "2026-08-11T10:00:00+00:00" })]));

    const mark = app().querySelector(".wcn-audit-edited");
    expect(mark, "an edited comment says nothing about it").not.toBeNull();
    expect(mark.textContent).toContain("CommentEdited");
    // "Edited" alone cannot answer "before or after I read it" — the instant is what settles that.
    expect(mark.getAttribute("title"), "the mark carries no date").toBeTruthy();
  });

  it("leaves an unedited comment unmarked", async () => {
    await boot(withFeed([comment({ editable: true })]));

    expect(app().querySelector(".wcn-audit-edited"), "an untouched comment claims to be edited").toBeNull();
  });

  /*
   * MUTATION TARGET (tombstone). A withdrawal must leave the ROW: a comment that vanished would renumber a
   * conversation other people quoted, and "somebody spoke here and took it back" is itself information.
   */
  it("keeps the row and shows a tombstone when a comment is withdrawn", async () => {
    await boot(withFeed([comment({ text: undefined, withdrawnAt: "2026-08-11T10:00:00+00:00" })]));

    const row = app().querySelector(".wcn-audit-comment");
    expect(row, "the withdrawn row vanished from the feed").not.toBeNull();
    expect(row.className).toContain("wcn-audit-item-withdrawn");
    expect(row.querySelector(".wcn-audit-withdrawn").textContent).toContain("CommentWithdrawn");
    // The author and the instant stay: the marker says WHO spoke here and WHEN.
    expect(row.querySelector(".wcn-audit-author").textContent).toContain("Diten Admin");
  });

  it("offers no controls on a withdrawn comment", async () => {
    await boot(withFeed([comment({ text: undefined, withdrawnAt: "2026-08-11T10:00:00+00:00", editable: false })]));

    expect(app().querySelector("[data-wcn-comment-withdraw]"), "a tombstone can be withdrawn again").toBeNull();
  });

  it("routes both writes through TasksApi, and applies nothing optimistically", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const edit = src.slice(src.indexOf("const editComment"), src.indexOf("const withdrawComment"));
    const withdraw = src.slice(src.indexOf("const withdrawComment"), src.indexOf("const consumeEntryBox"));
    expect(edit).toContain("TasksApi.updateComment");
    expect(withdraw).toContain("TasksApi.withdrawComment");
    // Both report through the shared writer, so the toast fires only after the server has answered.
    expect(edit).toContain("afterPhase2Write");
    expect(withdraw).toContain("afterPhase2Write");
  });

  it("asks before withdrawing — through the product's one confirm", () => {
    // The personal note deliberately asks nothing; this one does, because a withdrawn comment leaves a visible
    // gap in a conversation other people have already read.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const withdraw = src.slice(src.indexOf("const withdrawComment"), src.indexOf("const consumeEntryBox"));
    expect(withdraw).toContain("sharedConfirm");
  });

  it("seeds the edit box with the sentence being fixed, without growing the shared confirm", () => {
    /*
     * The shared wrapper has no `inputValue`, and it is NOT being given one — a shared component does not grow
     * to suit one module (standing rule). Its existing `didOpen` seam carries the value instead.
     */
    /*
     * ⚠ READ THE WHOLE FUNCTION, not a fixed slice. This used to take the first 3000 characters and broke the
     * day the seam grew a comment — a length is an assumption about the code's shape, and it rots exactly like
     * the call-count this suite's sibling had to give up for the same reason.
     */
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const start = src.indexOf("const sharedConfirm");
    const seam = src.slice(start, src.indexOf("\n    const ", start + 30));
    expect(seam).toContain("didOpen");
    const shared = read("Views", "Shared", "_GlobalConfirmation.cshtml");
    expect(shared, "the shared confirm grew an option for one module").not.toContain("inputValue");
  });

  it("ships the trail's words in all seven languages", () => {
    ["CommentEdited", "CommentWithdrawn", "CommentEdit", "CommentWithdraw", "CommentWithdrawConfirm"]
      .forEach((key) => ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) =>
        expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
          `${lang} has no ${key}`).toContain(`name="${key}"`)));
  });
});

describe("the tab the reader is on goes into the address", () => {
  /*
   * BL-087. `#etkinlik` worked on the way IN and not on the way OUT, so somebody sitting on Etkinlik who copied
   * the link sent the other person to Genel. The address is what people share; it has to say what they see.
   */
  it("writes the tab with replaceState, never pushState", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const writeDetailTabToAddress"),
      src.indexOf("const writeDetailTabToAddress") + 900);
    expect(fn, "the tab never reaches the address").toContain("replaceState");
    /*
     * On a two-tab page the Back button's job is to return to the LIST. A push per click would bury that exit
     * under however many times somebody glanced at the other tab.
     */
    expect(fn, "tab clicks are being pushed into history").not.toContain("pushState");
  });

  it("keeps nothing — no storage, no cookie", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const writeDetailTabToAddress"),
      src.indexOf("const writeDetailTabToAddress") + 900);
    // The address is visible and erasable; hidden memory that opens one link two ways for two people is not.
    expect(fn).not.toMatch(/localStorage|sessionStorage|document\.cookie/);
  });

  it("clears the fragment again when the reader returns to Genel", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const writeDetailTabToAddress"),
      src.indexOf("const writeDetailTabToAddress") + 900);
    // A stale `#etkinlik` on a link copied from Genel would be the same defect pointing the other way.
    expect(fn).toMatch(/'activity' \? '#etkinlik' : ''/);
  });
});

describe("the guidance answers the strip, so it comes after it", () => {
  /*
   * MUTATION TARGET (placement). MEASURED before the move: the guidance rendered at y=154 while the lifecycle
   * strip began at y=255 — a hundred pixels ABOVE the thing it is about, and outside the head card entirely
   * (`closest('.wcn-detail-head')` was null), because it was interpolated into the page-header block that sits
   * outside the grid.
   *
   * "Bu görev kabulünü bekliyor" is the ANSWER to the question the strip asks ("Beklemede · 1/4"). Printing the
   * answer above the question makes the reader hold a sentence they cannot place, then meet the state it was
   * about afterwards.
   */
  it("renders inside the head card, below the lifecycle strip", async () => {
    await boot(projectionItem({ admissionState: "pendingAcceptance" }));

    const guidance = app().querySelector(".wcn-guidance");
    expect(guidance, "the guidance disappeared").not.toBeNull();
    const head = app().querySelector(".wcn-detail-head");
    expect(head.contains(guidance), "the guidance is outside the head card").toBe(true);

    // Document order IS the visual order inside one card; jsdom lays nothing out, so this is the honest check.
    const strip = app().querySelector(".wcn-stepbar");
    expect(strip, "there is no lifecycle strip to answer").not.toBeNull();
    expect(strip.compareDocumentPosition(guidance) & Node.DOCUMENT_POSITION_FOLLOWING,
      "the answer is still printed before the question").toBeTruthy();
  });

  /*
   * ORDER AGAINST THE BLOCK NOTICE — guidance FIRST, blockers AFTER, and the reason is not aesthetic: the
   * guidance says what to do NEXT, a blocker says why something cannot be done YET. A reader who meets the
   * obstacle first has nothing to attach it to.
   *
   * ⚠ MEASURED LIVE AND NOT FOUND: across the whole live surface, 20 tasks are pendingAcceptance and 4 are
   * blocked or waiting — and the two sets do not intersect, so the pair never appears on a real task today. The
   * order is pinned HERE, where a fixture can hold both, rather than left to the day the two first meet.
   */
  it("puts the guidance before the block notice when a task carries both", async () => {
    // The contract requires every blocked code to name an action the reader can SEE disabled, so the fixture
    // carries that action too — a blocker pointing at nothing would have the item dropped, not rendered.
    await boot(projectionItem({
      admissionState: "pendingAcceptance",
      primaryActionCode: "complete",
      actions: [{
        code: "complete", label: { kind: "resource", key: "WorkAggregation_Action_complete" },
        semanticType: "complete", enabled: false, source: "provider",
        disabledReasonCode: "SUBTASK_BLOCKED",
        // A LABEL, not a bare string — the contract's `isLabel` is what makes a reason translatable.
        disabledReason: { kind: "display", text: "3 alt görev kapanmadan tamamlanamaz", locale: "und" },
        requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
        supportsBulk: false, riskLevel: "normal"
      }],
      blockedState: { blocked: true, affectedActionCodes: ["complete"],
        blockers: [{ code: "SUBTASK_BLOCKED", affectedActionCode: "complete",
          label: { kind: "resource", key: "WorkAggregation_ActionDisabled_SubtaskOpen" } }] }
    }));

    const guidance = app().querySelector(".wcn-guidance");
    const blocked = app().querySelector(".wcn-blocked");
    expect(guidance, "no guidance on a pending task").not.toBeNull();
    expect(blocked, "no block notice on a blocked task").not.toBeNull();
    expect(guidance.compareDocumentPosition(blocked) & Node.DOCUMENT_POSITION_FOLLOWING,
      "the obstacle is read before the instruction").toBeTruthy();
  });

  it("shares one rhythm with the notices beside it", () => {
    // MEASURED live: guidance 12px below the strip, `.wcn-blocked` 16px below it — one card, two gaps. The
    // guidance is the one that moved, so the guidance is the one that adapts.
    const rule = /^\.wcn-guidance \{([^}]*)\}/m.exec(CSS());
    expect(rule[1]).toMatch(/margin-block:\s*1rem 0/);
  });
});

describe("whose subtask this is, as a fact rather than a card", () => {
  /*
   * MUTATION TARGET (the parent row). MEASURED: a card with NO heading, 73px tall, whose entire content was one
   * sentence naming the parent. A card groups facts; this was one fact wearing a container — and the fact
   * belongs where the task's other facts are.
   */
  it("puts the parent task in the Summary, with the golden field pattern", async () => {
    await boot(projectionItem({ parentTaskItemId: "98d1f94e-1848-4539-8a99-774e72651b8a" }));

    const field = [...app().querySelectorAll(".wcn-sum .backbone-preview-field")]
      .find((f) => f.textContent.includes("DetailParentTask"));
    expect(field, "the parent never arrived in the Summary").not.toBeNull();
    expect(field.querySelector("i"), "the parent field lost its icon").not.toBeNull();
    expect(field.querySelector(".backbone-preview-label").textContent).toContain("DetailParentTask");
  });

  it("no longer draws a headless card for it", async () => {
    await boot(projectionItem({ parentTaskItemId: "98d1f94e-1848-4539-8a99-774e72651b8a" }));

    const headless = [...app().querySelectorAll(".wcn-detail-card")]
      .filter((c) => !c.querySelector("h6") && c.querySelector("a[href*='Details']"));
    expect(headless.length, "the one-line card came back").toBe(0);
  });

  /*
   * THE LINK MUST NOT BE DEAD. It pointed at `?id=…` — a query string this page's route does not read, so a
   * click reloaded the SAME task. Measured live before the move; measured live again after, with a real click
   * that landed on the parent's own detail page.
   */
  it("links to the parent's real detail route, not a query string nobody parses", async () => {
    await boot(projectionItem({ parentTaskItemId: "98d1f94e-1848-4539-8a99-774e72651b8a" }));

    const link = [...app().querySelectorAll(".wcn-sum a")]
      .find((a) => a.getAttribute("href")?.includes("98d1f94e"));
    expect(link, "the parent is named but not reachable").not.toBeNull();
    expect(link.getAttribute("href")).toBe("/WorkCenterNext/Details/98d1f94e-1848-4539-8a99-774e72651b8a");
    expect(link.getAttribute("href"), "the dead query-string link came back").not.toMatch(/^\?id=/);
  });

  it("draws no parent row on a task that has no parent", async () => {
    await boot(projectionItem({ parentTaskItemId: null }));

    expect([...app().querySelectorAll(".backbone-preview-label")]
      .some((l) => l.textContent.includes("DetailParentTask")), "a parentless task claims a parent").toBe(false);
  });

  it("uses a glyph no other surface has taken", () => {
    // The old notice's `bx-subdirectory-right` is spoken for — the Positions tree draws its descendants with it.
    // Right branches DOWN to a child; left comes BACK to a parent, which is the direction this row points.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src).toContain("bx-subdirectory-left");
  });

  it("ships the label in all seven languages", () => {
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) =>
      expect(read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`),
        `${lang} has no DetailParentTask`).toContain('name="DetailParentTask"'));
  });
});

describe("the grip is drawn in both modes — the reported contradiction is not one", () => {
  /*
   * REPORTED: `diten-checkitem.js` comments "Drawn in BOTH modes now" while the code reads
   * `if (!working) { el.appendChild(grip); }` — so is the comment stale or the code wrong?
   *
   * NEITHER. MEASURED: the grip is appended TWICE in the source, once per ordering branch. Authoring appends it
   * early (line ~139, the `if (!working)` guard), because that mode reads as "arrange these" and the handle
   * leads; working appends it late, inside the `if (working)` block, after the text and the level chip, because
   * that mode reads as "tick these off". The `if (!working)` line does not mean "authoring only" — it means
   * "here, in the authoring order". Read in isolation it looks like the opposite.
   *
   * Confirmed in the live DOM on the detail page (working mode): the row's children are box, text, level, GRIP,
   * move — the handle is present, and `bindChecklistDrag`'s `handle: '[data-diten-check-grip]'` therefore
   * matches something. Comment and code agree; the two lines simply cannot be read one at a time.
   */
  it("appends the grip in each ordering branch, so both surfaces can be dragged", () => {
    const src = read("wwwroot", "assets", "js", "shared", "diten-checkitem.js");
    const appends = src.match(/appendChild\(grip\)/g) || [];
    expect(appends.length, "the grip is appended in only one branch — one surface lost its drag").toBe(2);
  });

  it("draws the grip the detail page's Sortable is told to grab", async () => {
    // The contract couples capability and container BOTH ways, so the fixture declares every container it names.
    await boot(projectionItem({
      workItemCapabilities: ["planning", "execution", "checklist", "subtasks"],
      checklist: { version: 1, items: [
        { id: "c1", text: "tek madde", requirement: "Optional", done: false, evidenceRequired: false }
      ] }
    }));

    const handle = app().querySelector("[data-diten-check-grip]");
    expect(handle, "the drag handle is missing on the working surface").not.toBeNull();
    // …and it is the exact selector the page binds Sortable to. A handle nobody draws is a dead binding.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    expect(src).toContain("handle: '[data-diten-check-grip]'");
  });
});

describe("a checklist row is the same height whatever the list holds", () => {
  /*
   * MUTATION TARGET (reserved space). MEASURED: one item rendered a 33.39px row and six rendered 44px, in the
   * same list on the same page — adding a second item made every row grow by 10.61px. The cause was `d-none` on
   * the reorder controls, and `display: none` takes a control out of LAYOUT as well as out of sight.
   *
   * The controls STAY withdrawn on a one-item list — reordering one item is meaningless and a disabled arrow
   * still promises reordering. What changed is only how: `visibility: hidden` withdraws exactly as much
   * (invisible, unclickable, unfocusable, out of the accessibility tree) and keeps the box.
   */
  it("withdraws the reorder controls without collapsing their space", () => {
    const src = read("wwwroot", "assets", "js", "shared", "diten-checkitem.js");
    const fn = src.slice(src.indexOf("const applyMoveState"), src.indexOf("const applyMoveState") + 2200);
    const code = fn.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
    expect(code, "the controls collapse the row again").not.toContain("'d-none'");
    expect(code, "nothing withdraws them at all").toContain("-withdrawn");

    const rule = /^\.diten-checkitem-withdrawn \{([^}]*)\}/m.exec(CSS());
    expect(rule, "the withdrawn class has no rule").not.toBeNull();
    expect(rule[1], "the class hides the box instead of just its paint").toMatch(/visibility:\s*hidden/);
    expect(rule[1], "display:none came back through the CSS").not.toMatch(/display:\s*none/);
  });

  it("keeps the controls out of the tab order on a one-item list", () => {
    // `visibility: hidden` already removes them; the buttons are ALSO disabled by position. Both, deliberately —
    // this is the half of "hidden" that a class swap is most likely to drop.
    const src = read("wwwroot", "assets", "js", "shared", "diten-checkitem.js");
    const fn = src.slice(src.indexOf("const applyMoveState"), src.indexOf("const applyMoveState") + 2200);
    expect(fn).toMatch(/btn\.disabled = single \|\| atEnd/);
  });
});

describe("the Personal card carries only what nobody else can see", () => {
  const withNotes = (notes, extra) => projectionItem(Object.assign({
    personal: { snoozedUntil: null, notes }
  }, extra || {}));
  const note = (id, text) => ({ id, text, createdAt: "2026-08-10T09:00:00+00:00" });

  /*
   * (a) THE SNOOZE IS A ROW ONCE IT IS SET. A snooze that is ON is a FACT about this reader's inbox — "hidden
   * from me until the 22nd" — and a button reading "Ertelemeyi kaldır" states that only by implication, in the
   * negative, in a verb. The row says the date; the trailing control undoes it.
   */
  it("states the snooze as a row with its date, and a control that clears it", async () => {
    await boot(withNotes([], { personal: { snoozedUntil: "2099-01-05", notes: [] } }));

    const row = app().querySelector(".wcn-snooze-row");
    expect(row, "a snoozed task shows no snooze row").not.toBeNull();
    expect(row.textContent, "the row does not say it is snoozed").toContain("SnoozedLabel");
    expect(row.textContent, "the row does not say until when").toContain("2099-01-05");
    const clear = row.querySelector("[data-wcn-snooze]");
    expect(clear, "there is no way to clear it").not.toBeNull();
    expect(clear.textContent).toContain("SnoozeClear");
  });

  it("offers the plain Ertele action when nothing is snoozed — no row for a fact that is not true", async () => {
    await boot(withNotes([]));

    expect(app().querySelector(".wcn-snooze-row"), "an un-snoozed task claims to be snoozed").toBeNull();
    const button = app().querySelector(".wcn-personal-btn[data-wcn-snooze]");
    expect(button, "the offer to snooze disappeared").not.toBeNull();
    expect(button.textContent).toContain("Snooze");
  });

  /*
   * (b) RELATIVE ON SCREEN, ABSOLUTE IN title AND aria-label. "3 gün önce" is the right answer to glance at and
   * the wrong one to act on — and a screen-reader user has no hover to fall back on.
   */
  it("says when a note was written twice: relative to read, absolute to act on", async () => {
    await boot(withNotes([note("n1", "muhasebeye sordum")]));

    const when = app().querySelector(".wcn-note-when");
    expect(when.textContent.trim(), "the row has no relative time").not.toBe("");
    expect(when.getAttribute("title"), "no absolute date on hover").toBeTruthy();
    expect(when.getAttribute("aria-label"), "a screen reader gets only the relative words").toContain("NoteWrittenAt");
  });

  /*
   * MUTATION TARGET (no confirmation). Owner decision: a private note is cheap to lose and cheap to write again,
   * and an "are you sure" on every one of them trains the reader to dismiss dialogs — including the ones that
   * ask something.
   */
  it("deletes a note without asking, and without a confirm dialog anywhere in the path", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const removePersonalNote"), src.indexOf("// Snooze is a personal filter"));
    const code = fn.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
    expect(code, "note deletion grew a confirmation step").not.toMatch(/sharedConfirm|showConfirm|Swal/);
    expect(code, "the delete no longer reaches the API").toContain("deletePersonalNote");
  });

  /* (c) The hint is the PRIVACY sentence, and it is NOT the placeholder — a placeholder disappears the moment
     you start typing, which is exactly when "only you see this" needs to be readable. */
  it("keeps the privacy sentence on screen while the reader types", async () => {
    await boot(withNotes([]));

    const hint = app().querySelector(".wcn-note-hint");
    expect(hint, "the add row has no hint line").not.toBeNull();
    expect(hint.textContent).toContain("NoteAddHint");
    // …and it is not merely the placeholder repeated, which would vanish on the first keystroke.
    expect(app().querySelector("[data-wcn-note-add]").getAttribute("placeholder"))
      .not.toBe(hint.textContent.trim());
  });

  it("ships the privacy sentence itself in all seven languages, with no Enter instruction left in it", () => {
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const resx = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
      const value = new RegExp(
        '<data name="NoteAddHint"[^>]*>\\s*<value>([\\s\\S]*?)</value>').exec(resx)[1];
      expect(value.trim().length, `${lang} has an empty hint`).toBeGreaterThan(0);
      expect(value, `${lang} still instructs about Enter in the privacy line`).not.toMatch(/Enter/);
    });
  });

  /* (d) The add row IS the empty state — no "there is nothing here" line above a box for putting something there. */
  it("says nothing about emptiness when there are no notes", async () => {
    await boot(withNotes([]));

    expect(app().querySelectorAll(".wcn-note-row:not(.wcn-snooze-row)").length).toBe(0);
    expect(app().querySelector("[data-wcn-note-add]"), "a note cannot be added at all").not.toBeNull();
  });

  /* (e) The count, in the head, in the checklist head's own idiom — and only when there is something to count. */
  it("counts the notes in the card head, and only when there are any", async () => {
    await boot(withNotes([note("n1", "bir"), note("n2", "iki")]));
    const head = [...app().querySelectorAll(".wcn-detail-rail h6")]
      .find((h) => h.textContent.includes("PersonalCardLabel"));
    expect(head.querySelector(".wcn-count-inline").textContent).toBe("2");

    await boot(withNotes([]));
    const empty = [...app().querySelectorAll(".wcn-detail-rail h6")]
      .find((h) => h.textContent.includes("PersonalCardLabel"));
    expect(empty.querySelector(".wcn-count-inline"), "a zero badge labels an absence").toBeNull();
  });
});

describe("three lists, one row language", () => {
  /*
   * MEASURED, and it was a real drift: the note row carried `background: var(--bs-body-bg)` and
   * `padding: .4375rem .5rem` while the checklist item and the subtask row both carry `var(--bs-card-bg)` and
   * `.375rem .5rem` — and the note row had NO hover rule at all. The body colour is the exact mistake the
   * checklist rule warns about in its own comment: it measures rgb(245,245,249) against a white card, making
   * every row a grey panel inside a white panel.
   */
  const declarationsOf = (selector) => {
    const rule = new RegExp("^" + selector + "\\s*\\{([^}]*)\\}", "m").exec(CSS());
    return rule && rule[1].replace(/\/\*[\s\S]*?\*\//g, "");
  };
  const valueOf = (selector, property) => {
    const body = declarationsOf(selector);
    const hit = new RegExp(property + ":\\s*([^;]+);").exec(body || "");
    return hit && hit[1].trim();
  };

  it("gives the note row the same surface, border, radius and padding as its two siblings", () => {
    ["background", "border", "border-radius", "padding"].forEach((property) => {
      expect(valueOf("\\.wcn-note-row", property), `the note row's ${property} drifted`)
        .toBe(valueOf("\\.diten-checkitem", property));
    });
    // The subtask row states the same four in its own rule.
    ["background", "border", "border-radius", "padding"].forEach((property) => {
      expect(valueOf("\\.wcn-subtasks > li", property), `the subtask row's ${property} drifted`)
        .toBe(valueOf("\\.wcn-note-row", property));
    });
  });

  it("gives all three the same hover AND focus-within tint", () => {
    // The two older lists share one rule; the note row has its own with the same value.
    expect(CSS()).toMatch(/\.diten-checkitem:hover,[\s\S]{0,200}?\.wcn-subtasks > li:focus-within \{\s*background: rgba\(var\(--bs-primary-rgb\), \.03\);/);
    const note = /\.wcn-note-row:hover,\s*\.wcn-note-row:focus-within \{([^}]*)\}/.exec(CSS());
    expect(note, "the note row has no hover at all — a mouse user cannot see which row they are on").not.toBeNull();
    expect(note[1]).toMatch(/rgba\(var\(--bs-primary-rgb\), \.03\)/);
  });

  it("centres all three", () => {
    expect(valueOf("\\.wcn-note-row", "align-items")).toBe("center");
    expect(valueOf("\\.diten-checkitem", "align-items")).toBe("center");
    expect(valueOf("\\.wcn-check, \\.wcn-subtask", "align-items")).toBe("center");
  });
});

describe("the three small ones", () => {
  /*
   * (a) `data-bs-display="static"` switches Bootstrap off Popper. `dropup` already makes THIS menu's direction
   * deterministic, so the attribute bought nothing — and left in place it would drop the next dropdown added to
   * this page into the same below-the-fold trap, silently.
   */
  it("no longer turns Popper off for the whole page", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js")
      .replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
    expect(src, "a dropdown still disables Popper").not.toContain('data-bs-display="static"');
  });

  /*
   * (b) MUTATION TARGET (bulk result). BEHAVIOUR CHANGE: the partial-failure REPORT is a toast, not a modal.
   * A modal is for a decision and this asks for none — its one button exists to make it go away. The failed rows
   * stay selected and flagged on the surface behind it, which is the durable record; the modal was never where
   * the recovery happened.
   */
  it("reports a partial bulk failure as a toast, not as a dialog to dismiss", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const runBulk = "), src.indexOf("const runBulkWithProgress"));
    const code = fn.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
    expect(code, "the bulk result still stops the reader with a dialog").not.toMatch(/Swal\.fire/);
    expect(code, "the result is not reported at all").toContain("toast(tf('BulkResult'");
    // Total failure and partial failure still read differently.
    expect(code).toContain("'error'");
    expect(code).toContain("'warning'");
  });

  /*
   * (c) BL-138. "Only where the bar exists" was the COMMENT, not the rule: the media query alone applied 80px of
   * dead space to every narrow detail page, including the ones that draw no bar (a closed task offers no
   * actions, so renderActionBar returns an empty string).
   */
  it("reserves room under the bar only on pages that actually have one", () => {
    const rule = /@media \(max-width: 991\.98px\) \{\s*(\.wcn-details-page[^{]*)\{[^}]*padding-block-end/.exec(CSS());
    expect(rule, "the narrow-screen bottom padding rule vanished").not.toBeNull();
    expect(rule[1], "the padding still applies to pages with no bar").toContain(":has(.wcn-actionbar)");
  });
});

describe("the personal note list, on the page", () => {
  /*
   * The DOM half of the same change. The block above proves the WRITE goes to the server; this proves the READ
   * comes back — a stored note that no card draws is the plan date's own defect (f8d10259) repeated.
   */
  const withNotes = (notes, extra) => projectionItem(Object.assign({
    personal: { snoozedUntil: null, notes }
  }, extra || {}));

  it("draws one row per stored note, with its text", async () => {
    await boot(withNotes([
      { id: "n1", text: "muhasebeye sordum", createdAt: "2026-08-10T09:00:00+00:00" },
      { id: "n2", text: "cuma tekrar bak", createdAt: "2026-08-12T09:00:00+00:00" }
    ]));

    const rows = app().querySelectorAll(".wcn-note-row");
    expect(rows.length, "the stored notes did not reach the page").toBe(2);
    expect(rows[0].querySelector(".wcn-note-text").textContent).toBe("muhasebeye sordum");
    expect(rows[1].querySelector(".wcn-note-text").textContent).toBe("cuma tekrar bak");
  });

  it("gives every row a delete that names the note AND its task", async () => {
    await boot(withNotes([{ id: "n1", text: "tek not", createdAt: "2026-08-10T09:00:00+00:00" }]));

    const remove = app().querySelector("[data-wcn-note-remove]");
    expect(remove, "there is no way to delete a note").not.toBeNull();
    expect(remove.getAttribute("data-wcn-note-remove")).toBe("n1");
    // BOTH ids on the control: the handler must never have to guess which task the row belonged to.
    expect(remove.getAttribute("data-wcn-note-task")).toBe(TASK_ID);
    expect(remove.getAttribute("aria-label"), "the icon-only delete is unnamed").toBeTruthy();
  });

  it("says WHEN each note was written, from the instant and never from a stored count", async () => {
    await boot(withNotes([{ id: "n1", text: "not", createdAt: "2026-08-10T09:00:00+00:00" }]));

    expect(app().querySelector(".wcn-note-when").textContent.trim(), "the row has no time at all").not.toBe("");
    // The absolute instant is what travels; the words are derived at render. A note carrying a pre-computed
    // "2 days ago" is the `ago` field this project banned.
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const helper = src.slice(src.indexOf("const noteWhen"), src.indexOf("const noteWhen") + 400);
    expect(helper, "the time is not derived from the stored instant").toContain("agoLabel");
  });

  it("shows the add row even when there is not a single note", async () => {
    await boot(projectionItem());

    // No "there is nothing here" sentence above an empty box: the add row IS the empty state, the same rule the
    // checklist card follows.
    expect(app().querySelectorAll(".wcn-note-row").length).toBe(0);
    expect(app().querySelector("[data-wcn-note-add]"), "the note cannot be added at all").not.toBeNull();
  });

  it("carries the task id on the input, so Enter and the button cannot disagree", async () => {
    await boot(projectionItem());

    expect(app().querySelector("[data-wcn-note-add]").getAttribute("data-wcn-note-add")).toBe(TASK_ID);
    expect(app().querySelector("[data-wcn-note-save]").getAttribute("data-wcn-note-save")).toBe(TASK_ID);
  });

  it("says out loud that Enter also adds — the placeholder disappears as you type", async () => {
    await boot(projectionItem());

    expect(app().querySelector(".wcn-note-hint"), "the add row has no hint line").not.toBeNull();
  });

  it("has no textarea and no lone Save button left", async () => {
    await boot(withNotes([{ id: "n1", text: "not", createdAt: "2026-08-10T09:00:00+00:00" }]));

    // The old shape, gone: one box whose second sentence overwrote the first, under a button that saved nothing.
    expect(app().querySelector(".wcn-note textarea")).toBeNull();
  });
});

describe("the personal note is written to the server, not to a variable", () => {
  /*
   * WHAT THIS REPLACED. Until 2026-08-14 this block asserted `it.note =` — that the note was stored in a
   * JavaScript object — and that a toast fired beside it. Both were true, and together they were the defect: the
   * assignment was the whole of the save, and the toast said "Not kaydedildi" about it. The guard was pinning the
   * lie in place. It is REWRITTEN rather than deleted, so the next reader can see which behaviour was retired.
   */
  it("posts through TasksApi and never assigns the note in place", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const addPersonalNote"), src.indexOf("const removePersonalNote"));
    expect(fn, "the note write does not reach the API").toContain("TasksApi.addPersonalNote");
    expect(fn, "the note is still assigned in place for a real task").not.toMatch(/it\.note\s*=/);
  });

  /*
   * MUTATION TARGET (the toast). The confirmation must sit INSIDE the success path. `afterPhase2Write` is what
   * owns that rule — it toasts the success key only when `result.ok`, and every other message it emits is an
   * error — so the assertion is that the note write goes through it rather than toasting on its own.
   */
  it("says 'saved' only through the writer that checks the answer first", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const addPersonalNote"), src.indexOf("const removePersonalNote"));
    const real = fn.slice(fn.indexOf("TasksApi.addPersonalNote"));
    expect(real, "the toast escaped the success check").not.toMatch(/(?<!\/\/[^\n]*)\btoast\(/);
    expect(fn, "the write no longer reports through afterPhase2Write").toContain(
      "afterPhase2Write(result, 'ToastNoteSaved')"
    );
  });

  /*
   * MUTATION TARGET (the snooze). It was `item.snoozedUntil = dateStr` and nothing else — a park that lasted
   * until the next reload. A real task's snooze now goes to `/personal/snooze`.
   */
  it("sends the snooze to the server for a real task", () => {
    const src = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const fn = src.slice(src.indexOf("const toggleSnooze"), src.indexOf("const openNew"));
    expect(fn, "the snooze never reaches the API").toContain("TasksApi.setSnooze");
    // Both directions through the same call — clearing is a null date, not a second endpoint.
    expect(fn, "waking the task does not go to the server").toContain("snoozedUntil: null");
  });
});
