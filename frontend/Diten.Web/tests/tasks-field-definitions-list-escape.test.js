const { loadScript } = require("./load-script");
const { bootDataTableList } = require("./xss-harness-anything");

/*
 * ATT-FIX2 (6, security) — a tenant administrator types a field's code and section; the list draws them as TEXT.
 * Measured on the production column renderers: a row carrying `<img onerror>` produces no element.
 */
const XSS = '<img src=x onerror="window.__pwned=1">';
const drawn = (html) => { const cell = document.createElement("div"); cell.innerHTML = String(html); return cell; };

describe("ATT-FIX2 — Task field definitions list escapes what the administrator typed", () => {
  it("code, section and an unmapped value type are text, never markup", async () => {
    const options = await bootDataTableList(loadScript, "wwwroot/assets/js/Tasks/FieldDefinitions/index.js", "datatables-taskfielddefinitions");
    expect(options, "the list built no table").toBeTruthy();
    const render = (target) => options.config.columnDefs.find((c) => c.targets === target)?.render;
    const row = { id: "f1", code: XSS, section: XSS, valueType: XSS };

    expect(render(5), "the section column has no renderer — DataTables would insert it as HTML").toBeTruthy();
    [render(2)(XSS, "display", row), render(4)(XSS, "display", row), render(5)(XSS, "display", row)].forEach((html) => {
      expect(drawn(html).querySelector("img"), `an element was drawn from: ${html}`).toBeNull();
      expect(drawn(html).textContent).toContain("<img");
    });
  });
});
