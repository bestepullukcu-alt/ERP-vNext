'use strict';

(function () {
  const payload = document.getElementById('performance-improvement-index-l10n');
  const required = ['Apply', 'ColumnVisibility', 'Empty', 'Error401', 'Error403', 'Error404', 'Error409', 'Error503', 'ExpectedOutcome', 'Export', 'Filter', 'GatewayNotReady', 'Lifecycle', 'Loading', 'Open', 'Outcomes', 'PriorityRank', 'ProblemStatement', 'Reset', 'SaveView', 'Search', 'ShowAll'];
  const target = {};
  if (payload) {
    try {
      const raw = JSON.parse(payload.textContent || '{}');
      Object.keys(raw).forEach(key => { target[key.charAt(0).toUpperCase() + key.slice(1)] = raw[key]; });
    } catch (error) {
      console.error('[PerformanceImprovement] Localization payload could not be parsed.', error);
    }
  }
  required.forEach(key => { if (!target[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`); });
  window.PerformanceImprovementL10n = Object.freeze(target);
})();
