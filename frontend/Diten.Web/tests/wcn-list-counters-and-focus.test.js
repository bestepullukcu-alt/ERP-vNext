const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THE LIST PAGE'S FIRST ROUND — the audit's "misleading" and "blocking" findings.
 *
 *   ① a snoozed item leaked into the signal chip's count while every other counter excluded it
 *   ② two chip rows on one screen combined under two different rules (types OR, signals AND)
 *   ③ six of seven inbox type chips promised a population that does not exist
 *   ④ five control families had no visible focus ring under a real Tab
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");

const id = (n) => `aaaaaaaa-0000-0000-0000-${String(n).padStart(12, "0")}`;

/**
 * One İşlerim row. `signals` decides which chips it answers to; `snoozedUntil` parks it.
 *
 * The fixture is deliberately close to what the provider sends — the contract validates it on the way in, so a
 * shape drift fails here rather than rendering a chip row nobody can trust.
 */
const row = (n, { blocked = false, slaRisk = false, snoozed = null } = {}) => ({
  fixtureKind: "workItem",
  id: id(n),
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
  title: { kind: "display", text: `Satır ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: id(n), deepLink: `/Tasks/${id(n)}`
  },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["execution"],
  actions: blocked
    ? [{
        code: "complete", label: { kind: "display", text: "Tamamla", locale: "und" },
        semanticType: "complete", enabled: false, source: "provider",
        disabledReasonCode: "SUBTASK_BLOCKED",
        disabledReason: { kind: "display", text: "Bir alt görev hâlâ açık", locale: "und" },
        requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
        supportsBulk: false, riskLevel: "normal"
      }]
    : [{
        code: "complete", label: { kind: "display", text: "Tamamla", locale: "und" },
        semanticType: "complete", enabled: true, source: "provider",
        disabledReasonCode: null, disabledReason: null,
        requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
        supportsBulk: false, riskLevel: "normal"
      }],
  primaryActionCode: "complete",
  overflowActionCodes: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2090-01-01T00:00:00+00:00",
  /*
   * ⚠ THE PROJECTION'S OWN FIELDS, not ones invented here. The first version of this fixture set `dueAt` in
   * the past and a top-level `snoozedUntil`, and both silently produced nothing: `slaState` is SENT by the
   * provider (`item.slaState || 'no-sla'`), and the snooze date is read from `personal.snoozedUntil`. The
   * chips then read 0 and the test failed for a reason that had nothing to do with the code under test.
   */
  slaState: slaRisk ? "overdue" : "no-sla",
  personal: snoozed ? { snoozedUntil: snoozed } : undefined,
  blockedState: blocked
    ? {
        blocked: true, affectedActionCodes: ["complete"],
        blockers: [{
          code: "SUBTASK_BLOCKED", taskItemId: id(900 + n), dependencyType: null,
          affectedActionCode: "complete", label: { kind: "display", text: "Alt görev", locale: "und" }
        }]
      }
    : null
});

/*
 * The list opens on the Inbox, and these fixtures are owned+admitted work, so they land in İşlerim
 * (`tabFor`). The chips under test are that tab's, so the boot switches to it — measuring the Inbox's chip row
 * against İşlerim's fixtures would have been an assertion about nothing.
 */
const boot = async (items) => {
  const r = await bootSurface({ rootAttrs: 'data-wcn-page="list"', items });
  app().querySelector('[data-wcn-tab="islerim"]').click();
  await new Promise((x) => setTimeout(x, 0));
  return r;
};
const chip = (key) => app().querySelector(`[data-wcn-sigchip="${key}"]`);
const chipCount = (el) => (el ? Number(el.querySelector(".wcn-fchip-count").textContent) : null);
const segSum = () => [...app().querySelectorAll("[data-wcn-seg] .wcn-seg-count")]
  .reduce((a, e) => a + Number(e.textContent || 0), 0);
const rows = () => app().querySelectorAll(".wcn-row").length;

describe("① a parked item is not on screen, so it is not in a count either", () => {
  /*
   * MEASURED on live data before the fix: İşlerim read "SLA riski 14" while the segments under it summed to
   * 13 — off by EXACTLY the one snoozed row. The tab badge already subtracted it; the signal chip did not,
   * because the snooze rule sits behind `except !== 'signal'` and `signalCount` is the one facet that skips
   * the signal axis.
   */
  const set = () => [
    row(1, { slaRisk: true }), row(2, { slaRisk: true }),
    row(3, { slaRisk: true, snoozed: "2090-01-01" }),   // at risk AND parked
    row(4, { blocked: true })
  ];

  it("keeps the snoozed row out of the SLA chip's count", async () => {
    await boot(set());
    // MUTATION GUARD: let the leak back in and this reads 3 instead of 2.
    expect(chipCount(chip("sla-risk")), "the parked row is being counted again").toBe(2);
  });

  it("still counts it in the Ertelenmiş chip — that chip exists to reveal it", async () => {
    await boot(set());
    /*
     * ⚠ THE EXCEPTION, and it is not a leftover: a chip reading 0 that opens one row is the same lie in the
     * other direction. This is why the fix is a filter inside `signalCount` and not a blanket subtraction.
     */
    expect(chipCount(chip("snoozed"))).toBe(1);
  });

  it("proves it the way the counts are read: chip = sum of the segments beneath it", async () => {
    await boot(set());
    chip("sla-risk").click();
    await new Promise((r) => setTimeout(r, 0));
    // The condition the whole item was judged by. Unequal means the work is not done.
    expect(segSum(), "the chip and the segment bar describe different populations").toBe(2);
  });

  it("does not touch the chip's BEHAVIOUR — turning it on still reveals parked rows", async () => {
    await boot(set());
    chip("snoozed").click();
    await new Promise((r) => setTimeout(r, 0));
    expect(rows(), "the snoozed row stopped being reachable").toBe(1);
  });
});

describe("② signals widen the net; axes narrow it", () => {
  const set = () => [
    row(1, { blocked: true }), row(2, { blocked: true }),
    row(3, { slaRisk: true }), row(4, { slaRisk: true }), row(5, { slaRisk: true }),
    row(6, { blocked: true, slaRisk: true })   // the one that carries both
  ];

  it("combines two signals as OR, not AND", async () => {
    await boot(set());
    chip("blocked").click();
    await new Promise((r) => setTimeout(r, 0));
    chip("sla-risk").click();
    await new Promise((r) => setTimeout(r, 0));
    /*
     * MEASURED live before the fix: Bloke(4) and SLA(7) together produced ONE row — the intersection. A signal
     * answers "what needs attention"; picking two asks for a wider net.
     *
     * MUTATION GUARD: restore the `for` loop that demanded every signal and this collapses to 1.
     */
    expect(rows(), "the two chips intersected instead of uniting").toBe(6);
  });

  it("keeps AND ACROSS axes — a type and a signal still narrow together", async () => {
    // The rule that did NOT change: different questions intersect. Only same-axis answers unite.
    const code = APP.split("const passesFilters")[1].split("const foldForSearch")[0];
    expect(code, "the type axis stopped being its own gate").toContain("!state.typeFilter.has(item.itemType)");
    expect(code).toContain("if (!any) { return false; }");
  });

  it("still round-trips both signals through the URL", async () => {
    await boot(set());
    chip("blocked").click();
    await new Promise((r) => setTimeout(r, 0));
    chip("sla-risk").click();
    await new Promise((r) => setTimeout(r, 0));
    // This worked before and must keep working: the change was to the combining rule, not to the state.
    const q = new URL(global.location.href).searchParams.get("signals") || "";
    expect(q.split(",").sort()).toEqual(["blocked", "sla-risk"]);
  });
});

describe("③ a chip at zero is not drawn at all", () => {
  it("draws no inbox type chip for a population that does not exist", () => {
    /*
     * MEASURED live: six of seven inbox type chips read 0 (Onay · İnceleme · Sorun · İstisna · Toplantı
     * Daveti), all clickable, all leading to an empty list. Promising a population that is not there is the
     * defect this session removed nine times over.
     *
     * MUTATION GUARD: draw them again and this goes red. Asserted on the source because the rule is one line
     * and the inbox fixture needed to exercise it would be six items the provider cannot send.
     */
    /*
     * ⚠ ANCHORED ON THE TYPE-CHIP BLOCK, NOT ON THE WHOLE FUNCTION. The first version searched the entire
     * `buildInboxChips` body for the guard — and the RISK chips below have carried that exact line all along,
     * so deleting the guard from the type chips left the test green. A rule that another line can satisfy is a
     * rule nothing enforces; this session has now recorded that twice.
     */
    const fn = APP.split("const buildInboxChips")[1].split("const buildDefaultChips")[0];
    const mainChips = fn.split("const mainChips")[1].split("const riskChips")[0];
    expect(mainChips, "zero type chips are drawn again").toContain("if (!c && !on) { return ''; }");
  });

  it("keeps an ACTIVE chip drawn even at zero, or it could never be switched off", () => {
    const fn = APP.split("const buildInboxChips")[1].split("const buildDefaultChips")[0];
    const mainChips = fn.split("const mainChips")[1].split("const riskChips")[0];
    // The second half of the guard: `!on` is what keeps a switched-on chip reachable at zero.
    expect(mainChips).toContain("!c && !on");
  });

  it("keeps the Tümü chip, which is the axis's own zero state", () => {
    const fn = APP.split("const buildInboxChips")[1].split("const buildDefaultChips")[0];
    const all = fn.split("const allChip")[1].split("const mainChips")[0];
    expect(all, "the reset chip was hidden with the rest").not.toContain("!c && !on");
  });

  it("uses the mechanism that already existed, not a second one", () => {
    // The signal chips and the default tab's type chips have always hidden at zero. One rule, three callers.
    expect(APP.split("const buildDefaultChips")[1]).toContain("typeCount(ty) > 0 || state.typeFilter.has(ty)");
  });
});

describe("④ five controls the global reset had silenced", () => {
  /*
   * MEASURED with a REAL Tab press — a programmatic `.focus()` never raises `:focus-visible` and gave the
   * wrong answer on the first attempt. A global `button:focus, button:focus-visible { outline: 0 }` in
   * core.css strips the ring from every plain button; only controls with their own rule survived.
   */
  const RING = "outline: 2px solid var(--bs-primary)";

  it("gives all five the product's own ring", () => {
    // MUTATION GUARD: drop any one selector and this goes red naming it.
    // ⚠ Sliced at the RULE, not at the first `}`: the comment above it quotes `{ outline: 0 }`, which cut the
    // window short and made this pass for the wrong reason on the first attempt.
    const block = CSS.split("KEYBOARD FOCUS FOR THE FIVE CONTROLS")[1].split(RING)[0];
    [".wcn-inbox-action-primary:focus-visible", ".wcn-inbox-action-more:focus-visible",
      ".wcn-seg:focus-visible", ".wcn-views .wcn-viewbtn:focus-visible", ".wcn-list-pager .btn:focus-visible"]
      .forEach((sel) => { expect(block, `${sel} lost its ring`).toContain(sel); });
    expect(CSS.split("KEYBOARD FOCUS FOR THE FIVE CONTROLS")[1]).toContain(RING);
  });

  it("does not touch the global reset, which governs the whole product", () => {
    /*
     * The reset lives in core.css (the vendor theme) and this page is not the place to decide it away. Asserted
     * as "we did not author one here" — and NOT as a plain substring search, because the comment above our rule
     * QUOTES the reset, which is how the first version of this test passed for the wrong reason.
     */
    const authored = CSS.replace(/\/\*[\s\S]*?\*\//g, "");
    expect(authored).not.toContain("button:focus-visible");
  });

  it("uses the same ring the surviving controls use, not a new one", () => {
    // `--bs-btn-focus-box-shadow` measured EMPTY on this theme, so deriving from it would draw nothing.
    expect(CSS).toContain(".wcn-row:focus-visible { outline: 2px solid var(--bs-primary)");
    expect(CSS).toContain(RING);
  });
});
