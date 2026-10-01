const fs = require("fs");
const path = require("path");

/*
 * BL-493 — A TOAST'S MESSAGE IS TEXT.
 *
 * `window.showToast` (Views/Shared/_GlobalNotification.cshtml) is the one door every module's toast goes through, and
 * Notyf — the library behind it — writes its message with `innerHTML`. Messages quote text other people typed ("X
 * applied: {task title}"), so markup in a title was built into the page of whoever saw the toast.
 *
 * HOW THIS IS TESTED. The wrapper's script is EXTRACTED from the Razor view and run against the REAL vendored Notyf
 * in this DOM, so what is asserted is what the browser builds — not a regex on the source. Only Razor's localizer
 * calls are replaced, with the strings they would render.
 */
const webRoot = path.resolve(__dirname, "..");
const VIEW = fs.readFileSync(path.join(webRoot, "Views", "Shared", "_GlobalNotification.cshtml"), "utf8");
const NOTYF_SOURCE = fs.readFileSync(path.join(webRoot, "wwwroot/assets/vendor/libs/notyf/notyf.js"), "utf8");
const NOTIFLIX_SOURCE = fs.readFileSync(path.join(webRoot, "wwwroot/assets/vendor/libs/notiflix/notiflix.js"), "utf8");

const HOSTILE = '<img src="x" onerror="window.__toastProbe=1">Rapor taslağı';
const quiet = { log: () => {}, warn: () => {}, error: () => {} };

// A vendored UMD bundle, evaluated the way a <script> tag would: no module system, its globals land on `self`.
const loadVendor = (source) => {
  const host = {};
  // eslint-disable-next-line no-new-func
  new Function("self", "window", "document", "exports", "module", "define", source)(
    host, global.window, global.document, undefined, undefined, undefined);
  return host;
};

const loadShowToast = ({ Notyf, Notiflix }) => {
  const script = VIEW.slice(VIEW.indexOf("var _notyfInstance = null;"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const win = {};
  // eslint-disable-next-line no-new-func
  new Function("window", "Notyf", "Notiflix", "console", js)(win, Notyf, Notiflix, quiet);
  return win;
};

beforeEach(() => {
  document.body.innerHTML = "";
  delete global.window.__toastProbe;
});

describe("the shared toast, through the real Notyf", () => {
  const { Notyf } = loadVendor(NOTYF_SOURCE);

  it("loads the vendored library this product ships", () => {
    expect(typeof Notyf, "the vendored Notyf did not load; every assertion below would be vacuous").toBe("function");
  });

  it.each(["success", "error", "warning", "info"])("draws markup in a %s message as text", (type) => {
    const win = loadShowToast({ Notyf, Notiflix: undefined });
    win.showToast(HOSTILE, type);

    const message = document.querySelector(".notyf__message");
    expect(message, "no toast was drawn").toBeTruthy();
    expect(message.textContent).toBe(HOSTILE);
    expect(document.querySelector("img")).toBeNull();
    expect(global.window.__toastProbe).toBeUndefined();
  });

  it("shows ordinary punctuation exactly as typed — no entities on screen", () => {
    const win = loadShowToast({ Notyf, Notiflix: undefined });
    win.showToast('Onayla uygulandı: A&B "Q3" <taslak> O\'Neil', "success");

    expect(document.querySelector(".notyf__message").textContent)
      .toBe('Onayla uygulandı: A&B "Q3" <taslak> O\'Neil');
  });

  it("still resolves its own message keys", () => {
    const win = loadShowToast({ Notyf, Notiflix: undefined });
    win.showToast("RecordSaved", "success");

    expect(document.querySelector(".notyf__message").textContent).toBe("RecordSaved");
  });
});

/*
 * showToast has a second branch, for Notiflix. It cannot run today: the vendored bundle carries Block and Loading
 * only — no `Notiflix` global and no Notify — so there is no second sink to guard. This pins that. The day somebody
 * ships a Notify, this fails, and they must measure how IT writes a message before the branch goes live.
 */
describe("the Notiflix branch has nothing behind it", () => {
  it("the vendored bundle exposes no Notify", () => {
    const host = loadVendor(NOTIFLIX_SOURCE);

    expect(Object.keys(host).sort()).toEqual(["Block", "Loading"]);
    expect(global.window.Notiflix).toBeUndefined();
  });

  it("with no library at all a toast draws nothing and throws nothing", () => {
    const win = loadShowToast({ Notyf: undefined, Notiflix: undefined });

    expect(() => win.showToast(HOSTILE, "success")).not.toThrow();
    expect(document.querySelector("img")).toBeNull();
  });
});

describe("no caller escapes first", () => {
  // The door escapes. A caller that escapes too puts `&amp;` and `&lt;` on the reader's screen.
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) { return entry.name === "vendor" ? [] : walk(full); }
    return /\.(js|cshtml)$/.test(entry.name) ? [full] : [];
  });
  const files = walk(path.join(webRoot, "wwwroot", "assets", "js")).concat(walk(path.join(webRoot, "Views")));

  it("scans a real set of files", () => {
    expect(files.length).toBeGreaterThan(300);
  });

  it("finds no toast call whose message is escaped by the caller", () => {
    const offenders = [];
    const call = /\b(?:toast|showToast|notify)\([^;\n]*\b(?:esc|escapeHtml|escapeHTML|htmlEncode|encodeHtml)\(/;
    // …and the indirect shape: a message built from an escaped title a few lines above its toast.
    const indirect = /planWarningMessage\?\.\([^)]*\besc\(/;
    for (const file of files) {
      fs.readFileSync(file, "utf8").split("\n").forEach((line, index) => {
        if (call.test(line) || indirect.test(line)) { offenders.push(`${path.relative(webRoot, file)}:${index + 1}`); }
      });
    }
    expect(offenders).toEqual([]);
  });
});
