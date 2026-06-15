const { loadScript } = require("./load-script");

describe("DtDefaults refresh token and reload / retry logic", () => {
  let originalFetch;
  let originalLocation;

  beforeEach(() => {
    // Mock globals needed by dt-defaults.js
    window.DataTable = {
      Responsive: {
        display: {
          modal: vi.fn()
        }
      }
    };

    window.$ = window.jQuery = vi.fn(() => ({
      removeClass: vi.fn().mockReturnThis(),
      addClass: vi.fn().mockReturnThis(),
      css: vi.fn().mockReturnThis(),
      each: vi.fn(),
      find: vi.fn().mockReturnThis(),
      contents: vi.fn().mockReturnThis(),
      unwrap: vi.fn().mockReturnThis(),
      filter: vi.fn().mockReturnThis(),
      fadeOut: vi.fn(),
      fadeIn: vi.fn()
    }));
    window.$.extend = vi.fn((...args) => Object.assign({}, ...args));
    window.$.map = vi.fn((arr, cb) => arr.map(cb));
    window.$.ajax = vi.fn();

    // Mock window.location
    originalLocation = window.location;
    delete window.location;
    window.location = {
      pathname: "/WorkCenter",
      search: "",
      hostname: "localhost",
      reload: vi.fn(),
      href: ""
    };

    // Mock fetch
    originalFetch = window.fetch;
    window.fetch = vi.fn();

    // Clear and load DtDefaults
    delete window.DtDefaults;
    loadScript("wwwroot/assets/js/dt-defaults.js");
  });

  afterEach(() => {
    window.fetch = originalFetch;
    window.location = originalLocation;
  });

  it("should reload the page on successful token refresh when no ajaxSettings is provided", async () => {
    window.fetch.mockResolvedValueOnce({
      ok: true,
      headers: {
        get: () => "application/json"
      },
      json: () => Promise.resolve({ success: true, user: { id: "123", email: "test@example.com" } })
    });

    const refreshPromise = window.DtDefaults.handleUnauthorized();
    await refreshPromise;

    expect(window.fetch).toHaveBeenCalledWith("/account/refresh", expect.any(Object));
    expect(window.CurrentUser).toEqual({ id: "123", email: "test@example.com" });
    expect(window.location.reload).toHaveBeenCalled();
  });

  it("should retry the original AJAX request on successful token refresh when ajaxSettings is provided", async () => {
    window.fetch.mockResolvedValueOnce({
      ok: true,
      headers: {
        get: () => "application/json"
      },
      json: () => Promise.resolve({ success: true, user: { id: "123" } })
    });

    const ajaxSettings = { url: "/api/users", method: "GET" };
    const refreshPromise = window.DtDefaults.handleUnauthorized(ajaxSettings);
    await refreshPromise;

    expect(window.fetch).toHaveBeenCalled();
    expect(window.location.reload).not.toHaveBeenCalled();
    expect(window.$.ajax).toHaveBeenCalledWith({
      url: "/api/users",
      method: "GET",
      _retried: true
    });
  });

  it("should redirect to login on terminal refresh failure (401 or explicit reauthRequired: true)", async () => {
    window.fetch.mockResolvedValueOnce({
      ok: false,
      status: 401,
      headers: {
        get: () => "application/json"
      },
      json: () => Promise.resolve({ success: false, reauthRequired: true })
    });

    const refreshPromise = window.DtDefaults.handleUnauthorized();
    await refreshPromise;

    expect(window.location.href).toContain("/account/login");
  });

  it("should retry the original AJAX request once on soft refresh failure (503 or reauthRequired: false)", async () => {
    window.fetch.mockResolvedValueOnce({
      ok: false,
      status: 503,
      headers: {
        get: () => "application/json"
      },
      json: () => Promise.resolve({ success: false, reauthRequired: false })
    });

    const ajaxSettings = { url: "/api/users", method: "GET" };
    const refreshPromise = window.DtDefaults.handleUnauthorized(ajaxSettings);
    await refreshPromise;

    expect(window.location.href).not.toContain("/account/login");
    expect(window.$.ajax).toHaveBeenCalledWith({
      url: "/api/users",
      method: "GET",
      _retried: true
    });
  });
});
