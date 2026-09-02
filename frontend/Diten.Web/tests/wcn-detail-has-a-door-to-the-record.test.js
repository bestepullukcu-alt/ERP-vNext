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
