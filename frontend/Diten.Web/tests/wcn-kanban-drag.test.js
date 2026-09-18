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

  const task = (n, { status = "Pending", actions, assignee, requester } = {}) => ({
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
    dueAt: null,
    assignee: assignee || null,
    requester: requester || null
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

  // ── the person footer (WP-WCN-KANBAN-01 Dilim 4) ─────────────────────────────────────────────────────────

  /*
   * Sabotage-guarded: remove the footer entirely and this goes red. The circle is
   * DitenPersonPicker.personInitials's OWN initials logic (two words → first+last letter), never a hand-typed
   * guess, and the projection carries only a display-name string here (mock-data's personName) — no photo, so
   * this is the only avatar shape the card could ever draw honestly.
   */
  it("draws the assignee's initials and name below the card", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })],
      assignee: { id: "u-1", displayName: "Ayşe Yılmaz", isCurrentUser: false }
    });
    await boot([t1]);

    const person = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"] .wcn-kcard-person`);
    expect(person, "no person footer was drawn for an assigned task").not.toBeNull();
    expect(person.textContent).toContain("Ayşe Yılmaz");
    expect(person.querySelector(".wcn-kcard-avatar").textContent).toBe("AY");
  });

  it("falls back to the requester when nobody has claimed the work yet", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "claim", targetStatus: null })],
      assignee: null,
      requester: { id: "u-2", displayName: "Deniz Koç", isCurrentUser: false }
    });
    await boot([t1]);

    const person = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"] .wcn-kcard-person`);
    expect(person, "no person footer was drawn for the requester fallback").not.toBeNull();
    expect(person.textContent).toContain("Deniz Koç");
  });

  it("draws no person footer when the projection carries neither — no data is invented", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "claim", targetStatus: null })],
      assignee: null,
      requester: null
    });
    await boot([t1]);

    const card = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(card.querySelector(".wcn-kcard-person")).toBeNull();
  });

  /*
   * CT fix, WP-WCN-KANBAN-01 Dilim 4 correction round — the REAL projection shape today: the server has no
   * user-directory seam, so an assignee/requester with no resolvable identity arrives as a person object with
   * no displayName. mock-data's personName() then leaves the translated "name unavailable" LABEL in
   * item.assignee, which is truthy — a card that only checked "is this string non-empty" (the original Dilim 4
   * code) drew that label's initials as if it were a real person's. Sabotage: remove the
   * assigneeNameKnown/requesterNameKnown gate in kanbanCard (fall back to a plain truthy check) and this test
   * goes red.
   */
  it("draws no person footer when the assignee exists but the server sent no display name", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "claim", targetStatus: null })],
      assignee: { id: "u-3", isCurrentUser: false },
      requester: null
    });
    await boot([t1]);

    const card = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(
      card.querySelector(".wcn-kcard-person"),
      "the name-unavailable label was drawn as if it were a person"
    ).toBeNull();
  });

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

  // ── the dragged clone does not trail the pointer ────────────────────────────────────────────────────────

  /*
   * CT fix, WP-WCN-KANBAN-01 Dilim 4 correction round — `.wcn-kcard`'s own hover/select `transition: all .12s
   * ease` was also applying to Sortable's `forceFallback` clone (the element that follows the mouse), so the
   * card visibly trailed .12s behind the pointer on every drag. jsdom does not compute a cascade against an
   * external stylesheet, so this pins two things a screen cannot show here: that app.js actually asks Sortable
   * for a named class on the dragged clone (a real behaviour check, via the captured `options`), and that
   * backbone-custom.css turns the transition off for that class. Sabotage: delete either the `fallbackClass`
   * line from app.js's Kanban `Sortable.create` call or the `.wcn-kcard-dragging` rule from the CSS, and one of
   * these two goes red.
   */
  it("names a class for Sortable's dragged clone, so the CSS can turn its transition off", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);

    expect(sortableCalls.length).toBeGreaterThan(0);
    expect(
      sortableCalls[0].options.fallbackClass,
      "the Kanban Sortable binding no longer names a class for its dragged clone"
    ).toBe("wcn-kcard-dragging");
  });

  /*
   * CT fix, Dilim 4 correction round — `.diten-opt-avatar` (backbone-custom.css:8726) sets the SAME three
   * size properties at the SAME specificity (0,1,0) as `.wcn-kcard-avatar` and is declared LATER in the file,
   * so it was winning the cascade and the card drew a 2rem avatar instead of the intended 1.5rem one.
   * Sabotage: drop `.wcn-kcard-avatar` back to a single-class selector (undoing the `.diten-opt-avatar.` prefix
   * that raises its specificity to 0,2,0) and this goes red.
   */
  it("wins the avatar-size cascade against .diten-opt-avatar's later, equal-specificity rule", () => {
    const fs = require("fs");
    const path = require("path");
    const css = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "css", "backbone-custom.css"),
      "utf8"
    ).replace(/\/\*[\s\S]*?\*\//g, "");

    expect(css, "no rule raises .wcn-kcard-avatar's specificity above .diten-opt-avatar's").toMatch(
      /\.diten-opt-avatar\.wcn-kcard-avatar\s*\{[^}]*inline-size:\s*1\.5rem/
    );
  });

  it("turns the dragged clone's transition off in CSS, so it does not trail the pointer", () => {
    const fs = require("fs");
    const path = require("path");
    const css = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "css", "backbone-custom.css"),
      "utf8"
    ).replace(/\/\*[\s\S]*?\*\//g, "");

    expect(css, "no .wcn-kcard-dragging rule turns the transition off").toMatch(
      /\.wcn-kcard-dragging\s*\{[^}]*transition:\s*none/
    );
  });

  /*
   * CT fix, Dilim 4 correction round — hover/selected used to REPLACE `.wcn-kcard`'s base `box-shadow` outright:
   * hover with a hardcoded light-only `rgba(0, 0, 0, .1)` (invisible over a dark-theme card, since core.css's
   * dark block pairs a DIFFERENT rgba with --bs-box-shadow-lg) and `.selected` with only its ring, dropping the
   * resting elevation for as long as either state held. Both now STACK the theme's own `--bs-box-shadow`/
   * `--bs-box-shadow-lg` with their own addition instead. Sabotage: put the old hardcoded rgba back on
   * `:hover`, or drop `var(--bs-box-shadow)` from `.selected`'s rule, and this goes red.
   */
  it("stacks the base shadow under hover and selected instead of replacing it", () => {
    const fs = require("fs");
    const path = require("path");
    const css = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "css", "backbone-custom.css"),
      "utf8"
    ).replace(/\/\*[\s\S]*?\*\//g, "");

    expect(css, "no rgba(0, 0, 0 hardcoded shadow should remain on the Kanban card").not.toMatch(
      /\.wcn-kcard:hover\s*\{[^}]*rgba\(0,\s*0,\s*0/
    );
    expect(css, ".wcn-kcard:hover no longer stacks the theme's own base shadow").toMatch(
      /\.wcn-kcard:hover\s*\{[^}]*var\(--bs-box-shadow\)[^}]*var\(--bs-box-shadow-lg\)/
    );
    expect(css, ".wcn-kcard.selected no longer stacks the theme's own base shadow under its ring").toMatch(
      /\.wcn-kcard\.selected\s*\{[^}]*var\(--bs-box-shadow\)[^}]*color-mix/
    );
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

    // Dropped back on its OWN column (real SortableJS always reports where the pointer released) — Dilim 3b's
    // own "own column" no-op, so nothing runs; this test is only about the pale cleanup.
    const ownBody = app().querySelector('.wcn-kcol[data-wcn-status="Pending"] .wcn-kcol-body');
    onEnd({ item: cardEl, to: ownBody });
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

    // The drag itself — dropped back on its own column, so Dilim 3b's own no-op applies and no action runs
    // (this test is only about the trailing click, not the drop).
    const { onEnd } = sortableCalls[0].options;
    const ownBody = app().querySelector('.wcn-kcol[data-wcn-status="Pending"] .wcn-kcol-body');
    onEnd({ item: cardEl(), to: ownBody });

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

/*
 * WP-WCN-KANBAN-01 Dilim 3b — a drop RUNS the action, through the same address a button press reaches
 * (`performAction(item, action.key)`). This file stubs the SAME two network seams `wcn-action-outcome-stitch`
 * already stubs (`WorkCenterNextApi.dispatchAction`, `WorkCenterNextApi.fetchWorkItems`) and drives the drop
 * through the fake Sortable's `onStart`/`onEnd`, exactly as Dilim 3a's own tests do.
 */
describe("Kanban drop runs the action (Dilim 3b)", () => {
  const id = (n) => `eeeeeeee-0000-0000-0000-${String(n).padStart(12, "0")}`;

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

  const task = (n, { status = "Pending", actions, assignee, requester } = {}) => ({
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
    dueAt: null,
    assignee: assignee || null,
    requester: requester || null
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

  let dispatched;
  let dispatchResult;
  let fetchWorkItemsCalls;
  let toasted;

  const boot = async (items) => {
    fakeSortable();
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items,
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
    app().querySelector('[data-wcn-view="kanban"]').click();
    await tick();

    dispatched = null;
    dispatchResult = { ok: true, status: 204 };
    global.WorkCenterNextApi.dispatchAction = async (itemId, actionCode, providerCode, body) => {
      dispatched = { itemId, actionCode, providerCode, body };
      return dispatchResult;
    };
    fetchWorkItemsCalls = 0;
    const realFetch = global.WorkCenterNextApi.fetchWorkItems;
    global.WorkCenterNextApi.fetchWorkItems = (...args) => {
      fetchWorkItemsCalls += 1;
      return realFetch(...args);
    };
    // The product's own toast seam (MOD-0013) — recorded rather than rendered, matching every other test in
    // this suite that asserts on what the reader is TOLD (toast() is a no-op without this: `global.showToast?.`).
    toasted = [];
    global.showToast = (message, type) => { toasted.push({ message, type }); };
    // `inquire` fetches the assignable-people list BEFORE its reason dialog even opens (WAITING_ON_ACTIONS) —
    // unrelated to what any of these tests are about, so it is stubbed empty here rather than per-test.
    global.TasksApi = global.TasksApi || {};
    global.TasksApi.assignablePeople = async () => ({ ok: true, data: [] });
  };

  const drop = (cardEl, targetStatus) => {
    const { onStart, onEnd } = sortableCalls[0].options;
    onStart({ item: cardEl });
    const body = app().querySelector(`.wcn-kcol[data-wcn-status="${targetStatus}"] .wcn-kcol-body`);
    onEnd({ item: cardEl, to: body });
  };

  afterEach(() => { delete global.Sortable; delete global.Swal; delete global.showConfirm; });

  it("one enabled action: performAction runs with the right code, the card returns to the DOM, and the board re-reads from the server", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);
    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);

    drop(cardEl, "In Progress");
    await tick();

    expect(dispatched, "performAction never reached the dispatch endpoint").not.toBeNull();
    expect(dispatched.actionCode).toBe("start");
    expect(dispatched.itemId).toBe(id(1));
    expect(app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`), "the card vanished from the DOM").not.toBeNull();
    expect(fetchWorkItemsCalls, "the board never re-read the projection after a successful drop").toBeGreaterThan(0);
  });

  it("two enabled actions: the picker opens with both labels; choosing one runs it; Esc runs nothing", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [
        action({ code: "start", targetStatus: "Waiting", enabled: true, label: { kind: "display", text: "Başlat", locale: "und" } }),
        action({ code: "inquire", targetStatus: "Waiting", enabled: true, requiresReason: false, label: { kind: "display", text: "Bilgi bekle", locale: "und" } })
      ]
    });
    await boot([t1]);
    let seenOptions = null;
    let seenOnCancel;
    let seenConfirmText;
    let seenValidator;
    global.Swal = {};
    // `_GlobalConfirmation.cshtml`'s own real dismiss guard is `typeof options.onCancel === 'function'` — when
    // it is not, a dismissal (Esc / outside click) calls NOTHING, silently, by construction. So the proof that
    // "Esc runs nothing" is that `openKanbanActionPicker` never hands the picker an `onCancel` to call in the
    // first place — there is no path from a dismissal to an action, not merely one this stub chose not to take.
    global.showConfirm = (title, callback, opts) => {
      seenOptions = opts.inputOptions;
      seenOnCancel = opts.onCancel;
      seenConfirmText = opts.confirmButtonText;
      seenValidator = opts.inputValidator;
      // never call `callback` or `opts.onCancel` — this simulates the dialog sitting open, undecided.
    };

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "Waiting");
    await tick();

    expect(seenOptions, "the picker never opened for two candidates").not.toBeNull();
    expect(Object.keys(seenOptions).sort()).toEqual(["inquire", "start"]);
    expect(Object.values(seenOptions)).toEqual(expect.arrayContaining(["Başlat", "Bilgi bekle"]));
    // The harness's `t` echoes the key back — so this checks it is the PICKER's own confirm/validation keys,
    // not `PlanConfirm` (a different dialog's "Tarihi Ayarla") or the title repeated as the empty-answer reason.
    expect(seenConfirmText, "the confirm button is not using ConfirmProceed").toBe("ConfirmProceed");
    expect(seenValidator(""), "an empty answer does not name its own reason").toBe("KanbanActionPickerRequired");
    expect(dispatched, "a candidate ran before any was chosen").toBeNull();
    expect(typeof seenOnCancel, "a dismissal has a path to run something after all").not.toBe("function");
  });

  it("two enabled actions: choosing one from the picker runs exactly that one", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [
        action({ code: "start", targetStatus: "Waiting", enabled: true }),
        action({ code: "inquire", targetStatus: "Waiting", enabled: true })
      ]
    });
    await boot([t1]);
    global.Swal = {};
    global.showConfirm = (title, callback) => { callback("inquire"); };

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "Waiting");
    await tick();

    expect(dispatched, "the chosen candidate never ran").not.toBeNull();
    expect(dispatched.actionCode).toBe("inquire");
  });

  it("refused: the card stays in its own column and the localized reason is toasted (no new text — TasksApi.failureMessage)", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);
    dispatchResult = { ok: false, status: 409, reasonCode: "TASK_CONCURRENCY_CONFLICT" };
    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);

    drop(cardEl, "In Progress");
    await tick();

    expect(dispatched, "the drop never dispatched at all").not.toBeNull();
    // The card is back in ITS OWN column (Pending) — the drop was refused, nothing moved for real.
    expect(app().querySelector('.wcn-kcol[data-wcn-status="Pending"] .wcn-kcard')).not.toBeNull();
    expect(app().querySelector('.wcn-kcol[data-wcn-status="In Progress"] .wcn-kcard')).toBeNull();
    // A toast fired — submitRealTransition's own existing path (global.TasksApi.failureMessage), not new text —
    // and it is an ERROR toast, not the generic success default.
    expect(toasted.length, "no error was ever surfaced to the reader").toBeGreaterThan(0);
    expect(toasted[toasted.length - 1].type).toBe("error");
  });

  it("cancelled: dismissing a reason dialog the dropped action opened stays silent — no toast, no dispatch", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "inquire", targetStatus: "Waiting", enabled: true, requiresReason: true })]
    });
    await boot([t1]);
    global.Swal = { fire: () => Promise.resolve({ isConfirmed: false }) };

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "Waiting");
    await tick();
    await tick();

    expect(dispatched, "a dismissed reason dialog still dispatched").toBeNull();
  });

  it("dropping on Tamamlandı/İptal shows the 'Geçmiş'e taşındı' notice on success", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "cancel", targetStatus: "Cancelled", enabled: true, riskLevel: "destructive" })]
    });
    await boot([t1]);
    global.Swal = {};
    global.showConfirm = (title, callback) => { callback(); };

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "Cancelled");
    await tick();

    expect(dispatched, "cancel never dispatched").not.toBeNull();
    expect(dispatched.actionCode).toBe("cancel");
    expect(toasted.some((t) => t.message === "KanbanMovedToHistory"),
      "no 'moved to Geçmiş' notice was shown").toBe(true);
  });

  it("a REFUSED drop onto Tamamlandı/İptal never shows 'Geçmiş'e taşındı' — the card never actually left", async () => {
    // Sabotage-guarded: a drop onto a terminal column can be dispatched and still be refused (a stale
    // concurrency token, a gate the button path would have hit too). Showing "moved to Geçmiş" then would be a
    // lie — the card is still sitting right here, in its own column.
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "cancel", targetStatus: "Cancelled", enabled: true, riskLevel: "destructive" })]
    });
    await boot([t1]);
    dispatchResult = { ok: false, status: 409, reasonCode: "TASK_CONCURRENCY_CONFLICT" };
    global.Swal = {};
    global.showConfirm = (title, callback) => { callback(); };

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "Cancelled");
    await tick();

    expect(dispatched, "cancel never dispatched").not.toBeNull();
    expect(toasted.some((t) => t.message === "KanbanMovedToHistory"),
      "a REFUSED drop still claimed the card moved to Geçmiş").toBe(false);
    expect(app().querySelector('.wcn-kcol[data-wcn-status="Pending"] .wcn-kcard'),
      "the card is not back in its own column").not.toBeNull();
  });

  it("a card mid-drop cannot be picked up again until performAction resolves", async () => {
    // Sabotage-guarded: removing `!processing &&` from kanbanCard's `draggable` computation leaves a card
    // pickable while its own drop is still in flight — a second drag or drop-triggered action could start on
    // the SAME card before the first one is done.
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);
    let resolveDispatch;
    global.WorkCenterNextApi.dispatchAction = () => new Promise((resolve) => { resolveDispatch = resolve; });

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "In Progress");
    await tick();

    const midFlightCard = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(midFlightCard, "the card vanished instead of showing 'processing'").not.toBeNull();
    expect(midFlightCard.classList.contains("wcn-kcard-processing"), "no processing state was shown").toBe(true);
    expect(midFlightCard.hasAttribute("data-wcn-draggable"),
      "a card mid-drop can still be picked up again").toBe(false);
    // The library's OWN selectors, not merely a class for a human to notice: Sortable itself refuses to start a
    // drag on an element that fails `draggable` or matches `filter`, so the attribute's absence is what
    // actually locks the card, on every list this binding creates.
    const lastBinding = sortableCalls[sortableCalls.length - 1].options;
    expect(lastBinding.draggable).toBe('.wcn-kcard[data-wcn-draggable="1"]');
    expect(lastBinding.filter).toBe('.wcn-kcard:not([data-wcn-draggable="1"])');

    resolveDispatch({ ok: true, status: 204 });
    await tick();
    await tick();

    const cardAfter = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(cardAfter.classList.contains("wcn-kcard-processing"), "the processing state never cleared").toBe(false);
    expect(cardAfter.hasAttribute("data-wcn-draggable"), "the card never became draggable again").toBe(true);
  });

  it("a rejected promise (network failure) clears the processing state instead of leaving the card stuck", async () => {
    const t1 = task(1, {
      status: "Pending",
      actions: [action({ code: "start", targetStatus: "InProgress", enabled: true })]
    });
    await boot([t1]);
    global.WorkCenterNextApi.dispatchAction = async () => { throw new Error("network exploded"); };

    const cardEl = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    drop(cardEl, "In Progress");
    await tick();
    await tick();

    const cardAfter = app().querySelector(`.wcn-kcard[data-wcn-row="${id(1)}"]`);
    expect(cardAfter, "the card vanished instead of coming back").not.toBeNull();
    expect(cardAfter.classList.contains("wcn-kcard-processing"), "the card is stuck in 'processing' forever").toBe(false);
    expect(cardAfter.hasAttribute("data-wcn-draggable"), "a stuck card cannot be picked up again").toBe(true);
    expect(toasted.some((t) => t.type === "error"), "no error was ever surfaced").toBe(true);
  });

  it("speaks the three new keys in all seven languages", () => {
    const fs = require("fs");
    const path = require("path");
    const web = (...p) => path.resolve(__dirname, "..", ...p);
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const resx = fs.readFileSync(
        web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      ["KanbanActionPickerTitle", "KanbanProcessing", "KanbanMovedToHistory"].forEach((key) => {
        expect(resx, `${lang} is missing ${key}`).toContain(`name="${key}"`);
      });
    });
  });
});
