(function () {
  'use strict';

  const node = document.getElementById('process-modeling-editor-l10n');
  if (!node) return;

  try {
    window.ProcessModelingEditorL10n = Object.freeze(JSON.parse(node.textContent || '{}'));
  } catch (_error) {
    window.ProcessModelingEditorL10n = Object.freeze({});
  }
})();
