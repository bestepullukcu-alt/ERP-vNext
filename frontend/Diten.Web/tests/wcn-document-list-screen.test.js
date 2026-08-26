const fs = require("fs");
const path = require("path");

/*
 * DCP-005 slice 2 — the document-list SCREEN.
 *
 * Asserted on the source rather than on a booted page: the screen's three behaviours are decisions
 * (show-but-refuse, information-not-error, invalidate-on-change) and each one is a line somebody could
 * plausibly "simplify" away.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const JS = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "DocumentList", "index.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "DocumentList.cshtml"), "utf8");
const MANIFEST = fs.readFileSync(path.join(
  repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application",
  "Features", "Tasks", "SelfRegistration", "TaskManifestProvider.cs"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

describe("a blocked document is shown and refused, never hidden", () => {
  it("renders blocked rows instead of filtering them out", () => {
    /*
     * MUTATION GUARD: filter the search results to linkable rows and this goes red.
     *
     * 36 of the 358 cannot be cited — 23 planned, 7 void, 6 that QA's own register is missing. Hiding them
     * leaves "where is that SOP" unanswerable; showing them with a reason answers it.
     */
    expect(JS, "the results were filtered to citable rows").not.toMatch(/filter\([^)]*linkableInErp\)/);
    expect(JS).toContain("const blocked = !d.linkableInErp;");
  });

  it("says it in MORE THAN COLOUR", () => {
    /*
     * A row that reads as "cannot be chosen" only to somebody who can see the grey is a row a screen-reader
     * user would try to choose. Three signals: the state, the reason as text, and only then the muted class.
     *
     * MUTATION GUARD: drop `aria-disabled` and this goes red.
     */
    expect(JS, "unselectability is carried by colour alone").toContain('aria-disabled="true"');
    expect(JS, "the reason is not rendered").toContain("d.linkBlockedReason");
    expect(JS).toContain("DocListNotLinkable");
  });
});

describe("'already imported' is information, not a failure", () => {
  it("renders the 409 as an info note", () => {
    /*
     * MUTATION GUARD: report it as an error and this goes red.
     *
     * The reader already has the state they asked for. Calling it a failure sends them hunting for a problem
     * that does not exist — the same class as the toast that said "{1}" a few rounds ago.
     */
    /*
     * ⚠ ANCHORED ON THE CONDITION, NOT ON ITS EXACT TEXT. The first version pinned the literal
     * `} else if (status === 409) {`, and adding the reason-code check to the same branch broke the test
     * without breaking the behaviour. A window pinned to code that is allowed to grow is not a window.
     */
    const at = JS.search(/} else if \(status === 409/);
    expect(at, "the 409 branch is missing").toBeGreaterThan(-1);
    const branch = JS.slice(at).split("} else {")[0];
    expect(branch).toContain("alert-info");
    expect(branch, "a 409 was reported as an error toast").not.toContain("'error'");
  });

  it("says the same thing in the dry-run summary", () => {
    expect(JS).toContain("alreadyImportedAsVersion");
    expect(JS).toContain("DocListAlreadyImported");
  });
});

describe("a changed file invalidates the dry run", () => {
  it("closes the import until the new file is checked", () => {
    /*
     * MUTATION GUARD: remove the change listener's reset and this goes red.
     *
     * Copied from the taxonomy wizard deliberately: without it a person validates one file and imports
     * another, which is exactly what the server's 409 catches only AFTER the fact.
     */
    const onChange = JS.split("fileInput.addEventListener('change'")[1].split("});")[0];
    expect(onChange).toContain("validatedSignature = null");
    expect(onChange).toContain("setImportEnabled(false)");
  });

  it("re-guards at the button, because that is the last place it can be stopped", () => {
    const onImport = JS.split("btnImport.addEventListener('click'")[1].split("});")[0];
    expect(onImport).toContain("signatureOf(file) !== validatedSignature");
  });

  it("does not enable the import for a file that would not parse", () => {
    expect(JS).toContain("setImportEnabled((json.data.errors || []).length === 0)");
  });
});

describe("the page the manifest publishes actually exists", () => {
  it("is nav-visible only because the view and the route are here", () => {
    /*
     * ⚠ THE DEFECT THIS PREVENTS WAS REAL. The page shipped `IsNavigationVisible: true` with no view and no
     * action, so the sidebar would have grown an entry pointing at a 404. The rule: a manifest page is
     * published visible only in the round its screen is measured open.
     */
    const page = MANIFEST.split("PageCode: PageDocumentList")[1].split("]),")[0];
    expect(page).toContain("IsNavigationVisible: true");
    expect(page).toContain('RoutePath: "/Tasks/DocumentList"');
    // …and the two halves that make that true.
    expect(VIEW.length).toBeGreaterThan(500);
    const controller = fs.readFileSync(web("Controllers", "TasksController.cs"), "utf8");
    expect(controller).toContain('[HttpGet("DocumentList")]');
  });
});

describe("the screen speaks seven languages", () => {
  it("has every key it asks for, in all of them", () => {
    const keys = [...new Set([
      // ⚠ NOT SharedLocalizer — those live in SharedResource.*.resx, a different family.
      ...[...VIEW.matchAll(/(?<!Shared)Localizer\["([^"]+)"\]/g)].map((m) => m[1]),
      ...[...fs.readFileSync(web("Views", "Tasks", "_DocumentListL10n.cshtml"), "utf8")
        .matchAll(/(?<!Shared)Localizer\["([^"]+)"\]/g)].map((m) => m[1])
    ])];
    expect(keys.length).toBeGreaterThan(20);

    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(
        web("Resources", "Views", "Tasks", "TaskTypes", `TaskTypesIndex.${lang}.resx`), "utf8");
      const missing = keys.filter((k) => !resx.includes(`name="${k}"`));
      expect(missing, `${lang} is missing: ${missing.join(", ")}`).toEqual([]);
    });
  });
});

describe("the precedent's contrast defect was NOT copied", () => {
  it("does not bring `.qms-steps` into a second screen", () => {
    /*
     * MEASURED on the precedent: the step indicator sits at 1.83 (light) / 2.02 (dark), well under AA, because
     * of `--bs-secondary-color`. Copying the markup would copy the defect; fixing it locally would fork a
     * product-wide token that 197 rules depend on. The steps are named in words instead.
     */
    // ⚠ Asserted on the MARKUP, not the file: the comment above deliberately NAMES the class it is
    // refusing to copy, and a bare substring search would match that explanation.
    expect(VIEW, "the precedent's step markup was copied").not.toMatch(/class="[^"]*qms-steps/);
    expect(VIEW, "the deliberate omission is undocumented").toContain("DELIBERATELY NOT COPIED");
  });
});


describe("the write surfaces are hidden from a reader", () => {
  const VIEW2 = fs.readFileSync(web("Views", "Tasks", "DocumentList.cshtml"), "utf8");

  it("gates the upload form on the import permission", () => {
    /*
     * MEASURED before this round: the page was published behind the IMPORT permission while the search it
     * exists for asks only a read permission — so a QA reader who could see every row could not open the screen
     * showing them. And once inside, there was no check at all: the form was drawn for everyone and the button
     * answered 403.
     *
     * MUTATION GUARD: drop the gate and this goes red.
     */
    expect(VIEW2).toContain('Perms.Has("platform.tasks.document-list.import")');
    expect(VIEW2, "a reader is still shown the upload form").toContain("if (!canImport)");
    expect(VIEW2).toContain("DocListImportDenied");
  });

  it("hides the withdraw button from a reader, and says the server is the guard", () => {
    expect(JS).toContain("data-can-withdraw");
    expect(JS).toContain("const canWithdraw =");
    // Hiding is a courtesy; the endpoint is the rule. Written down so nobody mistakes one for the other.
    expect(JS).toContain("The server enforces it");
  });

  it("keeps a withdrawn version VISIBLE, marked, with its reason", () => {
    /*
     * MUTATION GUARD: filter withdrawn rows out of the version list and this goes red. Hiding them would erase
     * the record of what the tenant used to cite against — the one thing a withdrawal must not do.
     */
    expect(JS).toContain("v.withdrawnAt");
    expect(JS).toContain("DocListWithdrawn");
    expect(JS).toContain("v.withdrawnReason");
    expect(JS, "withdrawn versions were filtered out").not.toMatch(/filter\([^)]*withdrawnAt\)/);
  });

  it("asks for the reason in the product's own dialog, not a page-local prompt", () => {
    expect(JS).toContain("global.showConfirm?.(");
    expect(JS, "a second dialog language appeared").not.toContain("window.prompt");
    expect(JS).toContain("DocListWithdrawReasonRequired");
  });

  /*
   * Sütun başlıklarından biri ("ACTİONS") ekranda ham İngilizce çıktı ve 14 testin arasından geçti: view
   * `SharedLocalizer["Actions"]` diyordu, paylaşılan kaynakta öyle bir anahtar YOKTU, ve .NET karşılığı
   * bulunmayan anahtarı sessizce anahtarın KENDİSİ olarak basar. Yani eksik çeviri hata vermez — okunur
   * görünür. Bu yüzden muhafız "anahtar var mı"yı sorar, "çeviri doğru mu"yu değil.
   */
  it("every localizer key the screen names exists in all seven languages", () => {
    // Her anahtar KENDİ kaynağına sorulur: `SharedLocalizer` ürünün ortak kümesine, çıplak `Localizer`
    // ekranın kendi kümesine. İkisini tek dosyada aramak, meşru bir paylaşılan anahtarı sahte kırmızı yapar.
    const keys = [...VIEW.matchAll(/@?(Shared)?Localizer\["([^"]+)"\]/g)].map((m) => ({
      key: m[2], shared: m[1] === "Shared",
    }));
    expect(keys.length).toBeGreaterThan(15);
    expect(keys.some((k) => k.shared)).toBe(true);

    const missing = [];
    LANGS.forEach((lang) => {
      const own = fs.readFileSync(
        web("Resources", "Views", "Tasks", "TaskTypes", `TaskTypesIndex.${lang}.resx`), "utf8");
      const shared = fs.readFileSync(web("Resources", `SharedResource.${lang}.resx`), "utf8");
      keys.forEach(({ key, shared: isShared }) => {
        if (!(isShared ? shared : own).includes(`name="${key}"`)) { missing.push(`${lang}:${key}`); }
      });
    });
    expect(missing).toEqual([]);
  });
});
