const fs = require("fs");
const path = require("path");

/*
 * CT live check of T2b (2026-09-30): the Settings page told users "(D6)" and "(MOD-0280 paketi §22)" — the pack's
 * decision and section numbers, which mean nothing to the person reading the screen. The copy a user reads carries
 * no internal code: no decision number (D6), no pack section (§22), no module-pack or backlog code. Resx <comment>
 * elements are for translators and may name them.
 */
const ROOT = path.join(__dirname, "..", "Resources", "Views", "TimeEntry");
const INTERNAL = /\(D\d{1,2}\)|（D\d{1,2}）|§\s?\d|MOD-\d{4}|BL-\d{3}/;

const resxFiles = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
  e.isDirectory() ? resxFiles(path.join(dir, e.name)) : e.name.endsWith(".resx") ? [path.join(dir, e.name)] : []);

describe("time-entry screen copy names no internal code", () => {
  const files = resxFiles(ROOT);

  it("finds the time-entry resx files (the scan is not vacuous)", () => {
    expect(files.length).toBeGreaterThanOrEqual(7 * 4);
  });

  it("no <value> a user reads carries a decision number, pack section, module-pack or backlog code", () => {
    const leaks = files.flatMap((file) =>
      Array.from(fs.readFileSync(file, "utf8").matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g))
        .filter(([, , value]) => INTERNAL.test(value))
        .map(([, key, value]) => `${path.relative(ROOT, file)} ${key}: ${value}`));
    expect(leaks).toEqual([]);
  });
});
