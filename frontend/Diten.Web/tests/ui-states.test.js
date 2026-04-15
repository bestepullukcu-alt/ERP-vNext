const { loadScript } = require("./load-script");

describe("ui-states helpers", () => {
  beforeEach(() => {
    delete window.enterpriseStrategyUi;
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/ui-states.js");
  });

  it("maps stale version errors to friendly text", () => {
    const msg = window.enterpriseStrategyUi.getErrorMessage(
      { payload: { error: { code: "STALE_VERSION" } } },
      "fallback"
    );
    expect(msg).toBe("Record has changed. Reload and retry.");
  });

  it("returns fallback for unknown errors", () => {
    const msg = window.enterpriseStrategyUi.getErrorMessage({}, "fallback");
    expect(msg).toBe("fallback");
  });
});
