const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

/*
 * WP-UX-TOUR-DRIVERJS-01 — Driver.js 1.8.0 (MIT) is vendored, byte for byte, and loaded from this application only.
 *
 *   · each vendored file has the SHA-256 recorded when it was taken from the npm package (README.md beside it): one
 *     changed byte — a minify, a format, a "small fix" — is red, by file name;
 *   · the licence travels with the code;
 *   · nothing loads the library from anywhere else (no CDN, no second copy), and the one place that loads it is the
 *     help partial both shells render.
 */

const WEB = path.resolve(__dirname, "..");
const VENDOR = path.join(WEB, "wwwroot", "assets", "vendor", "libs", "driverjs");
const PINNED = {
  "driver.js.iife.js": "c6ade0b831c6c043daf480861208cd2fa45ea4aac581cc8bb8e234281c011ddf",
  "driver.css": "d095d440021fcf133ad46d37f18a2745fb76440f14f5208d17e203c039f765c9",
  "LICENSE": "ec3ce3a08736fefd6a03a6d5b52b0705e6919fe06de9d7bd3fb63dcfb492d76d"
};
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

const walk = (dir, filter) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
  const full = path.join(dir, entry.name);
  if (entry.isDirectory()) { return ["bin", "obj", "node_modules", "vendor"].includes(entry.name) ? [] : walk(full, filter); }
  return filter(entry.name) ? [full] : [];
});

describe("Driver.js is vendored byte for byte", () => {
  test.each(Object.entries(PINNED))("%s has its recorded SHA-256", (file, expected) => {
    expect(sha256(path.join(VENDOR, file)), `${file} changed — update README.md and this pin together, or restore it`)
      .toBe(expected);
  });

  test("the folder holds exactly the vendored files and their README", () => {
    expect(fs.readdirSync(VENDOR).sort()).toEqual([...Object.keys(PINNED), "README.md"].sort());
  });

  test("the licence is the MIT licence of the library", () => {
    const licence = fs.readFileSync(path.join(VENDOR, "LICENSE"), "utf8");
    expect(licence).toMatch(/^The MIT License/);
    expect(licence).toMatch(/Kamran Ahmed/);
  });

  test("README records the version and the same hashes the test pins", () => {
    const readme = fs.readFileSync(path.join(VENDOR, "README.md"), "utf8");
    expect(readme).toMatch(/\*\*1\.8\.0\*\*/);
    Object.entries(PINNED).forEach(([file, hash]) => expect(readme, file).toContain(hash));
  });
});

describe("the library is loaded from this application, once, by the help partial", () => {
  const PARTIAL = path.join(WEB, "Views", "Shared", "_PageHelpPanel.cshtml");

  test("the help partial loads the vendored script and stylesheet", () => {
    const partial = fs.readFileSync(PARTIAL, "utf8");
    expect(partial).toMatch(/<script src="~\/assets\/vendor\/libs\/driverjs\/driver\.js\.iife\.js"/);
    expect(partial).toMatch(/<link rel="stylesheet" href="~\/assets\/vendor\/libs\/driverjs\/driver\.css"/);
  });

  test("no view or script loads Driver.js from elsewhere (CDN or a second copy)", () => {
    const sources = [...walk(path.join(WEB, "Views"), (n) => n.endsWith(".cshtml")),
      ...walk(path.join(WEB, "wwwroot", "assets", "js"), (n) => n.endsWith(".js"))];
    const offenders = sources.filter((file) => {
      const text = fs.readFileSync(file, "utf8");
      return /https?:\/\/[^"'\s]*driver(\.js)?[^"'\s]*/i.test(text)
        || (/driver\.js\.iife\.js|driverjs\/driver\.css/.test(text) && file !== PARTIAL);
    }).map((file) => path.relative(WEB, file));
    expect(offenders).toEqual([]);
  });

  test("both shells render the help partial (tenant and platform admin)", () => {
    ["_LayoutTenantShell.cshtml", "_LayoutPlatformAdmin.cshtml"].forEach((shell) => {
      expect(fs.readFileSync(path.join(WEB, "Views", "Shared", shell), "utf8"), shell).toMatch(/_PageHelpPanel/);
    });
  });
});
