(function (window) {
  "use strict";

  const permissions = new Set(window.APP_PERMISSIONS || []);

  window.enterpriseStrategyPermissions = {
    can(permission) {
      if (!permission) return true;
      return permissions.has(permission);
    },
  };
})(window);
