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
    const block = source.slice(source.indexOf("const adminActions = {"), source.indexOf("document.addEventListener('click'", source.indexOf("const adminActions = {")));
    Object.entries(MENU_GLYPHS).forEach(([action, glyph]) => {
      const line = block.split("\n").find((l) => l.includes(`'${action}'`));
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
     */
    const source = JS_SOURCE();
    Object.entries(MENU_GLYPHS).forEach(([action, glyph]) => {
      const menuLine = source.split("\n").find((l) => l.includes(`className: '${action}`) || l.includes(`className: '${action}'`));
      expect(menuLine, `${action} has no menu entry`).toBeTruthy();
      expect(menuLine, `${action}: the menu draws a different glyph than the confirm`).toContain(glyph);
    });
  });

  test("the account-type change wears the same glyph as its own button", () => {
    const source = JS_SOURCE();
    expect((source.match(/icon: 'bx-id-card'/g) || []).length,
      "one of the two doors to the type change lost its glyph").toBe(2);
    expect(read("Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml"))
      .toMatch(/btnUserAccountKindChange[\s\S]{0,120}bx-id-card/);
  });

  test("every glyph named here exists in the icon set (BL-430)", () => {
    const icons = read("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css");
    const used = [...new Set([...JS_SOURCE().matchAll(/icon: '(bx-[a-z0-9-]+)'/g)].map((m) => m[1]))];
    expect(used.length, "no glyphs were found to check").toBeGreaterThan(3);
    const missing = used.filter((name) => !new RegExp(`\\.${name}\\b`).test(icons));
    expect(missing, "these would render as a filled square").toEqual([]);
  });
});
