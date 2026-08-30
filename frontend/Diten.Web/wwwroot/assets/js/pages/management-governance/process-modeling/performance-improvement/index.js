(function () {
  'use strict';

  const context = document.getElementById('performanceImprovementContext');
  if (!context) return;

  const L = window.PerformanceImprovementL10n || {};
  const tableElement = document.getElementById('dt-performance-improvements');
  const gatewayReady = context.dataset.ready === 'true';
  const canRead = context.dataset.read === 'true';
  const defaultOrder = [[4, 'asc'], [2, 'asc']];
  const defaultColumnOrder = [0, 1, 2, 3, 4, 5, 6, 7];
  const defaultVisibleColumns = defaultColumnOrder.map(() => true);
  const moduleKey = 'ManagementGovernance';
  const pageKey = 'PerformanceImprovement';
  let allRows = [];
  let appliedFilters = { lifecycle: [] };
  let savedView = null;
  let saveFilterArmed = false;
  let dataTable;

  const escape = value => String(value ?? '').replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[character]));
  const normalizeArray = value => Array.isArray(value)
    ? Array.from(new Set(value.map(item => String(item).trim()).filter(Boolean))).sort()
    : [];
  const normalizeView = view => ({
    filters: { lifecycle: normalizeArray(view?.filters?.lifecycle) },
    search: String(view?.search || '').trim(),
    colVis: Array.isArray(view?.colVis) ? view.colVis.map(Boolean) : defaultVisibleColumns.slice(),
    columnOrder: Array.isArray(view?.columnOrder) && view.columnOrder.length === defaultColumnOrder.length
      ? view.columnOrder.map(Number)
      : defaultColumnOrder.slice(),
    order: Array.isArray(view?.order) ? view.order.map(entry => [Number(entry[0]), String(entry[1])]) : defaultOrder
  });
  const factoryView = () => normalizeView({ filters: { lifecycle: [] }, search: '', colVis: defaultVisibleColumns, columnOrder: defaultColumnOrder, order: defaultOrder });

  function syncMultiSelectSummary($select) {
    const $container = $select.next('.select2-container');
    const $rendered = $container.find('.select2-selection__rendered');
    const $selection = $container.find('.select2-selection--multiple');
    if (!$container.length || !$rendered.length || !$selection.length) return;
    let $summary = $selection.find('.dt-inline-filter-multi__summary');
    let $actions = $selection.find('.dt-inline-filter-multi__actions');
    let $count = $selection.find('.dt-inline-filter-multi__count');
    if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
    if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
    if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
    const placeholder = String($select.data('placeholder') || '');
    const selectedValues = normalizeArray($select.val());
    const selectedTexts = ($select.select2('data') || []).map(item => String(item.text || '').trim()).filter(Boolean);
    $summary.text(placeholder);
    $rendered.attr('title', selectedTexts.join(', ') || placeholder);
    $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
    $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
  }

  function statusMessage(status) {
    if (status === 401) return L.Error401;
    if (status === 403) return L.Error403;
    if (status === 404) return L.Error404;
    if (status === 409) return L.Error409;
    return L.Error503;
  }

  async function read(path) {
    const response = await fetch(path, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) throw Object.assign(new Error(statusMessage(response.status)), { status: response.status });
    return response.json();
  }

  async function loadDefaultView() {
    if (!window.personalizationClient?.loadDefaultView) return null;
    try {
      const value = await window.personalizationClient.loadDefaultView(moduleKey, pageKey);
      return value ? normalizeView(value) : null;
    } catch (error) {
      if (!window.personalizationClient?.isAuthHandledError?.(error)) console.warn(error);
      return null;
    }
  }

  async function saveDefaultView(view) {
    if (!window.personalizationClient?.saveView) return;
    const payload = { ...normalizeView(view), viewName: String(L.SaveView || 'Default').trim() || 'Default' };
    await window.personalizationClient.saveView(moduleKey, pageKey, payload);
    savedView = normalizeView(payload);
    setSaveFilterVisible(false);
  }

  function getCurrentView(api) {
    return normalizeView({
      filters: appliedFilters,
      search: api.search(),
      colVis: api.columns().indexes().toArray().map(index => api.column(index).visible()),
      columnOrder: api.colReorder?.order?.() || defaultColumnOrder,
      order: api.order()
    });
  }

  function applySavedTableState(api, value) {
    const normalized = normalizeView(value || factoryView());
    appliedFilters = normalized.filters;
    $('#filterLifecycle').val(appliedFilters.lifecycle).trigger('change.select2');
    api.search(normalized.search);
    normalized.colVis.forEach((visible, index) => api.column(index).visible(visible, false));
    api.colReorder?.order?.(normalized.columnOrder, true);
    api.order(normalized.order);
  }

  function setSaveFilterVisible(visible) {
    document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
    window.DtDefaults?.refreshButtonGroupRadii?.();
  }

  function updateDirtyState() {
    if (!saveFilterArmed || !dataTable) return;
    const baseline = normalizeView(savedView || factoryView());
    setSaveFilterVisible(JSON.stringify(getCurrentView(dataTable)) !== JSON.stringify(baseline));
  }

  function filteredRows() {
    const selected = normalizeArray(appliedFilters.lifecycle);
    return selected.length ? allRows.filter(row => selected.includes(String(row.lifecycleState || ''))) : allRows.slice();
  }

  function refreshRows(api) {
    api.clear();
    api.rows.add(filteredRows());
    api.draw(false);
  }

  function renderActions(_value, _type, row) {
    if (!canRead || !row?.id) return '';
    return `<a class="btn btn-sm btn-label-primary" href="/management-governance/process-modeling/improvements/${encodeURIComponent(row.id)}">${escape(L.Open)}</a>`;
  }

  function mountFilter(api) {
    const host = document.getElementById('inlineFilterHost');
    const toolbar = document.querySelector('.dt-filter-btn')?.closest('.dt-layout-row');
    if (host && toolbar) {
      toolbar.insertAdjacentElement('afterend', host);
      host.classList.add('px-3');
    }
    const button = document.querySelector('.dt-filter-btn');
    const collapse = document.getElementById('inlineFilterCollapse');
    collapse?.addEventListener('shown.bs.collapse', () => button?.setAttribute('aria-expanded', 'true'));
    collapse?.addEventListener('hidden.bs.collapse', () => button?.setAttribute('aria-expanded', 'false'));
    applySavedTableState(api, savedView || factoryView());
    refreshRows(api);
  }

  async function initDataTable() {
    savedView = await loadDefaultView();
    if (gatewayReady && canRead) {
      try {
        const payload = await read('/management-governance/process-modeling/api/improvements');
        allRows = Array.isArray(payload?.data) ? payload.data : [];
      } catch (error) {
        window.showToast?.(error.message || L.Error503, 'warning');
      }
    }

    const filterBtn = {
      text: '<i class="bx bx-filter-alt"></i>',
      className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
      attr: { 'aria-label': L.Filter, title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
      action: () => bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).toggle()
    };
    const saveFilterBtn = {
      text: `<i class="bx bx-save"></i><span class="d-none d-lg-inline ms-1">${escape(L.SaveView)}</span>`,
      className: 'btn btn-label-secondary dt-save-filter-btn d-none',
      attr: { 'aria-label': L.SaveView, title: L.SaveView },
      action: () => saveDefaultView(getCurrentView(dataTable))
    };
    const options = window.DtDefaults.create({
      data: allRows,
      serverSide: false,
      processing: false,
      stateSave: false,
      order: defaultOrder,
      colReorder: { columns: ':gt(1):not(:last-child)' },
      columns: [
        { data: null, defaultContent: '', orderable: false, searchable: false, className: 'control' },
        { data: null, defaultContent: '', orderable: false, searchable: false, className: 'dt-checkboxes-cell', checkboxes: { selectRow: true } },
        { data: 'title', defaultContent: '' },
        { data: 'problemStatement', defaultContent: '' },
        { data: 'priorityRank', defaultContent: '' },
        { data: 'lifecycleState', defaultContent: '' },
        { data: row => row.updatedAtUtc || row.createdAtUtc, defaultContent: '' },
        { data: null, orderable: false, searchable: false, className: 'text-end pe-3', render: renderActions }
      ],
      language: { emptyTable: gatewayReady ? L.Empty : L.GatewayNotReady },
      select: { style: 'multi', selector: 'td:nth-child(2)' },
      buttons: window.DtDefaults.exportButtons(null, {}, { filterBtn, saveFilterBtn }, { exportColumns: [2, 3, 4, 5, 6], colvisColumns: [2, 3, 4, 5, 6] }),
      initComplete: function () {
        mountFilter(this.api());
        document.getElementById('skeleton-loader')?.classList.add('d-none');
        setTimeout(() => { saveFilterArmed = true; updateDirtyState(); }, 0);
      },
      drawCallback: function () {
        window.DtDefaults.updateVisualState(this.api(), appliedFilters.lifecycle.length);
        updateDirtyState();
      }
    });
    dataTable = new DataTable(tableElement, options);
    dataTable.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', updateDirtyState);
  }

  function initFilters() {
    const lifecycle = $('#filterLifecycle');
    lifecycle.select2({
      dropdownParent: $(document.body),
      dropdownCssClass: 'dt-inline-filter-dropdown',
      containerCssClass: 'dt-inline-filter-multi',
      selectionCssClass: 'form-select form-select-sm',
      minimumResultsForSearch: Infinity,
      width: 'element',
      closeOnSelect: false
    });
    lifecycle.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary(lifecycle));
    window.requestAnimationFrame(() => syncMultiSelectSummary(lifecycle));
    document.getElementById('btnFilterApply')?.addEventListener('click', () => {
      appliedFilters = { lifecycle: normalizeArray(lifecycle.val()) };
      refreshRows(dataTable);
      bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).hide();
      updateDirtyState();
    });
    document.getElementById('btnFilterReset')?.addEventListener('click', () => {
      applySavedTableState(dataTable, factoryView());
      refreshRows(dataTable);
      updateDirtyState();
    });
  }

  async function initDetails() {
    const host = document.getElementById('performanceImprovementDetail');
    if (!host || !gatewayReady) return;
    try {
      const payload = await read(`/management-governance/process-modeling/api/improvements/${encodeURIComponent(context.dataset.id)}`);
      const row = payload?.data;
      if (!row) throw Object.assign(new Error(L.Error404), { status: 404 });
      host.innerHTML = `<h5>${escape(row.title)}</h5><div class="improvement-field"><strong>${escape(L.ProblemStatement)}</strong><div>${escape(row.problemStatement)}</div></div><div class="improvement-field"><strong>${escape(L.ExpectedOutcome)}</strong><div>${escape(row.expectedOutcome)}</div></div><div class="improvement-field"><strong>${escape(L.PriorityRank)}</strong><div>${escape(row.priorityRank)}</div></div><div class="improvement-field"><strong>${escape(L.Lifecycle)}</strong><div>${escape(row.lifecycleState)}</div></div><div class="improvement-field"><strong>${escape(L.Outcomes)}</strong><div>${(row.outcomes || []).map(item => `<div>${escape(item.summary)}</div>`).join('') || '-'}</div></div>`;
    } catch (error) {
      host.innerHTML = `<div class="alert alert-warning mb-0">${escape(error.message || L.Error503)}</div>`;
    }
  }

  if (context.dataset.mode === 'details') initDetails();
  else if (tableElement) { initFilters(); initDataTable(); }
})();
