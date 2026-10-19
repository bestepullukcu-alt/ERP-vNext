const fs = require("fs");
const path = require("path");

/*
 * WP-LANE-UX-GUARDS-01 (micro round) — the Items page's "?" help names the page's OWN actions, in every tenant language,
 * with the exact label the page shows (ItemsIndex.{lang}.resx). A step that loses an action — or calls it by a word the
 * page does not show — is red here; page-help-ledger.test.js only measures that each step is non-empty.
 */

const WEB = path.resolve(__dirname, "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const decode = (text) => text.replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
  .replace(/&amp;/g, "&");
const values = (file) => Object.fromEntries([...fs.readFileSync(path.join(WEB, file), "utf8")
  .matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([^<]*)<\/value>/g)].map((m) => [m[1], decode(m[2]).trim()]));

// step → the page labels it must name
const NAMED = {
  "Help.ITEMS.Step2": ["AddNew", "LifecycleDraft", "Description"],
  "Help.ITEMS.Step3": ["Submit", "LifecyclePendingActivation", "LifecycleActive", "WithdrawApproval"],
  "Help.ITEMS.Step4": ["Deactivate", "Reactivate", "Retire"]
};

describe("Items help names the page's own actions, with the page's label, in every language", () => {
  test.each(LANGS)("%s", (lang) => {
    const help = values(`Resources/PageHelpResource.${lang}.resx`);
    const page = values(`Resources/Views/MasterDataManagement/Items/ItemsIndex.${lang}.resx`);
    const missing = Object.entries(NAMED).flatMap(([step, keys]) => keys
      .filter((key) => !page[key] || !(help[step] || "").includes(page[key]))
      .map((key) => `${step} lacks ${key} ("${page[key]}")`));
    expect(missing).toEqual([]);
  });
});
