const { bootSurface, app } = require("./wcn-boot");

/*
 * WC-1, layer three — the SCREEN half of the lifecycle event log.
 *
 * The feed now carries two kinds. A comment is a person speaking and can be replied to; an event is a state that
 * changed and cannot. They shared one row shape, at one weight, which made the conversation disappear into the
 * log the moment a task had any history at all. These tests are about the difference being STRUCTURAL — a
 * different shape on the page — rather than a different class name on the same box.
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
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "activity"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  activity: []
}, overrides);

const boot = (overrides) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item(overrides)]
});

const event = (code, index, extra) => Object.assign({
  id: `E${index}`,
  kind: "event",
  actor: "Diten Admin",
  at: `2026-08-0${(index % 9) + 1}T09:10:00+00:00`,
  event: { code, from: "Open", to: "Planned" }
}, extra);

const comment = (text, index) => ({
  id: `C${index}`,
  kind: "comment",
  text,
  actor: "Deniz Koç",
  at: "2026-08-05T09:10:00+00:00"
});

/** The click handler defers through a promise (app.js wraps onClick), so a render is one tick away. */
const tick = () => new Promise((resolve) => { setTimeout(resolve, 0); });

/** A run of events, so a threshold test states the count once. */
const manyEvents = (count) => Array.from({ length: count }, (_, i) => event("planned", i));

describe("a comment and an event do not look like the same kind of thing", () => {
  it("gives a comment a face and a name, and an event neither", async () => {
    await boot({ activity: [comment("Tedarikçiyi tekrar aradım", 1), event("planned", 2)] });

    const commentRow = app().querySelector(".wcn-audit-comment");
    const eventRow = app().querySelector(".wcn-audit-event");
    expect(commentRow).not.toBeNull();
    expect(eventRow).not.toBeNull();

    // STRUCTURE, not styling: the assertion is about which elements exist inside each row, so a future
    // restyle that merges the two back into one shape fails here rather than passing on a class name.
    expect(commentRow.querySelector(".wcn-audit-avatar")).not.toBeNull();
    expect(commentRow.querySelector(".wcn-audit-author").textContent).toContain("Deniz Koç");
    expect(commentRow.querySelector(".wcn-audit-text").textContent).toContain("Tedarikçiyi");

    expect(eventRow.querySelector(".wcn-audit-avatar")).toBeNull();
    expect(eventRow.querySelector(".wcn-audit-author")).toBeNull();
    // One line, carrying what happened, who and when — not a stacked body like the comment's.
    expect(eventRow.querySelector(".wcn-audit-body")).toBeNull();
    expect(eventRow.querySelector(".wcn-audit-line")).not.toBeNull();
    expect(eventRow.querySelector(".wcn-audit-arrow")).not.toBeNull();
  });

  it("builds the event's sentence from its code, in the reader's language", async () => {
    await boot({ activity: [event("released", 1)] });

    const line = app().querySelector(".wcn-audit-line").textContent;
    // The harness's translator answers with the key it was given, so the KEY is what appears here. That is
    // exactly the assertion worth making: the code was mapped to a resource key rather than printed raw, and
    // the actor and the time joined it on one line.
    expect(line).toContain("AuditEventReleased");
    expect(line).not.toContain("released");
    expect(line).toContain("Diten Admin");
    expect(line).toMatch(/Time(Today|Yesterday|DaysAgo)/);
  });

  it("shows the actor's own words when the act carried a reason", async () => {
    await boot({
      activity: [event("returned", 1, { event: { code: "returned", from: "Open", to: "Open", reason: "Bu finans ekibinin işi" } })]
    });

    expect(app().querySelector(".wcn-audit-reason").textContent).toContain("Bu finans ekibinin işi");
  });

  it("falls back to a generic line rather than printing a raw key for a code it does not know", async () => {
    // A server that ships a new transition before the shell has its string must not put `somethingNew` on
    // screen — a raw key reaching a user is a defect this codebase has shipped before.
    await boot({ activity: [event("somethingNobodyTranslatedYet", 1)] });

    const line = app().querySelector(".wcn-audit-line").textContent;
    expect(line).toContain("AuditEventUnknown");
    expect(line).not.toContain("somethingNobodyTranslatedYet");
  });
});

describe("the filter appears only once the events earn it", () => {
  it("is absent for an ordinary task's history", async () => {
    // A task that goes straight through records six entries and one that waits records eight. A filter here
    // would be permanent chrome on every finished task.
    await boot({ activity: [...manyEvents(7), comment("Bir not", 99)] });

    expect(app().querySelector(".wcn-audit-filter")).toBeNull();
  });

  it("appears once the machine log genuinely outnumbers the conversation", async () => {
    await boot({ activity: [...manyEvents(12), comment("Bir not", 99)] });

    const chips = app().querySelectorAll("[data-wcn-activity-filter]");
    expect(chips).toHaveLength(2);
    expect(app().querySelector(".wcn-audit-filter").textContent).toContain("ActivityFilterCommentsOnly");
    // Chips, never tabs: a tab would claim two lists with two owners, and the axis law reserves tabs for
    // ownership. This is one story told by two kinds of entry.
    expect(app().querySelector(".wcn-audit-filter").getAttribute("role")).toBe("group");
  });

  it("hides the events when the reader asks for comments only, and brings them back", async () => {
    await boot({ activity: [...manyEvents(12), comment("Bir not", 99)] });

    expect(app().querySelectorAll(".wcn-audit-event").length).toBeGreaterThan(0);

    app().querySelector('[data-wcn-activity-filter="comments"]').click();
    await tick();
    expect(app().querySelectorAll(".wcn-audit-event")).toHaveLength(0);
    expect(app().querySelectorAll(".wcn-audit-comment")).toHaveLength(1);

    // Reversible in place — a filter you cannot undo is a trap.
    app().querySelector('[data-wcn-activity-filter="all"]').click();
    await tick();
    expect(app().querySelectorAll(".wcn-audit-event").length).toBeGreaterThan(0);
  });

  it("says so when the filter leaves nothing, rather than showing an empty list", async () => {
    await boot({ activity: manyEvents(12) });

    app().querySelector('[data-wcn-activity-filter="comments"]').click();
    await tick();
    expect(app().textContent).toContain("ActivityNoComments");
  });
});

describe("a task older than the log says so instead of showing a hole", () => {
  it("tells the reader where the record starts when there is no created event", async () => {
    /*
     * THE BACKFILL DECISION on screen. Every task written from WC-1 onwards opens its history with `created`,
     * so a feed without one is a task whose earlier steps were never recorded. Nothing is reconstructed — the
     * reader is simply told, in one quiet line at the foot of the feed where the history runs out.
     */
    await boot({ activity: [event("completed", 1)] });

    const gap = app().querySelector(".wcn-audit-gap");
    expect(gap).not.toBeNull();
    expect(gap.textContent).toContain("ActivityHistoryStartsHere");
  });

  it("says nothing of the sort when the history is complete", async () => {
    await boot({ activity: [event("completed", 2), event("created", 1)] });

    expect(app().querySelector(".wcn-audit-gap")).toBeNull();
  });

  it("says it on a feed of comments alone, because that task predates the log too", async () => {
    // Every task written from WC-1 onwards opens with `created`, so a comments-only feed is not "a task where
    // nothing happened" — it is a task whose acts were never written down.
    await boot({ activity: [comment("Sadece konuşma", 1)] });

    expect(app().querySelector(".wcn-audit-gap")).not.toBeNull();
  });

  it("says it on an EMPTY feed, instead of reporting an unrecorded past as an empty one", async () => {
    /*
     * The case the first cut of this got wrong. A pre-WC-1 task usually has no entries at all, and the card
     * answered "Henüz etkinlik kaydı yok" — true about the RECORD, false about the task. That is the
     * partial-history trap in its zero case, on the very surface this feature exists to fix.
     */
    await boot({ activity: [] });

    expect(app().querySelector(".wcn-audit-gap")).not.toBeNull();
    expect(app().querySelector(".wcn-block-hint")).toBeNull();
  });

  /*
   * NOT COVERED HERE: the showcase surface, where the catalogue writes `eventKey` and never `event.code` and
   * every fixture would otherwise claim its history had been cut short. The rule gates on provenance 'api', and
   * this harness feeds everything through the API seam — so it cannot build a fixture-provenance item, and a
   * test that pretended to would be asserting the harness rather than the guard. It is verified on the live
   * Development showcase instead; said plainly here rather than left as a hole in the file.
   */
});

/*
 * D1–D5 — the activity card brought up to the maturity of the cards beside it.
 *
 * ⚠ WHAT THESE TESTS CAN AND CANNOT SEE. They boot the REAL app.js against jsdom and assert the DOM it actually
 * produced — no double stands in for the module under test. But jsdom applies no external stylesheet, so the
 * ::before/::after geometry D4 is made of (dot size, line height, the crop) is simply not observable here, and a
 * test that claimed to check it would be checking nothing. D4's geometry was measured on the running page in
 * both themes and both widths instead; what is asserted below is the part that lives in the markup.
 */
describe("the activity card matches the cards beside it", () => {
  const many = (events, comments) => [
    ...Array.from({ length: events }, (_, i) => event("planned", i)),
    ...Array.from({ length: comments }, (_, i) => comment(`yorum ${i}`, 100 + i))
  ];

  it("D1 — the history-gap notice is an alert, not a paragraph", async () => {
    // It was an 11px grey <p>, 15px tall, and it disappeared into the list it exists to qualify. The shell is
    // the surface's existing neutral in-card alert; no new tone was invented.
    await boot({ activity: [event("completed", 1)] });

    const gap = app().querySelector(".wcn-audit-gap");
    expect(gap.tagName).toBe("DIV");
    expect(gap.classList.contains("alert")).toBe(true);
    expect(gap.classList.contains("alert-secondary")).toBe(true);
    expect(gap.classList.contains("dt-inline-alert")).toBe(true);
    expect(gap.getAttribute("role")).toBe("note");
    expect(gap.querySelector("i.bx-info-circle")).not.toBeNull();
  });

  it("D1 — it sits BELOW the composer and ABOVE the list", async () => {
    /*
     * Order is the point, not presence. It used to sit at the FOOT, under a 320px scroll cap, where a reader
     * could finish the list without ever meeting the sentence that qualifies it. Asserted by document position
     * so it cannot pass on a coincidence of markup.
     */
    await boot({ activity: [event("completed", 1)] });

    const composer = app().querySelector(".wcn-composer");
    const gap = app().querySelector(".wcn-audit-gap");
    const list = app().querySelector(".wcn-audit");

    // Node.DOCUMENT_POSITION_FOLLOWING === 4
    expect(composer.compareDocumentPosition(gap) & 4).toBeTruthy();
    expect(gap.compareDocumentPosition(list) & 4).toBeTruthy();
  });

  it("D2 — the comment box uses the create form's field wrapper and glyph", async () => {
    /*
     * Every field on the create form is 38px inside `.diten-field` with an inset icon; this box was a 30px
     * `form-control-sm` with nothing. The wrapper is REUSED from the shared stylesheet rather than redeclared —
     * a second wrapper class here would have been the fork this round closes.
     */
    await boot({ activity: [] });

    const input = app().querySelector("[data-wcn-comment-input]");
    expect(input.tagName).toBe("INPUT");                       // still an input: Enter keeps its meaning
    expect(input.classList.contains("form-control")).toBe(true);
    expect(input.classList.contains("form-control-sm")).toBe(false);

    const wrapper = input.parentElement;
    expect(wrapper.classList.contains("diten-field")).toBe(true);
    expect(wrapper.querySelector("i.bx-message-rounded")).not.toBeNull();
  });

  it("D3 — the send button carries an icon, like every other action on the page", async () => {
    // Measured first: all five `data-wcn-action` buttons carry one. Adding it here is consistency; adding it
    // where the others had none would have been a fresh inconsistency.
    await boot({ activity: [] });

    const btn = app().querySelector("[data-wcn-comment-post]");
    expect(btn.querySelector("i.bx-send")).not.toBeNull();
    // `btn-sm` was what pinned it to 30px; the row's height now comes from the field beside it.
    expect(btn.classList.contains("btn-sm")).toBe(false);
  });

  it("D5 — the count badge copies the subtasks card exactly", async () => {
    await boot({ activity: many(2, 1) });

    const badge = app().querySelector(".wcn-audit-count");
    expect(badge.tagName).toBe("SPAN");
    // The same two classes the subtasks badge uses. Two cards side by side must not carry two badge styles.
    expect(badge.classList.contains("badge")).toBe(true);
    expect(badge.classList.contains("bg-label-secondary")).toBe(true);
    expect(badge.textContent).toBe("3");
    // …and it lives in the heading, not beside the list.
    expect(badge.closest("h6")).not.toBeNull();
  });

  it("D5 — the badge shows the TOTAL and does not move when the filter is applied", async () => {
    /*
     * THE CASE THE LIVE PAGE COULD NOT PROVE. The comments-only chip needs 12+ events to appear, and neither
     * real task has that many (8 and 6), so the invariance could only be measured here. The badge names the
     * CARD; a number that dropped on filtering would be reporting the view instead of the task.
     */
    await boot({ activity: many(12, 2) });

    expect(app().querySelector(".wcn-audit-count").textContent).toBe("14");

    app().querySelector('[data-wcn-activity-filter="comments"]').click();
    await tick();

    // Only two rows are rendered now — and the badge still says fourteen.
    expect(app().querySelectorAll(".wcn-audit-item")).toHaveLength(2);
    expect(app().querySelector(".wcn-audit-count").textContent).toBe("14");
  });

  it("D4 — the two kinds keep the distinct classes the dot styling hangs off", async () => {
    // The geometry is CSS and unobservable in jsdom (see the note above); what IS assertable is that the two
    // hooks the stylesheet distinguishes are still emitted, so a rename here fails loudly rather than silently
    // flattening the timeline into one marker.
    await boot({ activity: [event("planned", 1), comment("bir yorum", 2)] });

    expect(app().querySelectorAll(".wcn-audit-event")).toHaveLength(1);
    expect(app().querySelectorAll(".wcn-audit-comment")).toHaveLength(1);
  });
});
