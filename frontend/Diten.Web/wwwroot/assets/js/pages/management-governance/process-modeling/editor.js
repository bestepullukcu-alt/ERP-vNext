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

  let currentVersion = 0;
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
  }).then(payload => {
    currentVersion = Number(payload?.data?.version || 0);
    status.classList.add('d-none');
  }).catch(error => {
    status.textContent = error.message || L.Error503;
    status.className = 'alert alert-warning';
  });

  root.parentElement?.querySelectorAll('[data-model-action]').forEach(button => {
    button.addEventListener('click', async () => {
      const action = button.dataset.modelAction;
      const response = await fetch(`/management-governance/process-modeling/api/models/${encodeURIComponent(modelId)}/${encodeURIComponent(action)}`, {
        method: 'POST', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ expectedVersion: currentVersion })
      });
      if (!response.ok) {
        status.textContent = response.status === 409 ? L.Error409 : response.status === 403 ? L.Error403 : L.Error503;
        status.className = 'alert alert-warning';
        return;
      }
      window.location.reload();
    });
  });
})();
