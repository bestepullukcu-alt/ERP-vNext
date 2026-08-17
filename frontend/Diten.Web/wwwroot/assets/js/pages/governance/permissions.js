// FE-B (MOD-0018-FU9) — UX-ONLY canonical permission helper for tenant RBAC screens (FE-C).
// Reads the bounded snapshot the server serialized into window.__permissionSnapshot (canonical
// PKS-001 permission keys only) and exposes a tiny show/hide API.
//
// NOT ENFORCEMENT. This only decides whether to show or hide UI affordances. Backend
// [HasPermission] and the server-side ShellAccessFilter are the authoritative access controls; a user
// who manipulates this client state still cannot perform an action the backend denies.
(function (global) {
  "use strict";

  function buildSet(list) {
    var set = Object.create(null);
    if (Array.isArray(list)) {
      for (var i = 0; i < list.length; i++) {
        var key = list[i];
        if (typeof key === "string" && key.trim() !== "") {
          set[key.trim().toLowerCase()] = true;
        }
      }
    }
    return set;
  }

  function create(list) {
    var set = buildSet(list);
    var api = {
      // True if the current user holds the canonical permission key (case-insensitive). UX only.
      has: function (key) {
        return typeof key === "string" && set[key.trim().toLowerCase()] === true;
      },
      // True if the user holds at least one of the given keys.
      hasAny: function (keys) {
        if (!Array.isArray(keys)) return false;
        for (var i = 0; i < keys.length; i++) {
          if (api.has(keys[i])) return true;
        }
        return false;
      },
      // The bounded key list (copy).
      keys: function () { return Object.keys(set); }
    };
    return api;
  }

  global.Permissions = create(global.__permissionSnapshot || []);
  global.Permissions.create = create; // exposed for tests / re-initialisation
})(typeof window !== "undefined" ? window : this);
