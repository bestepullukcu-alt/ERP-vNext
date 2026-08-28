const fs = require("fs");
const path = require("path");

/*
 * THE SHARED CONFIRM'S CSS LIVES WHERE THIS PROJECT'S CSS LIVES (BL-190).
 *
 * It used to be an inline `<style>` block inside `_GlobalConfirmation.cshtml` — four rules that paint EVERY
 * modal in the product, sitting outside `backbone-custom.css` where FG-003 says this project's CSS belongs. One
 * of them, an unconditional `display: flex !important` on `.swal2-icon`, was strong enough to overrule the
 * LIBRARY'S OWN `display: none` on an icon that had been suppressed — which is why an icon nobody drew still
 * reserved 80px, and why the previous round had to work around it with `:empty`.
 *
 * These assertions are read off the CSS text because that is where the claim lives; the pixels were measured in
 * the browser on three screens (Rol İzinleri, Yeni Görev, Kullanıcılar) before and after the move, and are
 * reported with the round.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");
/** The view with its Razor comments removed — a note ABOUT a style block is not a style block. */
const VIEW_CODE = VIEW.replace(/@\*[\s\S]*?\*@/g, "");

/** A rule's declarations, comments stripped first so a rule is never matched by its own prose. */
const ruleBody = (selector) => {
  const text = CSS.replace(/\/\*[\s\S]*?\*\//g, "");
  const at = text.indexOf(`\n${selector} {`);
  if (at < 0) { return null; }
  return text.slice(at + selector.length + 3, text.indexOf("}", at));
};

describe("the block moved", () => {
  it("left no <style> behind in the partial", () => {
    expect(VIEW_CODE, "the component still carries its own stylesheet").not.toContain("<style>");
  });

  it("arrived whole — every rule the partial used to carry", () => {
    /*
     * MUTATION GUARD: drop any one of these on the way across and a modal somewhere in the product loses its
     * circle, its centring or its icon row, with nothing to say which screen broke.
     */
    ["\\.swal-icon-circle", "\\.swal-icon-circle i", "\\.swal2-title", "\\.swal2-icon:not\\(:empty\\)"]
      .forEach((sel) => expect(CSS, `${sel} did not arrive`).toMatch(new RegExp(`\\n${sel} \\{`)));
  });

  it("kept the circle exactly as it was — 80px, its own background and border", () => {
    const body = ruleBody(".swal-icon-circle");
    expect(body).toContain("width: 80px");
    expect(body).toContain("height: 80px");
    expect(body).toContain("border-radius: 50%");
    expect(body).toContain("rgba(255, 76, 81, 0.12)");
    expect(body).toContain("2px solid rgba(255, 76, 81, 0.25)");
    // The glyph's size travelled with it; its COLOUR still comes from the per-type text-* utility.
    expect(ruleBody(".swal-icon-circle i")).toContain("font-size: 2.5rem");
  });

  it("stopped forcing an icon that is not there to take up room", () => {
    /*
     * The rule is `:not(:empty)` now, so it says nothing about a suppressed icon and the library's own
     * `display: none` stands. The `:empty { display: none !important }` workaround is gone with it.
     */
    expect(CSS).toMatch(/\n\.swal2-icon:not\(:empty\) \{/);
    expect(CSS, "the workaround outlived the thing it worked around")
      .not.toContain(".swal2-icon:empty { display: none !important; }");
  });

  it("dropped the !important that had nothing to fight", () => {
    // `margin: 0 auto !important` on the circle was countermanding a margin the library never sets there.
    expect(ruleBody(".swal-icon-circle")).toContain("margin: 0 auto;");
    expect(ruleBody(".swal-icon-circle")).not.toContain("margin: 0 auto !important");
  });

  it("kept the ones that do — each beside its reason", () => {
    // The title's four resets fight Bootstrap's margin/padding utilities, which carry !important themselves.
    const title = ruleBody(".swal2-title");
    ["margin-left: 0 !important", "margin-right: 0 !important",
     "padding-left: 0 !important", "padding-right: 0 !important"].forEach((d) => expect(title).toContain(d));
    // The icon row's width/border/margin countermand `div:where(.swal2-icon)` in sweetalert2.css.
    const icon = ruleBody(".swal2-icon:not(:empty)");
    expect(icon).toContain("width: 100% !important");
    expect(icon).toContain("border: none !important");
    // …and `display` is NOT among them any more: that is the one that caused BL-190.
    expect(icon, "display is being forced again").not.toMatch(/display:[^;]*!important/);
  });
});

describe("the input stays where the library can find it", () => {
  const scoped = ".swal2-container .swal2-modal.swal2-popup .swal2-input.wcn-date-input";

  /*
   * ⚠ THE RULE THIS FILE EXISTS TO KEEP. SweetAlert finds its input by walking the popup's own FIXED SLOT LIST
   * — `.swal2-input`, `.swal2-file`, `.swal2-select`, … in order, among the popup's DIRECT CHILDREN — not by
   * querying the subtree. Wrapping the box in `.diten-field` made it a grandchild, and one wrapper broke three
   * things at once, measured in the browser:
   *     Swal.getInput()            → null
   *     the validator's value      → '' , so a FUTURE date was refused as "past"
   *     autofocus and Enter        → dead
   * `.diten-field` is correct everywhere else in this product. It is wrong only INSIDE this popup.
   */
  it("is never wrapped — the decoration is painted ON the box", () => {
    const app = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
    const snooze = app.slice(app.indexOf("const toggleSnooze"), app.indexOf("const openNew"));
    // MUTATION GUARD: put the input back inside a wrapper and this goes red.
    expect(snooze, "the input is being wrapped again").not.toMatch(/appendChild\(\s*input\s*\)/);
    expect(snooze).not.toContain("className = 'diten-field'");
    expect(snooze).toContain("classList.add('wcn-date-input')");
  });

  it("carries the product's own calendar glyph as the box's background", () => {
    const body = ruleBody(scoped);
    // The same SVG `.bx-calendar` carries in vendor/fonts/iconify-icons.css — the path, not a lookalike.
    expect(body).toContain("M5 22h14c1.103 0 2-.897 2-2V6c0-1.103-.897-2-2-2h-2V2h-2v2H9V2H7v2H5c-1.103 0-2");
    expect(body).toContain("background-repeat: no-repeat");
    // 0.9375rem is the theme's text inset — the number `.diten-field-icon` uses to place the same glyph.
    expect(body).toContain("background-position: left 0.9375rem center");
    expect(body).toContain("background-size: 1rem 1rem");
  });

  it("gives the glyph each theme's own grey, because a background cannot inherit currentColor", () => {
    // MEASURED on the create form's date field: --bs-secondary-color is #a7acb2 light, #7e7f96 dark.
    expect(ruleBody(scoped)).toContain("%23a7acb2");
    const dark = ruleBody('[data-bs-theme="dark"] ' + scoped);
    expect(dark, "the dark theme has no glyph rule").toBeTruthy();
    expect(dark).toContain("%237e7f96");
    // RTL moves the glyph to the other edge; background-position has no logical keyword.
    expect(ruleBody('[dir="rtl"] ' + scoped)).toContain("background-position: right 0.9375rem center");
  });

  it("wears the product's control box, measured against the create form's date field", () => {
    const body = ruleBody(scoped);
    expect(body).toContain("block-size: 38px");
    expect(body).toContain("font-size: 0.9375rem");
    expect(body).toContain("line-height: 1.375");
    expect(body).toContain("border-radius: var(--bs-border-radius)");
    expect(body).toContain("padding-block: calc(0.543rem - var(--bs-border-width))");
    expect(body).toContain("padding-inline-start: calc(0.9375rem + 1rem + 0.5rem)");
    expect(body).toContain("margin-inline: 0");
    expect(body).toContain("inline-size: 100%");
  });

  it("only decorates a box that asked for it", () => {
    // The class is the gate: a prose dialog's textarea never gets any of this.
    expect(CSS).not.toMatch(/\.swal2-modal\.swal2-popup \.swal2-input \{/);
    expect(ruleBody(scoped)).toBeTruthy();
  });

  it("shows a refusal in the product's alert language", () => {
    /*
     * The library's validation strip is #f0f0f0 at 16px with square corners — a colour this product uses
     * nowhere. Every value below is a Bootstrap alert token, so both themes follow with no colour written here.
     */
    const body = ruleBody(".swal2-container .swal2-modal.swal2-popup .swal2-validation-message");
    expect(body).toContain("var(--bs-danger-bg-subtle)");
    expect(body).toContain("var(--bs-danger-text-emphasis)");
    expect(body).toContain("var(--bs-danger-border-subtle)");
    expect(body).toContain("font-size: 0.8125rem");
  });

  it("is asked for by the dialog, not by the shared component", () => {
    expect(VIEW_CODE, "the shared component grew an icon slot").not.toContain("diten-field");
    expect(VIEW_CODE).not.toContain("wcn-date-input");
  });
});
