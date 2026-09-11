const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * MEASURED (2026-09-11): the detail page's "related records" card could never render real data.
 *
 * `renderRelated` in app.js gated on `hasCap(item, 'related')` and then read `item.related`. The executable
 * contract (fixture-contract.js CAPABILITIES / DATA_CAPABILITIES, lines ~176/192) names both the CAPABILITY and
 * the DATA FIELD `relatedRecords` — `task-detail-resolver.js` maps `relatedRecords` onto itself, and the
 * projection now emits `relatedRecords: [{ id, type, title, link }]` (WorkAggregationModels.cs /
 * TaskWorkItemProvider.cs). `hasCap(item, cap)` is `item.workItemCapabilities.indexOf(cap) >= 0`
 * (app.js:3132) — 'related' never appears in that array, only 'relatedRecords' does — so the gate was shut for
 * every item, fixture or real, regardless of data.
 *
 * The mapper needed NO fix: `adaptProjection`'s `Object.assign({}, dto)` (work-items-api.js:62) and
 * `toPresentation`'s `clone(fixture)` (mock-data.js:95/326) both copy every field through — including
 * `relatedRecords` — because neither whitelists fields. Only `renderRelated`'s two stale names were wrong.
 *
 * MUTATION GUARD: this file goes red if `renderRelated` (or anything else in app.js) reverts to the old names.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const APP_SRC = () => fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

// Comments may DISCUSS the retired names — that is the record of why. Code may not read them. Same discipline
// as wcn-icon-map-and-dead-panels.test.js and wcn-detail-three-regions.test.js.
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");

const TASK_ID = "b6b6b6b6-b6b6-4b6b-8b6b-b6b6b6b6b6b6";

/** A minimal work item the executable contract accepts — the same base shape proven valid across the detail-page
 *  suite (wcn-detail-three-regions.test.js's `projectionItem`), duplicated locally rather than imported: this
 *  file builds its own fixture instead of reaching into fixture-contract.js/canonical fixtures for one. */
const baseItem = (overrides) => Object.assign({
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
  title: { kind: "display", text: "Related records test task", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", isCurrentUser: true },
  requester: { id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", displayName: "Test Requester" },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-09-30T00:00:00+00:00"
}, overrides);

const boot = (item) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item]
});

const RECORDS = [
  { id: "PO-1001", type: "document", title: "Purchase Order 1001", link: "/Procurement/PurchaseOrders/PO-1001" },
  { id: "REQ-42", type: "parent", title: "Requisition 42", link: "/Procurement/Requisitions/REQ-42" }
];

describe("the related-records card renders the contract's actual field, not the stale one", () => {
  it("renders one row per record — type, title and href — when the item declares the capability", async () => {
    await boot(baseItem({
      workItemCapabilities: ["planning", "execution", "relatedRecords"],
      relatedRecords: RECORDS
    }));

    const rows = [...app().querySelectorAll(".wcn-related-row")];
    expect(rows, "no .wcn-related-row rendered at all").toHaveLength(2);

    expect(rows[0].getAttribute("href")).toBe(RECORDS[0].link);
    expect(rows[0].querySelector("strong").textContent).toBe(RECORDS[0].title);
    expect(rows[0].querySelector(".wcn-related-type").textContent).toBe("RelatedTypeDocument");

    expect(rows[1].getAttribute("href")).toBe(RECORDS[1].link);
    expect(rows[1].querySelector("strong").textContent).toBe(RECORDS[1].title);
    expect(rows[1].querySelector(".wcn-related-type").textContent).toBe("RelatedTypeParent");
  });

  it("renders nothing when the item does not declare the capability", async () => {
    // workItemCapabilities carries no 'relatedRecords', and the fixture carries no relatedRecords field at all.
    await boot(baseItem());
    expect(app().querySelector(".wcn-related-row")).toBeNull();
    expect(app().querySelector(".wcn-related-list")).toBeNull();
  });

  it("gates on 'relatedRecords' and reads item.relatedRecords — never the stale 'related' names", () => {
    const stripped = code(APP_SRC());
    expect(stripped, "still gates on the stale capability name").not.toMatch(/hasCap\(item,\s*'related'\)/);
    expect(stripped, "still reads the stale item.related field").not.toMatch(/item\.related\b/);
  });
});
