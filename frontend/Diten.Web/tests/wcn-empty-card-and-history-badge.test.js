const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ TWO THINGS THE TASK CENTER SAID WRONG, MEASURED IN A MANAGEMENT DEMO (2026-09-02) ═════════════════════
 *
 * ── 1. THE EMPTY-STATE CARD THAT WAS NEVER A CARD ────────────────────────────────────────────────────────
 * `.wcn-empty` declared `background: var(--bs-card-bg)` and `border-radius: var(--bs-card-border-radius)`.
 * Both are REAL declarations and both resolve to NOTHING, because the theme defines those two custom
 * properties INSIDE its `.card` rule — not at `:root`. An element that is not a `.card` therefore inherits no
 * value for them, the declarations become invalid at computed-value time, and the surface renders transparent
 * with square corners. The hairline border was the only part that ever painted, because `--bs-border-color`
 * IS a root token.
 *
 * So the defect is not "a shadow is missing" — it is that a non-card was asked to spend a card's tokens. The
 * cure is to make the empty state a real `.card` and let the theme decide what a card looks like, including
 * the skins where a card carries a RING instead of a shadow ([data-skin=bordered] redefines
 * --bs-card-box-shadow). Copying today's shadow value into this file would have looked identical on the
 * demo machine and diverged the first time anyone switched skin.
 *
 * The CSS guard below is derived, never restated: it reads the theme, works out which custom properties are
 * card-scoped, and refuses to let a non-card rule consume one. Restoring the old declarations turns it red.
 *
 * ── 2. THE RED BADGE OVER CLOSED WORK ────────────────────────────────────────────────────────────────────
 * The owner cancelled his own task, it landed in Geçmiş — correct — and the tab grew a red "1", which says
 * "one thing here is waiting for you". Nothing in Geçmiş waits for anybody: it is where work goes when it has
 * stopped. app.js states that rule itself, above `tabCount`:
 *
 *     "A badge says 'work waiting for you in this tab'. Work you parked is not waiting for you"
 *
 * ⚠ WC-D3's partial-board "+" was read before deciding, because it draws a badge EVEN AT ZERO. Its reason is
 * that a count a reader ACTS on must never print a confident zero over an incomplete board. Geçmiş prints no
 * count a reader acts on at all, so there is no number left for the "+" to qualify — and a grey "0+" on closed
 * work would be the same false claim in a quieter colour. Geçmiş therefore carries no badge in either state,
 * and the tests below pin BOTH states so a later "fix" cannot reintroduce one through the partial branch.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const CSS = () => read("wwwroot", "assets", "css", "backbone-custom.css");
const THEME = () => read("wwwroot", "assets", "vendor", "css", "core.css");

const ID = (n) => `7d2b41c0-19ae-4f77-b3d6-90cc51e7a10${n}`;

/** A REAL projection item, the shape TaskWorkItemProvider emits. */
const item = (n, overrides) => Object.assign({
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
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

/** The owner's own case: a task he opened and then cancelled. Terminal, so `inTab` files it under Geçmiş. */
const cancelledItem = (n) => item(n, {
  normalizedStatus: "Cancelled",
  taskLifecycle: "Cancelled",
  executionState: "notApplicable",
  nativeStatus: { code: "Cancelled", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Cancelled" } },
  closedAt: "2026-09-01T09:00:00Z",
  viewerRelation: "initiator"
});

const tabButton = (key) => document.getElementById(`wcn-tab-${key}`);
const badgeOn = (key) => tabButton(key)?.querySelector(".wcn-tab-count") || null;
const clickTab = async (key) => {
  tabButton(key).click();
  await new Promise((resolve) => setTimeout(resolve, 0));
};
const rowIds = () => Array.from(app().querySelectorAll("[data-wcn-row]"))
  .map((node) => node.getAttribute("data-wcn-row"));

// ══ 1. the empty state is a card, and the CSS cannot pretend otherwise ══════════════════════════════════

/**
 * Every custom property the theme declares, split by whether a non-card element can actually resolve it.
 *
 * "Root-scoped" means declared by a rule whose selector list mentions no element other than the document root
 * (`:root`, `html`, `[data-bs-theme=…]`, `[data-skin=…]` and their combinations). Anything declared ONLY inside
 * a rule that also names a class — `.card` is the one that matters here — is unavailable to elements outside it.
 */
const themeTokenScopes = () => {
  const css = THEME().replace(/\/\*[\s\S]*?\*\//g, "");
  const rootScoped = new Set();
  const scopedElsewhere = new Set();

  // Top-level rules only: `selector { body }` with no nested braces. That covers every declaration block the
  // theme writes its tokens in, including the ones inside @media (whose own header carries no custom property).
  const rule = /([^{}]+)\{([^{}]*)\}/g;
  let match;
  while ((match = rule.exec(css)) !== null) {
    const selector = match[1].trim();
    if (!selector || selector.startsWith("@")) { continue; }

    const declaredHere = (match[2].match(/--[\w-]+(?=\s*:)/g) || []);
    if (declaredHere.length === 0) { continue; }

    // A selector every element of the document can be under, i.e. one that names no class/id/tag of its own.
    const isRoot = selector.split(",")
      .every((part) => /^(:root|html|body)?(\s*\[[^\]]+\])*$/.test(part.trim()));

    declaredHere.forEach((name) => (isRoot ? rootScoped : scopedElsewhere).add(name));
  }
  return { rootScoped, scopedElsewhere };
};

/** The declaration block of a top-level rule in backbone-custom.css, by its exact selector list. */
const ruleBody = (css, selector) => {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(/\s+/g, "\\s*");
  const found = new RegExp(`(?:^|\\})\\s*${escaped}\\s*\\{([^}]*)\\}`, "m").exec(css);
  return found ? found[1] : null;
};

describe("the Task Center's empty state is a card, not a card-shaped wish", () => {
  test("the theme really does keep --bs-card-bg out of reach of a non-card — the whole premise", () => {
    // Non-vacuity: if this ever flips, the guard below is measuring a rule that no longer exists and every
    // assertion under it would pass for the wrong reason.
    const { rootScoped, scopedElsewhere } = themeTokenScopes();

    expect(rootScoped.size, "no root-scoped theme tokens were found at all").toBeGreaterThan(20);
    expect(scopedElsewhere.has("--bs-card-bg"), "--bs-card-bg is no longer card-scoped").toBe(true);
    expect(rootScoped.has("--bs-card-bg"), "--bs-card-bg is reachable from :root after all").toBe(false);
    expect(rootScoped.has("--bs-border-color"), "--bs-border-color should be a root token").toBe(true);
  });

  test("the .wcn-empty rule spends only tokens a non-card element can actually resolve", () => {
    const { rootScoped } = themeTokenScopes();
    const body = ruleBody(CSS(), ".wcn-empty, .wcn-system-page") ?? ruleBody(CSS(), ".wcn-empty");

    // Vacuity guard: an absent rule must fail loudly rather than satisfy the loop below with zero iterations.
    expect(body, "no .wcn-empty rule found in backbone-custom.css").not.toBeNull();

    const consumed = Array.from(new Set(body.match(/var\(\s*(--[\w-]+)/g) || []))
      .map((token) => token.replace(/var\(\s*/, ""));

    const unreachable = consumed.filter((token) => !rootScoped.has(token));
    expect(
      unreachable,
      `${unreachable.join(", ")} is declared by the theme only inside a rule the empty state does not match, `
      + "so the declaration using it is invalid at computed-value time and paints nothing. Wear the theme's own "
      + ".card class instead of borrowing a card's tokens."
    ).toEqual([]);
  });

  test("every empty state the app paints wears the theme's own .card class", async () => {
    // Boot a board with one item and land on a tab that item is not in, so the real empty state renders.
    await bootSurface({ rootAttrs: "", items: [item(1)] });
    await clickTab("havuz");

    const empty = app().querySelector(".wcn-empty");
    // Non-vacuity: a page that rendered nothing must not pass this file.
    expect(empty, "no empty state rendered — the assertion below would be vacuous").not.toBeNull();
    expect(empty.textContent).toContain("EmptyPoolTitle");

    expect(
      empty.classList.contains("card"),
      "the empty state is not a .card, so it resolves no card background, radius or shadow and reads as a page "
      + "that failed to draw"
    ).toBe(true);
  });

  test("the loading surface is a card too — it is the same panel, one moment earlier", async () => {
    await bootSurface({ rootAttrs: "", items: [], neverResolve: true });

    const loading = app().querySelector(".wcn-system-page");
    expect(loading, "no loading surface rendered").not.toBeNull();
    expect(loading.classList.contains("card")).toBe(true);
  });
});

// ══ 2. Geçmiş carries no badge, in either board state ═══════════════════════════════════════════════════

describe("closed work is not work waiting for you", () => {
  test("the row really is in Geçmiş — without this every assertion below is vacuous", async () => {
    await bootSurface({ rootAttrs: "", items: [cancelledItem(1)] });
    await clickTab("history");

    expect(rowIds()).toEqual([ID(1)]);
  });

  test("Geçmiş draws no count badge over a task the reader cancelled", async () => {
    await bootSurface({ rootAttrs: "", items: [cancelledItem(1)] });

    expect(
      badgeOn("history"),
      "Geçmiş wears a badge, which claims one thing there is waiting for the reader. It is closed work; "
      + "nothing there waits."
    ).toBeNull();
  });

  test("…and none over a PARTIAL board either — a '+' qualifies a count, and Geçmiş prints none", async () => {
    await bootSurface({
      rootAttrs: "",
      items: [cancelledItem(1)],
      unavailableSources: [{ providerCode: "documents", reason: "timeout" }]
    });

    expect(badgeOn("history")).toBeNull();
  });

  test("the other tabs keep their badges — this removes one claim, not the counter", async () => {
    await bootSurface({ rootAttrs: "", items: [item(1), cancelledItem(2)] });

    const mine = badgeOn("islerim");
    expect(mine, "İşlerim lost its badge; the fix went too wide").not.toBeNull();
    expect(mine.textContent.trim()).toBe("1");
  });

  test("a partial board still marks the LIVE tabs with the WC-D3 '+', even at zero", async () => {
    await bootSurface({
      rootAttrs: "",
      items: [cancelledItem(1)],
      unavailableSources: [{ providerCode: "documents", reason: "timeout" }]
    });

    const mine = badgeOn("islerim");
    expect(mine, "WC-D3's zero-that-might-not-be-zero badge is gone from İşlerim").not.toBeNull();
    expect(mine.textContent.trim()).toBe("0+");
  });
});
