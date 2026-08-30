'use strict';
(function () {
  const payload = document.getElementById('process-performance-index-l10n');
  const target = payload ? JSON.parse(payload.textContent || '{}') : {};
  window.ProcessPerformanceL10n = Object.freeze(target);
})();
