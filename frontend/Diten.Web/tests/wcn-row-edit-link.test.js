const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ "IT WORKS, BUT IT IS TOO MUCH TROUBLE" ══════════════════════════════════
 *
 * The owner could already edit a task they raised: Task Center detail →
 * "Kaynak kayıtta aç" → /Tasks/{id} → "Düzenle". Measured end to end, it saves —
 * the title changed and came back changed from the API. What they asked for was
 * the same destination from the LIST, on the rows that are theirs to correct.
 *
 * ⚠ NO NEW ROW LANGUAGE. The row's rule (app.js, above `unsnoozeBtn`) already
 * settled what may be added here: the pin's control, in `.wcn-row-actions`, with
 * a title/aria pair — "this row has no menu, and adding one for a single item
 * would be a second vocabulary for the same job". So this is an <a>, borrowing
 * `.wcn-pin`'s geometry, and NOT a toggle: no `aria-pressed`, never `.pinned`.
 *
 * ⚠ THE TRAP THIS FILE EXISTS TO HOLD SHUT. `toPresentation` overwrites
 * `item.requester` — the OBJECT carrying `isCurrentUser` — with a display STRING.
 * The link first shipped inert for exactly that reason: `requester?.isCurrentUser`
 * read off a string is `undefined`, so the condition was false on every row and
 * nothing rendered, with no error to notice. The fact is now recorded as
 * `raisedByViewer` BEFORE the overwrite. `viewerRole` is not a substitute: it is
 * an if/else that reports 'Owner' for a task you raised AND own, which is the
 * common case.
 */

const ID = (n) => `aaaaaaaa-aaaa-aaaa-aaaa-${String(n).padStart(12, "0")}`;
const ME = "dddddddd-dddd-dddd-dddd-dddddddddddd";
const SOMEONE_ELSE = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

const action = (code) => ({
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
});

const task = (n, overrides) => Object.assign({
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
    providerCode: "tasks", providerContractVersion: "1.0",
    objectType: "task", objectId: ID(n), deepLink: `/Tasks/${ID(n)}`
  },
  assignee: { id: ME, isCurrentUser: true },
  requester: { id: ME, isCurrentUser: true },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [action("complete")],
  primaryActionCode: "complete",
  concurrency: { kind: "version", token: "1" },
  waitingContext: null, escalation: null, dueAt: null
}, overrides);

/*
 * The surface opens on the Inbox, and these rows are owned work — so the tab is switched, the same
 * way workcenter-next-list-page.test.js does it. Booting without this paints zero rows and every
 * assertion below would pass against an empty list.
 */
const rowsFor = async (items, tabKey = "islerim") => {
  await bootSurface({ rootAttrs: "", items });
  const tab = app().querySelector(`[data-wcn-tab="${tabKey}"]`);
  expect(tab, `the ${tabKey} tab is gone — this file can no longer see its own rows`).toBeTruthy();
  tab.click();
  await new Promise((resolve) => setTimeout(resolve, 0));
  expect(app().querySelectorAll("[data-wcn-row]").length,
    "no rows painted; every assertion below would be vacuous").toBeGreaterThan(0);
  return app();
};
const editLinks = (root) => [...root.querySelectorAll("[data-wcn-edit]")];

describe("editing the task you raised, from the row", () => {
  it("offers the link on a task the reader raised and still holds open", async () => {
    const root = await rowsFor([task(1)]);
    const links = editLinks(root);
    expect(links, "no edit link on a row that qualifies").toHaveLength(1);
    expect(links[0].getAttribute("href"), "the link does not point at the record's edit form")
      .toBe(`/Tasks/${ID(1)}/Edit`);
  });

  it("survives the mapper overwriting `requester` with a display string", async () => {
    /*
     * The regression this file was written for. Reading `requester.isCurrentUser` at render time
     * returns undefined, because by then `requester` is a STRING — so the guard is that the row
     * still renders the link even though the object is long gone.
     */
    const root = await rowsFor([task(1)]);
    expect(editLinks(root), "the link vanished when the person object did").toHaveLength(1);
  });

  it("says nothing on someone else's task — theirs to correct, not yours", async () => {
    const root = await rowsFor([task(2, { requester: { id: SOMEONE_ELSE, isCurrentUser: false } })]);
    expect(editLinks(root)).toHaveLength(0);
  });

  it("says nothing once the task is closed — that is a rewrite, not a correction", async () => {
    for (const lifecycle of ["Done", "Cancelled"]) {
      // Closed work lives in Geçmiş, not İşlerim — asserting on the wrong tab would find an empty
      // list and pass for the wrong reason. The row-count guard in rowsFor is what caught that.
      const root = await rowsFor([task(3, {
        taskLifecycle: lifecycle, normalizedStatus: lifecycle,
        executionState: "notApplicable", actions: [], primaryActionCode: null
      })], "history");
      expect(editLinks(root), `a ${lifecycle} task still offered editing`).toHaveLength(0);
    }
  });

  it("says nothing for another provider's record — /Tasks/{id}/Edit is not its form", async () => {
    const root = await rowsFor([task(4, {
      lifecycleOwner: "documents",
      source: {
        providerCode: "documents", providerContractVersion: "1.0",
        objectType: "document", objectId: ID(4), deepLink: `/Documents/${ID(4)}`
      }
    })]);
    expect(editLinks(root)).toHaveLength(0);
  });

  it("borrows the row's existing control and stays a link, not a toggle", async () => {
    const root = await rowsFor([task(1)]);
    const link = editLinks(root)[0];
    expect(link.tagName, "a navigation control was rendered as a button").toBe("A");
    expect(link.className, "the row grew a second vocabulary instead of reusing the pin's")
      .toContain("wcn-pin");
    expect(link.hasAttribute("aria-pressed"), "a link claimed a toggle's state").toBe(false);
    expect(link.className, "a navigation link claimed the pin's ON state").not.toContain("pinned");
    // It sits where the row keeps its controls, not loose in the body.
    expect(link.closest(".wcn-row-actions"), "the link is not in the row's action area").toBeTruthy();
    // Named for a reader, in both channels the pin uses.
    expect(link.getAttribute("title")).toBeTruthy();
    expect(link.getAttribute("aria-label")).toBe(link.getAttribute("title"));
    // NOT asserted here: that the title is TRANSLATED. This harness echoes resource keys back on
    // purpose, so the check would be meaningless. It was measured on the running page instead —
    // "Görevi düzenle", after the resx reached the client through _L10n.cshtml's auto-enumeration.
    expect(link.getAttribute("title")).toBe("ActionEditRecord");
  });
});
