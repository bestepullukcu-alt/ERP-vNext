const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-WCN-KANBAN-01 Dilim 3a — sürükleme başlar, sütunlar izin verir ya da soluklaşır. Dropping runs no action
 * yet (Dilim 3b wires that); this file proves the DOM shape a drag produces: which cards get a handle, which
 * columns a drag allows or pales, the disabled-reason hint on a pale column, and that Sortable is never bound
 * where it must not be (Havuz, Geçmiş, the Team scope).
 *
 * A FAKE `global.Sortable` stands in for the real library — production code is never touched to add a test
 * seam. `create(el, options)` is captured; the test then calls `options.onStart`/`onMove`/`onEnd` directly with
 * synthetic event shapes, exactly the fields app.js's binding reads off them.
 */
describe("Kanban drag (Dilim 3a — columns allow or pale, dropping does nothing yet)", () => {
  const id = (n) => `dddddddd-0000-0000-0000-${String(n).padStart(12, "0")}`;

  const action = (overrides) => Object.assign({
    code: "start",
    label: { kind: "display", text: "Başlat", locale: "und" },
    semanticType: "start",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal",
    targetStatus: null
  }, overrides);

  const task = (n, { status = "Pending", actions } = {}) => ({
    fixtureKind: "workItem",
    id: id(n),
    workIntent: "task",
    assignmentMode: "direct",
    ownershipState: "owned",
    admissionState: "admitted",
    normalizedStatus: status,
    taskLifecycle: status === "Pending" ? "Open" : status,
    executionState: status === "InProgress" ? "active" : "notStarted",
    timerState: "notApplicable",
    systemState: "fresh",
    actionDepth: "inline",
    title: { kind: "display", text: `Kart ${n}`, locale: "und" },
    nativeStatus: { code: status, label: { kind: "display", text: status, locale: "und" } },
    source: {
      providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
      objectId: id(n), deepLink: `/Tasks/${id(n)}`
    },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["execution"],
    actions,
    primaryActionCode: actions[0]?.code || null,
    overflowActionCodes: [],
    concurrency: { kind: "version", token: "1" },
    waitingContext: status === "Waiting" ? { type: "externalInformation", waitingOn: null } : null,
    escalation: null,
    dueAt: null
  });

  let sortableCalls;
  const fakeSortable = () => {
    sortableCalls = [];
    global.Sortable = {
      create: (el, options) => {
        const instance = { el, options };
        sortableCalls.push(instance);
        return instance;
      }
    };
  };

  const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

  const boot = async (items, { tab = "islerim" } = {}) => {
    fakeSortable();
    const result = await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items,
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });
    app().querySelector(`[data-wcn-tab="${tab}"]`).click();
    await tick();
    app().querySelector('[data-wcn-view="kanban"]').click();
    await tick();
    return result;
  };

  afterEach(() => { delete global.Sortable; });

  // ── which cards get a handle ────────────────────────────────────────────────────────────────────────────

  it("a card with an enabled, target-bearing action is draggable", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress" })]
    });
    await boot([t1]);

    const card = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(card.getAttribute("data-wcn-draggable")).toBe("1");
  });

  it("a card whose actions carry no target (e.g. plan) is not draggable", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "plan", targetStatus: null })]
    });
    await boot([t1]);

    const card = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(card.hasAttribute("data-wcn-draggable")).toBe(false);
  });

  it("a card whose only target-bearing action is DISABLED is not draggable", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({
        code: "start", targetStatus: "InProgress", enabled: false,
        disabledReasonCode: "APPROVAL_PENDING",
        disabledReason: { kind: "display", text: "Onay bekleniyor", locale: "und" }
      })]
    });
    await boot([t1]);

    const card = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(card.hasAttribute("data-wcn-draggable")).toBe(false);
  });

  // ── the columns a drag allows or pales ───────────────────────────────────────────────────────────────────

  it("allows the columns an enabled action reaches, pales the rest, and never pales the card's own column", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [
        action({ code: "start", targetStatus: "InProgress", enabled: true }),
        action({
          code: "inquire", targetStatus: "Waiting", enabled: false,
          disabledReasonCode: "APPROVAL_PENDING",
          disabledReason: { kind: "display", text: "Onay bekleniyor", locale: "und" }
        }),
        action({ code: "cancel", targetStatus: "Cancelled", enabled: true })
      ]
    });
    await boot([t1]);

    expect(sortableCalls.length, "one Sortable list per Kanban column").toBeGreaterThan(0);
    const { onStart } = sortableCalls[0].options;
    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    onStart({ item: cardEl });

    const colByStatus = (status) => app().querySelector(`.wcn-kcol[data-wcn-status="${status}"]`);
    // Own column (Pending): never pale, whatever the allowed set says.
    expect(colByStatus("Pending").classList.contains("wcn-kcol-pale")).toBe(false);
    // Reached by an ENABLED action: allowed, not pale.
    expect(colByStatus("In Progress").classList.contains("wcn-kcol-pale")).toBe(false);
    expect(colByStatus("Cancelled").classList.contains("wcn-kcol-pale")).toBe(false);
    // Reached only by a DISABLED action: pale.
    expect(colByStatus("Waiting").classList.contains("wcn-kcol-pale")).toBe(true);
    // Reached by nothing at all: pale too (unreachable is still not a valid drop).
    expect(colByStatus("Done").classList.contains("wcn-kcol-pale")).toBe(true);
  });

  it("shows the disabled action's own localized reason on a pale column with exactly one action targeting it", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [
        action({ code: "start", targetStatus: "InProgress", enabled: true }),
        action({
          code: "inquire", targetStatus: "Waiting", enabled: false,
          disabledReasonCode: "APPROVAL_PENDING",
          disabledReason: { kind: "display", text: "Onay bekleniyor", locale: "und" }
        })
      ]
    });
    await boot([t1]);

    const { onStart } = sortableCalls[0].options;
    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    onStart({ item: cardEl });

    const waitingHead = app().querySelector('.wcn-kcol[data-wcn-status="Waiting"] .wcn-kcol-head');
    expect(waitingHead.getAttribute("title")).toBe("Onay bekleniyor");
    // A column with NO action targeting it at all gets no hint — there is nothing to explain.
    const doneHead = app().querySelector('.wcn-kcol[data-wcn-status="Done"] .wcn-kcol-head');
    expect(doneHead.hasAttribute("title")).toBe(false);
  });

  it("onMove refuses a pale column and accepts an allowed one", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);

    const { onStart, onMove } = sortableCalls[0].options;
    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    onStart({ item: cardEl });

    const inProgressBody = app().querySelector('.wcn-kcol[data-wcn-status="In Progress"] .wcn-kcol-body');
    const waitingBody = app().querySelector('.wcn-kcol[data-wcn-status="Waiting"] .wcn-kcol-body');
    expect(onMove({ to: inProgressBody })).toBe(true);
    expect(onMove({ to: waitingBody })).toBe(false);
  });

  // ── cleanup and the trailing click ──────────────────────────────────────────────────────────────────────

  it("clears every pale class when the drag ends, and drops the card back where it started", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);

    const { onStart, onEnd } = sortableCalls[0].options;
    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    onStart({ item: cardEl });
    expect(app().querySelectorAll(".wcn-kcol-pale").length, "onStart did not pale anything").toBeGreaterThan(0);

    onEnd({});
    expect(app().querySelectorAll(".wcn-kcol-pale").length).toBe(0);
    // The card is back in its own (only) column — no action ran, nothing moved for real.
    expect(app().querySelector(`.wcn-kcol[data-wcn-status="Pending"] [data-wcn-row="${id(1)}"]`)).not.toBeNull();
  });

  /*
   * WHAT A CLICK ACTUALLY DOES ON THIS PAGE: `openDetailPage` navigates away (`location.assign`), it does not
   * flip a `selected` class in place — that class only ever applies on the split/list views, never here (the
   * Kanban board has no in-place selection to toggle). jsdom refuses real navigation and `location.assign`
   * cannot be stubbed on this jsdom version (`Cannot redefine property: assign`), so the assertion below reads
   * `openDetailPage`'s own FIRST, un-refusable side effect instead: `rememberListUrl` writes the return URL to
   * sessionStorage BEFORE `location.assign` ever runs. That write happening is exactly "the click reached the
   * navigation branch" — the real question this test asks — and not happening is exactly it did not.
   */
  it("the click that follows a drag's mouseup does not reopen the card's detail page", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);
    const cardEl = () => app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    const clicked = () => !!global.sessionStorage.getItem("wcn:list-return-url");

    // Control: the SAME click, with no drag in the way, DOES reach the navigation branch. The click handler is
    // wrapped in `Promise.resolve().then(...)` (app.js's own onClickWrapped), so its effect lands a microtask
    // after dispatch — `tick()` (a real setTimeout) flushes that before each assertion reads sessionStorage.
    global.sessionStorage.clear();
    cardEl().dispatchEvent(new global.window.MouseEvent("click", { bubbles: true, cancelable: true }));
    await tick();
    expect(clicked(), "the control click never reached openDetailPage — the test proves nothing").toBe(true);

    // The drag itself.
    const { onEnd } = sortableCalls[0].options;
    onEnd({});

    global.sessionStorage.clear();
    cardEl().dispatchEvent(new global.window.MouseEvent("click", { bubbles: true, cancelable: true }));
    await tick();
    expect(clicked(), "the trailing click after a drag reopened the card's detail page").toBe(false);
  });

  // ── the drag's column key matches toPresentation's own item.status ──────────────────────────────────────

  /*
   * REGRESSION GUARD for the exact defect found while writing this dilim: mock-data.js's `toPresentation`
   * spells the In Progress card "In Progress" (a display string) while the wire's normalizedStatus/targetStatus
   * spell it "InProgress" (no space). app.js's `kanbanColumnKey` bridges the two, in exactly the place that
   * matters — the drag-allowed computation `onStart` runs — and if that bridge and toPresentation's own
   * transform ever drift apart again, a card's real, reachable column reads as PALE (unreachable) instead of
   * allowed, exactly as "In Progress" silently did before `kanbanColumnKey` existed.
   *
   * The test never hardcodes the expected column key itself: a SEPARATE probe item, already parked in the
   * target normalizedStatus, is run through the REAL `toPresentation`, and its `.status` is what the assertion
   * demands the drag treat as allowed — so a drift in EITHER function shows up here, not just one of them.
   */
  describe.each(["Pending", "InProgress", "Waiting", "Done", "Cancelled"])(
    "normalizedStatus %s", (normalizedStatus) => {
      it("a target-bearing action allows exactly the column toPresentation would key it as", async () => {
        // Different from the target in every case, so this is never the dragged card's OWN column (which is
        // never pale regardless of kanbanColumnKey, and would make the case trivially pass either way).
        const startStatus = normalizedStatus === "Waiting" ? "Pending" : "Waiting";
        const t1 = task(1, {
          status: startStatus,
          actions: [action({ code: "go", targetStatus: normalizedStatus, enabled: true })]
        });
        await boot([t1]);

        const expectedColumnKey = global.WorkCenterNextData.toPresentation(
          task(99, { status: normalizedStatus, actions: [] }), { provenance: "api" }
        ).status;

        const { onStart } = sortableCalls[0].options;
        onStart({ item: app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`) });

        const col = app().querySelector(`.wcn-kcol[data-wcn-status="${expectedColumnKey}"]`);
        expect(col, `no column keyed "${expectedColumnKey}" exists on the board`).not.toBeNull();
        expect(col.classList.contains("wcn-kcol-pale"),
          `the column toPresentation calls "${expectedColumnKey}" was pale — kanbanColumnKey disagrees with toPresentation`
        ).toBe(false);
      });
    }
  );

  // ── where Sortable is never bound ────────────────────────────────────────────────────────────────────────

  it("never binds Sortable on Havuz's board", async () => {
    fakeSortable();
    const pool = task(1, {
      status: "Pending",
      actions: [action({ code: "claim", targetStatus: null })]
    });
    pool.assignmentMode = "groupQueue";
    pool.admissionState = "pendingClaim";
    pool.pool = { id: "pos-1", label: { kind: "display", text: "Kuyruk 1", locale: "und" } };

    await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items: [pool],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });
    app().querySelector('[data-wcn-tab="havuz"]').click();
    await tick();
    app().querySelector('[data-wcn-view="kanban"]').click();
    await tick();

    expect(sortableCalls.length, "Havuz's board bound Sortable — it must stay read-only").toBe(0);
  });

  it("never binds Sortable on Geçmiş's board", async () => {
    const closed = task(1, {
      status: "Done",
      actions: []
    });
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items: [closed],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });
    app().querySelector('[data-wcn-tab="history"]').click();
    await tick();
    app().querySelector('[data-wcn-view="kanban"]').click();
    await tick();

    expect(sortableCalls.length, "Geçmiş's board bound Sortable — it must stay read-only").toBe(0);
  });

  it("never binds Sortable in the Team scope", async () => {
    fakeSortable();
    global.fetch = async (url) => {
      if (String(url).includes("team-availability")) {
        return { ok: true, status: 200, json: async () => ({ data: { hasTeam: true, memberCount: 2 } }) };
      }
      throw new Error(`unexpected fetch in this test: ${url}`);
    };
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items: [t1],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });

    const teamOption = app().querySelector('[data-wcn-scope="team"]');
    expect(teamOption.disabled, "the team option never enabled — the fetch stub was not read").toBe(false);
    teamOption.click();
    await tick();
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
    app().querySelector('[data-wcn-view="kanban"]').click();
    await tick();

    expect(sortableCalls.length, "the Team scope's board bound Sortable — a colleague's work is not this reader's to drag").toBe(0);
    delete global.fetch;
  });
});
