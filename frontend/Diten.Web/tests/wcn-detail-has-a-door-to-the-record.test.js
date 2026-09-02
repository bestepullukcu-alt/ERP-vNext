const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ "I OPENED THIS TASK AND I CANNOT CHANGE ITS TITLE" ════════════════════════════════════════════════════
 *
 * MEASURED (2026-09-02, management demo): the owner opened a task he had created himself in the Task Center
 * and found no way to change its title or description. /Tasks/{id}/Edit exists and works; nothing on this
 * surface leads there.
 *
 * ── THE DECISION WAS ALREADY MADE, AND IT WAS MADE ON PURPOSE ────────────────────────────────────────────
 * Three measurements, before deciding anything:
 *
 *  1. `Views/Tasks/Details.cshtml` opens with "MOD-0024 — read-only detail. The Task Center remains the
 *     personal action surface; this page is the module's own record view". The split is written down.
 *  2. The action rail does not hold a hand-written list of verbs. Every entry comes from the PROJECTION —
 *     a provider's own engine transitions — and MOD-0024 aggregates providers it does not own (documents,
 *     enterprise strategy, …). "Edit the title" is not a transition and means nothing for most of them, so
 *     an `edit` action in the rail would be MOD-0024 reaching into records that are not its own. That is the
 *     same boundary the approval work drew: this module reports and hands over, it does not decide.
 *  3. The product already HAS an idiom for "leave here and continue in the full surface", used twice:
 *     `ActionCompleteInSource` (work whose `actionDepth` is `deeplink`) and `SubtaskOpenFullDetail` — a
 *     secondary button with an external-link glyph.
 *
 * So this is option (a): the read-only rail is a real decision and stays. What was missing is the DOOR — the
 * reader was left in a cul-de-sac, which is this product's recurring defect, not a missing feature. An inline
 * item now offers the same "open the source record" link the deeplink items always had.
 *
 * ⚠ WHY IT IS NOT LABELLED AS A PERMISSION. The link is navigation; /Tasks/{id} decides what it offers, and
 * its header carries the Edit button. Guessing the reader's rights in the browser would either hide the way
 * out from somebody who has it, or claim to know something only the server knows.
 *
 * ⚠ WHY IT DOES NOT REPLACE THE PRIMARY ACTION. On a `deeplink` item the source link IS the lead, because the
 * work cannot be finished here. On an inline item the work CAN be finished here — "Tamamla" is still the one
 * filled button, and the door sits beneath it as a quiet secondary. Making it the lead would tell every
 * reader to leave a page that does its job.
 */

const ID = (n) => `9a44c1de-2b70-4d81-8f0c-6be1c7a5f30${n}`;

const action = (code, overrides) => Object.assign({
  code,
  label: { kind: "resource", key: `WorkAggregation_Action_${code}` },
  semanticType: code,
  enabled: true,
  source: "provider",
  disabledReasonCode: null,
  disabledReason: null,
  requiresConfirmation: false,
  requiresReason: false,
  requiresEvidence: false,
  supportsBulk: false,
  riskLevel: "normal"
}, overrides);

/** A task the reader opened and holds — MOD-0024's own, finishable here (`actionDepth: "inline"`). */
const ownTask = (n, overrides) => Object.assign({
  fixtureKind: "workItem",
  id: ID(n),
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
  title: { kind: "display", text: `Görev ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: ID(n),
    deepLink: `/Tasks/${ID(n)}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [action("complete")],
  primaryActionCode: "complete",
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

const openDetail = async (items, n) => {
  await bootSurface({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${ID(n)}"`, items });
};

const doorLink = () => app().querySelector("[data-wcn-source-link]");
const actionsCard = () => app().querySelector(".wcn-acts");

/** The source card's own button — the second control BL-329 measured beside the rail's door. */
const cardOpenButton = () => app().querySelector("[data-wcn-open]");
/** The source card itself, which holds the record's IDENTITY and outlives its button. */
const sourceCard = () => app().querySelector(".wcn-source");
/*
 * EVERY control on this surface that leads to the source record, counted together — the rail's bare door, the
 * rail's deeplink lead, and the source card's button. Counting them as three separate selectors is how the
 * duplicate survived: each one was asserted present on its own, and nobody counted the total.
 */
const sourceControls = () => app().querySelectorAll(
  "[data-wcn-source-link], [data-wcn-depth-link], [data-wcn-open]"
);

describe("the Task Center's detail is not a cul-de-sac", () => {
  it("really renders the actions card for this item — nothing below means anything without it", async () => {
    await openDetail([ownTask(1)], 1);

    expect(actionsCard(), "no actions card rendered on the detail surface").not.toBeNull();
    expect(app().textContent).toContain("Görev 1");
  });

  it("offers a door to the record the work item stands for", async () => {
    await openDetail([ownTask(1)], 1);

    const door = doorLink();
    expect(
      door,
      "the reader can act on the task and cannot reach the record behind it — /Tasks/{id}/Edit exists and "
      + "nothing on this page leads there"
    ).not.toBeNull();
    expect(door.getAttribute("href")).toBe(`/Tasks/${ID(1)}`);
    // The key, not a translation: the harness echoes keys, so this pins what the code chose to say.
    expect(door.textContent).toContain("ActionOpenInSource");
  });

  it("the door lives in the actions card, under the primary — it is a way out, not the thing to do", async () => {
    await openDetail([ownTask(1)], 1);

    expect(actionsCard().contains(doorLink()), "the door is drawn outside the actions card").toBe(true);
    // Still exactly one filled button: the card's one-fill rule is untouched by adding a link.
    expect(actionsCard().querySelectorAll(".wcn-act-fill")).toHaveLength(1);
    expect(doorLink().classList.contains("wcn-act-fill")).toBe(false);
  });

  it("draws no door when the projection gives no destination, rather than inventing one", async () => {
    // A provider that publishes no deep link. The contract permits it, and a link to nowhere is worse than none.
    await openDetail([ownTask(1, {
      source: {
        providerCode: "tasks",
        providerContractVersion: "1.0",
        objectType: "task",
        objectId: ID(1),
        deepLink: null
      }
    })], 1);

    expect(actionsCard(), "the card itself vanished — this assertion would be vacuous").not.toBeNull();
    expect(doorLink()).toBeNull();
  });

  it("does not draw a SECOND source link on work that already leads with one", async () => {
    /*
     * A `deeplink` item cannot be finished here, so `ActionCompleteInSource` is already its lead button. A
     * quiet second link to the same place, right underneath, would be the page saying one thing twice.
     */
    await openDetail([ownTask(1, { actionDepth: "deeplink", sourceModuleName: "Doküman" })], 1);

    expect(app().textContent).toContain("ActionCompleteInSource");
    expect(doorLink(), "the deeplink lead and the door both point at the source").toBeNull();
    // …and the source card must not quietly become the second control either (BL-329, below).
    expect(sourceControls(), "the deeplink lead has company again").toHaveLength(1);
  });

  /*
   * ══ BL-329 — TWO DOORS, ONE ROOM (measured 2026-09-02, live session) ═══════════════════════════════════
   *
   * The tests above proved the rail's door EXISTS. None of them counted, so nobody noticed that the source
   * card was drawing a second control to the identical href, one card lower, under a different label:
   *
   *     rail   <a data-wcn-source-link href="/Tasks/{id}">   "Kaynak kayıtta aç"   (ActionOpenInSource)
   *     card   <button data-wcn-open="{id}">                 "Kaynak kaydını aç"   (DetailOpenSource)
   *
   * MEASURED before the fix, on an inline task carrying `source.deepLink`: railLinks=1, cardButtons=1.
   *
   * The rule against this was ALREADY WRITTEN, in the source card's own comment — the button stands down when
   * the actions card has taken the destination. It was expressed as `actionDepth === 'deeplink'`, which is one
   * of the rail's TWO doors; `sourceDoor` (the bare link on inline work) came later and the guard never learned
   * about it. So this is the written rule reaching the case it always meant, not a new rule.
   *
   * OWNER'S DECISION (2026-09-02): the RAIL keeps the door — the rail is where this page collects what a reader
   * can do — and the card's button is withdrawn. The card's identity rows are the reason the card exists and
   * they stay, which the third test below pins so "withdraw the button" cannot quietly become "drop the card".
   */
  it("draws exactly ONE control to the source record, and it is the rail's", async () => {
    await openDetail([ownTask(1)], 1);

    expect(sourceControls(), "two controls, one destination — BL-329 all over again").toHaveLength(1);
    expect(doorLink(), "the rail lost the door it is supposed to keep").not.toBeNull();
    expect(cardOpenButton(), "the source card is still drawing its own button beside the rail's").toBeNull();
  });

  it("draws NO control at all when the record has no destination — a dead button is not a door", async () => {
    /*
     * MEASURED before the fix: railLinks=0, cardButtons=1. The card drew its button anyway, and its handler
     * reads `if (item && item.deepLink)` — so the reader got a control that did nothing when pressed.
     */
    await openDetail([ownTask(1, {
      source: {
        providerCode: "tasks",
        providerContractVersion: "1.0",
        objectType: "task",
        objectId: ID(1),
        deepLink: null
      }
    })], 1);

    expect(sourceCard(), "the card itself vanished — the assertions below would be vacuous").not.toBeNull();
    expect(sourceControls()).toHaveLength(0);
  });

  it("keeps the source card's identity rows — only the button was withdrawn, not the card", async () => {
    await openDetail([ownTask(1)], 1);

    expect(sourceCard(), "the source card went with its button; the reader can no longer see WHICH record")
      .not.toBeNull();
    expect(sourceCard().querySelector(".wcn-source-list"), "the card is left with a heading and nothing under it")
      .not.toBeNull();
    expect(sourceCard().textContent).toContain("DetailNativeStatusInSource");
  });

  it("keeps the card's button on work the rail draws no door for — closing a duplicate must not open a dead end", async () => {
    /*
     * ⚠ MEASURED, AND IT IS WHY THE GUARD IS NOT SIMPLY "IS THERE AN HREF". On an item with no applicable
     * action — a finished task, or one that is not yours — `renderActionRail` returns the "ActionsNoneClosed"
     * card and reaches NEITHER of its doors: railLinks=0. Withdrawing the card's button on the href alone would
     * have left a closed task with no way to its own record, which is the cul-de-sac this whole file exists to
     * close. The count is still exactly one; it is simply the card's turn to carry it.
     */
    await openDetail([ownTask(1, {
      normalizedStatus: "Done",
      taskLifecycle: "Done",
      executionState: "notApplicable",
      closedAt: "2026-08-01T10:00:00Z",
      actions: [],
      primaryActionCode: null
    })], 1);

    expect(actionsCard(), "the rail DID draw its card, so this scenario is not the one being described").toBeNull();
    expect(doorLink(), "the rail drew a door after all — the reason for this exception is gone").toBeNull();
    expect(sourceControls(), "a closed task with a deep link and no way to reach it").toHaveLength(1);
    expect(cardOpenButton()).not.toBeNull();
  });

  it("the list rows are untouched — this is a detail-surface door, not a new row action", async () => {
    await bootSurface({ rootAttrs: "", items: [ownTask(1)] });
    // The list boots on Inbox; this item is work the reader HOLDS, so it lives in İşlerim.
    document.getElementById("wcn-tab-islerim").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(app().querySelectorAll("[data-wcn-row]")).toHaveLength(1);
    expect(doorLink(), "a source link leaked into the list rows").toBeNull();
  });
});
