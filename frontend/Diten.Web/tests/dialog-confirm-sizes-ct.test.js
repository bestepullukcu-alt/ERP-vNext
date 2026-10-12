const fs = require("fs");
const path = require("path");

/*
 * THE CONFIRM'S READING SIZES — owner decision 2026-09-24 (variant A of the delete-confirm prototypes), CT guard.
 * Description 14px on a 1.55 line, the record chip 14px below it, the buttons 22px below the body. Set once, in the
 * shared stylesheet, for every confirm in the product. The description keeps its `small` class (other files pin it).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const css = fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const rule = (selector) => {
  const at = css.indexOf(selector + " {");
  if (at < 0) return null;
  return css.slice(at, css.indexOf("}", at));
};

describe("the shared confirm dialog's sizes (CT)", () => {
  it("the description reads at 14px on a 1.55 line, without touching its class", () => {
    const r = rule(".swal2-container .swal2-modal.swal2-popup .swal2-html-container.dt-dialog-body > .small");
    expect(r, "the description size rule is missing").toBeTruthy();
    expect(r).toMatch(/font-size:\s*0?\.875rem/);
    expect(r).toMatch(/line-height:\s*1\.55/);
  });

  it("the record chip is 14px and sits 14px under the description", () => {
    const r = rule(".swal2-container .swal2-modal.swal2-popup .dt-dialog-entity");
    expect(r).toMatch(/font-size:\s*0?\.875rem/);
    expect(r).toMatch(/margin-block-start:\s*0?\.875rem/);
  });

  it("the buttons sit 22px under the body", () => {
    const r = rule(".swal2-container .swal2-modal.swal2-popup .swal2-actions");
    expect(r, "the actions margin rule is missing").toBeTruthy();
    expect(r).toMatch(/margin-block-start:\s*1\.375rem/);
  });
});
