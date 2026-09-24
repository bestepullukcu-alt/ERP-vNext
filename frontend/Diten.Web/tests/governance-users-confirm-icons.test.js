const fs = require("fs");
const path = require("path");

/*
 * A CONFIRMATION SHOWS THE ACT IT IS CONFIRMING — owner report, 2026-09-23.
 *
 * MEASURED: every dialog on the Users screen opened with the builder's default question mark, because this
 * screen never passed the `icon` seam `showConfirm` has had since the snooze dialog needed it
 * (`_GlobalConfirmation.cshtml`: `iconHtml(type, options.icon)`). A question mark asks "are you sure" and says
 * nothing about WHAT — while the menu item the reader clicked a second earlier had its own picture: a paper
 * plane for resend, a key for reset, a circle-minus for suspend.
 *
 * So the rule is: the confirm carries the same glyph as the menu item that opened it. That makes the two
 * screens one act rather than two unrelated questions, and it costs nothing — the seam already existed.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const JS_SOURCE = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");

/** menu item class -> the glyph it draws in the kebab. */
const MENU_GLYPHS = {
  "js-user-disable": "bx-minus-circle",
  "js-user-enable": "bx-check-circle",
  "js-user-resend": "bx-mail-send",
  "js-user-reset": "bx-key"
};

describe("every admin confirmation wears its own action's glyph", () => {
  test("the four actions each declare one", () => {
    const source = JS_SOURCE();
    const block = source.slice(source.indexOf("const adminActions = {"), source.indexOf("const runAdminAction", source.indexOf("const adminActions = {")));
    Object.entries(MENU_GLYPHS).forEach(([action, glyph]) => {
      // BL-440 package 5: one entry per action carries its menu class AND its glyph (the kebab is built from it).
      const line = block.split("\n").find((l) => l.includes(`className: '${action}`));
      expect(line, `${action} is no longer in the action map`).toBeTruthy();
      expect(line, `${action} carries no icon, so its confirm opens with a question mark`).toContain(`icon: '${glyph}'`);
    });
  });

  test("and the glyph reaches the dialog", () => {
    // Declaring it and not passing it is the same defect with an extra step.
    expect(JS_SOURCE()).toMatch(/type: cfg\.type, icon: cfg\.icon/);
  });

  test("the confirm's glyph is the SAME one the menu item draws", () => {
    /*
     * The point of the change: the reader sees the picture they just clicked. If the kebab shows a plane and
     * the dialog a key, the dialog is describing a different act.
     *
     * BL-440 package 5: the menu item and the confirm now read ONE entry — the kebab builds `bx ${cfg.icon}` from
     * the same `cfg` the confirm passes as `icon: cfg.icon`, so the two cannot drift apart.
     */
    const source = JS_SOURCE();
    const builder = source.slice(source.indexOf("const adminAction = "), source.indexOf("\n    };", source.indexOf("const adminAction = ")));
    expect(builder, "the kebab entry no longer takes its glyph from the action's own entry").toMatch(/icon: `bx \$\{cfg\.icon\}`/);
    expect(builder).toMatch(/className: cfg\.className/);
    // Every entry of the map is both a kebab item and the row action that opens ITS confirm.
    expect(source).toContain("...Object.keys(adminActions).map((key) => ({ [key]: runAdminAction(adminActions[key]) }))");
    const block = source.slice(source.indexOf("const adminActions = {"), source.indexOf("const runAdminAction"));
    Object.keys(MENU_GLYPHS).forEach((action) => {
      const key = action.replace("js-user-", "");
      expect(block, `${action} is not an entry of the action map`).toMatch(new RegExp(`^\\s+${key}: \\{ className: '${action}`, "m"));
      expect(source, `${action} is not drawn in the kebab`).toContain(`adminAction('${key}'`);
    });
  });

  test("the account-type change keeps its glyph — and now there is only one door to it", () => {
    /*
     * ⚠ UPDATED WITH THE DESIGN, NOT LOOSENED (2026-09-23). This used to demand the glyph TWICE, because the
     * type was changed from two dialogs: the quick view's and one on the edit form. WP-AUTH-USER-KIND-UPDATE-01
     * made the edit side a saved FIELD — the kind now rides the form's own "Update" — so the edit dialog and
     * its button are gone. One dialog is left, and it still names the act it performs.
     */
    const source = JS_SOURCE();
    expect((source.match(/icon: 'bx-id-card'/g) || []).length,
      "the quick view's type change lost its glyph").toBe(1);
    // And the edit form asks no dialog at all: the field is saved with everything else.
    expect(read("Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml"),
      "the edit form grew a type dialog again — it is a saved field now")
      .not.toContain("btnUserAccountKindChange");
  });

  test("every glyph named here exists in the icon set (BL-430)", () => {
    const icons = read("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css");
    const used = [...new Set([...JS_SOURCE().matchAll(/icon: '(bx-[a-z0-9-]+)'/g)].map((m) => m[1]))];
    expect(used.length, "no glyphs were found to check").toBeGreaterThan(3);
    const missing = used.filter((name) => !new RegExp(`\\.${name}\\b`).test(icons));
    expect(missing, "these would render as a filled square").toEqual([]);
  });
});
