const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * A4 — THE DEPENDENCY ROW SAYS WHAT IT MEANS (2026-08-24, owner's option C).
 *
 * MEASURED BEFORE, on a live task: `ÖNCÜL · sasasa · FS · tamam` — four parts side by side with nothing
 * relating them, the expansion of `FS` reachable only through a `title` tooltip (absent on touch, never sought
 * on a desktop), and a `tamam` badge at the far right of the row that belongs to the PREDECESSOR but reads as
 * the row's own state.
 *
 * The compact single line survives. Three things changed: the direction is an ARROW, the edge type is a
 * HALF-SENTENCE in the row, and the abbreviation stays only as a demoted footnote.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");

const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";
const TYPES = ["FinishToStart", "FinishToFinish", "StartToStart", "StartToFinish"];
const ABBR = { FinishToStart: "FS", FinishToFinish: "FF", StartToStart: "SS", StartToFinish: "SF" };
const SUFFIX = { FinishToStart: "FS", FinishToFinish: "FF", StartToStart: "SS", StartToFinish: "SF" };

const dep = (type, direction, state = "not-started", n = 0) => ({
  id: `DEP-${direction}-${ABBR[type]}-${n}`,
  title: { kind: "display", text: `Öncül ${ABBR[type]} ${direction}`, locale: "und" },
  type, state, direction
});

const item = (dependencies) => ({
  fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
  admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress",
  executionState: "active", timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Bağımlılık gösterimi", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}` },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution", "dependencies"],
  subtasks: null, dependencies,
  actions: [], concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null
});

const boot = (dependencies) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item(dependencies)]
});

const rows = () => [...app().querySelectorAll(".wcn-dep")];

describe("the row states the rule instead of abbreviating it", () => {
  it("gives every one of the eight combinations its own sentence", async () => {
    /*
     * FOUR TYPES × TWO DIRECTIONS = EIGHT, and all eight are asserted here rather than one representative:
     * a table with a missing row is exactly the failure that leaves `SF/succ` rendering a bare title in
     * production while the test suite stays green.
     *
     * MUTATION GUARD: reduce the cell back to `esc(d.title)` (or to the `FS` abbreviation) and this goes red.
     */
    const all = [];
    TYPES.forEach((type) => ["pred", "succ"].forEach((direction) => all.push(dep(type, direction))));
    await boot(all);

    const drawn = rows();
    expect(drawn, "not every combination reached the card").toHaveLength(8);

    drawn.forEach((li, index) => {
      const { type, direction } = { type: all[index].type, direction: all[index].direction };
      const expected = `DepSentence${direction === "pred" ? "Pred" : "Succ"}${SUFFIX[type]}`;
      const text = li.querySelector(".wcn-dep-title").textContent;
      expect(text, `${direction}/${SUFFIX[type]} did not get its own sentence`).toContain(expected);
      // …and the OTHER direction's sentence is not what got used.
      const wrong = `DepSentence${direction === "pred" ? "Succ" : "Pred"}${SUFFIX[type]}`;
      expect(text, `${direction}/${SUFFIX[type]} used the opposite direction's sentence`).not.toContain(wrong);
    });

    // Eight DISTINCT keys — one sentence reused for all eight would satisfy every check above.
    const keys = drawn.map((li) => li.querySelector(".wcn-dep-title").textContent);
    expect(new Set(keys).size, "the eight rows do not read as eight different rules").toBe(8);
  });

  it("carries the task's own title inside the sentence", async () => {
    /*
     * The sentence is a FORMAT string, not a replacement for the name: a reader who cannot see which task the
     * rule is about has lost more than the abbreviation ever cost them.
     */
    await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [item([dep("FinishToStart", "pred")])],
      wcn: {
        t: (key) => key,
        tf: (key, ...args) => (String(key).startsWith("DepSentence") ? `${key}::${args[0]}` : key),
        tn: (key) => key
      }
    });
    expect(rows()[0].querySelector(".wcn-dep-title").textContent).toContain("Öncül FS pred");
    expect(APP, "the sentence stopped being a format string").toContain("tf(sentenceKey, d.title)");
  });

  it("keeps the direction words out and the arrow in", async () => {
    /*
     * MUTATION GUARD (direction): swap the two icons in `DEP_DIR_ICON` and this goes red.
     *
     * LEFT = something upstream holds me (predecessor). RIGHT = I hold something downstream (successor).
     */
    await boot([dep("FinishToStart", "pred"), dep("FinishToStart", "succ")]);
    const [pred, succ] = rows();

    expect(pred.className).toContain("wcn-dep-pred");
    expect(succ.className).toContain("wcn-dep-succ");
    expect(pred.querySelector(".wcn-dep-arrow").className).toContain("bx-left-arrow-alt");
    expect(succ.querySelector(".wcn-dep-arrow").className).toContain("bx-right-arrow-alt");
    expect(pred.querySelector(".wcn-dep-arrow").className, "the predecessor points downstream")
      .not.toContain("bx-right-arrow-alt");
    expect(succ.querySelector(".wcn-dep-arrow").className, "the successor points upstream")
      .not.toContain("bx-left-arrow-alt");

    // The words are gone from the row AND from the code that built it.
    expect(app().querySelector(".wcn-dep-dir"), "the direction word came back").toBeNull();
    expect(APP, "the direction is a word again").not.toContain("DepPredecessor");

    /*
     * ⚠ THE ARROW IS SILENT. The sentence already states the direction in words, so an icon that announced
     * itself would make a screen reader read the direction twice.
     */
    rows().forEach((li) =>
      expect(li.querySelector(".wcn-dep-arrow").getAttribute("aria-hidden")).toBe("true"));
  });

  it("keeps the abbreviation as a footnote, not as the statement", async () => {
    await boot([dep("StartToFinish", "pred")]);
    const abbr = rows()[0].querySelector(".wcn-dep-abbr");
    expect(abbr.textContent).toBe("SF");
    // Its expansion still exists for a pointer, but it is no longer the ONLY place the meaning lives.
    expect(abbr.getAttribute("title")).toBe("DepTypeSF");
    // Not a chip: a chip would claim the sentence's weight.
    expect(abbr.className, "the abbreviation is a chip again").not.toContain("wcn-chip");
    // And the row is readable with the abbreviation removed entirely.
    abbr.remove();
    expect(rows()[0].textContent).toContain("DepSentencePredSF");
  });

  it("leaves the state badge and its vocabulary alone", async () => {
    /*
     * DEP_STATE_KEY / DEP_STATE_KIND were NOT touched — `cancelled` included. What changed is that the arrow
     * now says whose state it is.
     */
    await boot([dep("FinishToStart", "pred", "done"), dep("FinishToStart", "pred", "not-started", 1),
      dep("FinishToStart", "pred", "cancelled", 2)]);
    const badges = rows().map((li) => li.querySelector(".wcn-badge"));
    expect(badges.map((b) => b.textContent)).toEqual(["DepDone", "DepNotStarted", "DepCancelled"]);
    expect(badges[0].className).toContain("wcn-badge-success");
    expect(badges[1].className).toContain("wcn-badge-secondary");
    expect(rows()[2].className, "a called-off predecessor stopped being marked").toContain("is-cancelled");
  });

  it("wears the product's own row language, with the product's own numbers", () => {
    // Copied value-for-value from `.diten-checkitem`; no new row dialect was invented.
    const rule = CSS.slice(CSS.indexOf("\n.wcn-dep {"), CSS.indexOf("}", CSS.indexOf("\n.wcn-dep {")));
    expect(rule).toContain("padding: .375rem .5rem");
    expect(rule).toContain("border: 1px solid var(--bs-border-color)");
    expect(rule).toContain("border-radius: .375rem");
    expect(rule).toContain("background: var(--bs-card-bg)");
    expect(rule).toContain("align-items: center");
  });

  it("stays read-only, and still points at the source", async () => {
    // On the spec's NEVER list: no editor, no Gantt, no graph in the Task Center.
    await boot([dep("FinishToStart", "pred")]);
    const card = app().querySelector(".wcn-deps").closest(".wcn-detail-section");
    expect(card.querySelector(".wcn-block-hint").textContent).toContain("DepsReadonlyHint");
    expect(card.querySelectorAll("button, input, select, textarea, a[href]"),
      "an editing control appeared on a read-only card").toHaveLength(0);
  });

  it("draws no card at all when there are no dependencies", async () => {
    // UNCHANGED this round, deliberately: the empty-state wording agreed in A5 arrives in its own turn.
    await boot([]);
    expect(app().querySelector(".wcn-deps")).toBeNull();
  });
});

describe("eight sentences, seven languages", () => {
  const KEYS = [];
  ["Pred", "Succ"].forEach((d) => ["FS", "FF", "SS", "SF"].forEach((t) => KEYS.push(`DepSentence${d}${t}`)));

  it("declares all eight in every language, each carrying its argument", () => {
    expect(KEYS).toHaveLength(8);
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      KEYS.forEach((key) => {
        const at = xml.indexOf(`name="${key}"`);
        expect(at, `${lang} is missing ${key}`).toBeGreaterThan(-1);
        const value = xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
        expect(value.trim(), `${lang}/${key} is empty`).not.toBe("");
        expect(value, `${lang}/${key} dropped its {0}`).toContain("{0}");
      });
    });
  });

  it("says something different for each of the eight, in each language", () => {
    // A copy-paste that left all four predecessor sentences identical would still pass the check above.
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      const values = KEYS.map((key) => {
        const at = xml.indexOf(`name="${key}"`);
        return xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
      });
      expect(new Set(values).size, `${lang} repeats a sentence`).toBe(8);
    });
  });

  it("derives its meaning from the vocabulary the product already had", () => {
    // The four rules are already stated as sentences for the red banner; these eight are the same rules read
    // from both ends. If those keys ever disappear, the derivation this table rests on is gone with them.
    ["BlockerFinishToStart", "BlockerFinishToFinish", "BlockerStartToStart", "BlockerStartToFinish",
      "DepTypeFS", "DepTypeFF", "DepTypeSS", "DepTypeSF"].forEach((key) =>
      expect(resx("tr").indexOf(`name="${key}"`), `${key} vanished`).toBeGreaterThan(-1));
  });
});
