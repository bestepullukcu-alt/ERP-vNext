const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * THE WIRE, TWO SEAMS — CT guards (package 3 acceptance, 2026-09-24).
 *
 * Two sabotages of `toServerQuery` left the agent's 39 server-mode tests green:
 *   1. values sent without encodeURIComponent — a search for "a&b=c+d" would become three parameters and a space;
 *      a Turkish or accented term would leave the browser to guess its encoding;
 *   2. orderDir compared case-sensitively — DataTables 2 emits lower case today, a saved view or a future version
 *      may not; "DESC" must still travel as desc, never silently as asc.
 * Both measured on the production function in a browser-shaped sandbox.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const SOURCE = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "diten-datatable.js");

function loadFactory() {
  const win = { L10n: {} };
  const sandbox = { window: win, document: { addEventListener() {}, querySelector: () => null, getElementById: () => null }, console, setTimeout, clearTimeout, requestAnimationFrame: (fn) => fn() };
  vm.runInNewContext(fs.readFileSync(SOURCE, "utf8"), sandbox, { filename: SOURCE });
  return win.DitenDataTable;
}

describe("server-mode wire: encoding and direction (CT)", () => {
  const D = loadFactory();
  const columns = [{ data: "id", orderable: false }, { data: "code" }, { data: "name" }];
  const fields = [{ key: "status", kind: "multi" }, { key: "owner", kind: "single" }];

  test("every value is URL-encoded — reserved characters and non-ASCII survive the trip intact", () => {
    const q = D.toServerQuery({ start: 0, length: 10, search: { value: "a&b=c+d Çelik" }, order: [{ column: 1, dir: "asc" }], columns, draw: 3 },
      fields, { status: ["Act&ive"], owner: "o'neil+x" });
    expect(q).toContain("search=" + encodeURIComponent("a&b=c+d Çelik"));
    expect(q).toContain("status=" + encodeURIComponent("Act&ive"));
    expect(q).toContain("owner=" + encodeURIComponent("o'neil+x"));
    expect(q, "an unencoded & would split the term into extra parameters").not.toMatch(/search=a&b/);
  });

  test("orderDir is normalised: DESC / Desc travel as desc, anything else as asc", () => {
    const of = (dir) => D.toServerQuery({ start: 0, length: 10, order: [{ column: 1, dir }], columns, draw: 1 }, [], {});
    expect(of("DESC")).toContain("orderDir=desc");
    expect(of("Desc")).toContain("orderDir=desc");
    expect(of("asc")).toContain("orderDir=asc");
    expect(of("sideways")).toContain("orderDir=asc");
  });
});
