const fs = require("fs");
const path = require("path");

/*
 * THE INVITE-LINK DIALOG WEARS THE PRODUCT'S DIALOG (owner report, 2026-09-21).
 *
 * What the owner photographed after "Daveti yeniden gönder": a 38px CENTRED title reading "Invite link (dev)",
 * an English sentence under it, and a Turkish "İptal" button — three defects with one cause and one extra.
 *
 *   • The look was hand-written. `_GlobalConfirmation.cshtml` publishes the product's dialog package precisely
 *     so a raw `Swal.fire` — which this has to be, since it carries a field plus a copy button and the shared
 *     confirm deliberately does not take those — still opens looking like every other dialog. This file wrote
 *     its own `padding`, `popup`, icon markup and title spacing instead, so it inherited none of it: SweetAlert's
 *     unclassed title is 38px and centred.
 *   • Not one of its strings existed. `InviteLinkTitle`, `InviteLinkHint` and `Copied` were in no resx at all —
 *     every line came from the `||` fallback in the JS, which is English by definition. The Turkish button was
 *     the only localized thing on screen, and it said the wrong word: "İptal" for a dialog that cancels nothing.
 *
 * So these tests pin BOTH halves: the dialog reads the published package (and keeps no copy of it), and every
 * visible string comes from the seven-language resources rather than from a fallback literal.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

const JS = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/** The dialog's own body — assertions must not be satisfied by some other dialog in the same file. */
const dialogSource = () => {
  const source = JS();
  const from = source.indexOf("const showInviteLink =");
  expect(from, "showInviteLink is gone — has the dialog moved?").toBeGreaterThan(-1);
  return source.slice(from, source.indexOf("\n    const bindEvents", from));
};

/** The published package, evaluated out of the Razor partial that owns it. */
const loadAppearance = () => {
  const view = read("Views", "Shared", "_GlobalConfirmation.cshtml");
  const slice = (marker) => {
    const at = view.indexOf(marker);
    expect(at, `${marker} is no longer declared in _GlobalConfirmation.cshtml`).toBeGreaterThan(-1);
    let depth = 0;
    for (let i = view.indexOf("{", at); i < view.length; i++) {
      if (view[i] === "{") { depth++; }
      else if (view[i] === "}") { depth--; if (depth === 0) { return view.slice(at, i + 1) + ";"; } }
    }
    throw new Error(`unbalanced braces after ${marker}`);
  };
  const description = /window\.DitenDialogAppearance\.description = ('[^']*');/.exec(view);
  expect(description, "the published description class is gone").toBeTruthy();
  const scope = { window: {} };
  // eslint-disable-next-line no-new-func
  new Function("window", [slice("window.DitenDialogAppearance = function"),
    slice("window.DitenDialogAppearance.iconHtml = function"),
    `window.DitenDialogAppearance.description = ${description[1]};`].join("\n"))(scope.window);
  return scope.window.DitenDialogAppearance;
};

describe("the dialog reads the product's package instead of drawing its own", () => {
  test("it spreads the published appearance, and widens it the one way a caller may", () => {
    const body = dialogSource();
    expect(body).toMatch(/\.\.\.look\(\{ width: '520px' \}\)/);
    // A URL needs the room; `width` is the single geometry _GlobalConfirmation lets a caller set.
    expect(body).toMatch(/const look = window\.DitenDialogAppearance;/);
  });

  test("it keeps NO copy of the look — not the popup class, not the padding, not the circle", () => {
    const body = dialogSource();
    // MUTATION GUARD: paste any of these back and this goes red. Two copies of a look drift within a fortnight.
    expect(body).not.toContain("rounded-4");
    expect(body).not.toContain("padding:");
    expect(body).not.toContain("swal-icon-circle");
    expect(body).not.toContain("customClass:");
  });

  test("the icon and the description class come from the builder, by name", () => {
    const body = dialogSource();
    expect(body).toContain("look.iconHtml(null, 'bx-link-alt')");
    expect(body).toContain("${look.description}");
  });

  test("the picture rides the TITLE — the library's icon slot is collapsed and would show nothing", () => {
    /*
     * MEASURED LIVE (2026-09-21): passing the circle as `iconHtml` drew no icon at all, because Option B moved
     * the picture onto the title's line and the package hides the slot (`dt-dialog-iconslot`). `showConfirm`
     * composes `iconHtml + '<span>' + title + '</span>'`; a raw dialog has to do the same thing.
     */
    const body = dialogSource();
    expect(body).toMatch(/title: look\.iconHtml\(null, 'bx-link-alt'\) \+ `<span>\$\{L\.InviteLinkTitle\}<\/span>`/);
    expect(body, "an icon in the collapsed slot is an icon nobody sees").not.toMatch(/\biconHtml:/);
    // And the slot really is collapsed — this is why, not an opinion.
    expect(read("wwwroot", "assets", "css", "backbone-custom.css"))
      .toMatch(/\.swal2-icon\.dt-dialog-iconslot \{ display: none !important; \}/);
  });

  test("the seam it reads is really there, and still says what the dialog depends on", () => {
    // Rename `.description` or drop the width override in the component and this — not a browser — reports it.
    const look = loadAppearance();
    expect(typeof look).toBe("function");
    expect(look({ width: "520px" }).width).toBe("520px");
    expect(look().customClass.title, "the title is no longer left-aligned product type").toMatch(/fs-5/);
    expect(look().customClass.title).toMatch(/text-start/);
    expect(typeof look.description).toBe("string");
    expect(look.description.length).toBeGreaterThan(0);
    expect(look.iconHtml(null, "bx-link-alt")).toContain("bx-link-alt");
    expect(look.iconHtml(null, "bx-link-alt")).toContain("swal-icon-circle");
  });

  test("it closes, it does not cancel — one neutral button", () => {
    const body = dialogSource();
    expect(body).toContain("confirmButtonText: L.Close");
    expect(body).toContain("showCancelButton: false");
    // "İptal" was the old fallback chain's answer, and it promised an undo that does not exist.
    expect(body).not.toContain("L.Cancel");
  });
});

describe("every string on it is localized — no English fallback survives", () => {
  test("the dialog names no literal of its own", () => {
    const body = dialogSource();
    [/Invite link/i, /Share this set-password link/i, /'Copied'/, /'Copy'/, /'OK'/]
      .forEach((literal) => expect(body, `a hardcoded string is still in the dialog: ${literal}`).not.toMatch(literal));
    expect(body).toContain("L.InviteLinkTitle");
    expect(body).toContain("L.InviteLinkHint");
    expect(body).toContain("L.Copy");
    expect(body).toContain("L.Copied");
  });

  LANGS.forEach((lang) => {
    test(`${lang} carries the two module keys and the shared one`, () => {
      const users = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
      const shared = read("Resources", `SharedResource.${lang}.resx`);
      [["InviteLinkTitle", users], ["InviteLinkHint", users], ["Copied", shared], ["Close", shared], ["Copy", shared]]
        .forEach(([key, resx]) => {
          const m = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(resx);
          expect(m, `${key} missing in ${lang}`).toBeTruthy();
          expect(m[1].trim().length, `${key} empty in ${lang}`).toBeGreaterThan(0);
        });
    });
  });

  test("the six non-English files are translated, not copies", () => {
    const value = (resx, key) => new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(resx)[1];
    const enUsers = read("Resources", "Views", "Governance", "Users", "UsersIndex.en.resx");
    const enShared = read("Resources", "SharedResource.en.resx");
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const users = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
      const shared = read("Resources", `SharedResource.${lang}.resx`);
      const same = [["InviteLinkTitle", users, enUsers], ["InviteLinkHint", users, enUsers], ["Copied", shared, enShared]]
        .filter(([key, mine, en]) => value(mine, key) === value(en, key))
        .map(([key]) => key);
      expect(same, `${lang} carries untranslated English for: ${same.join(", ")}`).toEqual([]);
    });
  });

  test("the bridge publishes them and the loader warns when one goes missing", () => {
    const bridge = read("Views", "Governance", "Users", "_IndexL10n.cshtml");
    const loader = read("wwwroot", "assets", "js", "Governance", "Users", "index.l10n.js");
    expect(bridge).toMatch(/InviteLinkTitle = Localizer\["InviteLinkTitle"\]\.Value/);
    expect(bridge).toMatch(/InviteLinkHint = Localizer\["InviteLinkHint"\]\.Value/);
    ["Close", "Copy", "Copied"].forEach((key) =>
      expect(bridge, `${key} is not bridged from the shared resource`).toMatch(new RegExp(`${key} = SharedLocalizer\\["${key}"\\]\\.Value`)));
    ["InviteLinkTitle", "InviteLinkHint", "Close", "Copy", "Copied"].forEach((key) =>
      expect(loader, `${key} is not in the loader's required list`).toMatch(new RegExp(`'${key}'`)));
  });
});
