// P-2026-10-07-BRAND-FIX4 (F5) — validity dates are calendar days. Run in a browser WEST of UTC: the zone is set before
// any Date is used (vitest runs each file in its own forked process, so it applies to this file only).
process.env.TZ = "America/New_York";

const fs = require("fs");
const path = require("path");
const vm = require("node:vm");

const web = (...p) => path.join(__dirname, "..", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const STORED = "2026-10-07T00:00:00+00:00";

/** The shipped Brands/index.js in an isolated DOM double; exposes formatDate, formatDateTime and populatePreview. */
const loadBrands = (lang = "en") => {
  const elements = {};
  const element = (id) => (elements[id] ??= { textContent: "", setAttribute: () => {} });
  const document = {
    documentElement: { lang },
    getElementById: (id) => (id === "brandRegister" ? { getAttribute: () => "false" } : element(id)),
    querySelector: () => null,
    addEventListener: () => {}
  };
  const source = read("wwwroot", "assets", "js", "MasterData", "Brands", "index.js")
    .replace("return { init };", "return { init, formatDate, formatDateTime, populatePreview };");
  const context = { window: { BrandL10n: {} }, document, console, Intl, Date, Number, String, Array, URLSearchParams, encodeURIComponent };
  vm.runInNewContext(`${source}\n;globalThis.__register = BrandsList;`, context);
  return { brands: context.__register, elements };
};

/** The shipped Products/index.js in an isolated DOM double; exposes its date and dateTime renderers. */
const loadProducts = () => {
  const anchor = "    const dateTime = value => value ? new Date(value).toLocaleString() : '—';";
  const source = read("wwwroot", "assets", "js", "MasterData", "Products", "index.js");
  expect(source).toContain(anchor);
  const window = { ProductL10n: {} };
  const document = {
    getElementById: (id) => (id === "dt-products" ? {} : null),
    querySelector: () => null,
    addEventListener: () => {}
  };
  const fetch = async () => { throw new Error("offline"); };
  vm.runInNewContext(source.replace(anchor, `${anchor}\n    window.__dates = { date, dateTime };`),
    { window, document, fetch, console, Intl, Date, Number, String, Object, Array, URLSearchParams, Error });
  return window.__dates;
};

describe("Validity dates west of UTC (F5)", () => {
  test("the test really runs west of UTC: midnight +00:00 is the previous local day", () => {
    expect(new Date(STORED).getDate()).toBe(6);
  });

  test("the brand list shows the stored calendar day, not the day before", () => {
    const { brands } = loadBrands("en");
    expect(brands.formatDate(STORED)).toBe("Oct 7, 2026");
    expect(brands.formatDate(null)).toBe("—");
  });

  test("the brand quick view shows the stored calendar days", () => {
    const { brands, elements } = loadBrands("en");
    brands.populatePreview({ brandId: "b", brandName: "B", brandCode: "B", effectiveFrom: STORED, effectiveTo: "2026-12-31T00:00:00+00:00" });
    expect(elements["oc-effective-from"].textContent).toBe("Oct 7, 2026");
    expect(elements["oc-effective-to"].textContent).toBe("Dec 31, 2026");
  });

  test("a real timestamp (updatedAt) keeps the reader's local time", () => {
    const { brands } = loadBrands("en");
    expect(brands.formatDateTime(STORED)).toContain("Oct 6, 2026");
  });

  test("the product list shows the stored calendar day, not the day before", () => {
    const { date, dateTime } = loadProducts();
    const expected = new Intl.DateTimeFormat(undefined, { timeZone: "UTC" }).format(Date.UTC(2026, 9, 7));
    expect(date(STORED)).toBe(expected);
    expect(date(STORED)).not.toBe(new Date(STORED).toLocaleDateString());
    expect(date(null)).toBe("—");
    expect(dateTime(STORED)).toBe(new Date(STORED).toLocaleString());
  });
});
