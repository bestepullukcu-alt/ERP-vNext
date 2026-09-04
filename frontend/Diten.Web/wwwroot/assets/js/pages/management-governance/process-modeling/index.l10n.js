(function () {
  'use strict';

  const node = document.getElementById('process-modeling-index-l10n');
  if (!node) return;

  try {
    window.ProcessModelingL10n = Object.freeze(JSON.parse(node.textContent || '{}'));
  } catch (_error) {
    window.ProcessModelingL10n = Object.freeze({});
  }
})();
