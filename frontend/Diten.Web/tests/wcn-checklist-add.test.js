const { bootSurface, app } = require("./wcn-boot");

/*
 * Adding a checklist item from the task's DETAIL page.
 *
 * The page could tick items and could not create one. AddChecklistItemCommand had been on the server the whole
 * time, and `TasksApi.addChecklistItem` was already written and called from nowhere — the wire was complete and
 * had no control at either end of it.
 *
 * The case that matters most is the EMPTY one: a task created without items has to be able to grow its first,
 * which is why the provider now ships the checklist container for every task rather than only for tasks that
 * already have a run.
 */
const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

const item = (overrides) => Object.assign({
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
  title: { kind: "display", text: "Ay sonu kapanış", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "checklist"],
  // What the provider now ships for a task with NO run: an empty list at version 0.
  checklist: { items: [], version: 0 },
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null
}, overrides);

const boot = (overrides) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item(overrides)]
});

const tick = () => new Promise((resolve) => { setTimeout(resolve, 0); });

const input = () => app().querySelector("[data-wcn-check-input]");

describe("a task with an empty checklist can still grow one", () => {
  it("offers the add row when the list is empty", async () => {
    /*
     * The trap this replaces: while the capability followed the DATA, a task with no run declared nothing, the
     * card was never drawn, and the only place to add a first item was a task that already had one.
     */
    await boot();

    expect(input()).not.toBeNull();
    // …and no "there is nothing here" sentence above a box for putting something there.
    expect(app().textContent).not.toContain("ChecklistEmpty");
  });

  it("offers it on a list that already has items too", async () => {
    await boot({
      checklist: {
        items: [{ id: "c1", label: { kind: "display", text: "Fatura eki" }, completed: false, required: true, blocking: true }],
        version: 3
      }
    });

    expect(input()).not.toBeNull();
  });

  it("uses the SAME add row shape the subtask card uses", async () => {
    // A third add pattern in one product is how a product starts reading as three.
    await boot();

    const row = app().querySelector(".wcn-subtask-add");
    expect(row).not.toBeNull();
    expect(row.querySelector(".wcn-search-inline")).not.toBeNull();
  });

  it("hides the add row on a closed task, whose checklist is history", async () => {
    await boot({
      normalizedStatus: "Done",
      taskLifecycle: "Done",
      executionState: "notApplicable",
      actions: []
    });

    expect(input()).toBeNull();
    // …and then the empty state gets its sentence back, because there is nothing else to say.
    expect(app().textContent).toContain("ChecklistEmpty");
  });
});

describe("the level is part of the add, not an afterthought", () => {
  it("starts at Optional", async () => {
    // Same reason the create form does: a Blocking default manufactures tasks nobody can close and nobody
    // chose that.
    await boot();

    const chip = app().querySelector("[data-wcn-check-level]");
    expect(chip.getAttribute("data-level")).toBe("Optional");
    expect(chip.textContent).toContain("ChecklistLevelOptional");
  });

  it("cycles weakest-first and sticks between adds", async () => {
    await boot();

    app().querySelector("[data-wcn-check-level]").click();
    await tick();
    expect(app().querySelector("[data-wcn-check-level]").getAttribute("data-level")).toBe("Required");

    app().querySelector("[data-wcn-check-level]").click();
    await tick();
    expect(app().querySelector("[data-wcn-check-level]").getAttribute("data-level")).toBe("Blocking");

    app().querySelector("[data-wcn-check-level]").click();
    await tick();
    expect(app().querySelector("[data-wcn-check-level]").getAttribute("data-level")).toBe("Optional");
  });
});

/*
 * ⚠ WHY THESE ASSERT DELTAS AND NOT COUNTS.
 *
 * app.js binds its keydown handler to `document`, and jsdom keeps one document per test FILE. Every boot loads
 * app.js again, so a listener from an earlier test in this file is still attached — and because the handler
 * reads `global.TasksApi` at call time, those stale listeners write into the CURRENT stub. One keypress
 * therefore records one entry per boot so far.
 *
 * That is a property of the shared harness, not of the code under test, and it is not this round's to fix: the
 * cure is a teardown seam in app.js that every existing surface test would have to be re-proved against. What
 * matters here — the exact body that goes on the wire, and that a blank press sends nothing — is measured by
 * comparing before and after, which is immune to it. Stated rather than left as a puzzle for the next reader.
 */
describe("the add writes what the row says", () => {
  const press = async (value) => {
    const box = input();
    box.value = value;
    box.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
    await tick();
  };

  it("posts the text, the level and version 0 for a task with no run", async () => {
    /*
     * VERSION 0 IS A REAL VALUE. AddChecklistItemHandler reads it as "no run exists, start one"; sending 1
     * would claim a document that is not there and turn a first item into a phantom concurrency conflict for
     * the only person on the page.
     */
    const { checklistAdds } = await boot();
    const before = checklistAdds.length;

    await press("Fatura eki yüklendi");

    expect(checklistAdds.length).toBeGreaterThan(before);
    expect(checklistAdds.at(-1).taskId).toBe(TASK_ID);
    expect(checklistAdds.at(-1).body).toEqual({
      text: "Fatura eki yüklendi",
      requirement: "Optional",
      expectedVersion: 0
    });
  });

  it("carries the chosen level", async () => {
    const { checklistAdds } = await boot();

    app().querySelector("[data-wcn-check-level]").click();
    await tick();
    app().querySelector("[data-wcn-check-level]").click();
    await tick();
    await press("Fatura eki yüklendi");

    expect(checklistAdds.at(-1).body.requirement).toBe("Blocking");
  });

  it("sends the RUN's version when one already exists", async () => {
    const { checklistAdds } = await boot({
      checklist: {
        items: [{ id: "c1", label: { kind: "display", text: "ilk madde" }, completed: false, required: false, blocking: false }],
        version: 7
      }
    });

    await press("ikinci madde");

    expect(checklistAdds.at(-1).body.expectedVersion).toBe(7);
  });

  it("says nothing and posts nothing for an empty box", async () => {
    // The placeholder already says what the box is for; a toast for pressing Enter in an empty field is noise.
    const { checklistAdds } = await boot();
    const before = checklistAdds.length;

    await press("   ");

    expect(checklistAdds.length).toBe(before);
  });
});

/*
 * ITEM 12 — "Required" was stored and did nothing.
 *
 * Three levels shipped and two behaved. Blocking refused completion (verified live, 409 CHECKLIST_INCOMPLETE),
 * Optional was meant to do nothing, and Required was indistinguishable from Optional ANYWHERE on screen: no
 * counter, no notice, no word at the moment of closing. A user chose it and the system ignored it.
 *
 * The claim these tests lock is exactly the one that was false: "the difference between Required and Optional
 * is visible". Both signals are asserted, and so is the thing that must NOT change — Required still does not
 * block.
 */
const checklistOf = (entries) => ({
  items: entries.map((e, i) => ({
    id: `c${i}`,
    label: { kind: "display", text: e.text },
    completed: !!e.done,
    required: e.level !== "Optional",
    blocking: e.level === "Blocking",
    evidenceRequired: !!e.evidence
  })),
  version: 1
});

describe("a Required item has a visible consequence, and still does not block", () => {
  it("names how many required items are open", async () => {
    await boot({
      checklist: checklistOf([
        { text: "Muhasebe onayi", level: "Required" },
        { text: "Arsiv kopyasi", level: "Required" },
        { text: "Serbest not", level: "Optional" }
      ])
    });

    const notice = app().querySelector(".wcn-check-required");
    expect(notice).not.toBeNull();
    expect(notice.textContent).toContain("ChecklistRequiredOpen");
  });

  it("says NOTHING when the only open items are optional", async () => {
    // The other direction. A counter that appeared for optional items would make the two levels
    // indistinguishable again, from the opposite side.
    await boot({
      checklist: checklistOf([
        { text: "Serbest not", level: "Optional" },
        { text: "Muhasebe onayi", level: "Required", done: true }
      ])
    });

    expect(app().querySelector(".wcn-check-required")).toBeNull();
  });

  it("does not double-count a Blocking item, which is 'required' on the wire too", async () => {
    /*
     * ToChecklist derives `required` as "not Optional", so a Blocking item arrives with BOTH flags. Counting
     * naively would report the same item in two sentences at once — one saying the task cannot close, the
     * other saying it can.
     */
    await boot({ checklist: checklistOf([{ text: "Fatura eki", level: "Blocking" }]) });

    expect(app().querySelector(".wcn-check-required")).toBeNull();
    // …while the blocking notice DOES speak for it.
    expect(app().textContent).toContain("WorkAggregation_ActionDisabled_ChecklistIncomplete");
  });

  it("REQUIRED DOES NOT BLOCK — the difference the level exists for", async () => {
    // The half that must not regress. `complete` stays offered and enabled with required items open; only
    // Blocking takes it away.
    await boot({
      checklist: checklistOf([{ text: "Muhasebe onayi", level: "Required" }]),
      actions: [{
        code: "complete",
        label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
        semanticType: "complete",
        enabled: true,
        source: "provider",
        disabledReasonCode: null,
        disabledReason: null,
        requiresConfirmation: true,
        requiresReason: false,
        requiresEvidence: false,
        supportsBulk: false,
        riskLevel: "normal"
      }]
    });

    const complete = app().querySelector('[data-wcn-action="complete"]');
    expect(complete).not.toBeNull();
    expect(complete.disabled).toBe(false);
  });
});

describe("the paperclip says what it is and when it will bite", () => {
  it("marks the flagged row and explains the mark once, under the list", async () => {
    /*
     * EvidenceRequired has been on the model since Phase 1 with nothing enforcing it: MOD-0024 has no
     * task↔document link at all (pack §12 Y4 keeps attachments out), and the document module is live but
     * unconnected. The flag is still stored — nothing a user recorded is lost — but a control that implies
     * "this cannot be ticked without evidence" and then lets it be ticked is worse than no control.
     */
    await boot({
      checklist: checklistOf([
        { text: "Fatura eki", level: "Blocking", evidence: true },
        { text: "Serbest not", level: "Optional" }
      ])
    });

    expect(app().querySelectorAll(".wcn-check-evidence")).toHaveLength(1);
    const hint = app().querySelector(".wcn-check-evidence-hint");
    expect(hint).not.toBeNull();
    expect(hint.textContent).toContain("ChecklistEvidenceHint");
  });

  it("stays quiet when nothing on the list asks for evidence", async () => {
    await boot({ checklist: checklistOf([{ text: "Serbest not", level: "Optional" }]) });

    expect(app().querySelector(".wcn-check-evidence")).toBeNull();
    expect(app().querySelector(".wcn-check-evidence-hint")).toBeNull();
  });

  it("is a statement, not a control — it takes no tab stop and no click", async () => {
    // The mark must not look like a way to attach something, because there is nothing to attach to yet.
    await boot({ checklist: checklistOf([{ text: "Fatura eki", level: "Required", evidence: true }]) });

    const mark = app().querySelector(".wcn-check-evidence");
    expect(mark.tagName).toBe("I");
    expect(mark.closest("button")).toBeNull();
  });
});

/*
 * C2 / C3 on the DETAIL page — the same two rules the create form now follows, so the two surfaces do not
 * drift into different dialects of the same product.
 */
describe("the detail card speaks the same visual language as the form", () => {
  it("renders the evidence notice as an alert, not a hint line", async () => {
    await boot({ checklist: checklistOf([{ text: "Fatura eki", level: "Required", evidence: true }]) });

    const notice = app().querySelector(".wcn-check-evidence-hint");
    expect(notice).not.toBeNull();
    expect(notice.classList.contains("alert")).toBe(true);
    expect(notice.classList.contains("dt-inline-alert")).toBe(true);
    // Wording and condition are unchanged — only the presentation moved.
    expect(notice.textContent).toContain("ChecklistEvidenceHint");
  });

  it("gives every card heading a glyph", async () => {
    /*
     * Derived from what actually rendered, not from a list of card names: every heading on the page must carry
     * one, so a card added later without an icon fails here rather than shipping as the odd one out.
     */
    await boot({ checklist: checklistOf([{ text: "Fatura eki", level: "Required" }]) });

    const headings = [...app().querySelectorAll("h6.text-heading")];
    expect(headings.length).toBeGreaterThan(3);

    headings.forEach((h6) => {
      expect(h6.querySelector("i.bx"), `no icon on: ${h6.textContent.trim().slice(0, 40)}`).not.toBeNull();
    });
  });

  it("keeps the checklist's own count beside its title", async () => {
    // Adopting the shared heading helper must not have dropped what the heading already showed.
    await boot({
      checklist: checklistOf([
        { text: "bir", level: "Required", done: true },
        { text: "iki", level: "Optional" }
      ])
    });

    const heading = [...app().querySelectorAll("h6.text-heading")]
      .find((h) => h.textContent.includes("ChecklistLabel"));
    expect(heading.querySelector(".wcn-count-inline").textContent).toBe("1/2");
  });
});
