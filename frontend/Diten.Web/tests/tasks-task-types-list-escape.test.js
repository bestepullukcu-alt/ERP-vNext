const { loadScript } = require("./load-script");
const { bootDataTableList } = require("./xss-harness-anything");

/* ATT-FIX2 (6, security) — a task type's code (typed by a tenant administrator) is drawn as TEXT. */
const XSS = '<img src=x onerror="window.__pwned=1">';
const drawn = (html) => { const cell = document.createElement("div"); cell.innerHTML = String(html); return cell; };

describe("ATT-FIX2 — Task types list escapes what the administrator typed", () => {
  it("the code and an unmapped record class are text, never markup", async () => {
    const options = await bootDataTableList(loadScript, "wwwroot/assets/js/Tasks/TaskTypes/index.js", "datatables-tasktypes");
    expect(options, "the list built no table").toBeTruthy();
    const render = (target) => options.config.columnDefs.find((c) => c.targets === target)?.render;

    [render(2)(XSS), render(4)(XSS)].forEach((html) => {
      expect(drawn(html).querySelector("img"), `an element was drawn from: ${html}`).toBeNull();
      expect(drawn(html).textContent).toContain("<img");
    });
  });
});
