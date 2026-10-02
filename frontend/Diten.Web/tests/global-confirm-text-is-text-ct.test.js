const fs = require("fs");
const path = require("path");

/*
 * WP-SHARED-CONFIRM-XSS-01 — CONTROL TOWER ACCEPTANCE.
 *
 * The delivery made the shared confirm write every caller value as text, and counted its callers by the literal
 * `showConfirm(`. Two callers reach the dialog through an ALIAS (`const confirm = global.showConfirm`): the Task
 * Center's own seam and the invitation card. They had been escaping their words themselves, or handing over markup
 * they built — so after the fix every Task Center confirmation printed `<div class="wcn-confirm-body">…` and a task
 * called "R&D plan" read "R&amp;D plan". The independent review found it; this file pins what was missing:
 *   - the dialog rules the delivery's own test did not assert (measured by mutation: five of ten stayed green);
 *   - the two aliases, by name, and what they may hand over;
 *   - a dialog that cannot be drawn is NOT a yes.
 */
const webRoot = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(webRoot, ...parts), "utf8");
const VIEW = read("Views", "Shared", "_GlobalConfirmation.cshtml");
const APP = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
const INVITE = read("wwwroot", "assets", "js", "shared", "diten-invite-card.js");
const RESOURCE = read("wwwroot", "assets", "js", "CRM", "TerritoryManagement", "resource-assignments.js");
const SWAL_SOURCE = read("wwwroot", "assets", "vendor", "libs", "sweetalert2", "sweetalert2.js");

const HOSTILE = '<img src="x" onerror="window.__ctProbe=1">Rapor & <b>taslak</b>';
const quiet = { log: () => {}, warn: () => {}, error: () => {} };

const loadSwal = () => {
  const host = {};
  // eslint-disable-next-line no-new-func
  new Function("self", "window", "document", "exports", "module", "define", SWAL_SOURCE)(
    host, global.window, global.document, undefined, undefined, undefined);
  return host.Swal || global.window.Swal;
};
// `Swal` is a parameter so the native path (no library) can be run with `undefined`; `confirmFn` is the browser's.
const load = (Swal, confirmFn = () => false) => {
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const win = {};
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(win, Swal, quiet, confirmFn);
  return win;
};
const Swal = loadSwal();
const popup = () => document.querySelector(".swal2-popup");
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

beforeEach(() => {
  if (Swal && Swal.isVisible && Swal.isVisible()) { Swal.close(); }
  document.body.innerHTML = "";
  delete global.window.__ctProbe;
});

const nothingBuilt = () => {
  expect(popup(), "no dialog was drawn").toBeTruthy();
  expect(Array.from(popup().querySelectorAll("img")).map((i) => i.className)).toEqual(["swal2-image"]);
  expect(popup().querySelector("b")).toBeNull();
  expect(global.window.__ctProbe).toBeUndefined();
};

describe("dialog rules the delivery's test left unasserted", () => {
  it("the vendored SweetAlert loaded (otherwise everything below is vacuous)", () => {
    expect(typeof Swal?.fire).toBe("function");
  });

  // Asked one at a time: with both set, SweetAlert draws the placeholder beside the box and a mutation of the
  // label's rule stayed green (measured in acceptance).
  it("a checkbox's placeholder is text", () => {
    load(Swal).showConfirm("AreYouSure", () => {},
      { showInput: true, inputType: "checkbox", inputPlaceholder: HOSTILE });

    nothingBuilt();
    expect(popup().textContent).toContain("Rapor & <b>taslak</b>");
  });

  it("a checkbox's label is text", () => {
    load(Swal).showConfirm("AreYouSure", () => {},
      { showInput: true, inputType: "checkbox", inputLabel: HOSTILE });

    nothingBuilt();
    expect(popup().textContent).toContain("Rapor & <b>taslak</b>");
  });

  it("options that arrive through a promise are text", async () => {
    load(Swal).showConfirm("AreYouSure", () => {},
      { showInput: true, inputType: "select", inputOptions: Promise.resolve({ one: HOSTILE }) });
    await settle();
    await settle();

    nothingBuilt();
    const labels = Array.from(popup().querySelectorAll("select.swal2-select option")).map((o) => o.textContent);
    expect(labels).toContain(HOSTILE);
  });

  it("an option with no label is empty, not the word 'null'", () => {
    load(Swal).showConfirm("AreYouSure", () => {},
      { showInput: true, inputType: "select", inputOptions: { one: null, two: undefined, three: "Üç" } });

    const labels = Array.from(popup().querySelectorAll("select.swal2-select option")).map((o) => o.textContent);
    expect(labels).not.toContain("null");
    expect(labels).not.toContain("undefined");
    expect(labels).toContain("Üç");
  });

  it("a text box's placeholder is NOT escaped (it is a property; escaping would show the entities)", () => {
    load(Swal).showConfirm("AreYouSure", () => {},
      { showInput: true, inputType: "text", inputPlaceholder: "Ar & Ge" });

    expect(popup().querySelector("input.swal2-input").getAttribute("placeholder")).toBe("Ar & Ge");
  });

  it("a glyph that tries to leave the class attribute is refused — no tag is needed for that", () => {
    load(Swal).showConfirm("AreYouSure", () => {}, { icon: 'bx-x" onmouseover="window.__ctProbe=1' });

    const glyph = popup().querySelector(".swal-icon-circle > i.bx");
    expect(glyph, "the type's own glyph is drawn instead").toBeTruthy();
    expect(Array.from(popup().querySelectorAll("*")).some((e) => e.hasAttribute("onmouseover"))).toBe(false);
  });

  it("the published icon builder refuses it too — raw dialogs call the builder directly", () => {
    const html = load(Swal).DitenDialogAppearance.iconHtml("warning", 'bx-x" onmouseover="alert(1)');

    expect(html).not.toContain("onmouseover");
    expect(html).toContain("bx-error");
    expect(load(Swal).DitenDialogAppearance.iconHtml("warning", "bx-moon")).toContain("bx-moon");
  });

  it("an explicit null subtextHtml is not a form body: the sentence is still drawn", () => {
    load(Swal).showConfirm("AreYouSure", () => {}, { subtextHtml: null, subtext: "Bu kayıt silinecek." });

    expect(popup().querySelector(".swal2-html-container").textContent).toBe("Bu kayıt silinecek.");
  });

  it("an attribute NAME that is live (on…, style) is dropped; an ordinary one is kept", () => {
    load(Swal).showConfirm("AreYouSure", () => {}, {
      showInput: true, inputType: "text",
      inputAttributes: { onfocus: "window.__ctProbe=1", style: "display:none", maxlength: "20" },
    });

    const input = popup().querySelector("input.swal2-input");
    expect(input.hasAttribute("onfocus")).toBe(false);
    expect(input.hasAttribute("style") && /display:\s*none/.test(input.getAttribute("style"))).toBe(false);
    expect(input.getAttribute("maxlength")).toBe("20");
  });
});

describe("a question that was never shown has not been answered", () => {
  it("an option that throws while the dialog is being built does NOT confirm the action", () => {
    let confirmed = 0;
    let dismissed = 0;
    const cyclic = {};
    cyclic.self = cyclic;   // the label walk recurses into objects → RangeError inside the try

    load(Swal).showConfirm("DeleteConfirmation", () => { confirmed += 1; },
      { showInput: true, inputType: "select", inputOptions: cyclic, onCancel: () => { dismissed += 1; } });

    expect(confirmed, "a dialog that could not be drawn ran the destructive callback").toBe(0);
    expect(dismissed).toBe(1);
  });

  it("without the library, a FORM body cannot be answered: it is dismissed and the browser is not even asked", () => {
    let asked = 0;
    let confirmed = 0;
    let dismissed = 0;

    load(undefined, () => { asked += 1; return true; }).showConfirm("Dışa aktar", () => { confirmed += 1; },
      { subtextHtml: "<form><input name='reason'></form>", onCancel: () => { dismissed += 1; } });

    expect(asked).toBe(0);
    expect(confirmed).toBe(0);
    expect(dismissed).toBe(1);
  });

  it("without the library, a plain question is still asked with the caller's words", () => {
    const seen = [];
    let confirmed = 0;

    load(undefined, (message) => { seen.push(message); return true; })
      .showConfirm("Sil", () => { confirmed += 1; }, { subtext: "Ar & Ge <taslak>" });

    expect(seen).toEqual(["Sil\n\nAr & Ge <taslak>"]);
    expect(confirmed).toBe(1);
  });
});

describe("the two aliases of the shared confirm", () => {
  const jsFiles = [];
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) { if (entry.name !== "vendor") { walk(full); } }
    else if (entry.name.endsWith(".js")) { jsFiles.push(full); }
  });
  walk(path.join(webRoot, "wwwroot", "assets", "js"));

  it("are exactly these two — a new alias is invisible to the caller scan, so it is added here on purpose", () => {
    const aliasing = jsFiles
      .filter((file) => /=\s*(?:o\.confirm\s*\|\|\s*)?(?:global|window)\.showConfirm\s*;/.test(fs.readFileSync(file, "utf8")))
      .map((file) => path.relative(webRoot, file).split(path.sep).join("/"))
      .sort();

    expect(aliasing).toEqual([
      "wwwroot/assets/js/WorkCenterNext/app.js",
      "wwwroot/assets/js/shared/diten-invite-card.js",
    ]);
  });

  describe("the Task Center seam (sharedConfirm)", () => {
    const seam = APP.slice(APP.indexOf("const sharedConfirm = (options) =>"), APP.indexOf("inputValidator: options.input && options.input.validate"));

    it("hands on a built body only through the named HTML input", () => {
      expect(seam).toContain("subtextHtml: options.subtextHtml,");
      expect(seam).toContain("entityName: options.entityName,");
    });

    it("no caller in the module escapes its words first, and none puts a tag into text", () => {
      const lines = APP.split("\n").map((line) => line.trim());
      const textValues = lines.filter((line) => /^(subtext|entityName|title|confirmText|cancelText):/.test(line));

      expect(textValues.length, "the scan found nothing — it is broken").toBeGreaterThan(10);
      expect(textValues.filter((line) => /^(subtext|entityName):\s*esc\(/.test(line))).toEqual([]);
      expect(textValues.filter((line) => /^(subtext|entityName):\s*[`'"][^`'"]*</.test(line))).toEqual([]);
    });

    it("exactly two dialogs of the module build their body as markup, and every value inside is escaped there", () => {
      const built = APP.split("\n").map((line) => line.trim()).filter((line) => /^subtextHtml:\s*`/.test(line));

      expect(built).toEqual([
        "subtextHtml: `<div class=\"wcn-confirm-body\">${esc(t('CommentWithdrawConfirm'))}</div>`,",
        "subtextHtml: `${outcomeLead(action)}<div class=\"wcn-confirm-body\">${esc(body)}</div>${requiredWarning}`,",
      ]);
      // The two helpers that contribute markup to the second one escape what they interpolate.
      expect(APP).toContain("return key ? `<p class=\"wcn-dialog-lead\">${esc(t(key))}</p>` : '';");
      expect(APP).toContain("? `<div class=\"wcn-confirm-warning\">${esc(tf('ConfirmRequiredOpen', stillOpen.length))}</div>`");
    });
  });

  it("the invitation card hands its sentence over as text — the block title somebody typed is not escaped twice", () => {
    const question = INVITE.slice(INVITE.indexOf("const confirm = o.confirm || global.showConfirm;"), INVITE.indexOf("confirmButtonText: label('InviteAcceptAnyway'"));

    expect(question).toContain("block.title, formatHours(block.startAt, block.endAt, zone));");
    expect(question).not.toMatch(/esc\(/);
    expect(question).not.toContain("subtextHtml");
  });
});

describe("the escape that feeds the HTML door of resource assignments", () => {
  it("escapes quotes: it is used inside quoted attributes", () => {
    const body = RESOURCE.slice(RESOURCE.indexOf("function esc(v) {"), RESOURCE.indexOf("}", RESOURCE.indexOf("function esc(v) {")) + 1);
    // eslint-disable-next-line no-new-func
    const esc = new Function(`${body}; return esc;`)();

    expect(esc('x" onclick="alert(1)')).toBe("x&quot; onclick=&quot;alert(1)");
    expect(esc("Ali's <b> & co")).toBe("Ali&#39;s &lt;b&gt; &amp; co");
    expect(esc(null)).toBe("");
  });
});
