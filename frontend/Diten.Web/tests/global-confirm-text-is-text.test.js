const fs = require("fs");
const path = require("path");

/*
 * WP-SHARED-CONFIRM-XSS-01 — WHAT A CALLER HANDS THE SHARED CONFIRM IS TEXT.
 *
 * `window.showConfirm` (Views/Shared/_GlobalConfirmation.cshtml) is every confirmation in the product, and
 * SweetAlert — the library behind it — writes its title, its body, its buttons, its select options and its
 * validation message with `innerHTML`. Callers pass text other people typed: a user's e-mail, a role's name, a
 * task's title. Handed over raw, markup in a record's name was built into the page of whoever opened that record's
 * "Delete" dialog — a stored script in ANOTHER user's session.
 *
 * THE RULE. The dialog turns every caller value into text at the door, with one helper, so no caller has to
 * remember — and a caller must NOT escape first, or the reader sees the entities. A caller that builds a FORM for
 * the body says so by name: `subtextHtml`, and the callers that use it are pinned below.
 *
 * HOW THIS IS TESTED. The wrapper's script is EXTRACTED from the Razor view and run against the REAL vendored
 * SweetAlert in this DOM, so what is asserted is what the browser builds — not a regex on the source, and not a
 * stub's idea of where a value goes. Only Razor's localizer calls are replaced.
 */
const webRoot = path.resolve(__dirname, "..");
const VIEW = fs.readFileSync(path.join(webRoot, "Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");
const TOAST_VIEW = fs.readFileSync(path.join(webRoot, "Views", "Shared", "_GlobalNotification.cshtml"), "utf8");
const SWAL_SOURCE = fs.readFileSync(
  path.join(webRoot, "wwwroot/assets/vendor/libs/sweetalert2/sweetalert2.js"), "utf8");

const HOSTILE_IMG = '<img src="x" onerror="window.__confirmProbe=1">Rapor & <b>taslak</b>';
const HOSTILE_SCRIPT = "<script>window.__confirmProbe=1</script>Ali's \"rol\"";
const quiet = { log: () => {}, warn: () => {}, error: () => {} };

// The vendored bundle, evaluated the way a <script> tag would: no module system, its global lands on `window`.
const loadSwal = () => {
  const host = {};
  // eslint-disable-next-line no-new-func
  new Function("self", "window", "document", "exports", "module", "define", SWAL_SOURCE)(
    host, global.window, global.document, undefined, undefined, undefined);
  return host.Swal || global.window.Swal;
};

const loadShowConfirm = (Swal) => {
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const win = {};
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(win, Swal, quiet, () => false);
  return win;
};

// (The input tests name `inputType: "text"`: the library's default textarea arms a resize timer that outlives a
// test's DOM in jsdom. The type changes nothing about where a label or a validation message is written.)
const Swal = loadSwal();
const popup = () => document.querySelector(".swal2-popup");

beforeEach(() => {
  if (Swal && Swal.isVisible && Swal.isVisible()) { Swal.close(); }
  document.body.innerHTML = "";
  delete global.window.__confirmProbe;
});

const expectNothingBuilt = () => {
  expect(popup(), "no dialog was drawn").toBeTruthy();
  // The library keeps ONE <img> of its own in every popup (its hidden image slot); anything else came from a value.
  const images = Array.from(popup().querySelectorAll("img"));
  expect(images.map((image) => image.className), "an <img> was built from a caller's value").toEqual(["swal2-image"]);
  expect(images[0].hasAttribute("onerror")).toBe(false);
  expect(popup().querySelector("script"), "a <script> was built from a caller's value").toBeNull();
  expect(popup().querySelector("b"), "a <b> was built from a caller's value").toBeNull();
  expect(global.window.__confirmProbe).toBeUndefined();
};

describe("the shared confirm, through the real SweetAlert", () => {
  it("loads the vendored library this product ships", () => {
    expect(typeof Swal?.fire, "the vendored SweetAlert did not load; every assertion below would be vacuous")
      .toBe("function");
  });

  describe.each([["an <img onerror>", HOSTILE_IMG], ["a <script>", HOSTILE_SCRIPT]])("%s", (_, hostile) => {
    it("in the record's name (entityName) is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { entityName: hostile });

      expectNothingBuilt();
      expect(popup().querySelector(".dt-dialog-entity").textContent).toBe(hostile);
    });

    it("as the bare third argument (the short form of entityName) is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, hostile);

      expectNothingBuilt();
      expect(popup().querySelector(".dt-dialog-entity").textContent).toBe(hostile);
    });

    it("in the sentence (subtext) is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { subtext: hostile });

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-html-container").textContent).toBe(hostile);
    });

    it("in the title is drawn as text, and the icon is still drawn beside it", () => {
      loadShowConfirm(Swal).showConfirm(hostile, () => {});

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-title > span").textContent).toBe(hostile);
      expect(popup().querySelector(".swal2-title > .swal-icon-circle > i.bx")).toBeTruthy();
    });

    it("in the title of a dialog without an icon is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm(hostile, () => {}, { hideIcon: true });

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-title").textContent).toBe(hostile);
    });

    it("on either button is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {},
        { confirmButtonText: hostile, cancelButtonText: hostile });

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-confirm").textContent).toBe(hostile);
      expect(popup().querySelector(".swal2-cancel").textContent).toBe(hostile);
    });

    it("in a select's options and placeholder is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, {
        showInput: true, inputType: "select", inputPlaceholder: hostile,
        inputOptions: { one: hostile, group: { two: hostile } },
      });

      expectNothingBuilt();
      const labels = Array.from(popup().querySelectorAll("select.swal2-select option")).map((o) => o.textContent);
      expect(labels).toEqual([hostile, hostile, hostile]);
    });

    it("in a select's options handed over as a Map is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, {
        showInput: true, inputType: "select", inputOptions: new Map([["one", hostile]]),
      });

      expectNothingBuilt();
      expect(popup().querySelector("select.swal2-select option").textContent).toBe(hostile);
    });

    it("in a radio's options is drawn as text", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, {
        showInput: true, inputType: "radio", inputOptions: { one: hostile },
      });

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-radio .swal2-label").textContent).toBe(hostile);
    });

    it("in the input's label is drawn as text, once", () => {
      loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { showInput: true, inputType: "text", inputLabel: hostile });

      expectNothingBuilt();
      // The library writes this one with `innerText` already: escaping it at the door would show the entities.
      // (jsdom keeps `innerText` as a plain property, so that is where the written value is read back from.)
      const label = popup().querySelector(".swal2-input-label");
      expect(label.innerText).toBe(hostile);
      expect(label.children.length).toBe(0);
    });

    // The dialog's own `preConfirm` is what speaks here; it is run exactly as the library would run it on "Yes",
    // and it writes through the library's real `showValidationMessage` into the real popup.
    const answerYes = (options) => {
      let config;
      const fire = Swal.fire.bind(Swal);
      const recording = Object.create(Swal);
      recording.fire = (given) => { config = given; return fire(given); };
      loadShowConfirm(recording).showConfirm("AreYouSure", () => {}, options);
      return config.preConfirm("");
    };

    it("in the 'required' message is drawn as text", () => {
      expect(answerYes({ showInput: true, inputType: "text", inputRequired: true, inputValidationMessage: hostile })).toBe(false);

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-validation-message").textContent).toBe(hostile);
    });

    it("in what the caller's validator answers is drawn as text", () => {
      expect(answerYes({ showInput: true, inputType: "text", inputValidator: () => hostile })).toBe(false);

      expectNothingBuilt();
      expect(popup().querySelector(".swal2-validation-message").textContent).toBe(hostile);
    });
  });

  it("a glyph name that is not a class name is not written into the icon's markup", () => {
    loadShowConfirm(Swal).showConfirm("AreYouSure", () => {},
      { icon: 'bx-x"><img src=x onerror="window.__confirmProbe=1">' });

    expectNothingBuilt();
    // The type's own glyph is drawn instead: the dialog still has its picture.
    expect(popup().querySelector(".swal2-title > .swal-icon-circle > i.bx-help-circle")).toBeTruthy();
  });

  it("a glyph name that IS a class name is still honoured", () => {
    loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { icon: "bx-moon" });

    expect(popup().querySelector(".swal2-title > .swal-icon-circle > i.bx-moon")).toBeTruthy();
  });

  it("the built-in sentence and buttons are unchanged", () => {
    loadShowConfirm(Swal).showConfirm("DeleteConfirmation", () => {}, { entityName: "Ayşe Yılmaz" });

    expect(popup().querySelector(".swal2-title > span").textContent).toBe("DeleteConfirmationTitle");
    expect(popup().querySelector(".swal2-html-container > div").textContent).toBe("DeleteConfirmationSubText");
    expect(popup().querySelector(".dt-dialog-entity").textContent).toBe("Ayşe Yılmaz");
    expect(popup().querySelector(".swal2-confirm").textContent).toBe("DeleteConfirmationYesBtn");
    expect(popup().querySelector(".swal2-cancel").textContent).toBe("Cancel");
  });

  it("the native fallback (no SweetAlert) asks with the caller's words, not with entities", () => {
    const asked = [];
    const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
    const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
    const win = {};
    // eslint-disable-next-line no-new-func
    new Function("window", "Swal", "console", "confirm", js)(win, undefined, quiet, (q) => { asked.push(q); return false; });

    win.showConfirm("R&D <ekip>", () => {}, { subtext: "Ali's" });

    expect(asked).toEqual(["R&D <ekip>\n\nAli's"]);
  });
});

describe("a body that is a FORM says so by name (subtextHtml)", () => {
  const FORM = '<div class="form-check"><input type="checkbox" id="probe-box"><label for="probe-box">Bağlantılar</label></div>';

  it("subtextHtml is built as markup — that is what it is for", () => {
    loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { subtextHtml: FORM });

    expect(popup().querySelector("#probe-box")).toBeTruthy();
  });

  it("the same markup through subtext is text", () => {
    loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { subtext: FORM });

    expect(popup().querySelector("#probe-box")).toBeNull();
    expect(popup().querySelector(".swal2-html-container").textContent).toBe(FORM);
  });

  it("the record's name beside a form body is still text", () => {
    loadShowConfirm(Swal).showConfirm("AreYouSure", () => {}, { subtextHtml: FORM, entityName: HOSTILE_IMG });

    expect(popup().querySelectorAll("img").length).toBe(1);
    expect(popup().querySelector(".dt-dialog-entity").textContent).toBe(HOSTILE_IMG);
  });
});

describe("one helper turns a value into text", () => {
  const corpus = [HOSTILE_IMG, HOSTILE_SCRIPT, "R&D", "a < b > c", "\"q\" 'a'", "&amp;", "", 0, null, undefined, 42];

  it("the confirm's helper and the toast's helper are the same function, character for character", () => {
    const body = (view, name) => {
      const start = view.indexOf("window." + name + " = function (value) {");
      expect(start, name + " is not declared the way this guard reads it").toBeGreaterThan(-1);
      return view.slice(view.indexOf("{", start), view.indexOf("};", start)).replace(/\s+/g, " ");
    };

    // Each door is loaded on its own by its own tests, so each carries the helper; they may never drift.
    expect(body(VIEW, "DitenText")).toBe(body(TOAST_VIEW, "DitenToastText"));
  });

  it("is declared exactly once in the confirm, and escapes the five characters", () => {
    expect(VIEW.split("window.DitenText = function").length - 1).toBe(1);
    const win = loadShowConfirm(Swal);
    expect(win.DitenText('<a href="x">&\'</a>')).toBe("&lt;a href=&quot;x&quot;&gt;&amp;&#39;&lt;/a&gt;");
    corpus.forEach((value) => {
      const host = document.createElement("div");
      host.innerHTML = win.DitenText(value);
      expect(host.textContent).toBe(String(value));
      expect(host.children.length).toBe(0);
    });
  });
});

/*
 * ── THE CALLERS ──────────────────────────────────────────────────────────────────────────────────────────────
 *
 * Read from the source: every `showConfirm(...)` / `DitenModal.confirm(...)` call in the product's own scripts and
 * views. Two rules are held here, and both are about the door doing the escaping:
 *   - no caller escapes a value it hands over (the reader would see `&amp;`);
 *   - a body of markup goes through `subtextHtml`, and only the callers named below use it.
 */
const SCRIPT_ROOTS = ["wwwroot/assets/js", "Views"];
const TEXT_KEYS = ["entityName", "subtext", "confirmButtonText", "cancelButtonText", "inputLabel",
  "inputPlaceholder", "inputValidationMessage"];
const ESCAPES = /\b(esc\w*|escape\w*|\w*encodeHtml\w*|sanitize\w*|DitenText|DitenToastText)\s*\(/i;

const walk = (dir, out) => {
  fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "vendor" || entry.name === "libs" || entry.name === "node_modules") { return; }
      walk(full, out);
    } else if (/\.(js|cshtml)$/.test(entry.name) && entry.name !== "_GlobalConfirmation.cshtml") {
      out.push(full);
    }
  });
  return out;
};

// The text between a call's parentheses, and a split of it on top-level commas — strings and nesting respected.
const readCall = (source, open) => {
  let depth = 1; let quote = null; let i = open;
  for (; i < source.length && depth > 0; i += 1) {
    const c = source[i];
    if (quote) { if (c === "\\") { i += 1; } else if (c === quote) { quote = null; } }
    else if (c === '"' || c === "'" || c === "`") { quote = c; }
    else if (c === "(") { depth += 1; }
    else if (c === ")") { depth -= 1; }
  }
  return source.slice(open, i - 1);
};
const splitTop = (text) => {
  const parts = []; let depth = 0; let quote = null; let current = "";
  for (let i = 0; i < text.length; i += 1) {
    const c = text[i];
    if (quote) { current += c; if (c === "\\") { current += text[i + 1]; i += 1; } else if (c === quote) { quote = null; } }
    else if (c === '"' || c === "'" || c === "`") { quote = c; current += c; }
    else if ("([{".includes(c)) { depth += 1; current += c; }
    else if (")]}".includes(c)) { depth -= 1; current += c; }
    else if (c === "," && depth === 0) { parts.push(current); current = ""; }
    else { current += c; }
  }
  parts.push(current);
  return parts.map((part) => part.trim());
};

const calls = [];
SCRIPT_ROOTS.forEach((root) => walk(path.join(webRoot, root), []).forEach((file) => {
  const source = fs.readFileSync(file, "utf8");
  const pattern = /(?:showConfirm|DitenModal\??\.confirm)\s*(?:\?\.)?\s*\(/g;
  let match;
  while ((match = pattern.exec(source)) !== null) {
    const args = splitTop(readCall(source, match.index + match[0].length));
    const options = {};
    if (args[2] && args[2].startsWith("{")) {
      splitTop(args[2].slice(1, -1)).forEach((pair) => {
        const keyed = /^([A-Za-z]+)\s*:\s*([\s\S]*)$/.exec(pair);
        if (keyed) { options[keyed[1]] = keyed[2]; }
      });
    }
    calls.push({
      file: path.relative(webRoot, file).split(path.sep).join("/"),
      line: source.slice(0, match.index).split("\n").length,
      title: args[0] || "",
      options,
    });
  }
}));

describe("the callers of the shared confirm", () => {
  it("are found (the scan is not vacuous)", () => {
    expect(calls.length).toBeGreaterThan(200);
    expect(calls.filter((call) => call.options.entityName).length).toBeGreaterThan(100);
  });

  it("hand over their values as they are — none escapes first", () => {
    const escaping = [];
    calls.forEach((call) => {
      if (ESCAPES.test(call.title)) { escaping.push(`${call.file}:${call.line} title: ${call.title}`); }
      TEXT_KEYS.forEach((key) => {
        if (call.options[key] && ESCAPES.test(call.options[key])) {
          escaping.push(`${call.file}:${call.line} ${key}: ${call.options[key]}`);
        }
      });
    });

    expect(escaping, "the dialog escapes at the door; a caller that escapes too shows the reader `&amp;`")
      .toEqual([]);
  });

  it("pass a body of markup only through subtextHtml, and only these do", () => {
    const users = calls.filter((call) => call.options.subtextHtml !== undefined)
      .map((call) => call.file).sort();

    expect(users).toEqual([
      "wwwroot/assets/js/CRM/Contacts/index.js",
      "wwwroot/assets/js/CRM/TerritoryManagement/resource-assignments.js",
      "wwwroot/assets/js/Platform/AuditLog/index.js",
    ]);
  });

  it("never write a tag into a value that is text", () => {
    const tagged = [];
    calls.forEach((call) => TEXT_KEYS.concat(["title"]).forEach((key) => {
      const value = key === "title" ? call.title : call.options[key];
      if (value && /<\s*\/?\s*[a-zA-Z][^>]*>/.test(value)) { tagged.push(`${call.file}:${call.line} ${key}`); }
    }));

    expect(tagged).toEqual([]);
  });
});
