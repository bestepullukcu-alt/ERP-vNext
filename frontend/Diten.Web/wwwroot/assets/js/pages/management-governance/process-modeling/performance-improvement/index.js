(function () {
  'use strict';

  const context = document.getElementById('performanceImprovementContext');
  if (!context) return;

  const L = window.PerformanceImprovementL10n || {};
  const tableElement = document.getElementById('dt-performance-improvements');
  const gatewayReady = context.dataset.ready === 'true';
  const canRead = context.dataset.read === 'true';
  const canCreate = context.dataset.create === 'true';
  const canUpdate = context.dataset.update === 'true';
  const canQualify = context.dataset.qualify === 'true';
  const canOutcome = context.dataset.outcome === 'true';
  const canClose = context.dataset.close === 'true';
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
  let listFailureMessage = '';
  const statusHost = document.getElementById('performanceImprovementStatus');
  const form = document.getElementById('formImprovement');
  const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value
    || document.querySelector('#formImprovementOutcome input[name="__RequestVerificationToken"]')?.value || '';
  let detailRow = null;

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
    if (status === 400) return L.Error400;
    if (status === 401) return L.Error401;
    if (status === 403) return L.Error403;
    if (status === 404) return L.Error404;
    if (status === 409) return L.Error409;
    return L.Error503;
  }

  function setState(state, message, warning = false) {
    if (!statusHost) return;
    statusHost.dataset.improvementState = state;
    statusHost.textContent = message || '';
    statusHost.classList.toggle('d-none', !message);
    statusHost.classList.toggle('alert-warning', warning);
    statusHost.classList.toggle('alert-info', Boolean(message) && !warning);
    if (message) statusHost.focus({ preventScroll: true });
  }

  function failureState(error) {
    if (!Number.isInteger(error?.status)) return ['offline', L.ErrorOffline];
    return [`error-${error.status}`, statusMessage(error.status)];
  }

  async function read(path) {
    const response = await fetch(path, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) {
      let failure = null;
      try { failure = await response.json(); } catch (_) { failure = null; }
      const reason = failure?.reason_code || failure?.reasonCode || failure?.errors?.[0];
      throw Object.assign(new Error(`${statusMessage(response.status)}${reason ? ` (${reason})` : ''}`), { status: response.status, reason });
    }
    return response.json();
  }

  async function write(path, method, body) {
    let response;
    try {
      response = await fetch(path, { method, credentials: 'same-origin', headers: { Accept: 'application/json', 'Content-Type': 'application/json', RequestVerificationToken: token, 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify(body) });
    } catch (error) { throw Object.assign(error, { offline: true }); }
    let payload = null;
    if (response.status !== 204) { try { payload = await response.json(); } catch (_) { payload = null; } }
    const reason = payload?.reason_code || payload?.reasonCode || payload?.errors?.[0];
    if (!response.ok) throw Object.assign(new Error(`${statusMessage(response.status)}${reason ? ` (${reason})` : ''}`), { status: response.status, reason });
    return payload;
  }

  function openForm(row) {
    form.reset();
    form.dataset.mode = row ? 'edit' : 'create';
    document.getElementById('improvementId').value = row?.id || '';
    document.getElementById('improvementExpectedVersion').value = row?.version ?? row?.expectedVersion ?? '';
    document.getElementById('improvementProcessModelVersionId').value = row?.processModelVersionId || '';
    document.getElementById('improvementTitle').value = row?.title || '';
    document.getElementById('improvementProblem').value = row?.problemStatement || '';
    document.getElementById('improvementExpectedOutcome').value = row?.expectedOutcome || '';
    document.getElementById('improvementPriority').value = row?.priorityRank ?? '';
    document.getElementById('improvementProcessModelVersionId').readOnly = Boolean(row);
    bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasImprovementForm')).show();
    document.getElementById('improvementTitle').focus();
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
    const payload = { ...normalizeView(view), viewName: String(L.SaveView).trim() };
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
    return window.DitenDataTable?.renderActions?.([
      { text: L.Open, icon: 'bx bx-show', attrs: { href: `/management-governance/process-modeling/improvements/${encodeURIComponent(row.id)}` } },
      ...(canUpdate && row.lifecycleState === 'Open' ? [{ text: L.Edit, icon: 'bx bx-edit', attrs: { 'data-row-action': 'edit-improvement', 'data-id': encodeURIComponent(row.id) } }] : [])
    ]) || `<a class="btn btn-sm btn-label-primary" href="/management-governance/process-modeling/improvements/${encodeURIComponent(row.id)}">${escape(L.Open)}</a>`;
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
      setState('loading', L.Loading);
      try {
        const payload = await read('/management-governance/process-modeling/api/improvements');
        allRows = Array.isArray(payload?.data) ? payload.data : [];
        listFailureMessage = '';
        setState(allRows.length ? 'ready' : 'empty', allRows.length ? '' : L.Empty);
      } catch (error) {
        const [state, message] = failureState(error);
        listFailureMessage = message;
        setState(state, message, true);
        window.showToast?.(message, 'warning');
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
        { data: 'lifecycleState', defaultContent: '', render: value => escape(L[`Lifecycle${value}`] || value) },
        { data: row => row.updatedAtUtc || row.createdAtUtc, defaultContent: '' },
        { data: null, orderable: false, searchable: false, className: 'text-end pe-3', render: renderActions }
      ],
      language: { emptyTable: listFailureMessage || (gatewayReady ? L.Empty : L.GatewayNotReady) },
      select: { style: 'multi', selector: 'td:nth-child(2)' },
      buttons: window.DtDefaults.exportButtons(canCreate ? L.AddNew : null, { 'aria-label': L.AddNew, title: L.AddNew }, { filterBtn, saveFilterBtn }, { exportColumns: [2, 3, 4, 5, 6], colvisColumns: [2, 3, 4, 5, 6] }),
      initComplete: function () {
        mountFilter(this.api());
        document.getElementById('skeleton-loader')?.classList.add('d-none');
        window.DitenDataTable?.bindActionDispatcher?.({ tableEl: tableElement, onRowAction: { 'edit-improvement': trigger => { const row = dataTable.row(trigger.closest('tr')).data(); if (row) openForm(row); } } });
        document.querySelector('.add-new')?.addEventListener('click', event => { event.preventDefault(); if (gatewayReady && canCreate) openForm(null); });
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
    setState('loading', L.Loading);
    try {
      const payload = await read(`/management-governance/process-modeling/api/improvements/${encodeURIComponent(context.dataset.id)}`);
      const row = payload?.data;
      if (!row) throw Object.assign(new Error(L.Error404), { status: 404 });
      detailRow = row;
      const actions = [
        canUpdate && row.lifecycleState === 'Open' ? `<button type="button" class="btn btn-label-primary" data-improvement-action="edit">${escape(L.Edit)}</button>` : '',
        canQualify && row.lifecycleState === 'Open' ? `<button type="button" class="btn btn-primary" data-improvement-action="qualify">${escape(L.Qualify)}</button>` : '',
        canOutcome && row.lifecycleState === 'Qualified' ? `<button type="button" class="btn btn-label-primary" data-improvement-action="outcome">${escape(L.RecordOutcome)}</button>` : '',
        canClose && row.lifecycleState === 'Qualified' ? `<button type="button" class="btn btn-danger" data-improvement-action="close">${escape(L.Close)}</button>` : ''
      ].filter(Boolean).join('');
      host.className = 'card backbone-preview-section p-4';
      host.innerHTML = `<div class="d-flex justify-content-between gap-3 flex-wrap"><h5>${escape(row.title)}</h5><div class="d-flex gap-2 flex-wrap">${actions}</div></div><div class="improvement-field"><strong>${escape(L.ProblemStatement)}</strong><div>${escape(row.problemStatement)}</div></div><div class="improvement-field"><strong>${escape(L.ExpectedOutcome)}</strong><div>${escape(row.expectedOutcome)}</div></div><div class="improvement-field"><strong>${escape(L.PriorityRank)}</strong><div>${escape(row.priorityRank)}</div></div><div class="improvement-field"><strong>${escape(L.Lifecycle)}</strong><div>${escape(L[`Lifecycle${row.lifecycleState}`] || row.lifecycleState)}</div></div><div class="improvement-field"><strong>${escape(L.Outcomes)}</strong><div>${(row.outcomes || []).map(item => `<div>${escape(item.summary)}</div>`).join('') || escape(L.NotAvailable)}</div></div>`;
      bindDetailActions();
      setState('ready', '');
    } catch (error) {
      const [state, message] = failureState(error);
      setState(state, message, true);
      host.innerHTML = '';
    }
  }

  async function mutateDetail(suffix, body) {
    try {
      await write(`/management-governance/process-modeling/api/improvements/${encodeURIComponent(detailRow.id)}/${suffix}`, 'POST', body);
      window.showToast?.(L.ActionCompleted, 'success');
      await initDetails();
    } catch (error) { const [state, message] = failureState(error); setState(state, message, true); }
  }

  function bindDetailActions() {
    document.querySelector('[data-improvement-action="edit"]')?.addEventListener('click', () => openForm(detailRow));
    document.querySelector('[data-improvement-action="outcome"]')?.addEventListener('click', () => { bootstrap.Modal.getOrCreateInstance(document.getElementById('outcomeModal')).show(); document.getElementById('outcomeSummary').focus(); });
    for (const action of ['qualify', 'close']) document.querySelector(`[data-improvement-action="${action}"]`)?.addEventListener('click', async event => {
      window.showConfirm?.(event.currentTarget.textContent.trim(), async () => {
        await mutateDetail(action, { expectedVersion: Number(detailRow.version ?? detailRow.expectedVersion) }); event.currentTarget.focus();
      }, { type: action === 'close' ? 'danger' : 'warning', confirmButtonText: event.currentTarget.textContent.trim(), cancelButtonText: L.Cancel });
    });
  }

  form?.addEventListener('submit', async event => {
    event.preventDefault(); if (!form.reportValidity()) return;
    const editing = form.dataset.mode === 'edit';
    const id = document.getElementById('improvementId').value;
    const payload = { title: document.getElementById('improvementTitle').value.trim(), problemStatement: document.getElementById('improvementProblem').value.trim(), expectedOutcome: document.getElementById('improvementExpectedOutcome').value.trim() || null, priorityRank: document.getElementById('improvementPriority').value ? Number(document.getElementById('improvementPriority').value) : null, ...(editing ? { expectedVersion: Number(document.getElementById('improvementExpectedVersion').value) } : { processModelVersionId: document.getElementById('improvementProcessModelVersionId').value }) };
    try {
      const result = await write(`/management-governance/process-modeling/api/improvements${editing ? `/${encodeURIComponent(id)}` : ''}`, editing ? 'PUT' : 'POST', payload);
      bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasImprovementForm'))?.hide();
      window.showToast?.(result?.reason_code === 'no_op' ? L.NoOp : L.Saved, 'success');
      if (context.dataset.mode === 'details') await initDetails(); else { const refreshed = await read('/management-governance/process-modeling/api/improvements'); allRows = refreshed?.data || []; refreshRows(dataTable); }
    } catch (error) { const [state, message] = failureState(error); setState(state, message, true); }
  });

  document.getElementById('formImprovementOutcome')?.addEventListener('submit', async event => {
    event.preventDefault(); if (!event.currentTarget.reportValidity()) return;
    await mutateDetail('outcomes', { summary: document.getElementById('outcomeSummary').value.trim(), expectedVersion: Number(detailRow.version ?? detailRow.expectedVersion) });
    bootstrap.Modal.getInstance(document.getElementById('outcomeModal'))?.hide();
  });

  if (context.dataset.mode === 'details') initDetails();
  else if (tableElement) { initFilters(); initDataTable(); }
})();
