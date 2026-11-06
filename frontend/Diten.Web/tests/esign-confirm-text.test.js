const fs = require("fs");
const path = require("path");

/*
 * MVP-1 ortak arayüz kuralları — THE E-SIGNATURE CONFIRMS HAND OVER TEXT, AND THE READER SEES IT AS TYPED.
 *
 * ESignature/esign-pages.js wraps the shared confirm twice: `reason(…)` (a reason of 10–1000 characters) and `confirm(…)`.
 * Both escaped the question with ESign.escapeHtml before handing it over — but the dialog turns `subtext` into text at
 * the door (window.DitenText), so a question with "&" reached the reader as "&amp;". `reason(…)` also built its change
 * lines as a `<ul><li>` list inside the same text value, so the policy save showed the reader its tags.
 *
 * HOW THIS IS TESTED. The production pieces, not a copy: the wrapper's script EXTRACTED from
 * Views/Shared/_GlobalConfirmation.cshtml (only Razor's localizer calls replaced), the REAL vendored SweetAlert, and the
 * real esign-pages.js evaluated on `window`. What is asserted is what the popup draws.
 */
const webRoot = path.resolve(__dirname, "..");
const VIEW = fs.readFileSync(path.join(webRoot, "Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");
const SWAL_SOURCE = fs.readFileSync(path.join(webRoot, "wwwroot/assets/vendor/libs/sweetalert2/sweetalert2.js"), "utf8");
const PAGES_SOURCE = fs.readFileSync(path.join(webRoot, "wwwroot/assets/js/ESignature/esign-pages.js"), "utf8");

const loadSwal = () => {
  const host = {};
  // eslint-disable-next-line no-new-func
  new Function("self", "window", "document", "exports", "module", "define", SWAL_SOURCE)(
    host, global.window, global.document, undefined, undefined, undefined);
  return host.Swal || global.window.Swal;
};
const Swal = loadSwal();

const loadPages = () => {
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const quiet = { log: () => {}, warn: () => {}, error: () => {} };
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(global.window, Swal, quiet, () => false);
  // esign-dialog.js's surface, as far as esign-pages.js reads it at load: t (key back) and its escaper (still used by the badges).
  global.window.ESign = {
    t: (key) => key,
    escapeHtml: (s) => String(s ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;"),
  };
  // eslint-disable-next-line no-new-func
  new Function("window", PAGES_SOURCE)(global.window);
  return global.window.ESignPages;
};

const body = () => document.querySelector(".swal2-popup .swal2-html-container");

beforeEach(() => {
  if (Swal && Swal.isVisible && Swal.isVisible()) { Swal.close(); }
  document.body.innerHTML = "";
});
afterEach(() => {
  if (Swal && Swal.isVisible && Swal.isVisible()) { Swal.close(); }
  delete global.window.ESign;
  delete global.window.ESignPages;
  delete global.window.showConfirm;
});

describe("the e-signature confirms, through the shared confirm and the real SweetAlert", () => {
  it("load the production pieces (the assertions below are not vacuous)", () => {
    expect(typeof Swal?.fire).toBe("function");
    const pages = loadPages();
    expect(typeof pages.confirm).toBe("function");
    expect(typeof pages.reason).toBe("function");
    expect(typeof global.window.showConfirm).toBe("function");
  });

  it("confirm(…): a question with \"&\" is read with \"&\", never \"&amp;\"", () => {
    loadPages().confirm({ title: "Onay", subtext: "Rapor & taslak <b>v2</b>", confirmText: "OK" });
    expect(body(), "no dialog was drawn").toBeTruthy();
    expect(body().textContent).toContain("Rapor & taslak <b>v2</b>");
    expect(body().textContent).not.toContain("&amp;");
    expect(body().querySelector("b"), "a tag was built from the question").toBeNull();
  });

  it("reason(…): the question and its change lines are read as typed — no \"&amp;\", no list tags", () => {
    loadPages().reason({
      title: "Politika", subtext: "Kaydet & yayınla", lines: ["Onay: zorunlu → isteğe bağlı", "A & B"], confirmText: "OK",
    });
    expect(body(), "no dialog was drawn").toBeTruthy();
    const said = body().textContent;
    expect(said).toContain("Kaydet & yayınla · Onay: zorunlu → isteğe bağlı · A & B");
    expect(said).not.toContain("&amp;");
    expect(said, "the list was handed over as text and shown as its tags").not.toMatch(/<\/?(ul|li)\b/);
  });
});
