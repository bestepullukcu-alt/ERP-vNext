const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * "THIS CAME BACK TO YOU" — the return signal on the row and on the detail page.
 *
 * THE DEFECT. Returning a task has worked since WC-1: the handler demands a reason, refuses anyone but the
 * assignee, hands the work back to its requester and records `Returned` with the returner's own sentence. None
 * of it reached the browser — MEASURED 2026-09-03, `"Returned"` appeared in zero lines of
 * `TaskWorkItemProvider`. So a returned task arrived in the inbox indistinguishable from one raised that
 * morning, and the sentence explaining why was written, stored, and never shown to the person it was for.
 *
 * IT IS A SIGNAL, NOT A STATE. The task really is `Open` — somebody has to do it. "It has been here before" is
 * ORIGIN, and this shell already sorts those apart: tab is ownership, segment is state, chip is type and signal.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CONTRACT = fs.readFileSync(
  web("wwwroot", "assets", "js", "WorkCenterNext", "fixture-contract.js"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");

const TASK_ID = "3c9a1f52-77aa-4c11-9b2e-51a0c4d8e001";

/*
 * The harness's DEFAULT `tf` throws its arguments away (`tf: (key) => key`), which would make every assertion
 * about the count or the quoted sentence pass for the wrong reason. This one carries them, so a sentence that
 * dropped a fact fails here — the same override `workcenter-next-sla-closed-freeze` uses and for the same reason.
 */
const KEY_AND_ARGS = {
  t: (key) => key,
  tf: (key, ...args) => `${key}(${args.join(",")})`,
  tn: (key) => key
};

const item = (overrides) => Object.assign({
  fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
  /*
   * ⚠ `pendingAcceptance`, and it is not a convenience: the return handler calls `ReopenAcceptanceGate()`
   * precisely so the work lands in the requester's INBOX rather than silently among their active work. An
   * `admitted` fixture would sit on the "islerim" tab and the list assertions would be measuring a state no
   * returned task is ever in.
   */
  admissionState: "pendingAcceptance", normalizedStatus: "Pending", taskLifecycle: "Open",
  executionState: "notStarted", timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Sapma raporunu yaz", locale: "und" },
  nativeStatus: { code: "Open", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Open" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution"],
  actions: [], concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null
}, overrides);

const returned = (count, reasonText, extra) => item(Object.assign({
  returned: {
    at: "2026-09-01T09:15:00+00:00",
    count,
    reason: reasonText ? { kind: "display", text: reasonText, locale: "und" } : undefined
  }
}, extra || {}));

const bootList = (projection) => bootSurface({ items: [projection], wcn: KEY_AND_ARGS });

/*
 * A CLOSED item lives on the "history" tab and the default tab is the inbox — so booting one and asserting the
 * chip is absent would pass because NOTHING rendered, which is the emptiest kind of green.
 *
 * MEASURED: with the terminal guard deliberately removed, the suppression test still passed until this helper
 * existed. It clicks through and then proves the ROW is really on screen before asking what is missing from it.
 */
const bootHistory = async (projection) => {
  await bootSurface({ items: [projection], wcn: KEY_AND_ARGS });
  app().querySelector('[data-wcn-tab="history"]').click();
  await new Promise((resolve) => { setTimeout(resolve, 0); });
};
const bootDetail = (projection) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [projection],
  wcn: KEY_AND_ARGS
});

/** The chips on the one row the list drew. */
const chips = () => [...app().querySelectorAll(".wcn-chip")];
const chipTexts = () => chips().map((c) => c.textContent.trim());
const returnChip = () => chips().find((c) => /Returned(Label|Count)/.test(c.textContent));

describe("(a) a task that never came back shows nothing", () => {
  it("draws no return chip on an ordinary row", async () => {
    /*
     * The overwhelming majority of rows. A chip that appears on every task is not a signal — and the projection
     * omits the block entirely rather than sending `count: 0`, so this is testing an absence the shell can
     * actually see.
     */
    await bootList(item({}));

    expect(returnChip(), "a return chip appeared on a task that was never returned").toBeUndefined();
    expect(chipTexts().join(" ")).not.toMatch(/Returned/);
  });

  it("draws no note on the detail page either", async () => {
    await bootDetail(item({}));

    expect(app().innerHTML).not.toMatch(/StepReturned/);
  });
});

describe("(b) a returned task says so, and says why", () => {
  it("draws the chip and carries the returner's own sentence as its tooltip", async () => {
    /*
     * The visible word is short and the TOOLTIP carries the sentence — the rule the blocked chip beside it
     * already follows ("the chip clips at its own max width and carries the full sentence as its tooltip"). The
     * reason is a DISPLAY label: what the returner typed, in the language they typed it in.
     */
    await bootList(returned(1, "Parti numarası yanlış"));

    const chip = returnChip();
    expect(chip, "the return chip is missing on a returned task").toBeTruthy();
    expect(chip.getAttribute("title")).toBe("Parti numarası yanlış");
    // Given a title, the chip becomes an image with the long sentence as its label — the shared chip's own rule.
    expect(chip.getAttribute("role")).toBe("img");
    expect(chip.getAttribute("aria-label")).toBe("Parti numarası yanlış");
  });

  it("summarises it in one sentence on the detail page", async () => {
    /*
     * A SUMMARY, not a second narrative: the activity feed already holds the `returned` event with its actor and
     * timestamp, and that row stays the record. This answers the question someone opening a returned task asks
     * first, without scrolling to find it.
     */
    await bootDetail(returned(1, "Parti numarası yanlış"));

    const note = app().querySelector(".wcn-step-paused");
    expect(note, "the detail page says nothing about the return").toBeTruthy();
    expect(note.textContent).toContain("StepReturnedBecause");
    expect(note.textContent).toContain("Parti numarası yanlış");
  });

  it("still says it came back when no sentence survived", async () => {
    // The handler requires a reason, so this is the row written before that rule or by something else. The fact
    // still travels; only the quotation is missing, and an empty quotation would read as though nobody spoke.
    await bootList(returned(1, null));

    expect(returnChip(), "the chip vanished when the sentence did").toBeTruthy();
  });
});

describe("(c) the count appears only once it means something", () => {
  it("shows no number on a single return", async () => {
    /*
     * "1" beside a chip that already means "this came back" says nothing the chip did not, and a number that is
     * almost always the same number stops being read.
     */
    await bootList(returned(1, "Bir kez"));

    expect(returnChip().textContent).toContain("ReturnedLabel");
    expect(returnChip().textContent).not.toContain("ReturnedCount");
  });

  it("shows the number from the second return onward", async () => {
    // From here the number IS the story — and it is the same count the rework rate (Faz 5) will be built from.
    await bootList(returned(3, "Üçüncü kez"));

    const text = returnChip().textContent;
    expect(text).toContain("ReturnedCount");
    expect(text).toContain("3");
  });

  it("says how many times in the detail sentence too", async () => {
    await bootDetail(returned(3, "Üçüncü kez"));

    const note = app().querySelector(".wcn-step-paused");
    expect(note.textContent).toContain("StepReturnedTimesBecause");
    expect(note.textContent).toContain("3");
    expect(note.textContent).toContain("Üçüncü kez");
  });
});

describe("the chip is triage, so finished work does not carry it", () => {
  it("hides the chip on a closed task while the projection still carries the fact", async () => {
    /*
     * ⚠ THE SPLIT, ASSERTED ON BOTH SIDES. There is nothing to triage on a task nobody has to pick up, so the
     * ROW stays quiet. The projection still states the fact (TaskReturnedSignalTests proves that half), and the
     * DETAIL page reads it — "this came back twice before it was finished" is part of what happened to the work.
     */
    await bootHistory(returned(2, "Kapanmadan önce geri gelmişti", {
      normalizedStatus: "Done", taskLifecycle: "Done", executionState: "notApplicable",
      closedAt: "2026-09-02"
    }));

    // NON-VACUITY FIRST: the row has to be on screen, or "no chip" says nothing at all.
    expect(chips().length, "the closed row never rendered — this assertion would pass for the wrong reason")
      .toBeGreaterThan(0);
    expect(returnChip(), "a triage chip appeared on finished work").toBeUndefined();
  });

  it("still tells the story on a closed task's detail page", async () => {
    await bootDetail(returned(2, "Kapanmadan önce geri gelmişti", {
      normalizedStatus: "Done", taskLifecycle: "Done", executionState: "notApplicable",
      closedAt: "2026-09-02"
    }));

    expect(app().querySelector(".wcn-step-paused").textContent).toContain("StepReturnedTimesBecause");
  });
});

describe("the signal is declared, not merely read", () => {
  it("has its shape stated in the contract", () => {
    /*
     * This file's own recurring lesson: a value that "existed in the provider, in the shell's icon map and in
     * its label map, and in none of them as a stated contract" drifts silently — and `taskContext` went further,
     * gating a card on a capability the vocabulary did not contain, so the card never drew at all.
     */
    expect(CONTRACT).toContain("RETURNED_AT_INVALID");
    expect(CONTRACT).toContain("RETURNED_COUNT_INVALID");
    expect(CONTRACT).toContain("RETURNED_REASON_INVALID");
  });

  it("did NOT add a lifecycle value — the task is still Open", () => {
    /*
     * ⚠ THE SCOPE GUARD. A `Returned` lifecycle member would cost a persisted backend enum, this contract's own
     * list, every switch over either and every provider that maps a native status into ours — for a fact about
     * where the work came from. If that is ever reconsidered, it gets argued here rather than slipped in.
     */
    const lifecycles = /const TASK_LIFECYCLES = \[([^\]]*)\]/.exec(CONTRACT);
    expect(lifecycles).toBeTruthy();
    expect(lifecycles[1]).not.toContain("Returned");
  });

  it("reads the signal through one helper rather than by hand at each site", () => {
    // `reasonKey` and `viewerRole` are what happens when a field is touched in four places by hand: three of
    // them are right. The guard is the helper, and the chip and the note both go through it.
    expect(APP).toContain("const returnedSignal =");

    // Every read of the raw field must sit INSIDE the helper (or in the comment that explains why). A fourth
    // occurrence means a call site started reaching past it.
    const helper = /const returnedSignal = [^;]*;/.exec(APP);
    expect(helper).toBeTruthy();
    const outside = APP.replace(helper[0], "").split("\n")
      .filter((line) => line.includes("item.returned") && !line.trim().startsWith("*"));
    expect(outside, `item.returned is read outside returnedSignal: ${outside.join(" | ")}`).toHaveLength(0);
  });

  it("styles through classes only, and reuses the note class rather than cloning it", () => {
    // FG-003. The detail note is already "a note under the step bar"; a second class with the same rules is how
    // the two drift apart.
    const note = APP.slice(APP.indexOf("const returnedNote ="), APP.indexOf("const returnedNote =") + 700);
    expect(note).toContain("wcn-step-paused");
    expect(note).not.toMatch(/style="/);
  });
});

describe("seven languages, or it is not localized", () => {
  const KEYS = [
    "ReturnedLabel", "ReturnedCount",
    "StepReturned", "StepReturnedTimes", "StepReturnedBecause", "StepReturnedTimesBecause"
  ];

  it.each(LANGS)("%s defines every key this slice added", (lang) => {
    const source = resx(lang);
    KEYS.forEach((key) => {
      expect(source.includes(`name="${key}"`), `${key} missing in ${lang}`).toBe(true);
    });
  });

  it("keeps the placeholders the code passes — a dropped {0} loses the count or the sentence", () => {
    /*
     * `tf` substitutes by position, so a translation that drops a slot silently prints a sentence missing its
     * fact. Measured per language rather than trusted: this is exactly the shape the harness's own tf-seed
     * comment says was found by `WaitingOnWithReason`.
     */
    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1] : "";
    };

    LANGS.forEach((lang) => {
      expect(value(lang, "ReturnedCount"), `ReturnedCount/${lang}`).toContain("{0}");
      expect(value(lang, "StepReturnedTimes"), `StepReturnedTimes/${lang}`).toContain("{0}");
      expect(value(lang, "StepReturnedBecause"), `StepReturnedBecause/${lang}`).toContain("{0}");
      // Two facts, two slots: how many times, and the most recent sentence.
      expect(value(lang, "StepReturnedTimesBecause"), `StepReturnedTimesBecause/${lang}`).toContain("{0}");
      expect(value(lang, "StepReturnedTimesBecause"), `StepReturnedTimesBecause/${lang}`).toContain("{1}");
    });
  });

  it("translates rather than leaving English in every language", () => {
    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1].trim() : null;
    };

    ["ReturnedLabel", "StepReturned"].forEach((key) => {
      const english = value("en", key);
      expect(english).toBeTruthy();
      LANGS.filter((l) => l !== "en").forEach((lang) => {
        expect(value(lang, key), `${key}/${lang} is still the English text`).not.toBe(english);
      });
    });
  });
});
