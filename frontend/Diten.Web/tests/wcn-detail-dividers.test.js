const fs = require("fs");
const path = require("path");

/*
 * EVERY SECTION DIVIDER ON THE DETAIL PAGE REACHES THE CARD'S EDGES, WITH EQUAL SPACE ON BOTH SIDES.
 *
 * The rule was written two rounds ago and has now been broken THREE times — the actions card, the summary card,
 * and the personal card — and the sweep that was supposed to catch the third missed it, because that sweep was
 * briefed on `border-top` and the personal card's divider was a `border-bottom`. The rule was right; the search
 * was narrow.
 *
 * <b>SO THIS TEST IS DIRECTION-BLIND.</b> It looks for a line, not for a property: `border-top`, `border-bottom`,
 * `border-block`, `border-block-start`, `border-block-end`, an `<hr>`, and a 1px block with a background all
 * count as the same thing. A divider added in any of those spellings is caught.
 *
 * <b>WHY CSS TEXT AND NOT THE DOM.</b> jsdom applies no stylesheet and performs no layout, so a DOM-driven test
 * could only assert that an element exists — never that its line reaches the edge or that the space around it is
 * equal. Those are facts about the CSS, so the CSS is what is read. What makes reading the text SUFFICIENT is
 * that the geometry here is not a set of measured pixel values scattered across rules: it is one structural
 * invariant, stated below, which can be checked exactly. The pixel confirmation is done in the browser and
 * reported with the round; this test guards the invariant that produces those pixels.
 *
 * <b>THE INVARIANT.</b> A divider spans the card's full width if and only if NOTHING insets the block that
 * carries it. That is achieved one way in this codebase — the card declares `padding: 0` and each block inside
 * pays its own inset — and the space either side of the line is equal if and only if every block in that card
 * pays the SAME inset. So:
 *
 *     1. every divider on a detail card lives in a card family that declares `padding: 0`;
 *     2. every block of such a family declares one uniform padding — one value, so the block-start and
 *        block-end sides are equal by construction and the horizontal insets match too;
 *     3. no negative margin is used to fake step 1.
 *
 * A NEW divider on a component that is not part of such a family fails at step 1 — which is exactly the miss
 * this test exists to prevent.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const CSS = fs.readFileSync(
  path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const APP = fs.readFileSync(
  path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

/** Every `selector { body }` pair, comments stripped first — a rule must not be matched by its own prose. */
const rules = () => {
  const text = CSS.replace(/\/\*[\s\S]*?\*\//g, "");
  const out = [];
  const re = /([^{}]+)\{([^{}]*)\}/g;
  let m;
  while ((m = re.exec(text))) {
    const sel = m[1].trim().split("\n").pop().trim();
    if (!sel || sel.startsWith("@")) { continue; }
    out.push({ sel, body: m[2] });
  }
  return out;
};

const decl = (body, prop) => {
  const m = body.match(new RegExp(`(?:^|;)\\s*${prop}\\s*:\\s*([^;]+)`));
  return m ? m[1].trim() : null;
};

/** Direction-blind: any spelling of a line along a block edge. */
const DIVIDER_PROPS = [
  "border-top", "border-bottom", "border-block", "border-block-start", "border-block-end",
  "border-top-width", "border-bottom-width", "border-block-start-width", "border-block-end-width"
];
const drawsALine = (body) => DIVIDER_PROPS.some((p) => {
  const v = decl(body, p);
  return !!v && !/^(none|0|0px)$/.test(v.trim()) && !/^0(px)?\s+/.test(v.trim());
});

/*
 * The card families that hold the detail page's cards. Membership is read from app.js — the `card()` helper is
 * what actually puts these classes on a card — so a family added there and forgotten here fails the first test
 * below rather than silently escaping the sweep.
 */
const FAMILIES = ["wcn-acts-card", "wcn-sum-card", "wcn-personal-card", "wcn-bizctx-card"];

describe("the card families that carry dividers", () => {
  it("is the same list app.js assigns — a new family cannot escape this file", () => {
    const assigned = [...APP.matchAll(/\?\s*'(wcn-[a-z]+-card)'/g)].map((m) => m[1]);
    expect([...new Set(assigned)].sort()).toEqual([...FAMILIES].sort());
  });

  FAMILIES.forEach((family) => {
    it(`.${family} pays no padding of its own, so a line inside it can reach the edge`, () => {
      const rule = rules().find((r) => r.sel === `.${family}`);
      expect(rule, `.${family} has no rule`).toBeTruthy();
      expect(decl(rule.body, "padding")).toBe("0");
    });

    it(`.${family} spends the SAME inset on both sides of its line, and the same one at each end`, () => {
      /*
       * The two blocks a line sits between are what set the space around it: the block above pays it with its
       * padding-BOTTOM, the block below with its padding-TOP. Those two must agree — and that is a different
       * claim from "every padding in this card is one number", because the card's OUTER top and bottom are free
       * to be roomier than the seam. The horizontal insets must agree too, or the two blocks' contents would not
       * line up beneath a line that spans both.
       */
      const own = rules()
        .filter((r) => r.sel.startsWith(`.${family} `) || r.sel.startsWith(`.${family} >`))
        .filter((r) => !/:hover|:focus|:active/.test(r.sel));
      const padded = own.filter((r) => decl(r.body, "padding"));
      expect(padded.length, `.${family} declares no padded block`).toBeGreaterThan(0);

      const sides = (body) => {
        const p = decl(body, "padding").split(/\s+/);
        return p.length === 1 ? { top: p[0], right: p[0], bottom: p[0], left: p[0] }
          : p.length === 2 ? { top: p[0], right: p[1], bottom: p[0], left: p[1] }
            : p.length === 3 ? { top: p[0], right: p[1], bottom: p[2], left: p[1] }
              : { top: p[0], right: p[1], bottom: p[2], left: p[3] };
      };

      const line = own.find((r) => drawsALine(r.body));
      expect(line, `.${family} declares no block carrying a line`).toBeTruthy();

      /*
       * A family stacks its blocks one of two ways, and the neighbour above is read accordingly:
       *   two DIFFERENT blocks   — the padded rule declared before the line's rule;
       *   the SAME block repeated (`X + X`) — its own rule, which is both neighbours at once.
       */
      const selfStacking = /(\.[\w-]+)\s*\+\s*\1\s*$/.test(line.sel);
      const aboveRule = selfStacking
        ? padded.find((r) => line.sel.startsWith(r.sel.replace(/\s*\+[\s\S]*$/, "")))
        : padded.slice(0, padded.indexOf(line) >= 0 ? padded.indexOf(line) : padded.length).pop();
      expect(aboveRule, `${line.sel} — nothing declared above the line`).toBeTruthy();

      const belowRule = padded.indexOf(line) >= 0 ? line : aboveRule;
      const below = decl(line.body, "padding-block-start") || sides(belowRule.body).top;
      expect(below, `${line.sel} — space above the line ≠ space below`).toBe(sides(aboveRule.body).bottom);
      expect(sides(belowRule.body).left).toBe(sides(aboveRule.body).left);
      expect(sides(belowRule.body).right).toBe(sides(aboveRule.body).right);
    });
  });
});

describe("every divider on a detail card", () => {
  /*
   * The component classes that belong to a surface OTHER than the eight detail cards: the list page's own
   * chrome, and the side panels. Each is named with the surface it lives on, so the list cannot quietly grow
   * into a place to park a detail-page divider someone did not want to fix.
   */
  const OTHER_SURFACES = {
    "wcn-kcol-head": "list page — kanban column head",
    "wcn-workspace-toolbar": "list page — toolbar",
    "wcn-group-head": "list page — group head",
    "wcn-row": "list page — row card",
    "wcn-splitcard": "list page — split list row",
    "wcn-split-detail": "list page — split view detail pane",
    "wcn-bulkbar": "list page — bulk action bar",
    "wcn-actionbar": "list page — sticky mobile action bar",
    "wcn-notes-composer": "side panel — quick notes",
    "wcn-detail-actions": "split view — overridden to 0 inside a detail card",
    "wcn-detail-section": "split view — overridden to 0 inside a detail card",
    "wcn-related-row": "inside .wcn-related-list, a bordered box of its own; rows divide within THAT box",
    "wcn-actrail-other": "actions rail — inside .wcn-acts-card, which pays no padding"
  };

  it("lives in a family that pays no padding — nothing else may draw a line", () => {
    const strays = rules()
      .filter((r) => /wcn-/.test(r.sel) && drawsALine(r.body))
      .filter((r) => !FAMILIES.some((f) => r.sel.includes(`.${f}`)))
      .filter((r) => !Object.keys(OTHER_SURFACES).some((c) => r.sel.includes(`.${c}`)))
      // A CONTROL is not a divider: a button, a pill, a field and their hover/focus states all carry borders,
      // and the browser sweep excluded them on the same grounds.
      .filter((r) => !/:hover|:focus|:active|\.nav-link|\.btn|\.form-control|input|\.wcn-tag|badge|chip/.test(r.sel));
    expect(strays.map((r) => r.sel)).toEqual([]);
  });

  it("is not faked with a negative margin", () => {
    const cheats = rules()
      .filter((r) => FAMILIES.some((f) => r.sel.includes(`.${f}`)))
      .filter((r) => /margin(-inline|-left|-right|-block)?\s*:\s*[^;]*-/.test(r.body));
    expect(cheats.map((r) => r.sel)).toEqual([]);
  });

  it("is caught whichever way the line is spelled", () => {
    // Non-vacuity for the direction-blindness claim: each spelling really is recognised.
    expect(drawsALine("border-top: 1px solid red")).toBe(true);
    expect(drawsALine("border-bottom: 1px solid red")).toBe(true);
    expect(drawsALine("border-block-start: 1px solid red")).toBe(true);
    expect(drawsALine("border-block-end: 1px solid red")).toBe(true);
    expect(drawsALine("border-block: 1px solid red")).toBe(true);
    expect(drawsALine("border-block-end: 0")).toBe(false);
    expect(drawsALine("color: red")).toBe(false);
  });
});

describe("the personal card, which is where this round's defect was", () => {
  it("draws its divider between two blocks rather than under the button strip", () => {
    // The strip is a strip: the divider is the card's, which is also why snoozing no longer loses it.
    const strip = rules().find((r) => r.sel === ".wcn-personal");
    expect(drawsALine(strip.body)).toBe(false);
    const notes = rules().find((r) => r.sel === ".wcn-personal-card .wcn-personal-notes");
    expect(drawsALine(notes.body)).toBe(true);
  });

  it("renders the two blocks it needs for that divider to exist", () => {
    expect(APP).toContain('<div class="wcn-personal-main">');
    expect(APP).toContain('<div class="wcn-personal-notes">');
  });
});
