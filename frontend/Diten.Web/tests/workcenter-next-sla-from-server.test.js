const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WC-2 — the SLA state belongs to the SERVER, and the browser stopped deciding it.
 *
 * THE DEFECT. `mock-data.js computeSla()` decided overdue / due-soon / on-track for EVERY item, real work
 * included: calendar-day subtraction against a hard-coded `<= 2`, no notion of working time, no holidays. That
 * inverted the surface's own law — "the browser never derives eligibility from lifecycle, permission, blockers or
 * system state" — and it left the working calendar (BL: Calendar) with nothing on the server to arrive at, which
 * was the entire point of WC-2.
 *
 * ORDERING (BL-031's lesson, applied deliberately): the provider filled the field FIRST, the client then read it,
 * and only then did the contract learn the vocabulary. The contract rule validates `slaState` when PRESENT and
 * never requires it — because validateItems DROPS what it cannot validate, so a required field is not a nudge to
 * a silent provider, it is a delete. That is asserted below rather than assumed.
 */
const SCRIPT_ROOT = "wwwroot/assets/js/WorkCenterNext/";

const PINNED_NOW = () => new Date(2026, 6, 26, 10, 0, 0); // 2026-07-26

const realItem = (overrides) => Object.assign({
  fixtureKind: "workItem",
  id: "3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234",
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
  title: { kind: "display", text: "Gerçek iş", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: "3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234",
    deepLink: "/Tasks/3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234"
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  // Deliberately close enough that the OLD client rule would have said "due-soon".
  dueAt: "2026-07-27T00:00:00+00:00"
}, overrides);

const loadRealMode = () => {
  ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
    .forEach((key) => { delete global[key]; });
  global.WCN = { t: (key) => key, tf: (key) => key, tn: (key) => key };
  document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
  loadScript(SCRIPT_ROOT + "fixture-contract.js");
  loadScript(SCRIPT_ROOT + "mock-data.js");
  loadScript(SCRIPT_ROOT + "work-items-api.js");
  global.WorkCenterNextData.setNowProvider(PINNED_NOW);
};

const mapOne = (overrides) => global.WorkCenterNextApi.mapPayload([realItem(overrides)]).items[0];

describe("WC-2: the SLA state comes from the projection", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("takes the server's answer verbatim", () => {
    expect(mapOne({ slaState: "overdue" }).slaState).toBe("overdue");
    expect(mapOne({ slaState: "on-track" }).slaState).toBe("on-track");
    expect(mapOne({ slaState: "no-sla" }).slaState).toBe("no-sla");
  });

  it("does NOT re-derive it, even when its own maths would disagree", () => {
    /*
     * The heart of the slice. This deadline is one day away against a pinned clock, so the browser's old rule
     * would have called it due-soon with total confidence. The server says on-track — perhaps tomorrow is a
     * public holiday, which is exactly the kind of thing only the server can know — and the server wins.
     */
    expect(mapOne({ slaState: "on-track" }).slaState).toBe("on-track");
    expect(mapOne({ slaState: "on-track" }).slaState).not.toBe("due-soon");
  });

  it("says no-sla for a real item whose provider stayed silent", () => {
    /*
     * The honest answer: "this provider does not track deadlines". Falling back to the browser's own maths here
     * is what the old code did, and it is the one thing this slice must not leave a door open for.
     */
    expect(mapOne().slaState).toBe("no-sla");
  });

  it("keeps the DAY COUNT derived late, from the absolute due date", () => {
    /*
     * The count is the WORDING ("2 gün kaldı"), never the decision. It is derived at render time from the
     * absolute date the projection already carries, because a count computed on the SERVER freezes the moment it
     * is serialized — this project shipped exactly that once (`ago`) and banned it.
     */
    const item = mapOne({ slaState: "overdue", dueAt: "2026-07-22T00:00:00+00:00" });

    expect(item.slaDiffDays).toBe(-4);
    // …and it did not touch the state.
    expect(item.slaState).toBe("overdue");
  });

  it("never ships a remaining-day count on the wire", () => {
    // The banned shape, pinned. A provider adding `slaDiffDays` to the projection would be re-introducing `ago`.
    const item = mapOne({ slaState: "on-track", slaDiffDays: 99 });

    // Whatever the wire said, the count on screen is the one derived here from dueAt.
    expect(item.slaDiffDays).toBe(1);
    expect(item.slaDiffDays).not.toBe(99);
  });

  it("leaves the showcase catalogue deriving its own, against its authored day", () => {
    /*
     * Non-vacuity, and a real requirement: the showcase has no server behind it, so its fixtures must keep
     * reading correctly against the day they were authored for. If the client had simply stopped computing, the
     * whole demo catalogue would have gone flat.
     */
    const fixture = global.WorkCenterNextData.toPresentation(
      realItem({ dueAt: "2026-07-20" }), { provenance: "fixture" });

    expect(fixture.slaState).toBe("overdue");
  });
});

describe("WC-2: the contract knows the vocabulary, and drops nothing", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("declares the four states", () => {
    expect(global.WorkCenterNextContract.enums.SLA_STATES)
      .toEqual(["overdue", "due-soon", "on-track", "no-sla"]);
  });

  it("rejects a state nobody declared", () => {
    // Present-but-unknown is the state the shell renders as an uncoloured chip under a blank heading.
    const { errors } = global.WorkCenterNextApi.mapPayload([realItem({ slaState: "urgent-ish" })]);

    expect(errors.some((e) => /SLA_STATE_INVALID/.test(JSON.stringify(e)))).toBe(true);
  });

  it("KEEPS an item whose provider sent no slaState at all", () => {
    /*
     * BL-038's lesson, as an assertion. validateItems DROPS what fails, so making this field required would not
     * have nudged a silent provider — it would have deleted its work from the surface. A provider that does not
     * track deadlines must still be able to show its items.
     */
    const { items, errors } = global.WorkCenterNextApi.mapPayload([realItem()]);

    expect(items).toHaveLength(1);
    expect(errors).toEqual([]);
  });

  it("KEEPS an item that explicitly sends null", () => {
    const { items, errors } = global.WorkCenterNextApi.mapPayload([realItem({ slaState: null })]);

    expect(items).toHaveLength(1);
    expect(errors).toEqual([]);
  });
});

describe("WC-2: the list surface renders the server's answer", () => {
  /*
   * An INBOX item: that is the tab the surface opens on, so a row built any other way renders on a tab nobody
   * is looking at and every assertion below would pass against an empty list.
   */
  const listItem = (n, overrides) => realItem(Object.assign({
    id: `98d1f94e-1848-4539-8a99-77e72651b8a${n}`,
    title: { kind: "display", text: `Görev ${n}`, locale: "und" },
    admissionState: "pendingAcceptance",
    assignmentMode: "offered",
    ownershipState: "assigned",
    normalizedStatus: "Pending",
    taskLifecycle: "Open",
    executionState: "notStarted"
  }, overrides));

  it("groups and orders rows by the state the projection stated", async () => {
    /*
     * The badge and the ordering are what a reader actually acts on, and both key off slaState — so if the
     * client ever went back to deriving, this is where it would show. Overdue leads regardless of input order.
     */
    await bootSurface({
      items: [
        listItem(1, { slaState: "on-track", dueAt: "2026-09-01T00:00:00+00:00" }),
        listItem(2, { slaState: "overdue", dueAt: "2026-07-01T00:00:00+00:00" }),
        listItem(3, { slaState: "due-soon", dueAt: "2026-07-27T00:00:00+00:00" })
      ]
    });

    const titles = Array.from(app().querySelectorAll("[data-wcn-row]"))
      .map((row) => (row.textContent.match(/Görev \d/) || [""])[0])
      .filter(Boolean);

    // Non-vacuity: an empty list would satisfy any ordering claim.
    expect(titles).toHaveLength(3);
    expect(titles[0]).toBe("Görev 2");
    // The accent stripe is driven by the same state, so a row cannot be sorted as overdue and painted as calm.
    expect(app().querySelectorAll(".wcn-row-accent-danger").length).toBeGreaterThan(0);
  });

  it("paints an item the server called on-track as on-track, whatever its date says", async () => {
    /*
     * Deliberately a date the browser's old rule would have flagged. If any derivation survives anywhere in the
     * render path, this row comes out red.
     */
    await bootSurface({ items: [listItem(1, { slaState: "on-track", dueAt: "2026-07-01T00:00:00+00:00" })] });

    expect(app().querySelectorAll(".wcn-row-accent-danger").length).toBe(0);
    expect(app().querySelectorAll(".wcn-row-accent-success").length).toBe(1);
  });
});
