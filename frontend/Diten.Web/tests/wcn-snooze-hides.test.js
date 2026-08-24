const { bootSurface, app } = require("./wcn-boot");

/*
 * A SNOOZE ACTUALLY HIDES (BL-181).
 *
 * MEASURED BEFORE: `snoozedUntil` appeared in exactly three places, all of them drawings — a chip, a note row
 * and a parked banner. Nothing filtered on it, on the client or the server, so "erteleme" MARKED an item and
 * hid nothing. The dialog that sets it deliberately did not claim otherwise (its sentence was written around
 * the gap), and this is the round that closes it.
 *
 * THE SHAPE, and why it is what it is:
 *   · hidden in `passesFilters`, NOT in `inTab` — `facetItems('signal')` skips the signal axis, so the chip can
 *     count exactly what the hiding removes. In `inTab` the chip would read 0 over three unreachable rows.
 *   · the chip works BACKWARDS on purpose: every other signal narrows, this one reveals.
 *   · the tab badge does not count parked work: a badge means "waiting for you", and you decided it isn't.
 */
const TASK_ID = (n) => `0000000${n}-0000-0000-0000-00000000000${n}`;
const FUTURE = "2099-01-01";

const item = (n, overrides) => Object.assign({
  fixtureKind: "workItem",
  id: TASK_ID(n),
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
  title: { kind: "display", text: `İş ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID(n),
    deepLink: `/Tasks/${TASK_ID(n)}` },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null
}, overrides);

const snoozed = (n) => item(n, { personal: { snoozedUntil: `${FUTURE}T20:59:59+00:00` } });

/** Five items in İşlerim, two of them parked. */
const boot = () => bootSurface({
  rootAttrs: 'data-wcn-page="list"',
  items: [item(1), item(2), item(3), snoozed(4), snoozed(5)]
});

const rows = () => [...app().querySelectorAll("[data-wcn-row]")];
const chip = (sig) => app().querySelector(`[data-wcn-sigchip="${sig}"]`);
const chipCount = (sig) => { const c = chip(sig); return c ? Number(c.textContent.replace(/\D+/g, "")) : null; };
const tabBadge = (tab) => {
  const el = app().querySelector(`#wcn-tab-${tab}`);
  return el ? Number((el.textContent.match(/\d+/) || [0])[0]) : null;
};
const settle = () => new Promise((r) => setTimeout(r, 40));

const openIslerim = async () => { app().querySelector("#wcn-tab-islerim").click(); await settle(); };

describe("by default a parked item is not on screen", () => {
  beforeEach(async () => { await boot(); await openIslerim(); });

  it("shows only the three that are not parked", () => {
    expect(rows()).toHaveLength(3);
    expect(app().textContent).not.toContain("İş 4");
    expect(app().textContent).not.toContain("İş 5");
  });

  it("counts three in the tab badge, not five", () => {
    /*
     * MUTATION GUARD: add parked work back to the badge and this goes red. The badge and the chip describe two
     * DIFFERENT populations on purpose — "3 waiting for you" and "2 you put away" — and the chip never claims
     * to be a subset of the badge.
     */
    expect(tabBadge("islerim")).toBe(3);
  });

  it("still offers the door: a chip that counts what it hides", () => {
    /*
     * MUTATION GUARD: move the hiding from `passesFilters` into `inTab` and this reads 0 — the rows vanish from
     * `tabItems()`, which is where every faceted counter starts.
     */
    expect(chipCount("snoozed")).toBe(2);
  });
});

describe("the chip works backwards, and that is the feature", () => {
  beforeEach(async () => { await boot(); await openIslerim(); chip("snoozed").click(); await settle(); });

  it("reveals ONLY the parked rows", () => {
    // MUTATION GUARD: make it a normal narrowing signal and this shows 0 rows — a chip that filters a set it
    // has already removed.
    expect(rows()).toHaveLength(2);
    expect(app().textContent).toContain("İş 4");
    expect(app().textContent).toContain("İş 5");
    expect(app().textContent).not.toContain("İş 1");
  });

  it("keeps its own count while it is on — a facet never filters its own axis", () => {
    expect(chipCount("snoozed")).toBe(2);
    expect(chip("snoozed").getAttribute("aria-pressed")).toBe("true");
  });

  it("moves every OTHER counter to the revealed population (BL-045)", () => {
    // The type chip and the segments must describe what is actually on screen, or the round reopens BL-045.
    const type = app().querySelector('[data-wcn-typechip="task"]');
    expect(Number(type.textContent.replace(/\D+/g, ""))).toBe(2);
    const segments = [...app().querySelectorAll(".wcn-seg")].map((s) => Number(s.textContent.replace(/\D+/g, "")));
    expect(segments.reduce((a, b) => a + b, 0)).toBe(2);
  });

  it("puts the way back ON the revealed row", () => {
    /*
     * The pin's own language, reused: same place in the row's actions, same small icon button, same
     * `aria-pressed`, same one-click toggle through the handler that already exists. A reader who opened the
     * drawer to look at what they parked should not have to leave the list to take one back.
     */
    const buttons = rows().map((r) => r.querySelector("[data-wcn-snooze]"));
    expect(buttons.filter(Boolean)).toHaveLength(2);
    expect(buttons[0].getAttribute("aria-pressed")).toBe("true");
    expect(buttons[0].querySelector("i").className).toContain("bxs-moon");
  });
});

describe("where parking applies", () => {
  it("leaves the pool and the past alone", async () => {
    /*
     * `havuz` is work nobody holds — a personal overlay has nothing to hide there. `history` is finished: an
     * item you parked and later completed must still appear in your own past. Measured live too: a snoozed
     * finished task stayed in Geçmiş with its badge unchanged.
     */
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"',
      /*
       * The tab is DERIVED (`tabFor`), so these two differ from the İşlerim items only in what actually decides
       * the tab: an unclaimed pool item, and a finished one. Everything else is left alone so the contract
       * validation the harness performs still passes — a fixture no provider could send proves nothing.
       */
      items: [
        item(1, { admissionState: "pendingClaim", personal: { snoozedUntil: `${FUTURE}T20:59:59+00:00` } }),
        // `executionState` moves with the lifecycle or the contract refuses the item outright
        // (TERMINAL_EXECUTION_ACTIVE) — a finished task cannot still be running. `notApplicable` is the
        // contract's own vocabulary for it; "completed" is not one of the four it allows.
        item(2, { normalizedStatus: "Done", taskLifecycle: "Done", executionState: "notApplicable",
          nativeStatus: { code: "Done", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Done" } },
          personal: { snoozedUntil: `${FUTURE}T20:59:59+00:00` } })
      ]
    });
    app().querySelector("#wcn-tab-havuz").click();
    await settle();
    expect(rows(), "the pool hid a parked item").toHaveLength(1);
    app().querySelector("#wcn-tab-history").click();
    await settle();
    expect(rows(), "the past hid a parked item").toHaveLength(1);
  });
});

describe("an expired snooze comes back on its own", () => {
  it("is not hidden once its date has passed", async () => {
    /*
     * We rely on the SERVER for this: `TaskWorkItemProvider` projects `snoozedUntil` only while it is still in
     * the future, so yesterday's snooze arrives as null. This pins the client half — nothing here re-hides an
     * item whose overlay came back empty — and the past date below is what a provider would never send.
     */
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"',
      items: [item(1), item(2, { personal: { snoozedUntil: "2020-01-01T20:59:59+00:00" } })]
    });
    await openIslerim();
    expect(rows()).toHaveLength(2);
    expect(chip("snoozed"), "an expired snooze is still being treated as one").toBeNull();
  });
});

describe("the words", () => {
  const fs = require("fs");
  const path = require("path");
  it("ship in all seven languages", () => {
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const resx = fs.readFileSync(path.resolve(__dirname, "..", "Resources", "Views", "WorkCenterNext",
        `WorkCenterNextIndex.${lang}.resx`), "utf8");
      expect(resx, `${lang} has no SignalSnoozed`).toContain('name="SignalSnoozed"');
    });
  });
});
