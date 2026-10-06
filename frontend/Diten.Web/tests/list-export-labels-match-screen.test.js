const fs = require("fs");
const path = require("path");

/*
 * BL-452 package 1 — the export FILE speaks the screen's words. AuthService and DevEnablement write the file headers
 * (and the status / account-kind / reference-type values) server-side, in the request culture, from their own resx —
 * because the services have no access to the Web's resources. This guard keeps those copies equal to the words the
 * screens show, in all seven tenant languages: a translator who fixes a header on the screen and forgets the service
 * turns this red, instead of shipping a file whose headers disagree with the table it came from.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const readResx = (file) => {
  const xml = fs.readFileSync(path.join(repoRoot, file), "utf8");
  const out = {};
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) {
    out[m[1]] = m[2].replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, "&");
  }
  return out;
};
const serviceFile = (dir, base, lang) => `${dir}/${lang === "en" ? `${base}.resx` : `${base}.${lang}.resx`}`;
const web = (rel, lang) => `frontend/Diten.Web/Resources/${rel}.${lang}.resx`;

const CASES = [
  {
    name: "AuthService Users export",
    service: ["services/Diten.AuthService/src/Diten.AuthService.Api/Resources", "UserExport"],
    screen: { u: "Views/Governance/Users/UsersIndex", s: "SharedResource" },
    map: {
      Title: ["u", "UsersTitle"], Email: ["u", "Email"], FirstName: ["u", "FirstName"], LastName: ["u", "LastName"], Roles: ["u", "Roles"],
      AccountKind: ["u", "AccountKind"], Status: ["s", "Status"], StatusActive: ["s", "Active"], StatusInactive: ["s", "Passive"],
      StatusInvited: ["u", "StatusInvited"], AccountKindUnknown: ["u", "AccountKindUnknown"], AccountKindHuman: ["u", "AccountKindHuman"],
      AccountKindService: ["u", "AccountKindService"]
    }
  },
  {
    name: "DevEnablement Golden Compact export",
    service: ["services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Resources", "GoldenCompactExport"],
    screen: { g: "Views/DevEnablement/GoldenReferenceCompact/GoldenReferenceCompactIndex", s: "SharedResource" },
    map: {
      Title: ["g", "GoldenReferenceCompactTitle"], Code: ["g", "Code"], Name: ["g", "Name"], ReferenceType: ["g", "ReferenceType"],
      Category: ["g", "Category"], Owner: ["g", "Owner"], Version: ["g", "Version"], Priority: ["g", "Priority"], Status: ["s", "Status"],
      StatusActive: ["s", "Active"], StatusPassive: ["s", "Passive"], ReferenceTypeStandard: ["g", "ReferenceTypeStandard"],
      ReferenceTypeCustom: ["g", "ReferenceTypeCustom"], ReferenceTypePro: ["g", "ReferenceTypePro"]
    }
  }
];

describe.each(CASES)("$name: the file's words are the screen's words", ({ service, screen, map }) => {
  test.each(LANGS)("%s — every key present, non-empty, equal to the screen", (lang) => {
    const svc = readResx(serviceFile(service[0], service[1], lang));
    const src = Object.fromEntries(Object.entries(screen).map(([k, rel]) => [k, readResx(web(rel, lang))]));
    expect(Object.keys(svc).sort()).toEqual(Object.keys(map).sort());
    for (const [key, [from, screenKey]] of Object.entries(map)) {
      expect(svc[key], `${lang} ${key}`).toBeTruthy();
      expect(svc[key], `${lang} ${key} differs from the screen's ${screenKey}`).toBe(src[from][screenKey]);
    }
  });
});

describe("ExportTooLarge (413) is said in all seven languages and bridged to the toast", () => {
  test.each(LANGS)("%s", (lang) => {
    const shared = readResx(web("SharedResource", lang));
    expect(shared.ExportTooLarge, lang).toBeTruthy();
    if (lang !== "en") expect(shared.ExportTooLarge, `${lang} is a copy of English`).not.toBe(readResx(web("SharedResource", "en")).ExportTooLarge);
  });

  test("the toast bridge knows the key", () => {
    const bridge = fs.readFileSync(path.join(repoRoot, "frontend/Diten.Web/Views/Shared/_GlobalNotification.cshtml"), "utf8");
    expect(bridge).toMatch(/'ExportTooLarge': @Json\.Serialize\(SharedLocalizer\["ExportTooLarge"\]\.Value\)/);
  });
});
