const fs = require("fs");
const path = require("path");

/*
 * WP-MDM-ABB-ACCEPT-01 FIX1 #12 (FG-003) and S5 (UAS-001) — the ABB register page's own files.
 */
const WEB = path.resolve(__dirname, "..");
const VIEWS = path.join(WEB, "Views/MDM/ProductAbbreviationRegister");
const read = (file) => fs.readFileSync(file, "utf8");

describe("ABB register page", () => {
  it.each(fs.readdirSync(VIEWS).filter((file) => file.endsWith(".cshtml")))(
    "%s styles through classes, never style=\"\"", (file) => {
      expect(read(path.join(VIEWS, file))).not.toMatch(/\sstyle\s*=/i);
    });

  it("sets no element style from script", () => {
    const js = read(path.join(WEB, "wwwroot/assets/js/MDM/ProductAbbreviationRegister/index.js"));
    expect(js).not.toMatch(/\.style\.|setAttribute\(\s*['"]style['"]/);
  });

  it("draws only the access-denied surface for a reader without the permission, before anything else", () => {
    const index = read(path.join(VIEWS, "Index.cshtml"));
    const denied = index.indexOf('<partial name="_AccessDenied"');
    expect(denied).toBeGreaterThan(-1);
    expect(index.slice(0, denied)).toContain('@if (!permissions.Contains("mdm.product-abbreviations.read"))');
    expect(index.slice(denied, index.indexOf("}", denied))).toContain("return;");
    expect(index.indexOf('id="product-abbreviation-register"')).toBeGreaterThan(denied);
  });
});
