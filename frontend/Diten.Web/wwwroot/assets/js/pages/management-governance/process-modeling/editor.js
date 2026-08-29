(function () {
  'use strict';

  const root = document.getElementById('processModelEditor');
  if (!root) return;

  const L = window.ProcessModelingEditorL10n || {};
  const ready = root.dataset.gatewayReady === 'true';
  if (!ready) return;

  const modelId = root.dataset.modelId;
  const status = root.querySelector('[data-editor-state]');
  status.textContent = L.Loading;
  status.className = 'alert alert-info';

  fetch(`/management-governance/process-modeling/api/models/${encodeURIComponent(modelId)}`, {
    method: 'GET',
    credentials: 'same-origin',
    headers: { Accept: 'application/json' }
  }).then(async response => {
    if (!response.ok) {
      const message = response.status === 401 ? L.Error401
        : response.status === 403 ? L.Error403
          : response.status === 404 ? L.Error404
            : response.status === 409 ? L.Error409 : L.Error503;
      throw new Error(message);
    }
    return response.json();
  }).then(() => {
    status.classList.add('d-none');
  }).catch(error => {
    status.textContent = error.message || L.Error503;
    status.className = 'alert alert-warning';
  });
})();
