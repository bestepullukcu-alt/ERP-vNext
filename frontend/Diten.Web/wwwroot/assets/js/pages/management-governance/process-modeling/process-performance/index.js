(function () {
  'use strict';
  const context = document.getElementById('processPerformanceContext');
  const table = document.getElementById('dt-process-performance');
  if (!context || !table) return;
  const L = window.ProcessPerformanceL10n || {};
  const statusHost = document.getElementById('processPerformanceStatus');
  const defaultOrder = [[2, 'asc']];
  const defaultColumnOrder = [0, 1, 2, 3, 4, 5, 6];
  const defaultVisibleColumns = defaultColumnOrder.map(() => true);
  let dataTable;
  let savedView = null;
  let appliedFilters = { referenceStatus: [] };
  let saveFilterArmed = false;

  const normalizeArray = value => Array.isArray(value) ? Array.from(new Set(value.map(String).map(x => x.trim()).filter(Boolean))).sort() : [];
  const normalizeView = view => ({
    filters: { referenceStatus: normalizeArray(view?.filters?.referenceStatus) },
    search: String(view?.search || '').trim(),
    colVis: Array.isArray(view?.colVis) ? view.colVis.map(Boolean) : defaultVisibleColumns.slice(),
    columnOrder: Array.isArray(view?.columnOrder) && view.columnOrder.length === defaultColumnOrder.length ? view.columnOrder.map(Number) : defaultColumnOrder.slice(),
    order: Array.isArray(view?.order) ? view.order.map(x => [Number(x[0]), String(x[1])]) : defaultOrder
  });
  const factoryView = () => normalizeView({});
  const statusMessage = status => ({ 401: L.Error401, 403: L.Error403, 404: L.Error404, 409: L.Error409, 503: L.Error503 }[status] || L.Error503);
  function renderFailure(status) {
    statusHost.dataset.performanceState = `error-${status}`;
    statusHost.textContent = statusMessage(status);
    statusHost.classList.remove('d-none');
  }
  async function readUnavailable() {
    const response = await fetch('/management-governance/process-modeling/api/kpi-bindings', { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) throw Object.assign(new Error(), { status: response.status });
    return response.json();
  }
  async function loadDefaultView() {
    if (!window.personalizationClient?.loadDefaultView) return null;
    try { const view = await window.personalizationClient.loadDefaultView('ManagementGovernance', 'ProcessPerformance'); return view ? normalizeView(view) : null; }
    catch (error) { if (!window.personalizationClient?.isAuthHandledError?.(error)) console.warn(error); return null; }
  }
  async function saveDefaultView(view) {
    if (!window.personalizationClient?.saveView) return;
    await window.personalizationClient.saveView('ManagementGovernance', 'ProcessPerformance', { ...normalizeView(view), viewName: L.SaveView });
    savedView = normalizeView(view); setSaveFilterVisible(false);
  }
  function getCurrentView(api) { return normalizeView({ filters: appliedFilters, search: api.search(), colVis: api.columns().indexes().toArray().map(i => api.column(i).visible()), columnOrder: api.colReorder?.order?.() || defaultColumnOrder, order: api.order() }); }
  function applySavedTableState(api, view) {
    const state = normalizeView(view || factoryView()); appliedFilters = state.filters;
    $('#filterReferenceStatus').val(appliedFilters.referenceStatus).trigger('change.select2'); api.search(state.search);
    state.colVis.forEach((visible, index) => api.column(index).visible(visible, false)); api.colReorder?.order?.(state.columnOrder, true); api.order(state.order);
  }
  function setSaveFilterVisible(visible) { document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible); window.DtDefaults?.refreshButtonGroupRadii?.(); }
  function isDirtyComparedToDefault() { return JSON.stringify(getCurrentView(dataTable)) !== JSON.stringify(normalizeView(savedView || factoryView())); }
  function updateDirtyState() { if (saveFilterArmed && dataTable) setSaveFilterVisible(isDirtyComparedToDefault()); }
  async function initDataTable() {
    savedView = await loadDefaultView();
    try { await readUnavailable(); } catch (error) { renderFailure(Number.isInteger(error?.status) ? error.status : 503); }
    const filterBtn = { text: '<i class="bx bx-filter-alt"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn', attr: { 'aria-label': L.Filter, title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' }, action: () => bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).toggle() };
    const saveFilterBtn = { text: '<i class="bx bx-save"></i>', className: 'btn btn-icon btn-label-secondary dt-save-filter-btn d-none', attr: { 'aria-label': L.SaveView, title: L.SaveView }, action: () => saveDefaultView(getCurrentView(dataTable)) };
    dataTable = new DataTable(table, window.DtDefaults.create({
      data: [], serverSide: false, processing: false, stateSave: false, order: defaultOrder, colReorder: { columns: ':gt(1):not(:last-child)' },
      columns: [{ data: null, defaultContent: '', orderable: false, searchable: false, className: 'control' }, { data: null, defaultContent: '', orderable: false, searchable: false }, { data: 'processModel', defaultContent: '' }, { data: 'processVersion', defaultContent: '' }, { data: 'metricReference', defaultContent: '' }, { data: 'referenceStatus', defaultContent: '' }, { data: null, defaultContent: '', orderable: false, searchable: false }],
      language: { emptyTable: L.ProviderUnavailable },
      buttons: window.DtDefaults.exportButtons(null, {}, { filterBtn, saveFilterBtn }, { exportColumns: [2, 3, 4, 5], colvisColumns: [2, 3, 4, 5] }),
      initComplete: function () { applySavedTableState(this.api(), savedView || factoryView()); document.getElementById('skeleton-loader')?.classList.add('d-none'); setTimeout(() => { saveFilterArmed = true; updateDirtyState(); }, 0); },
      drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), appliedFilters.referenceStatus.length); updateDirtyState(); }
    }));
    dataTable.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', updateDirtyState);
  }
  const filter = $('#filterReferenceStatus');
  filter.select2({ dropdownParent: $(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', minimumResultsForSearch: Infinity, width: 'element', closeOnSelect: false });
  document.getElementById('btnFilterApply')?.addEventListener('click', () => { appliedFilters = { referenceStatus: normalizeArray(filter.val()) }; dataTable.draw(false); updateDirtyState(); });
  document.getElementById('btnFilterReset')?.addEventListener('click', () => { applySavedTableState(dataTable, factoryView()); dataTable.draw(false); updateDirtyState(); });
  initDataTable();
  window.ProcessPerformancePresentation = Object.freeze({ statusMessage });
})();
