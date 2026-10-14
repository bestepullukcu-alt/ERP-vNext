const fs = require("fs");
const path = require("path");

/*
 * WP-UX-HELP-PANEL-02 — the "?" panel's new face (owner's design 2026-10-13) and what its text promises.
 *
 *   · every panel label the partial asks for (Help["Help.Panel.*"]) is real text in all 7 tenant languages;
 *   · the new page keys (Audience, Immediate, Step{n}Title, Related.*) are complete in every language that has one;
 *   · Users step 2 says Ctrl / ⌘ + Enter in every language, with the screen's own labels — its panel is `chord`
 *     (an invitation e-mail goes), so a bare Enter does not save;
 *   · the 5 pages' steps say Enter / Ctrl+Enter only where the page's real primary mark says so;
 *   · the "?" button does not jump on hover (Sneat's `.btn:hover { translateY(-1px) }` stops there).
 *
 * Measured against the production files (the partial, the resx, the panels' views, the stylesheets), never a copy.
 */

const WEB = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(WEB, ...parts), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const decode = (text) => text.replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
  .replace(/&amp;/g, "&");
const values = (file) => Object.fromEntries([...read(file).matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([^<]*)<\/value>/g)]
  .map((m) => [m[1], decode(m[2]).trim()]));
const help = Object.fromEntries(LANGS.map((lang) => [lang, values(`Resources/PageHelpResource.${lang}.resx`)]));

describe("panel labels", () => {
  const used = [...new Set([...read("Views", "Shared", "_PageHelpPanel.cshtml").matchAll(/Help\["(Help\.Panel\.[A-Za-z]+)"\]/g)]
    .map((m) => m[1]))];

  test("the partial asks for the new labels (guards against a vacuous list)", () => {
    expect(used).toEqual(expect.arrayContaining(["Help.Panel.Eyebrow", "Help.Panel.TabOverview", "Help.Panel.TabSteps",
      "Help.Panel.TabKeys", "Help.Panel.GeneralCard", "Help.Panel.RelatedCard", "Help.Panel.KeysEverywhere",
      "Help.Panel.KeysThisPage", "Help.Panel.MacNote", "Help.Panel.Close"]));
  });

  test.each(LANGS)("%s carries every label the partial draws, as its own text", (lang) => {
    const missing = used.filter((key) => !help[lang][key]);
    const english = lang === "en" ? [] : used.filter((key) => help[lang][key] && help[lang][key] === help.en[key]);
    expect(missing, "missing").toEqual([]);
    expect(english, "left in English").toEqual([]);
  });
});

describe("the new page keys", () => {
  const pattern = /^Help\.[A-Z0-9]+\.(Audience|Immediate|Step[1-5]Title|Related\.[A-Z0-9]+)$/;
  const keys = [...new Set(LANGS.flatMap((lang) => Object.keys(help[lang]).filter((key) => pattern.test(key))))];

  test("the 5 pages carry them (guards against a vacuous list)", () => {
    ["USERS", "ROLES", "ROLEPERMISSIONS", "USERROLES", "ITEMS"].forEach((code) => {
      expect(keys, code).toEqual(expect.arrayContaining([`Help.${code}.Audience`, `Help.${code}.Step1Title`]));
    });
  });

  test("each is real text in all 7 languages", () => {
    const gaps = keys.flatMap((key) => LANGS.filter((lang) => !help[lang][key]).map((lang) => `${lang}/${key}`));
    const english = keys.flatMap((key) => LANGS.filter((lang) => lang !== "en" && help[lang][key] === help.en[key])
      .map((lang) => `${lang}/${key}`));
    expect(gaps, "missing").toEqual([]);
    expect(english, "left in English").toEqual([]);
  });

  test("a step title exists only where its step does", () => {
    const orphans = keys.filter((key) => /Step\dTitle$/.test(key))
      .filter((key) => LANGS.some((lang) => !help[lang][key.replace(/Title$/, "")]));
    expect(orphans).toEqual([]);
  });
});

// The keyboard words each language uses for Enter, and "Ctrl+Enter" in that language.
const ENTER = /(?:^|[^\p{L}])(Enter|Entrée|Intro)(?![\p{L}])/u;
const CHORD = /Ctrl\s*\+\s*(Enter|Entrée|Intro)/;
const MAC_CHORD = /⌘\s*\+\s*(Enter|Entrée|Intro)/;

// Where each page's side panel (or form) lives: the mark is READ from the view, never written here.
const SURFACES = {
  USERS: "Views/Governance/Users/_CreateEditOffcanvas.cshtml",
  ROLES: "Views/Governance/Roles/_CreateEditOffcanvas.cshtml",
  ITEMS: "Views/MasterDataManagement/Items/_Form.cshtml",
  ROLEPERMISSIONS: null,
  USERROLES: null
};
const markOf = (file) => {
  if (!file) { return "none"; }
  const source = read(file);
  const marks = [...source.matchAll(/data-dt-primary="(enter|chord)"/g)].map((m) => m[1]);
  if (marks.length) { return marks.includes("chord") ? "chord" : "enter"; }
  return /type="submit"/.test(source) ? "submit" : "none";
};
const steps = (lang, code) => [1, 2, 3, 4, 5].map((n) => help[lang][`Help.${code}.Step${n}`]).filter(Boolean);

describe("Users step 2 says Ctrl / ⌘ + Enter — its panel is `chord` (an invitation e-mail goes)", () => {
  test("the Users panel's primary button is `chord`", () => {
    expect(markOf(SURFACES.USERS)).toBe("chord");
  });

  test.each(LANGS)("%s", (lang) => {
    const step = help[lang]["Help.USERS.Step2"];
    const page = values(`Resources/Views/Governance/Users/UsersIndex.${lang}.resx`);
    const shared = values(`Resources/SharedResource.${lang}.resx`);
    expect(step).toMatch(CHORD);
    expect(step).toMatch(MAC_CHORD);
    // the labels it names are the screen's own
    [page.AddNew, page.Email, page.FirstName, page.LastName, shared.Save].forEach((label) => {
      expect(label, "a label the screen has").toBeTruthy();
      expect(step).toContain(label);
    });
  });
});

describe("the 5 pages' steps say Enter / Ctrl+Enter as their panel's real mark does", () => {
  const cases = Object.keys(SURFACES).flatMap((code) => LANGS.map((lang) => [code, lang]));

  test.each(cases)("%s / %s", (code, lang) => {
    const mark = markOf(SURFACES[code]);
    const texts = steps(lang, code);
    expect(texts.length, "the page has steps").toBeGreaterThan(0);
    const wrong = [];
    texts.forEach((text, i) => {
      const where = `Step${i + 1}: ${text}`;
      if (mark === "chord") {
        // a step that tells how to save says the chord; nowhere a bare Enter without it
        if (ENTER.test(text) && !CHORD.test(text)) { wrong.push(`bare Enter on a chord panel — ${where}`); }
      } else if (CHORD.test(text)) {
        wrong.push(`Ctrl+Enter on a ${mark} surface — ${where}`);
      }
      if (mark === "none" && ENTER.test(text)) { wrong.push(`Enter on a page with no panel — ${where}`); }
    });
    if (mark === "chord") {
      expect(texts.some((text) => CHORD.test(text)), "no step says the chord").toBe(true);
    }
    expect(wrong).toEqual([]);
  });
});

describe("the \"?\" button does not jump", () => {
  const css = read("wwwroot", "assets", "css", "backbone-custom.css").replace(/\/\*[\s\S]*?\*\//g, "");
  const rule = (selector) => {
    const at = css.split(/(?<=\})/).find((block) => block.split("{")[0].split(",").map((s) => s.trim()).includes(selector));
    return at ? at.slice(at.indexOf("{")) : "";
  };

  test("Sneat moves every .btn on hover (the reason for the rule)", () => {
    expect(read("wwwroot", "assets", "vendor", "css", "core.css")).toMatch(/\.btn:hover \{\s*transform: translateY\(-1px\);/);
  });

  test("the button is a .btn carrying .diten-help-toggle", () => {
    expect(read("Views", "Shared", "_PageHelpButton.cshtml")).toMatch(/class="[^"]*\bbtn\b[^"]*\bditen-help-toggle\b/);
  });

  test.each([".btn.diten-help-toggle:hover", ".btn.diten-help-toggle:focus", ".btn.diten-help-toggle:active"])(
    "%s keeps it still", (selector) => {
      expect(rule(selector)).toMatch(/transform:\s*none/);
    });
});
