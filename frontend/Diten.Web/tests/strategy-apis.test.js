const { loadScript } = require("./load-script");

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("strategy-apis", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
    delete window.strategyGoalsApi;
    delete window.strategyObjectivesApi;
    delete window.strategyConnectionsApi;
    delete window.initiativeStrategyApi;
    delete window.projectStrategyApi;
    delete window.strategyPlanningApi;
    delete window.strategyKpisApi;
    delete window.strategyLibraryApi;
    window.APP_CONFIG = { API_BASE_URL: "" };
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("caches GET list calls", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ success: true, data: { items: [{ id: "g1" }] } }));
    vi.stubGlobal("fetch", fetchMock);
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-apis.js");

    const a = await window.strategyGoalsApi.list();
    const b = await window.strategyGoalsApi.list();

    expect(a.items.length).toBe(1);
    expect(b.items.length).toBe(1);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("invalidates cache on mutation", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { items: [{ id: "g1" }] } }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { id: "g2" } }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { items: [{ id: "g1" }, { id: "g2" }] } }));
    vi.stubGlobal("fetch", fetchMock);
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-apis.js");

    await window.strategyGoalsApi.list();
    await window.strategyGoalsApi.create({ id: "g2", name: "new" });
    const refreshed = await window.strategyGoalsApi.list();

    expect(refreshed.items.length).toBe(2);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("uses APP_CONFIG enterprise-strategy API base", async () => {
    const fetchMock = vi.fn().mockImplementation(async () => jsonResponse({ success: true, data: { items: [] } }));
    vi.stubGlobal("fetch", fetchMock);
    window.APP_CONFIG = { API_BASE_URL: "http://127.0.0.1:5003" };
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-apis.js");

    await window.strategyGoalsApi.list();
    await window.strategyKpisApi.list();
    await window.strategyPlanningApi.listActiveByScope();

    expect(fetchMock).toHaveBeenNthCalledWith(1, "http://127.0.0.1:5003/api/v1/enterprise-strategy/goals?page=1&pageSize=5000", undefined);
    expect(fetchMock).toHaveBeenNthCalledWith(2, "http://127.0.0.1:5003/api/v1/enterprise-strategy/kpis", undefined);
    expect(fetchMock).toHaveBeenNthCalledWith(3, "http://127.0.0.1:5003/api/esbp/strategy-periods/active-by-scope", undefined);
  });

  it("refreshes strategy library catalog after template-affecting mutations", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { items: [] } }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { batchId: "b1" } }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { items: [{ id: "GT-1" }] } }));
    vi.stubGlobal("fetch", fetchMock);
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-apis.js");

    await window.strategyLibraryApi.catalog({ page: 1, pageSize: 200 });
    await window.strategyLibraryApi.importWorkbook({ sheets: {} });
    const refreshed = await window.strategyLibraryApi.catalog({ page: 1, pageSize: 200 });

    expect(refreshed.items).toEqual([{ id: "GT-1" }]);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it.each([
    ["problem JSON", 403, JSON.stringify({ title: "Forbidden", detail: "Permission denied" }), "Permission denied"],
    ["HTML outage", 503, "<html><body>Service unavailable</body></html>", "Service unavailable"],
    ["failed success envelope", 200, JSON.stringify({ success: false, message: "Operation denied" }), "Operation denied"],
  ])("preserves %s errors and does not cache a failed GET", async (_kind, status, body, message) => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(body, { status }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { items: [{ id: "g1" }] } }));
    vi.stubGlobal("fetch", fetchMock);
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-apis.js");

    await expect(window.strategyGoalsApi.list()).rejects.toMatchObject({ message, status, rawText: body });
    await expect(window.strategyGoalsApi.list()).resolves.toEqual({ items: [{ id: "g1" }] });
    await expect(window.strategyGoalsApi.list()).resolves.toEqual({ items: [{ id: "g1" }] });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
