(function () {
  'use strict';
  const root = document.getElementById('processModelEditor');
  if (!root) return;
  const L = window.ProcessModelingEditorL10n || {};
  const ready = root.dataset.gatewayReady === 'true';
  const canUpdate = root.dataset.canUpdate === 'true';
  const modelId = root.dataset.modelId;
  const statusHost = root.querySelector('[data-editor-state]');
  const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  const saveButton = document.getElementById('btnSaveDraft');
  let current = null;
  let pending = false;
  const unwrap = value => value?.data ?? value?.Data ?? value;
  const messageFor = (status, reason) => `${status === 400 ? L.Error400 : status === 401 ? L.Error401 : status === 403 ? L.Error403 : status === 404 ? L.Error404 : status === 409 ? L.Error409 : L.Error503}${reason ? ` (${reason})` : ''}`;
  function setState(state, message, tone) {
    statusHost.dataset.editorState = state;
    statusHost.textContent = message || '';
    statusHost.className = message ? `alert alert-${tone || 'info'}` : 'alert d-none';
    if (message) statusHost.focus({ preventScroll: true });
  }
  async function request(path, method, body) {
    const write = method !== 'GET';
    let response;
    try {
      response = await fetch(`/management-governance/process-modeling/api/${path}`, {
        method, credentials: 'same-origin',
        headers: { Accept: 'application/json', ...(write ? { 'Content-Type': 'application/json', RequestVerificationToken: token, 'Idempotency-Key': crypto.randomUUID() } : {}) },
        ...(write ? { body: JSON.stringify(body) } : {})
      });
    } catch (error) { throw Object.assign(error, { offline: true }); }
    let payload = null;
    if (response.status !== 204) { try { payload = await response.json(); } catch (_) { payload = null; } }
    const reason = payload?.reason_code || payload?.reasonCode || payload?.errors?.[0];
    if (!response.ok) throw Object.assign(new Error(messageFor(response.status, reason)), { status: response.status, reason });
    return payload;
  }
  const parseArray = id => { const value = JSON.parse(document.getElementById(id).value.trim() || '[]'); if (!Array.isArray(value)) throw new Error(); return value; };
  const pretty = value => JSON.stringify(Array.isArray(value) ? value : [], null, 2);
  function render(payload) {
    const model = unwrap(payload) || {};
    const version = model.currentVersion || model.version || model.processModelVersion || model;
    current = {
      versionId: version.id || model.currentVersionId || model.versionId,
      expectedVersion: Number(version.version ?? model.expectedVersion ?? model.version ?? 0),
      lifecycle: version.lifecycleState || model.lifecycleState || ''
    };
    document.getElementById('versionTitle').value = version.title || model.name || '';
    document.getElementById('versionDescription').value = version.description || '';
    document.getElementById('modelActivitiesJson').value = pretty(version.activities || model.activities);
    document.getElementById('modelControlsJson').value = pretty(version.controlPoints || model.controlPoints);
    document.getElementById('modelRelationshipsJson').value = pretty(version.relationships || model.relationships);
    document.getElementById('processModelVersionSummary').textContent = `${L[`Lifecycle${current.lifecycle}`] || current.lifecycle} — ${L.VersionTitle} ${current.expectedVersion}`;
    const draft = current.lifecycle === 'Draft';
    root.querySelectorAll('input:not([type="hidden"]), textarea').forEach(el => { el.disabled = !ready || !canUpdate || !draft; });
    saveButton.disabled = !ready || !canUpdate || !draft;
    root.querySelectorAll('[data-lifecycle-action]').forEach(button => {
      const action = button.dataset.lifecycleAction;
      const allowed = action === 'request-review' ? draft : ['return-to-draft', 'publish'].includes(action) ? current.lifecycle === 'Review' : action === 'retire' ? current.lifecycle === 'Published' : action === 'create-revision' ? ['Published', 'Retired'].includes(current.lifecycle) : false;
      button.disabled = !ready || !allowed;
    });
    setState('ready', '');
  }
  async function reload() {
    setState('loading', L.Loading);
    try { render(await request(`models/${encodeURIComponent(modelId)}`, 'GET')); }
    catch (error) { setState(error.offline ? 'offline' : `error-${error.status}`, error.offline ? L.ErrorOffline : error.message, 'warning'); }
  }
  async function mutate(path, method, body) {
    if (pending) return;
    pending = true;
    try { await request(path, method, body); window.showToast?.(L.ActionCompleted, 'success'); await reload(); }
    catch (error) { const message = error.offline ? L.ErrorOffline : error.message; setState(error.offline ? 'offline' : `error-${error.status}`, message, 'warning'); window.showToast?.(message, 'warning'); }
    finally { pending = false; }
  }
  saveButton?.addEventListener('click', async () => {
    const title = document.getElementById('versionTitle').value.trim();
    if (!title) { document.getElementById('versionTitle').focus(); return; }
    try { await mutate(`model-versions/${encodeURIComponent(current.versionId)}/draft-content`, 'PUT', { title, description: document.getElementById('versionDescription').value.trim() || null, activities: parseArray('modelActivitiesJson'), controlPoints: parseArray('modelControlsJson'), relationships: parseArray('modelRelationshipsJson'), expectedVersion: current.expectedVersion }); }
    catch (_) { setState('validation', L.InvalidGraphJson, 'danger'); }
  });
  root.addEventListener('keydown', event => { if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's' && !saveButton?.disabled) { event.preventDefault(); saveButton.click(); } });
  root.querySelectorAll('[data-lifecycle-action]').forEach(button => button.addEventListener('click', async () => {
    const action = button.dataset.lifecycleAction;
    window.showConfirm?.(button.textContent.trim(), async () => {
      const path = action === 'create-revision' ? `models/${encodeURIComponent(modelId)}/revisions` : `model-versions/${encodeURIComponent(current.versionId)}/${action}`;
      const body = action === 'create-revision' ? { title: document.getElementById('versionTitle').value.trim(), description: document.getElementById('versionDescription').value.trim() || null, expectedVersion: current.expectedVersion } : { expectedVersion: current.expectedVersion };
      await mutate(path, 'POST', body); button.focus();
    }, { type: action === 'retire' ? 'danger' : 'warning', confirmButtonText: button.textContent.trim(), cancelButtonText: L.Cancel });
  }));
  if (ready) reload();
})();
