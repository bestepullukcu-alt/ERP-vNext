const { loadScript } = require("./load-script");

const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";

// WC-1b DEC-3 — the backend supplies a resource label whose arguments are a NAMED map ({objectType}/{objectId}),
// while the pre-existing tf() does POSITIONAL ({0}/{1}) substitution. tn() is additive: it must render named
// tokens WITHOUT regressing tf().
describe("WorkCenterNext l10n named-token substitution (DEC-3)", () => {
  beforeEach(() => {
    delete global.WCN;
    delete global.WorkCenterNextData;
    delete global.WorkCenterNextFixtures;
    // Seed the l10n payload the server normally renders.
    document.body.innerHTML =
      '<script id="workcenternext-l10n" type="application/json">' +
      JSON.stringify({
        WorkAggregation_Title_Approval: "Onay: {objectType} {objectId}",
        Positional: "{0} / {1}",
        Plain: "Düz metin"
      }) +
      "</script>";
    loadScript(scriptRoot + "l10n.js");
  });

  it("substitutes named tokens", () => {
    expect(global.WCN.tn("WorkAggregation_Title_Approval", { objectType: "invoice", objectId: "INV-42" }))
      .toBe("Onay: invoice INV-42");
  });

  it("leaves the string untouched when there are no args", () => {
    expect(global.WCN.tn("Plain", null)).toBe("Düz metin");
    expect(global.WCN.tn("Plain")).toBe("Düz metin");
  });

  it("falls back to the key when the resource is missing (so a gap stays visible)", () => {
    expect(global.WCN.tn("NoSuchKey", { a: "1" })).toBe("NoSuchKey");
  });

  it("does NOT regress the positional tf() helper", () => {
    expect(global.WCN.tf("Positional", "a", "b")).toBe("a / b");
    expect(global.WCN.t("Plain")).toBe("Düz metin");
  });

  it("renders a backend title through resolveLabel with its named args", () => {
    loadScript(scriptRoot + "mock-data.js");
    const rendered = global.WorkCenterNextData.resolveLabel({
      kind: "resource",
      key: "WorkAggregation_Title_Approval",
      args: { objectType: "invoice", objectId: "INV-42" }
    });
    // The literal placeholders must NOT survive into the UI.
    expect(rendered).toBe("Onay: invoice INV-42");
    expect(rendered).not.toMatch(/\{objectType\}|\{objectId\}/);
  });
});
