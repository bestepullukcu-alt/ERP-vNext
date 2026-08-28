const fs = require("fs");
const path = require("path");

/*
 * A small cleanup round, both items in app.js.
 *
 *   BL-245 — "Görevi iptal et" wore the default forward arrow, because the icon map had no `cancel`.
 *   BL-244 — five click handlers listening for markup deleted a round earlier, plus everything they held up.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

/** Every shipped front-end file — a deletion is only real if nothing anywhere still names it. */
const shipped = () => {
  const out = [];
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== "vendor" && e.name !== "node_modules") { walk(p); } }
    else if (/\.(js|cshtml|css)$/.test(e.name)) { out.push(p); }
  });
  walk(web("wwwroot", "assets"));
  walk(web("Views"));
  return out;
};
// Comments may DISCUSS a deletion — that is the record of why. Code may not perform it.
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");

describe("BL-245 — a cancellation looks like a cancellation", () => {
  it("maps `cancel` onto the family it belongs to", () => {
    /*
     * MEASURED (task 848a624f, the narrow bar's menu): `cancel` was absent from `inboxActionIcon`, so
     * "Görevi iptal et" fell to the default `bx-right-arrow-alt` — red text, a neutral icon, pointing FORWARD
     * at a task that is not going forward.
     *
     * MUTATION GUARD: drop `cancel` from the map and this goes red.
     */
    expect(APP).toContain("cancel: 'bx-x-circle'");
    // Same glyph as its two siblings, deliberately: one family, one shape. The label carries the meaning.
    const map = APP.split("const inboxActionIcon")[1].split("}[action.key]")[0];
    ["reject", "decline", "cancel"].forEach((key) => {
      expect(map, `${key} left the negative family`).toContain(`${key}: 'bx-x-circle'`);
    });
  });

  it("gives the lifecycle verbs their own glyphs, and only them", () => {
    /*
     * MEASURED: 26 drawable codes, 12 mapped, and the fourteen on the default included `start` and `complete`
     * — the two most-used acts — while rarities carried bespoke glyphs. Frequency order fixed; vocabulary not
     * grown.
     *
     * MUTATION GUARD: drop any one of these and this goes red naming it.
     */
    const map = APP.split("const inboxActionIcon")[1].split("}[action.key]")[0];
    const VERBS = {
      // One movement, one glyph — resuming is starting again.
      start: "bx-play", resume: "bx-play",
      // NOT `bx-check`: that is `accept` ("I take this on"). Finishing is the second tick.
      complete: "bx-check-double",
      // Out of the pool and back into it, drawn as the pair they are.
      claim: "bx-user-plus", release: "bx-user-minus",
      submitReview: "bx-send"
    };
    Object.entries(VERBS).forEach(([key, glyph]) => {
      expect(map, `${key} lost its glyph`).toContain(`${key}: '${glyph}'`);
    });
  });

  it("EVERY mapped glyph exists in the icon font", () => {
    /*
     * ⚠ THE ASSERTION A NAME-ONLY TEST CANNOT MAKE. An invented class renders an empty square and passes any
     * check that only looks at the source. So each glyph is looked up in the stylesheet that actually defines
     * it — a typo fails here rather than on someone's screen.
     */
    const css = fs.readFileSync(web("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css"), "utf8");
    const map = APP.split("const inboxActionIcon")[1].split("}[action.key]")[0];
    const glyphs = [...new Set((map.match(/'(bx-[a-z-]+)'/g) || []).map((g) => g.slice(1, -1)))];
    expect(glyphs.length, "the map was not read").toBeGreaterThan(10);
    glyphs.forEach((g) => {
      expect(css.includes(`.${g}`) || css.includes(`"${g}"`), `${g} is not in the icon font`).toBe(true);
    });
  });

  it("leaves seven codes on the default DELIBERATELY, and says so", () => {
    // An icon that says nothing is noise; 26 glyphs is a dictionary nobody learns. The reason is in the code
    // so the next reader does not "complete" the map thinking it was forgotten.
    const map = APP.split("const inboxActionIcon")[1].split("}[action.key]")[0];
    ["acceptMeeting", "acceptOffer", "declineMeeting", "delegate", "dispute", "replan", "resolve"]
      .forEach((key) => { expect(map, `${key} was mapped`).not.toContain(`${key}:`); });
    expect(map, "the deliberate omission is undocumented").toContain("ON THE DEFAULT ON PURPOSE");
  });

  it("drops the map entry no source can reach", () => {
    // MEASURED at zero sources: no provider and no fixture emits `reviewMeeting`. `scheduleReviewMeeting` is
    // the live one. A glyph chosen for a button that cannot exist is the map's own version of dead code.
    const map = APP.split("const inboxActionIcon")[1].split("}[action.key]")[0];
    expect(map).not.toContain("reviewMeeting: 'bx-calendar-event'");
    expect(map, "the live one went with it").toContain("scheduleReviewMeeting: 'bx-calendar-event'");
  });

  it("chooses the glyph in the MAP, never at a call site", () => {
    /*
     * Twice this session an icon picked where a button is drawn gave one action two different icons. Every
     * surface asks the one function; nothing hard-codes a `bx-` class next to an action button.
     */
    const drawn = code(APP).match(/data-wcn-action="\$\{[^}]*\}"[^`]*bx-[a-z-]+/g) || [];
    expect(drawn, `a glyph was chosen beside an action button: ${drawn.join(" | ")}`).toEqual([]);
  });
});

describe("BL-244 — the deleted panels took their handlers with them", () => {
  /*
   * MUTATION GUARD: paste any of these back and this goes red WITH ITS NAME. All five listened for markup that
   * NOTHING emitted — measured at zero emissions each — after `renderNotes` and `renderAgenda` were deleted a
   * round earlier. A listener for markup nothing draws is not dormant code; it reads as a working feature.
   */
  const DELETED = [
    // The panel toggles themselves.
    "data-wcn-toggle",
    // The global notes panel: its input, its add button, its "turn this into a task" link.
    "data-wcn-global-note-input", "data-wcn-global-note-add", "data-wcn-note-convert",
    "addGlobalNote", "convertGlobalNote",
    // The agenda panel's follow-up.
    "data-wcn-meeting-followup", "createMeetingFollowup",
    // What the panels held up: two open/closed flags, the query parameter that restored them, the fixture feed.
    "state.agendaOpen", "state.notesOpen", "state.notes", "buildNotes"
  ];

  DELETED.forEach((needle) => {
    it(`no code still names ${needle}`, () => {
      const offenders = shipped().filter((p) => code(fs.readFileSync(p, "utf8")).includes(needle));
      expect(offenders.map((p) => path.basename(p)), `${needle} came back`).toEqual([]);
    });
  });

  it("drops `panel` from the URL whitelist it no longer has panels for", () => {
    expect(code(APP), "a query parameter nothing reads or writes")
      .not.toContain("panel: ['', 'agenda', 'notes']");
  });

  it("LEAVES THE PERSONAL NOTE CARD ALONE — it is a different thing entirely", () => {
    /*
     * ⚠ THE NAMES ARE ONE WORD APART AND THE TWO ARE UNRELATED. The global panel wrote to browser memory;
     * this card is drawn by the detail page and goes to the server through `TasksApi.addPersonalNote`.
     * Deleting it while chasing the dead one would have removed a working feature, so it is pinned here.
     */
    expect(APP).toContain("addPersonalNote");
    expect(APP).toContain("data-wcn-note-input");
    expect(APP).toContain("data-wcn-note-add");
    // And it is still DRAWN, not merely referenced — the distinction the whole measurement rested on.
    expect(APP, "the card stopped emitting its own input").toContain('data-wcn-note-add="${item.id}"');
  });
});
