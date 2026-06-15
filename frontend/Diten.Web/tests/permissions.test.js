const { loadScript } = require("./load-script");

// FE-B (MOD-0018-FU9) — UX-only permission helper. Verifies the bounded snapshot behaviour; this is
// NOT an enforcement test (backend [HasPermission] is authoritative).
describe("Permissions UX helper", () => {
  function load(snapshot) {
    window.__permissionSnapshot = snapshot;
    delete window.Permissions;
    loadScript("wwwroot/assets/js/pages/governance/permissions.js");
    return window.Permissions;
  }

  it("has() is true for a present canonical key, case-insensitively", () => {
    const p = load(["auth.users.read", "auth.roles.read"]);
    expect(p.has("auth.users.read")).toBe(true);
    expect(p.has("AUTH.USERS.READ")).toBe(true);
    expect(p.has("  auth.roles.read  ")).toBe(true);
  });

  it("has() is false for an absent key", () => {
    const p = load(["auth.users.read"]);
    expect(p.has("auth.users.create")).toBe(false);
    expect(p.has("")).toBe(false);
    expect(p.has(undefined)).toBe(false);
  });

  it("empty snapshot → has() always false and keys() empty", () => {
    const p = load([]);
    expect(p.has("auth.users.read")).toBe(false);
    expect(p.keys()).toEqual([]);
  });

  it("hasAny() returns true only when at least one key is present", () => {
    const p = load(["mdm.legal-entities.read"]);
    expect(p.hasAny(["x.y.z", "mdm.legal-entities.read"])).toBe(true);
    expect(p.hasAny(["a.b.c"])).toBe(false);
    expect(p.hasAny("not-an-array")).toBe(false);
  });

  it("is bounded: keys() returns only the provided permission keys, no leakage", () => {
    const p = load(["auth.users.read"]);
    expect(p.keys()).toEqual(["auth.users.read"]);
  });

  it("create() builds an independent snapshot for explicit lists", () => {
    load([]);
    const p = window.Permissions.create(["auth.roles.read"]);
    expect(p.has("auth.roles.read")).toBe(true);
    expect(window.Permissions.has("auth.roles.read")).toBe(false); // global stays empty
  });
});
