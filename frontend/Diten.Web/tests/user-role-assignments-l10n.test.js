const fs = require("fs");
const path = require("path");

/*
 * WP-URA-FIX-01 (XMC-001) — the User Roles screen speaks every tenant language.
 *
 *   · No value in fr / es / zh / ar / ru may equal the English one: seven keys (Title, PageDescription, SelectUser,
 *     SelectUserPrompt, NoRoles, Assign, Assigned) shipped in English in all five, so the screen's heading, its picker
 *     label and its Assign button read English there. A deliberate exception (a name, an abbreviation) must be listed
 *     below by key — the list is empty today.
 *   · Every key exists, non-empty, in all seven files.
 *   · The page heading and the menu name (Nav.Page.*) say the same thing, for User Roles and for Role Permissions —
 *     fr / ru / ar had two different wordings for the second one.
 */

const ROOT = path.resolve(__dirname, "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const NON_ENGLISH = ["tr", "fr", "es", "zh", "ar", "ru"];

// Keys whose value may legitimately equal English in a language (a product name, a code). Empty on purpose.
const SAME_AS_ENGLISH_ALLOWED = {};

// XML entities decoded, so `&amp;` / `&#39;` cannot make an English copy look different.
const decode = (text) => text
  .replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&apos;/g, "'")
  .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(Number(d)))
  .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16)))
  .replace(/&amp;/g, "&");

const read = (relative) => {
  const xml = fs.readFileSync(path.join(ROOT, relative), "utf8");
  const out = new Map();
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) { out.set(m[1], decode(m[2]).trim()); }
  return out;
};

// FIX1 — "the same as English" is measured on the WORDS: case, trailing punctuation and invisible characters
// (zero-width, bidi marks) do not make "assign", "Assign." or "Assign\u200B" a translation of "Assign".
const comparable = (text) => String(text)
  .replace(/[\u200B-\u200F\u202A-\u202E\u2060-\u2069\uFEFF\u061C]/g, "")
  .replace(/[\s.!?…:;,。！？،؛]+$/u, "")
  .trim()
  .toLocaleLowerCase("en");

const screen = Object.fromEntries(LANGS.map((lang) => [lang,
  read(`Resources/Views/Governance/UserRoleAssignments/UserRoleAssignmentsIndex.${lang}.resx`)]));
const rolePermissions = Object.fromEntries(LANGS.map((lang) => [lang,
  read(`Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.${lang}.resx`)]));
const shared = Object.fromEntries(LANGS.map((lang) => [lang, read(`Resources/SharedResource.${lang}.resx`)]));

describe("User Roles screen: every tenant language", () => {
  test("the seven keys that shipped in English are present (guards against a vacuous scan)", () => {
    ["Title", "PageDescription", "SelectUser", "SelectUserPrompt", "NoRoles", "Assign", "Assigned"]
      .forEach((key) => expect(screen.en.has(key), key).toBe(true));
  });

  test("every key exists, non-empty, in all seven languages", () => {
    const gaps = [];
    for (const key of screen.en.keys()) {
      LANGS.forEach((lang) => { if (!(screen[lang].get(key) || "").length) { gaps.push(`${lang}/${key}`); } });
    }
    expect(gaps).toEqual([]);
  });

  test("no value in another language is the English text (exceptions only by name)", () => {
    const english = [];
    for (const [key, value] of screen.en) {
      NON_ENGLISH.forEach((lang) => {
        if (comparable(screen[lang].get(key)) === comparable(value) && !(SAME_AS_ENGLISH_ALLOWED[lang] || []).includes(key)) {
          english.push(`${lang}/${key} = "${value}"`);
        }
      });
    }
    expect(english).toEqual([]);
  });
});

describe("the guard's comparison cannot be dodged by spelling", () => {
  test.each(["assign", "Assign.", "Assign\u200B", " ASSIGN ", "Assign&#46;"])("%j still counts as the English 'Assign'", (variant) => {
    expect(comparable(decode(variant))).toBe(comparable("Assign"));
  });
});

describe("Arabic Role Permissions speaks of permissions with ONE word (أذونات)", () => {
  test("no value in the Arabic Role Permissions file uses صلاحيات", () => {
    const mixed = [...rolePermissions.ar].filter(([, value]) => /صلاحي/.test(value)).map(([key]) => key);
    expect(mixed).toEqual([]);
  });
});

describe("page heading and menu name say the same thing", () => {
  test.each(LANGS)("%s: User Roles heading = Nav.Page.USERROLES", (lang) => {
    expect(screen[lang].get("Title")).toBe(shared[lang].get("Nav.Page.USERROLES"));
  });

  test.each(LANGS)("%s: Role Permissions heading = Nav.Page.ROLEPERMISSIONS", (lang) => {
    expect(rolePermissions[lang].get("Title")).toBe(shared[lang].get("Nav.Page.ROLEPERMISSIONS"));
  });
});
